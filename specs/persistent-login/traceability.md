# Trazabilidad — Inicio de sesión persistente

| Requisito | Diseño/área prevista | Verificación prevista |
| --- | --- | --- |
| REQ-01–05 | DTO y `AuthService.LoginAsync`/registro | Integración: login default, `rememberMe` true/false, login fallido no muta sesión |
| REQ-06–09 | Configuración de cookie en endpoints de auth | Integración: HttpOnly/Secure/SameSite/Path/Max-Age, borrado, política prod/dev |
| REQ-10–14 | Servicio de sesión persistente y endpoint refresh | Integración: renovación válida, expiración, revocación, cookie inválida, DTO sin secreto |
| REQ-11–13 | Entidad, hash y rotación transaccional | Integración PostgreSQL: una rotación, refresh concurrente, replay revoca familia |
| REQ-15–17 | Endpoint logout y revocación por FamilyId | Integración: idempotencia, familia destino revocada, otras familias y JWT corto |
| REQ-18–20 | Validación de origen/cabecera, rate limit y OpenAPI | Integración: origen/cabecera/método/content type incorrectos; inspección OpenAPI |
| REQ-21–22 | `PlatformIdentityDbContext`, migración y `TimeProvider` | Prueba migración, expiración simulada y recuperación tras reiniciar API |
| REQ-23 | Logging y mapeo de errores | Pruebas/revisión: no aparecen secretos en respuesta ni log |
| REQ-24 | Autenticación JWT bearer existente | Regresión: `/me` bearer-only y health/vocabulario/progreso sin cambios |
| Integración web | [`specs/persistent-login`](../../../ez-nihongo-web/specs/persistent-login/) | Contrato compartido: cookie same-origin, restore/logout E2E y tokens sólo en memoria |

## Criterio de cierre

Todos los requisitos REQ-01–REQ-24 deben tener pruebas aprobadas. Migración,
variables de configuración, política HTTPS/proxy y contrato para el cliente
deben estar documentados. La especificación web debe apuntar a esta funcionalidad como
fuente de verdad de la API.
