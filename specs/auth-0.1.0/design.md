# Diseño técnico — Autenticación de plataforma 0.1.0

## Contexto actual

La API es una Minimal API de ASP.NET Core 10. `Program.cs` registra OpenAPI,
Problem Details y el catálogo de vocabulario cargado desde JSON. No hay
persistencia relacional, modelo de usuario ni autenticación. Las rutas actuales
son públicas y deben permanecerlo.

## Arquitectura propuesta

- Mantener Minimal APIs y separar endpoints de autenticación en un módulo
  dedicado, mapeado desde `Program.cs`.
- Usar ASP.NET Core Identity para gestión y hash de contraseñas.
- Usar Entity Framework Core con el proveedor Npgsql para PostgreSQL.
- Usar JWT bearer para autenticar peticiones a `/api/v1/auth/me`.
- No agregar roles de Identity ni políticas de autorización por rol.
- Mantener DTOs de API separados de entidades de persistencia.

## Contratos HTTP

### `POST /api/v1/auth/register`

Solicitud:

```json
{
  "email": "student@example.com",
  "password": "example-password"
}
```

Respuesta `201 Created`:

```json
{
  "user": {
    "id": "<stable-user-id>",
    "email": "student@example.com"
  },
  "accessToken": "<signed-jwt>",
  "tokenType": "Bearer",
  "expiresAt": "<UTC timestamp>"
}
```

La respuesta incluye `Location: /api/v1/auth/me`, que identifica el recurso
creado para el usuario autenticado. Errores previstos: `400` para campos o
contraseña no válidos, `409` para correo duplicado y `429` al superar el límite
de intentos. La respuesta no incluye hash, contraseña, claims internos ni
estado de verificación del correo.

### `POST /api/v1/auth/login`

Usa el mismo cuerpo de entrada que registro. Responde `200 OK` con la misma
forma de token y usuario. Credenciales inválidas responden `401` con texto
genérico, sin distinguir cuenta inexistente de contraseña incorrecta.

### `GET /api/v1/auth/me`

Requiere `Authorization: Bearer <accessToken>`. Responde `200 OK`:

```json
{
  "id": "<stable-user-id>",
  "email": "student@example.com"
}
```

La ausencia, firma inválida o expiración del token produce `401 Unauthorized`.

### Endpoints existentes

`/health` y `/api/v1/vocabulary/**` no reciben la política de autorización.
Los cambios deben incluir una prueba de regresión de acceso anónimo.

## Identidad y persistencia

- Entidad de usuario basada en Identity con ID estable y correo normalizado.
- Configurar Identity para permitir iniciar sesión sin correo confirmado; activar
  la unicidad de correo normalizado.
- PostgreSQL como almacén de usuarios y metadatos de Identity.
- Contraseña almacenada únicamente como hash con el `IPasswordHasher` de
  Identity.
- Índice/constraint único sobre el correo normalizado para proteger también
  frente a registros concurrentes.
- Esquema creado y actualizado mediante migraciones EF Core versionadas.
- El catálogo JLPT continúa cargándose desde JSON y no se migra a PostgreSQL.

## JWT y comportamiento de sesión

- Emitir un JWT de acceso con sujeto estable (`sub`), correo y claims estándar
  necesarios para validación. No incluir contraseña, hash ni roles.
- Validar firma, emisor, audiencia y expiración con parámetros explícitos.
- El emisor, audiencia, clave de firma y duración se configuran por entorno.
- Propuesta de duración inicial: 15 minutos, configurable. La duración debe
  poder ajustarse sin recompilar.
- No hay refresh token ni lista persistente de revocación. El cliente cierra su
  sesión descartando el token; un JWT emitido conserva validez hasta expirar.
- En producción, la clave JWT debe provenir de un almacén/secret manager de
  despliegue. No usar claves de desarrollo ni valores de ejemplo en producción.

## Validación, respuestas y abuso

- Validación explícita de solicitudes de Minimal API en .NET 10.
- Respuestas de validación y conflictos en Problem Details, sin incluir valores
  secretos ni mensajes internos de Identity.
- Asegurar que correo duplicado se responda consistentemente y sin devolver
  datos de la cuenta.
- Aplicar rate limiting a registro y login; configurar política y partición de
  forma coherente con el proxy/despliegue. No incluir datos sensibles en las
  claves de partición ni en logs.
- HTTPS es obligatorio fuera del desarrollo local.
- OpenAPI describe los cuerpos, respuestas de error y esquema HTTP bearer.

## Configuración y despliegue

Variables/configuración requeridas (nombres exactos a fijar al implementar):

- Cadena de conexión de PostgreSQL.
- Clave JWT de alta entropía.
- Emisor, audiencia y duración del JWT.
- Parámetros del rate limiter.

La integración del workspace define PostgreSQL en `compose.yaml`, con volumen
persistente, health check y conexión interna para la API. Las credenciales se
inyectan desde variables locales no versionadas. El esquema continúa aplicándose
explícitamente mediante migraciones versionadas antes de usar autenticación.

## Pruebas

- Pruebas de servicios/validación de Identity y generación/validación JWT.
- Pruebas de integración contra PostgreSQL real o instancia efímera compatible
  con PostgreSQL; evitar depender de semánticas distintas de SQLite para índices
  y normalización.
- Cobertura de status codes, Problem Details, correo duplicado concurrente y
  ausencia de filtraciones en respuestas.
- Prueba de autorización en `/me` y regresión de rutas públicas existentes.
- Verificación manual de OpenAPI y de configuración de inicio.
