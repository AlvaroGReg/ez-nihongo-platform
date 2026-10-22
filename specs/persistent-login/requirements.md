# Requisitos — Inicio de sesión persistente

## Convenciones

- **JWT de acceso:** credencial bearer firmada, de vida corta, enviada en
  `Authorization` para las APIs autenticadas existentes.
- **Token de renovación:** valor opaco aleatorio emitido por la API, no JWT, que
  sólo se transporta mediante cookie `HttpOnly`.
- Los errores HTTP usan `application/problem+json`, excepto donde se especifica
  `204 No Content`.
- La duración de sesión persistente es absoluta y no supera 30 días desde el
  login que la creó.

## Historias y requisitos

### US-01 — Elegir una sesión persistente

Como cliente de autenticación, quiero indicar al hacer login si necesito
restaurar la sesión tras reiniciar el navegador.

- **REQ-01:** `POST /api/v1/auth/login` acepta `rememberMe` booleano opcional;
  si falta, se interpreta como `false` para mantener compatibilidad.
- **REQ-02:** Con `rememberMe=false`, credenciales válidas devuelven el JWT de
  acceso y la identidad actuales, pero no crean sesión persistente. Si el
  navegador presenta una cookie de renovación anterior, la API la revoca tras
  validar el login y la expira.
- **REQ-03:** Con `rememberMe=true`, la API devuelve el JWT de acceso corto e
  identidad y crea una sesión persistente cuya expiración absoluta es
  `CreatedAt + 30 días`.
- **REQ-04:** Registro no acepta ni activa `rememberMe`, no crea sesión
  persistente y no establece cookie de renovación.
- **REQ-05:** Credenciales inválidas mantienen el `401` genérico actual; no
  emiten JWT ni crean, rotan o revocan una sesión existente.

### US-02 — Transportar la sesión persistente

Como usuario, quiero que el token de renovación no pueda ser leído por scripts de
la aplicación ni enviado a endpoints que no requieren renovación.

- **REQ-06:** El token de renovación sólo se entrega mediante cookie
  `ez-nihongo-refresh`; nunca aparece en cuerpo, URL, logs o errores.
- **REQ-07:** En producción, la cookie es host-only (sin `Domain`), `HttpOnly`,
  `Secure`, `SameSite=Strict`, `Path=/api/v1/auth` y `Max-Age` no supera el
  tiempo restante de la sesión absoluta.
- **REQ-08:** La cookie se expira usando el mismo nombre, ruta y host scope al
  completar logout, rechazar un token inválido o reemplazar una sesión anterior.
- **REQ-09:** `Secure` sólo puede deshabilitarse mediante configuración explícita
  de entorno de desarrollo local HTTP; una configuración ausente no puede
  degradar producción.

### US-03 — Rotar la sesión

Como cliente web, quiero intercambiar la cookie por un JWT de acceso corto y una
nueva cookie para que una credencial robada no pueda reutilizarse indefinidamente.

- **REQ-10:** `POST /api/v1/auth/refresh` acepta la cookie y emite JWT de acceso
  válido e identidad sólo si la sesión existe, no venció, no está revocada y el
  usuario sigue existiendo.
- **REQ-11:** Cada refresh válido invalida atómicamente el token presentado y
  emite un token aleatorio nuevo de al menos 256 bits de entropía para la misma
  familia y fecha de vencimiento absoluta.
- **REQ-12:** La reutilización de un token ya rotado revoca todos los tokens
  activos de su familia. La respuesta no distingue entre token inexistente,
  vencido, revocado o reutilizado.
- **REQ-13:** La API sólo persiste SHA-256 o HMAC del token opaco; el valor
  original no se escribe a PostgreSQL ni a logs.
- **REQ-14:** Una cookie ausente, incorrecta, vencida o revocada devuelve `401`
  genérico y `Set-Cookie` para expirar la cookie.

### US-04 — Revocar una sesión

Como usuario, quiero cerrar la sesión de este navegador sin cerrar mis otras
sesiones.

- **REQ-15:** `POST /api/v1/auth/logout` revoca la sesión/familia indicada por
  cookie si existe, expira la cookie y responde `204 No Content` incluso cuando
  ya no existe una sesión válida.
