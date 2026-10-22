# Tareas — Inicio de sesión persistente

## Fase 1 — Especificación y coordinación

- [ ] Revisar contrato HTTP con `ez-nihongo-web/specs/persistent-login/` y
      acordar nombres de campos, cookie, orígenes y errores.
- [ ] Confirmar política de `Secure` para Compose local, HTTPS de producción y
      proxy de confianza.
- [ ] Confirmar la estrategia de migraciones para instalaciones existentes.

## Fase 2 — Persistencia

- [ ] Añadir entidad de sesión persistente, FK a usuario, índices e invariantes.
- [ ] Añadir migración EF Core aditiva y comprobar upgrade desde el esquema
      actual.
- [ ] Implementar servicio para crear familia, buscar por hash, rotar
      atómicamente, detectar replay y revocar familia.
- [ ] Inyectar `TimeProvider`; mantener expiración absoluta no deslizante de 30
      días.

## Fase 3 — Contratos y endpoints

- [ ] Extender DTO de login con `rememberMe=false` por defecto; no cambiar DTO de
      registro.
- [ ] Crear cookie de producción con `HttpOnly`, `Secure`, `SameSite=Strict`,
      ruta auth, host-only y `Max-Age` restante.
- [ ] Implementar refresh que valide origen/CSRF y rote una única vez.
- [ ] Implementar logout idempotente, revocación por familia y expiración de
      cookie.
- [ ] Mantener JWT bearer para `/me` y APIs actuales; preservar rate limit y
      documentar status/contratos en OpenAPI.
- [ ] Añadir configuración tipada de orígenes y flags por entorno con validación
      segura al arranque.

## Fase 4 — Pruebas de seguridad e integración

- [ ] Cubrir login persistente/no persistente y login fallido con PostgreSQL.
- [ ] Cubrir cookie flags, expiración absoluta y `TimeProvider`.
- [ ] Cubrir rotación, refresh concurrente y reuse detection con revocación de
      familia.
- [ ] Cubrir logout, aislamiento de sesiones de otros dispositivos y usuario
      eliminado.
- [ ] Cubrir CSRF/origin, cabeceras requeridas, rate limit y ausencia de secretos
      en respuestas/logs.
- [ ] Verificar migración, reinicio del servicio, `/me` bearer-only y rutas
      públicas actuales.
- [ ] Ejecutar `dotnet build` y la suite completa de pruebas con PostgreSQL.

## Fase 5 — Integración con web

- [ ] Verificar restore inicial, petición `credentials: include`, cookie same
      origin y logout con la implementación del cliente.
- [ ] Ejecutar E2E de sesión persistente, sesión normal, vencimiento, pestañas
      concurrentes y cierre de sesión.
- [ ] Marcar trazabilidad completa y actualizar documentación operativa de
      configuración, HTTPS/proxy y migración.
