# Requisitos — Lista personal de contenido conocido

## Propiedad y coordinación

Esta especificación define el comportamiento y los contratos de la API de
`ez-nihongo-platform`. La experiencia de navegador y el filtro de configuración
se detallan en
[`ez-nihongo-web/specs/personal-known-content/`](../../../ez-nihongo-web/specs/personal-known-content/).
La API es la fuente de verdad para DTO, rutas, persistencia y validación. Las
dos especificaciones deben permanecer alineadas.

## Requisitos funcionales

### REQ-01 — Autenticación obligatoria

Todas las operaciones de la lista personal y del contenido propio requieren JWT
bearer válido. El propietario se obtiene de `sub`/NameIdentifier. No se aceptan
ni confían IDs de usuario aportados por el cliente.

### REQ-02 — Consultar la lista propia

El usuario puede recuperar sólo sus entradas mediante
`GET /api/v1/me/known-content`, paginadas y en orden estable. La respuesta sigue
el DTO común de `design.md`, con tipo, origen (`catalog` o `custom`), identidad,
campos de presentación y nivel JLPT sólo para catálogo. El servicio no devuelve
entradas de otras cuentas. La web pagina hasta leer el total; no hay endpoint
separado de elementos para ejercicios.

### REQ-03 — Añadir un elemento de catálogo

El usuario puede añadir una referencia válida de vocabulario o kanji. La API
valida tipo e ID contra su catálogo correspondiente, deriva el propietario del
JWT y rechaza referencias inválidas. Añadir una referencia ya presente para ese
usuario es idempotente y no crea duplicados.

### REQ-04 — Crear contenido propio

El usuario puede añadir vocabulario o kanji que no exista en los catálogos. La
API valida los campos y persiste el contenido bajo su cuenta como una entrada de
la lista. Las entradas personalizadas no reciben ni afirman un nivel JLPT. No
pueden leerse ni usarse en ejercicios desde otra cuenta.

Vocabulario requiere escritura, lectura kana, romaji y significado. Kanji
requiere exactamente un carácter kanji, al menos una lectura kana y un
significado. Se aplican los límites acordados en `design.md`: escritura/kanji
hasta 100 valores escalares, lectura y romaji hasta 200, significado hasta 500,
hasta 20 lecturas/significados por array y 1000 elementos por usuario. La API
valida limpieza Unicode, contenido vacío y kana en servidor.

Antes de crear contenido propio, la API detecta una coincidencia exacta con el
catálogo: vocabulario por escritura y lectura normalizadas; kanji por carácter.
Si existe, rechaza la creación e incluye el identificador oficial para que la
web ofrezca añadirlo desde el catálogo. La API es la autoridad final aunque la
búsqueda de catálogo del navegador no haya encontrado el registro.

### REQ-05 — Eliminar de la lista

El usuario puede quitar cualquiera de sus entradas. Quitar una referencia de
catálogo elimina sólo la pertenencia del usuario; no modifica el catálogo. Quitar
una entrada personalizada la elimina de la lista y la hace no disponible para
futuros ejercicios de ese usuario. Eliminar un ID inexistente o ajeno no revela
si existe para otra cuenta y se trata de forma segura/idempotente.

La eliminación de una entrada personalizada es definitiva; la lista es su único
almacenamiento y no existe papelera/archivo.

### REQ-06 — Elementos para ejercicios

La consulta paginada de REQ-02 permite recuperar las entradas propias necesarias
para formar el conjunto de preguntas de vocabulario o kanji. El parámetro `type`
filtra el tipo y el DTO devuelve IDs estables y campos de pregunta (incluidas
variantes de lecturas/significados de kanji). El cliente carga todas las páginas,
combina el conjunto propio con el catálogo y aplica el modo de filtro solicitado.
No existe un endpoint separado de elementos para ejercicios.

### REQ-07 — Búsqueda del catálogo

El cliente puede buscar y presentar los elementos compartidos ya existentes en
los catálogos actuales. La búsqueda puede reutilizar los proveedores/catálogos
públicos existentes; añadir desde un resultado siempre envía una referencia
estable que la API vuelve a validar. No se crea un duplicado personalizado
cuando el usuario elige añadir el resultado del catálogo.

### REQ-08 — Privacidad y límites

Las consultas y mutaciones están limitadas al usuario autenticado. Deben
validarse tamaño de página y límites de texto/listas antes de persistir. Las
respuestas y logs no deben exponer contenido ni IDs privados de otras cuentas.

## Criterios de aceptación

1. Sin JWT válido, las operaciones protegidas responden `401`.
2. Dos cuentas sólo consultan y usan su propia lista.
3. Añadir un ID de catálogo inválido o con tipo incompatible no crea datos.
4. Añadir dos veces el mismo elemento de catálogo no duplica la pertenencia.
5. El contenido propio cumple la validación y aparece en la lista y en el origen
   de preguntas sólo de su propietario.
6. Una creación que coincide con el catálogo responde `409` con código
   `catalog-duplicate` y el ID oficial; una duplicación propia responde `409`
   con código `duplicate`.
7. Las longitudes, cantidades y límite por usuario de `design.md` se validan en
   servidor, contando caracteres Unicode según la regla documentada.
8. Quitar un elemento no altera el catálogo compartido y el efecto sobre
   contenido propio está definido por REQ-05.
9. Las consultas admiten paginación estable y no permiten límites fuera del
   intervalo documentado.
10. Las pruebas de integración cubren migración, autorización, aislamiento,
    referencias de catálogo, contenido personalizado, duplicados y eliminación.
11. POST de catálogo indica mediante `alreadyPresent` si creó la pertenencia o ya
    existía, también bajo solicitudes concurrentes.

## Fuera de alcance

- Listas múltiples, etiquetas, notas, importación/exportación y sincronización
  offline.
- Persistencia de intentos/SRS o cambios automáticos de pertenencia tras
  responder ejercicios.
- Exponer el contenido personalizado en los endpoints públicos de catálogo.
