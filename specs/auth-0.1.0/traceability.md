# Trazabilidad — Autenticación de plataforma 0.1.0

| Requisito | Diseño/área prevista | Verificación prevista |
| --- | --- | --- |
| REQ-01 | Endpoint `POST /api/v1/auth/register`, DTO y servicio de Identity | Integración: registro nuevo devuelve `201`, usuario y JWT válido |
| REQ-02 | Normalización Identity e índice único en PostgreSQL | Integración: mismo correo con distinta capitalización devuelve `409`; caso concurrente |
| REQ-03 | Modelo/contrato de cuenta sin estado de verificación | Contrato y prueba de registro sin correo verificado |
| REQ-04 | Validación de contraseña y Problem Details | Unidad/integración: contraseña insuficiente devuelve `400` sin valores secretos |
| REQ-05 | Endpoint `POST /api/v1/auth/login` | Integración: credenciales válidas devuelven `200` y JWT validable |
| REQ-06 | Respuesta genérica de autenticación | Integración: correo inexistente y contraseña errónea devuelven mismo status y detalle público |
| REQ-07 | Identity password hasher y política de logging | Prueba de persistencia verifica hash; revisión de logs/respuestas |
| REQ-08 | Endpoint protegido `GET /api/v1/auth/me` | Integración: devuelve identidad del `sub`, no admite ID arbitrario |
| REQ-09 | Middleware JWT bearer | Integración: token ausente, alterado o expirado produce `401` |
| REQ-10 | Configuración de rutas existente en `Program.cs` | Regresión anónima de `/health` y endpoints de vocabulario |
| REQ-11 | Metadatos OpenAPI | Inspección de `/openapi/v1.json`: rutas, DTOs, errores y esquema bearer |
| REQ-12 | ASP.NET Core Identity | Prueba de almacenamiento: no hay contraseña en claro ni hash manual |
| REQ-13 | Configuración JWT bearer | Pruebas de firma, emisor, audiencia, expiración y configuración ausente |
| REQ-14 | Rate limiter en endpoints de registro/login | Integración: superar el límite configurable devuelve `429` |
| REQ-15 | Manejo de errores y logging | Pruebas/revisión: respuestas y logs sin token, password ni clave |
| REQ-16 | Inicio de servicios y conexión PostgreSQL | Prueba de configuración e integración: fallo de conexión/configuración no habilita auth insegura |

## Criterio de cierre

La especificación se considera implementada cuando todos los requisitos tienen
una verificación aprobada, las rutas previas mantienen compatibilidad, no se
introduce refresh token/roles/verificación/recuperación y la integración de
PostgreSQL necesaria para ejecución local está documentada.
