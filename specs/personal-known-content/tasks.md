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

- [x] Sincronizar rutas, DTO, límites, unicidad, borrado y entradas de ejercicio con
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

## Fase 4 — Proveedor privado para ejercicios (sesión API 3)

- [x] Implementar la consulta por tipo del conjunto personal completo, con campos
      de pregunta tipados y límite de tamaño acordado.
- [x] Verificar que contenido personalizado sólo aparece para su propietario y
      que entradas de catálogo conservan IDs estables.
- [x] Añadir pruebas de integración de conjunto vacío, mixto y aislamiento.
- [x] Confirmar OpenAPI y actualizar traceability del contrato.

## Fase 5 — Integración y cierre (sesión conjunta)

- [ ] Integrar consumo web y comprobar flujos con cuenta A/cuenta B, catálogo,
      contenido propio, filtros y borrado.
- [x] Ejecutar `dotnet build` y suite API con `AUTH_TEST_POSTGRES_CONNECTION`
       apuntando a la base local de desarrollo/pruebas acordada; las pruebas
       aplican migraciones y escriben registros en esa base. Usar PostgreSQL
       temporal aislado sólo si se necesita aislamiento.
- [ ] Revisar diffs/status de esta y la web; completar trazabilidad y enlazar
      limitaciones o decisiones diferidas.
