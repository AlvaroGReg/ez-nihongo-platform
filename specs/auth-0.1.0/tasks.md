# Tareas — Autenticación de plataforma 0.1.0

## Fase 1 — Especificación

- [x] Registrar decisiones confirmadas en `proposal.md`.
- [x] Definir requisitos y escenarios en `requirements.md`.
- [x] Describir contratos y arquitectura en `design.md`.
- [x] Mapear requisitos a verificación en `traceability.md`.
- [x] Fijar una duración inicial propuesta del JWT de 15 minutos, configurable
      por entorno; validar el valor al iniciar.

## Fase 2 — Persistencia e identidad

- [x] Añadir paquetes EF Core, herramientas de migración y proveedor Npgsql
      compatibles con .NET 10.
- [x] Definir DbContext y usuario basado en ASP.NET Core Identity, sin roles.
- [x] Configurar correo normalizado y único con protección ante carreras de
      registro.
- [x] Añadir migración inicial y documentar comandos para aplicarla.
- [x] Registrar servicios Identity y DbContext en DI.
- [x] Añadir validación de configuración y fallo seguro si PostgreSQL no está
      configurado o accesible.

## Fase 3 — JWT y endpoints

- [x] Configurar autenticación JWT bearer con validación explícita de firma,
      emisor, audiencia y expiración.
- [x] Añadir DTOs de registro, login, token e identidad; no exponer entidades
      Identity.
- [x] Implementar registro con `201 Created`, cuenta persistida y JWT.
- [x] Implementar login con `200 OK`, JWT y respuesta genérica `401`.
- [x] Implementar `GET /api/v1/auth/me` protegido y respuestas `401`.
- [x] Asegurar que vocabulario y health siguen siendo anónimos.
- [x] Añadir rate limiting configurable para registro y login.
- [x] Añadir metadatos OpenAPI de respuestas y seguridad bearer.

## Fase 4 — Pruebas y documentación operativa

- [x] Añadir pruebas unitarias de validación de solicitudes y configuración JWT;
      comprobar normalización y emisión/validación de tokens en integración.
- [x] Añadir pruebas de integración PostgreSQL para registro, colisión de
      correo concurrente, login, `/me` y acceso público a rutas actuales.
- [x] Verificar `400`, `401`, `409` y `429` junto con Problem Details.
- [x] Comprobar en respuestas y flujo de persistencia que no se exponen secretos
      ni se almacena la contraseña en claro.
- [x] Documentar variables requeridas, configuración de desarrollo y migración
      en README.
- [x] Crear un archivo `.http` con ejemplos de registro, login y `/me`.
- [x] Ejecutar `dotnet build` y las pruebas disponibles; la suite PostgreSQL
      queda omitida si `AUTH_TEST_POSTGRES_CONNECTION` no está configurada.
- [x] Integrar PostgreSQL en el Compose del workspace con persistencia,
      health check y configuración de conexión para la API.