- **REQ-16:** La revocación afecta sólo a la familia de esa cookie; no invalida
  sesiones de otros navegadores/dispositivos.
- **REQ-17:** Logout no puede revocar inmediatamente JWT bearer ya emitidos; éstos
  conservan validez hasta su expiración corta configurada.

### US-05 — Proteger operaciones basadas en cookie

Como responsable de plataforma, quiero evitar que un sitio externo provoque
renovaciones o cierres de sesión no autorizados.

- **REQ-18:** Refresh y logout requieren `Origin` que coincida exactamente con
  uno de los orígenes de aplicación configurados y la cabecera
  `X-EZ-Nihongo-Request: 1`; solicitudes inválidas se rechazan sin cambiar la
  sesión.
- **REQ-19:** Las operaciones que usan la cookie requieren método y tipo de
  contenido esperados. No se habilita CORS con credenciales y origen `*`.
- **REQ-20:** Login, refresh y logout permanecen bajo rate limit configurable y
  sus respuestas OpenAPI documentan cuerpos, cookies (sin exponer su valor) y
  estados principales.

### US-06 — Persistir sesiones con seguridad

Como operador, quiero que las sesiones persistan a reinicios y se validen en el
servidor, no confiando en estado controlado por el cliente.

- **REQ-21:** Las sesiones se almacenan en PostgreSQL con migración EF Core
  aditiva y FK al usuario Identity. Usuario propietario deriva de la cuenta
  validada, nunca de un ID proporcionado por el cliente.
- **REQ-22:** La API valida expiración y revocación usando UTC y `TimeProvider`.
  La ausencia/falla de base de datos no habilita un modo sin persistencia seguro
  ni aceptación ciega de tokens.
- **REQ-23:** Ninguna respuesta, excepción o log de login/refresh/logout incluye
  contraseña, JWT, token de renovación, secreto de firma ni valor original de
  cookie.
- **REQ-24:** `/health`, `/api/v1/vocabulary/**` y contratos existentes de JWT
  bearer conservan el comportamiento actual; la cookie de renovación no sirve
  como autenticación para esas rutas ni para `/api/v1/auth/me`.

## Escenarios verificables

```gherkin
Scenario: login persistente crea sesión revocable
  Given credenciales válidas
  When POST /api/v1/auth/login recibe rememberMe=true
  Then responde 200 con identidad y JWT de acceso corto
  And establece una cookie HttpOnly con vencimiento máximo de 30 días
  And almacena sólo el hash del token de renovación
```

```gherkin
Scenario: login sin persistencia
  Given credenciales válidas
  When POST /api/v1/auth/login recibe rememberMe=false o no recibe el campo
  Then responde con identidad y JWT de acceso corto
  And no crea sesión persistente
  And expira cualquier cookie de renovación previa de ese navegador
```

```gherkin
Scenario: refresh rota el token y conserva fecha absoluta
  Given una cookie de sesión vigente
  When el cliente llama POST /api/v1/auth/refresh desde origen permitido
  Then recibe un JWT de acceso y una cookie nueva
  And la cookie anterior ya no puede renovar
  And la nueva cookie no extiende el vencimiento original de 30 días
```

```gherkin
Scenario: reutilización revoca familia
  Given un token de renovación que ya se rotó
  When se presenta de nuevo
  Then la API revoca las credenciales activas de esa familia
  And requiere iniciar sesión otra vez
```

```gherkin
Scenario: origen externo no puede usar cookie
  Given una cookie de renovación válida
  When refresh o logout llega con Origin no permitido o sin cabecera requerida
  Then la API rechaza la solicitud
  And no renueva ni revoca la sesión
```

```gherkin
Scenario: logout no afecta otra sesión
  Given dos familias de sesión válidas para el mismo usuario
  When logout se invoca con la cookie de una familia
  Then esa familia queda revocada y su cookie expirada
  And la otra familia continúa válida
```

## Fuera de alcance verificable

- Tokens de acceso almacenados en cookie o renovación de JWT bearer en endpoints
  de negocio.
- Refresh JWT de larga duración, contraseña recordada o cifrado de contraseña
  en cliente.
- Logout global, administración de sesiones, rotación por cambio de contraseña,
  recuperación de cuenta, MFA y revocación inmediata de JWT de acceso.
