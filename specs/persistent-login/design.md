# Diseño técnico — Inicio de sesión persistente

## Contexto y compatibilidad

La plataforma actual es una Minimal API ASP.NET Core 10 con Identity/PostgreSQL,
JWT bearer corto y endpoints `/api/v1/auth/register`, `/login` y `/me`. Esta
funcionalidad extiende login y añade renovación/revocación. El cliente web
mantiene el JWT bearer en memoria; sólo el endpoint de renovación acepta cookie.
El diseño del cliente es
[`design.md`](../../../ez-nihongo-web/specs/persistent-login/design.md).

## Contratos HTTP

Todas las operaciones permanecen dentro del grupo de autenticación sujeto al
rate limit. Errores públicos usan Problem Details genérico; no distinguir
token inexistente, vencido, rotado o revocado.

### `POST /api/v1/auth/login`

Petición:

```json
{
  "email": "student@example.com",
  "password": "<contraseña>",
  "rememberMe": true
}
```

`rememberMe` es nullable/opcional y default `false`. Respuesta `200 OK` mantiene
el DTO de token actual (`user`, `accessToken`, `tokenType`, `expiresAt`). Con
`true`, el servidor establece la cookie HttpOnly; no añade el token de
renovación al JSON. Con `false`, el servidor no crea una sesión y, tras validar
credenciales, revoca/expira una sesión anterior presentada por esa cookie. Un
login fallido no cambia una sesión previa.

### `POST /api/v1/auth/refresh`

Requiere cookie y cabecera `X-EZ-Nihongo-Request: 1`, además de origen
configurado. Respuesta `200 OK` devuelve el DTO de acceso actual y una cookie
rotada. Una sesión inválida responde `401` genérico y expira cookie.

### `POST /api/v1/auth/logout`

Requiere origen y cabecera de aplicación. Revoca la familia de la cookie, si la
hay, expira cookie y responde `204 No Content` de forma idempotente. En ausencia
de cookie no falla por sesión inexistente.

### Registro y `/me`

`POST /register` conserva su contrato existente, sin persistencia ni cookie.
`GET /me` sigue requiriendo `Authorization: Bearer`; la cookie no autentica
`/me`.

## Cookie y CSRF

- Nombre: `ez-nihongo-refresh`.
- Producción: `HttpOnly; Secure; SameSite=Strict; Path=/api/v1/auth`, sin
  `Domain`; `Max-Age` es el tiempo restante hasta la fecha absoluta.
- Dev local sobre HTTP: permitir `Secure=false` sólo mediante entorno de
  desarrollo explícito, validado al arranque. En cualquier otro entorno la
  política requiere HTTPS y `Secure=true`.
- Expirar utilizando idénticos nombre y ruta, sin atributo `Domain`.
- Validar `Origin` exacto contra una lista configurada para refresh y logout;
  rechazar `Origin` ausente, `null` o no registrado. Validar método POST,
  `Content-Type: application/json` y cabecera `X-EZ-Nihongo-Request: 1`.
- Si cliente y API son cross-origin, CORS permite sólo los orígenes configurados
  con credenciales; no usar comodines. El despliegue recomendado es same-origin
  mediante el proxy `/api`.
- No usar la cookie de renovación como esquema bearer automático ni exponerla en
  metadata OpenAPI como valor JSON.

## Modelo de persistencia

Agregar `PersistentLoginSession` a `PlatformIdentityDbContext` y una migración
aditiva. Una fila representa un token emitido en una familia de sesión.

Campos mínimos:

- `Id`: clave interna.
- `UserId`: FK requerida a `Users`, cascade delete.
- `FamilyId`: ID aleatorio que agrupa todas las rotaciones de un navegador.
- `TokenHash`: SHA-256 binario o HMAC-SHA-256 del token raw; único e indexado.
- `CreatedAt`, `ExpiresAt`: `DateTimeOffset` UTC, expiración original fija.
- `RotatedAt`, `RevokedAt`: fechas UTC nullable para distinguir token consumido y
  revocado.
- `ReplacedById`: FK nullable a la fila rotadora para trazabilidad, no para
  reconstruir credenciales.

El token raw se genera con `RandomNumberGenerator` (al menos 32 bytes) y se
codifica Base64Url. Sólo se entrega al escribir la cookie. Las filas expiradas
pueden limpiarse por tarea posterior; la expiración siempre se valida en cada
operación.

## Servicio y concurrencia

Crear un servicio scoped de sesiones persistentes separado de la emisión JWT.
Login valida Identity, emite el JWT corto existente y, si se solicita, crea una
familia con un token hash y fecha absoluta `now + 30 días`.

Refresh busca por hash de cookie y valida existencia, estado, expiración y
usuario. La rotación debe ser atómica en PostgreSQL mediante transacción con
bloqueo o actualización condicional que sólo permita consumir una vez la fila
activa. El token nuevo hereda `FamilyId` y `ExpiresAt` y nunca mueve la fecha.
Si se presenta una fila rotada, revocar todas las filas vigentes de esa familia.
Evitar incluir token raw o hash en logs/excepciones.

Logout marca la familia indicada como revocada. Un usuario eliminado pierde sus
sesiones por FK cascade. Las sesiones de otras familias quedan intactas.

`TimeProvider` se inyecta para todos los cálculos de expiración y pruebas. La
respuesta de refresh emite `Max-Age = max(0, ExpiresAt - now)`.

## Registro de servicios y configuración

- Registrar servicio de sesiones y DbContext con scope coherente.
- Ampliar configuración validada al inicio con orígenes permitidos de auth y
  política de cookie/entorno. No usar `Request.IsHttps` como único indicador
  cuando hay reverse proxy: configurar forwarded headers confiables correctamente.
- Mantener validación JWT bearer de firma, emisor, audiencia y expiración actual.
- Mantener rate limiting existente en login; aplicar política de autenticación
  también a refresh/logout para evitar abuso, sin depender sólo de la IP para
  controlar credenciales de sesión.
- Añadir metadatos OpenAPI de `Set-Cookie` como comportamiento, no como token de
  respuesta.

## Organización sugerida

- `Authentication/AuthEndpoints.cs`: `rememberMe`, cookie, refresh y logout.
- `Authentication/AuthContracts.cs`: DTO de login y sesión de acceso; conservar
  cuerpos seguros y separados de entidades.
- `Authentication/AuthService.cs`: validación credenciales/emisión JWT.
- `Authentication/PersistentLoginSessionService.cs` y su interfaz: emisión,
  lookup por hash, rotación, replay detection, logout.
- `Identity/PersistentLoginSession.cs`, configuración en
  `PlatformIdentityDbContext` y migración EF Core.
- Configuración tipada nueva para orígenes, expiración y seguridad de cookie.

## Verificación

- Unit tests: token aleatorio/hash, fecha absoluta y política de cookie por
  entorno con `TimeProvider`.
- Integración PostgreSQL: login con/sin remember, cookie flags y expiración,
  migración, refresh y rotación, expiry, logout idempotente, aislamiento entre
  familias, usuario inexistente, reuse detection y dos refresh concurrentes.
- Seguridad HTTP: refresh/logout rechazan origen faltante/no permitido, cabecera
  incorrecta, método/tipo de contenido incorrectos; respuestas no revelan
  motivos ni secretos.
- Regresión: registro no crea cookie; `/me` sigue exigiendo bearer; vocabulario,
  progreso y health mantienen sus contratos.
- Ejecutar `dotnet build` y todas las pruebas API con PostgreSQL disponible.
