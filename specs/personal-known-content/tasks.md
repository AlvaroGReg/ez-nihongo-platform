# Tareas — Lista personal de contenido conocido (plataforma)

## Reglas para sesiones de implementación

- Antes de cada sesión: leer esta carpeta y la especificación web enlazada;
  comprobar rama/estado de ambos repositorios y preservar cambios ajenos.
- Trabajar sólo en los archivos asignados a la tarea. No cambiar contratos sin
  actualizar primero ambas especificaciones.
- No iniciar Fase 2 hasta que el contrato congelado de `design.md` esté reflejado
  en ambas especificaciones. OpenAPI se verifica en Fase 3, al implementar rutas.
- Ejecutar y reportar validaciones de la tarea; no marcar tareas completas sólo
  porque compilen archivos aislados.

## Fase 1 — Cierre SDD (sesión de coordinación, sin código)

- [x] Sincronizar rutas, DTO, límites, unicidad y borrado con
      `ez-nihongo-web/specs/personal-known-content/`.
- [x] Confirmar qué constituye exactamente un carácter kanji y normalización
      Unicode compatible con búsqueda de duplicados.
- [x] Confirmar que romaji obligatorio, kanji de un carácter y límites acordados
      coinciden con los requisitos web.
- [x] Actualizar requisitos, diseños y traceability en ambos repositorios y
      congelar contrato; OpenAPI se verifica durante la sesión de endpoints.

## Fase 2 — Persistencia de lista (sesión API 1)

- [x] Añadir entidad, configuración EF, índices únicos y restricciones de origen.
- [x] Añadir migración aditiva y comprobar upgrade desde esquema actual.
- [x] Implementar servicio de consulta paginada y mutaciones con propietario del
      JWT, sin exponer IDs de otra cuenta.
- [x] Añadir pruebas de unidad de normalización y pruebas PostgreSQL de
       constraints/doble escritura concurrente.
- [x] Validar: `dotnet build` y tests de integración relacionados.

## Fase 3 — Contratos HTTP y validadores (sesión API 2)

- [x] Añadir DTOs/validadores de añadir catálogo y crear contenido propio.
- [x] Mapear GET paginado, POST catálogo `{item,alreadyPresent}`, POST custom y
       DELETE; incluir autorización, Problem Details y OpenAPI del contrato.
- [x] Resolver refs con catálogos oficiales en servidor; añadir sólo datos
       canónicos y rechazar tipo/ID incompatibles.
- [x] Añadir pruebas HTTP de estado, autenticación, contenido inválido, duplicados,
       paginación y eliminación idempotente.
- [x] Validar: `dotnet build` y tests API relacionados.

## Fase 4 — Consulta privada del diccionario (sesión API 3)

- [x] Confirmar que GET paginado por tipo expone campos de presentación del
       diccionario, con límite de tamaño acordado.
- [x] Verificar que contenido personalizado sólo aparece para su propietario y
       que entradas de catálogo conservan IDs estables.
- [x] Añadir pruebas de integración de lista vacía/mixta e aislamiento.
- [x] Confirmar OpenAPI y actualizar traceability del contrato. No se crea acceso
       a contenido personal para ejercicios.

## Fase 5 — Integración y cierre (sesión conjunta)

Estado: **En curso**. El bloqueo de navegación identificado en el cliente está
corregido y probado; siguen pendientes los escenarios integrados de CRUD y
aislamiento desde navegador, y la revisión final.

- [ ] Integrar consumo web y comprobar flujos con cuenta A/cuenta B, catálogo,
       contenido propio y borrado en diccionario.
- [x] Ejecutar `dotnet build` y suite API con `AUTH_TEST_POSTGRES_CONNECTION`
       apuntando a la base local de desarrollo/pruebas acordada; las pruebas
       aplican migraciones y escriben registros en esa base. Usar PostgreSQL
       temporal aislado sólo si se necesita aislamiento.
- [ ] Revisar diffs/status de esta y la web; completar trazabilidad y enlazar
      limitaciones o decisiones diferidas.

Ejecución conjunta parcial (2026-11-10): se consultó el OpenAPI servido por la
API temporal y coincidieron las cinco rutas de contenido conocido con el diseño;
GET de lista declara bearer. La API respondió `201` al registro y `200` al login
desde la interfaz web, pero ésta no salió de autenticación. No se ejecutaron aquí
los escenarios CRUD/aislamiento desde la UI. La suite PostgreSQL descartable de
21 pruebas del 2026-11-10 ya estaba confirmada y no se repitió. Fase 5 sigue
pendiente.

Seguimiento (2026-11-10): la investigación atribuyó el bloqueo de navegación al
guard del cliente, no al contrato ni a la API. El guard web ya conserva el bearer
emitido por login; la prueba del flujo pasó en el repositorio web. La API activa
responde en `/health` y el OpenAPI observado mantiene rutas/seguridad aprobadas.
Sin cambios de plataforma no se repitieron build ni pruebas PostgreSQL. La
aceptación UI/API de CRUD, aislamiento A/B e independencia de ejercicios continúa
pendiente; Fase 5 no se marca completa.

Intento de aceptación segura (2026-11-10): la API activa respondió `/health` y
OpenAPI publicó las cinco rutas de contenido conocido con bearer. El único
PostgreSQL activo pertenece al stack Compose y usa el volumen persistente
`ez-nihongo-project_postgres-data`; al no haber un dataset de prueba aislado y
autorizado, no se registraron cuentas ni se hicieron mutaciones. `playwright-cli`
y Playwright local no están disponibles; Edge sí está instalado, pero la web
activa es una imagen preexistente, no código web actual. Escenarios integrados
1–7: **sin ejecutar**. Para completar Fase 5 se necesita web actual servida en
navegador y API contra PostgreSQL descartable; ninguna prueba destructiva afectó
datos compartidos.
