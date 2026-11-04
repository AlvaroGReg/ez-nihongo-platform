# Trazabilidad — Lista personal de contenido conocido (plataforma)

| Requisito | Diseño/contrato | Verificación prevista |
| --- | --- | --- |
| REQ-01 Autenticación y propietario | `KnownContentEndpoints`: grupo autenticado; usuario derivado del JWT | Prueba HTTP para GET/POST anónimos y aislamiento; ejecución pendiente de PostgreSQL |
| REQ-02 Consulta propia | `GET /api/v1/me/known-content`, DTO discriminado, paginación/language hasta 1000 | Prueba HTTP para tipo, paginación, idioma inválido e aislamiento; ejecución pendiente de PostgreSQL |
| REQ-03 Referencia de catálogo | POST catálogo `{item,alreadyPresent}`; IDs resueltos en catálogos de plataforma | Prueba HTTP para ID inválido, duplicado oficial e idempotencia; ejecución pendiente de PostgreSQL |
| REQ-04 Contenido propio | POST custom vocabulario/kanji; validación, normalización y dedupe oficial | Prueba HTTP para entrada inválida, alta, duplicado propio y coincidencia oficial; ejecución pendiente de PostgreSQL |
| REQ-05 Eliminación | DELETE por ID propio | Prueba HTTP para borrado idempotente y no eliminación por otra cuenta; ejecución pendiente de PostgreSQL |
| REQ-06 Consulta del diccionario | Todas las páginas de GET lista (`type` opcional); conjunto completo limitado a 1000 | conjunto mixto/vacío, tipo, IDs de catálogo estables y propietario; prueba HTTP PostgreSQL aprobada |
| REQ-07 Búsqueda de catálogo | UI usa catálogos existentes; servidor revalida | integración web con IDs canónicos |
| REQ-08 Límites/privacidad | Constraints, límites, Problem Details | entrada excesiva y acceso cruzado |

Estado: Fases 1, 2 y 3 completadas. `dotnet build
src/EzNihongo.Platform.Api/EzNihongo.Platform.Api.csproj` pasa sin advertencias y
las 21 pruebas API pasan contra PostgreSQL en la ejecución registrada
anteriormente; `/openapi/v1.json` responde correctamente e incluye las cinco rutas
conocidas con seguridad bearer. Para Fase 4 se confirmó que la consulta paginada
existente por propietario/tipo sirve a la vista de diccionario; no se añade un
endpoint paralelo ni integración de ejercicios. Se ampliaron las pruebas HTTP
para conjunto mixto, tipo kanji, IDs canónicos, cuenta aislada y disponibilidad
de OpenAPI. Tras
corregir el dato de prueba para que el kanji custom no colisionara con el catálogo,
`dotnet build` pasa sin advertencias y las 21 pruebas API pasan contra la base
PostgreSQL `eznihongo`, incluidas migración, persistencia, rutas y los casos de
Fase 4. Fases 1–4 completadas; Fase 5 (aceptación web/API) sigue pendiente.

Verificación de punto de guardado (2026-10-30): `dotnet build
src/EzNihongo.Platform.Api/EzNihongo.Platform.Api.csproj` pasa sin advertencias.
`dotnet test tests/EzNihongo.Platform.Api.IntegrationTests` pasó 8 pruebas y
omitió 13 porque esta ejecución no tenía configurada
`AUTH_TEST_POSTGRES_CONNECTION`; por ello no constituye una nueva verificación
de PostgreSQL. Fase 5 continúa pendiente hasta completar la aceptación conjunta
con Web y la revisión final de ambos repositorios.
