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

- [ ] Configurar autenticación JWT bearer con validación explícita de firma,
      emisor, audiencia y expiración.
- [ ] Añadir DTOs de registro, login, token e identidad; no exponer entidades
      Identity.
- [ ] Implementar registro con `201 Created`, cuenta persistida y JWT.
- [ ] Implementar login con `200 OK`, JWT y respuesta genérica `401`.
- [ ] Implementar `GET /api/v1/auth/me` protegido y respuestas `401`.
- [ ] Asegurar que vocabulario y health siguen siendo anónimos.
- [ ] Añadir rate limiting configurable para registro y login.
- [ ] Añadir metadatos OpenAPI de respuestas y seguridad bearer.

## Fase 4 — Pruebas y documentación operativa

- [ ] Añadir pruebas unitarias de validación, normalización y emisión/validación
      de tokens.
- [ ] Añadir pruebas de integración PostgreSQL para registro, colisión de
      correo, login, `/me` y acceso público a rutas actuales.
- [ ] Verificar `400`, `401`, `409` y `429` junto con Problem Details.
- [ ] Revisar que logs y respuestas no contengan secretos ni hash de contraseña.
- [ ] Documentar variables requeridas, configuración de desarrollo y migración
      en README.
- [ ] Crear un archivo `.http` con ejemplos de registro, login y `/me`.
- [ ] Ejecutar `dotnet build` y todas las pruebas disponibles.
- [ ] Planificar por separado la integración de PostgreSQL en Compose.
