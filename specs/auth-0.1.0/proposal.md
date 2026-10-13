# Propuesta SDD — Autenticación de plataforma 0.1.0

## Estado

Propuesta aprobada para planificación. Las decisiones de producto registradas
en este documento están fijadas para la primera implementación.

## Problema

La API de plataforma sirve actualmente el catálogo JLPT sin cuentas ni
autenticación. Se necesita una capacidad mínima para que una persona cree una
cuenta y obtenga una identidad autenticada, sin añadir todavía funciones de
gestión de cuenta que no forman parte del alcance inicial.

## Objetivo

Proporcionar registro e inicio de sesión en la API de plataforma usando correo y
contraseña, persistencia PostgreSQL y JWT de acceso.

## Decisiones confirmadas

- Se incluye el registro de usuarios.
- El correo no se confirma ni se verifica en esta versión.
- No se incluye recuperación ni cambio de contraseña.
- Se emiten JWT de acceso; no hay refresh tokens.
- PostgreSQL es el almacén persistente de usuarios.
- No hay roles ni permisos asociados a roles.
- El alta registra al usuario y devuelve un JWT para iniciar sesión de inmediato.
- La autenticación y las cuentas pertenecen a la API de `ez-nihongo-platform`.

## Alcance

- Crear una cuenta con correo y contraseña.
- Iniciar sesión con correo y contraseña.
- Consultar la identidad de la cuenta usando un JWT válido.
- Persistir usuarios en PostgreSQL.
- Documentar y configurar la autenticación de la API mediante OpenAPI.
- Limitar intentos de registro e inicio de sesión para dificultar abuso.
- Conservar los endpoints públicos existentes de salud y vocabulario.

## Fuera de alcance

- Confirmación/verificación de correo.
- Recuperación, restablecimiento o cambio de contraseña.
- Refresh tokens, sesiones persistentes del servidor y endpoint de logout.
- Inicio de sesión social, OAuth/OIDC y proveedores externos.
- Roles, permisos por rol, suscripciones o entitlements.
- Interfaz de usuario y cambios en el cliente web.
- Sincronización de datos de estudio o progreso del usuario.
- Cambio de la infraestructura de despliegue del workspace. El Compose deberá
  configurarse con PostgreSQL antes de desplegar conjuntamente la API con esta
  especificación.

## Criterio de éxito

Un usuario nuevo puede registrarse y recibir credenciales; puede usar el JWT
para consultar su identidad; credenciales erróneas o tokens inválidos no dan
acceso; y las rutas de catálogo existentes siguen operativas y públicas.
