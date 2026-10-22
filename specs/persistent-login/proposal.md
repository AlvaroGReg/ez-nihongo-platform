# Propuesta SDD — Inicio de sesión persistente

## Estado

Propuesta para implementar en conjunto con el cliente web. La API es propietaria
de los contratos HTTP, la persistencia de sesiones y la seguridad de las cookies.

## Problema

La autenticación existente emite JWT de acceso, pero no proporciona renovación
ni revocación de sesiones. El cliente guarda el JWT en `localStorage`. Extender
esa persistencia o almacenar contraseñas en el navegador aumentaría la exposición
de credenciales.

## Objetivo

Añadir sesiones persistentes de hasta 30 días a la API usando una credencial de
renovación opaca, revocable y transportada en cookie `HttpOnly`, sin persistir
contraseñas ni JWT de acceso en el navegador. Mantener JWT bearer de vida corta
para las APIs actuales.

## Decisiones de alcance

- La autenticación, renovación y revocación son responsabilidad de la API de
  `ez-nihongo-platform`.
- El cliente elige al iniciar sesión si solicita persistencia con `rememberMe`.
- La duración máxima es de 30 días absolutos por sesión; rotar no extiende ese
  vencimiento.
- Los tokens de renovación se rotan y sólo se persiste su hash en PostgreSQL.
- Registro y login no persistente no crean sesión de renovación.
- El JWT de acceso conserva su duración corta configurable y no se acepta la
  cookie como credencial para endpoints de negocio.
- La implementación se coordina con
  [`specs/persistent-login`](../../../ez-nihongo-web/specs/persistent-login/),
  propietaria del flujo y estado del navegador.

## Alcance

- Extender login con una opción `rememberMe` opcional, compatible con clientes
  que no la envían.
- Crear sesión persistente, emitir cookie protegida, rotar tokens, detectar
  reutilización, revocar sesión y expirar cookie.
- Añadir tabla y migración EF Core sobre la base Identity existente.
- Proteger endpoints cookie-authenticated frente a CSRF y limitar su origen.
- Documentar contratos OpenAPI, configuración, migración y pruebas.

## Fuera de alcance

- Guardar o recuperar contraseñas desde el navegador.
- Cambiar el flujo actual de JWT bearer para vocabulario, progreso u otras APIs.
- Sesiones de duración superior a 30 días, gestión de dispositivos desde UI,
  MFA, recuperación/cambio de contraseña o proveedores externos.
- Revocar inmediatamente JWT de acceso ya emitidos; el efecto del logout en un
  JWT queda limitado a su vencimiento corto.

## Criterio de éxito

La API restaura un login persistente mediante cookie HttpOnly hasta el límite
absoluto, permite revocarlo, detecta reutilización de tokens rotados, y no
persiste ni expone contraseñas o tokens de renovación en claro. La web nunca
persiste el JWT de acceso y ambas especificaciones integran sus contratos sin
duplicar la propiedad de implementación.
