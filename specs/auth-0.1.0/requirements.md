# Requisitos — Autenticación de plataforma 0.1.0

## Convenciones

- **JWT de acceso:** token firmado y de duración limitada enviado como bearer
  token en la cabecera HTTP `Authorization`.
- Los errores de API usan `application/problem+json` (Problem Details).
- El correo se normaliza antes de comprobar unicidad o autenticar.

## Historias y requisitos

### US-01 — Crear una cuenta

Como persona nueva, quiero registrarme con mi correo y contraseña para obtener
una identidad en la plataforma.

- **REQ-01:** `POST /api/v1/auth/register` debe validar los campos requeridos,
  crear la cuenta y responder `201 Created` con los datos públicos de la cuenta
  y un JWT de acceso.
- **REQ-02:** El correo debe ser único tras normalizarlo. Una dirección ya
  registrada debe producir `409 Conflict` sin exponer datos de la cuenta
  existente.
- **REQ-03:** La API no debe afirmar ni requerir que el correo esté verificado.
- **REQ-04:** Una contraseña que no cumpla la política configurada debe producir
  `400 Bad Request` con errores de validación que no revelen secretos.

### US-02 — Iniciar sesión

Como usuario registrado, quiero autenticarme con correo y contraseña para
obtener un JWT de acceso.

- **REQ-05:** `POST /api/v1/auth/login` debe devolver `200 OK` y un JWT de acceso
  ante credenciales válidas.
- **REQ-06:** Credenciales incorrectas deben producir `401 Unauthorized` con un
  mensaje genérico idéntico tanto si el correo existe como si no.
- **REQ-07:** La contraseña nunca se almacena ni se registra en texto claro.

### US-03 — Consultar la identidad autenticada

Como cliente autenticado, quiero consultar mi propia identidad sin enviar un
identificador de usuario manipulable.

- **REQ-08:** `GET /api/v1/auth/me` debe exigir un JWT válido y devolver el ID y
  el correo normalizado de la identidad correspondiente al sujeto del token.
- **REQ-09:** Una petición sin token, con firma inválida o con token expirado
  debe producir `401 Unauthorized`.

### US-04 — Mantener acceso público al catálogo

Como consumidor actual de la API, quiero seguir consultando vocabulario sin
crear una cuenta.

- **REQ-10:** `/health` y todos los endpoints existentes de vocabulario deben
  permanecer anónimos y conservar sus contratos y validaciones actuales.
- **REQ-11:** La API debe documentar los esquemas bearer/JWT y las respuestas
  principales en OpenAPI.

### US-05 — Proteger credenciales y disponibilidad

Como responsable de la plataforma, quiero proteger los datos de cuenta y
reducir el abuso de los endpoints de autenticación.

- **REQ-12:** Las contraseñas deben procesarse mediante el hasher seguro de
  ASP.NET Core Identity; nunca se almacenan en claro ni mediante hash propio.
- **REQ-13:** El JWT debe validar firma, emisor, audiencia y expiración. La clave
  de firma se obtiene de configuración segura y no se incluye en el repositorio.
- **REQ-14:** Los intentos de registro e inicio de sesión deben estar limitados
  por una política configurable; superar el límite devuelve `429 Too Many
  Requests`.
- **REQ-15:** Las respuestas de error y los logs no deben incluir contraseñas,
  JWT, claves de firma ni detalles internos de persistencia.
- **REQ-16:** Si PostgreSQL o la configuración JWT requeridos no están
  disponibles, la aplicación no debe iniciar en un estado que acepte
  autenticaciones inseguras. Los errores de base de datos no deben filtrar
  información interna al cliente.

## Escenarios verificables

```gherkin
Scenario: registro correcto
  Given que el correo aún no está registrado
  When el cliente envía correo y contraseña válidos a POST /api/v1/auth/register
  Then la API responde 201 Created
  And persiste la cuenta en PostgreSQL
  And devuelve los datos públicos de la cuenta y un JWT de acceso válido
```

```gherkin
Scenario: correo duplicado
  Given que el correo ya está registrado, sin importar mayúsculas/minúsculas
  When el cliente intenta registrarlo de nuevo
  Then la API responde 409 Conflict
  And la respuesta no revela datos de la cuenta existente
```

```gherkin
Scenario: login correcto
  Given una cuenta registrada con contraseña válida
  When el cliente envía sus credenciales a POST /api/v1/auth/login
  Then la API responde 200 OK con un JWT de acceso válido
```

```gherkin
Scenario: credenciales incorrectas
  When el cliente envía credenciales inválidas a POST /api/v1/auth/login
  Then la API responde 401 Unauthorized con un error genérico
  And la respuesta no revela si el correo está registrado
```

```gherkin
Scenario: consultar identidad propia
  Given un JWT de acceso válido
  When el cliente llama GET /api/v1/auth/me con Authorization Bearer
  Then la API responde 200 OK con el ID y correo de la cuenta del token
```

```gherkin
Scenario: token ausente o inválido
  When el cliente llama GET /api/v1/auth/me sin un JWT válido
  Then la API responde 401 Unauthorized
```

```gherkin
Scenario: rutas actuales siguen siendo públicas
  When un cliente anónimo consulta /health o una ruta de vocabulario
  Then la API conserva la respuesta actual sin exigir autenticación
```

```gherkin
Scenario: límite de intentos excedido
  Given que un cliente ha superado el límite configurado
  When vuelve a llamar a un endpoint limitado de autenticación
  Then la API responde 429 Too Many Requests
```

## Fuera de alcance verificable

- No hay endpoints de refresh, logout, recuperación ni confirmación de correo.
- No se aceptan ni emiten roles o permisos de usuario.
- Desechar el JWT en el cliente cierra la sesión desde ese cliente; el token ya
  emitido sigue siendo válido hasta su expiración.
