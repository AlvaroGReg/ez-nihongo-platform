# Diseño técnico — Lista personal de contenido conocido

## Límites entre repositorios

La plataforma posee esquema de datos, autorización, DTO, validación y APIs. La
web posee las pantallas y búsqueda visual del catálogo para el diccionario
personal. La especificación de integración del cliente está en
[`web design`](../../../ez-nihongo-web/specs/personal-known-content/design.md).

## Modelo persistido propuesto

Crear una entidad `UserKnownContent` en `PlatformIdentityDbContext`, con FK a
Identity Users y borrado en cascada. Una fila representa una entrada en la lista:

- `Id`: UUID estable de lista.
- `UserId`: propietario derivado del principal.
- `ContentType`: `vocabulary` o `kanji`.
- `Source`: `catalog` o `custom`.
- `CatalogContentId`: requerido sólo para `catalog`.
- Campos de contenido personalizado sólo para `custom`: escritura, lecturas,
  romaji cuando es vocabulario, significados y datos de tipo necesarios.
- `CreatedAt`: fecha UTC asignada por el servidor.

Preferir un esquema relacional de columnas tipadas (no serializar todo el objeto
como JSON). Añadir índices únicos que impidan duplicar la misma referencia de
catálogo por usuario y tipo. Para vocabulario propio, imponer unicidad por
propietario, tipo y pareja de escritura/lectura normalizadas; para kanji, por
propietario, tipo y carácter normalizado, usando el algoritmo Unicode definido
abajo. Índice de consulta por `(UserId, ContentType, CreatedAt, Id)`. La
migración es aditiva y no cambia datos Identity ni catálogos.

Las restricciones de origen garantizan que la fila tiene o bien una referencia
de catálogo, o bien los campos requeridos de contenido propio, pero nunca ambas
formas. Las lecturas/significados plurales se almacenan en formato compatible
con Npgsql/PostgreSQL ya utilizado por el proyecto, con validación de tamaños y
serialización de respuesta explícita.

## Contrato HTTP compartido

Todas las rutas requieren bearer JWT y se agrupan bajo
`/api/v1/me/known-content`. Este prefijo y los nombres de ruta siguientes son el
contrato aprobado para implementar en ambos repositorios.

### `GET /api/v1/me/known-content?type=vocabulary&offset=0&limit=200&language=en`

`type` es opcional y acepta sólo `vocabulary` o `kanji`; `offset >= 0`, `limit`
entre 1 y 200 (`offset=0`, `limit=50` por defecto); `language` acepta `en` o `es`
y por defecto es `en`. Devuelve `200 OK` con
`total`, `offset`, `limit` e `items`. Orden estable: más reciente primero,
desempate por ID. El cliente recorre páginas hasta recibir `total` (máximo 1000).

Cada item tiene esta forma discriminada:

```json
{
  "id": "<uuid-de-la-lista>",
  "contentType": "vocabulary",
  "source": "catalog",
  "catalogContentId": "<id-estable>",
  "createdAt": "<fecha-UTC>",
  "content": {
    "word": "勉強",
    "reading": "べんきょう",
    "romaji": "benkyou",
    "meaning": "study",
    "level": 5
  }
}
```

Para `contentType=kanji`, `content` contiene `kanji`, `readings`, `meanings` y
`level`. Para `source=custom`, `catalogContentId` es `null`; `content` contiene
los campos personalizados y omite `level`. Las entradas de catálogo devuelven
significados en el idioma pedido con el fallback vigente del catálogo. Los
significados personalizados son textos literales y no se traducen.

La variante de `content` se valida por `contentType`:

- `vocabulary`: `{ "word", "reading", "romaji", "meaning", "level" }`.
- `kanji`: `{ "kanji", "readings", "meanings", "level" }`.

`level` es un entero JLPT y sólo se devuelve para catálogo. `meaning` es un texto
para vocabulario; `meanings` es una lista no vacía de variantes para kanji.
Campos no aplicables no se devuelven. Todas las fechas son UTC ISO 8601.

### `POST /api/v1/me/known-content/catalog?language=en`

```json
{ "contentType": "vocabulary", "catalogContentId": "<stable-id>" }
```

Valida ID/type con `VocabularyCatalog` o `KanjiCatalog`; persiste pertenencia y
devuelve `200 OK` con `{ "item": <KnownContentItem>, "alreadyPresent": false }`.
Si ya existía, devuelve el mismo item y `alreadyPresent: true`. La comprobación y
el insert son atómicos, para distinguir duplicados incluso entre pestañas y
peticiones concurrentes. `language` es `en` o `es`, por defecto `en`; sólo
localiza datos de catálogo de la respuesta, no afecta a la persistencia.

### `POST /api/v1/me/known-content/custom/vocabulary`

```json
{ "word": "勉強", "reading": "べんきょう", "romaji": "benkyou", "meaning": "study" }
```

Campos obligatorios no vacíos. Romaji es input explícito porque la web actual no
incluye conversor kana→romaji y forma parte de la entrada de diccionario. Devuelve
`201 Created` con el item; los duplicados devuelven `409`.

### `POST /api/v1/me/known-content/custom/kanji`

```json
{ "kanji": "学", "readings": ["がく", "まなぶ"], "meanings": ["study", "learn"] }
```

Requiere exactamente un valor escalar Unicode asignado como kanji (incluyendo
extensiones soportadas por el runtime), lecturas de kana y significados. Devuelve
`201 Created` con el item; los duplicados devuelven `409`. El significado es un
texto libre literal, sin selección de idioma ni traducción.

## Normalización y límites acordados

- Escritura de vocabulario y carácter de kanji: 100 valores escalares Unicode.
- Lectura y romaji: 200 caracteres cada uno.
- Significado: 500 caracteres por valor; hasta 20 lecturas y 20 significados en
  los arrays de kanji.
- Máximo de 1000 entradas por usuario.
- Texto se recorta en extremos y se rechazan valores vacíos/control. El valor
  presentado se conserva en NFC después del recorte; la clave de comparación se
  genera así:
  1. Aplicar NFKC.
  2. Para lecturas, convertir katakana U+30A1–U+30F6 a hiragana restando U+0060,
     mapear signos de iteración U+30FD/U+30FE a U+309D/U+309E y conservar `ー`;
     no colapsar vocales pequeñas.
  3. No aplicar case-folding ni eliminar espacios o puntuación interiores.
- La identidad de vocabulario propio es escritura+lectura normalizadas; romaji y
  significado no participan. La identidad de kanji propio es el carácter
  normalizado. La misma clave se usa para detectar coincidencias de catálogo.
- Escritura/kanji se cuentan como valores escalares Unicode, no unidades UTF-16.
  Kanji debe ser exactamente un valor escalar reconocido como ideograma Han o
  ideograma de compatibilidad CJK; no se aceptan marcas de variación adicionales.
- Lecturas aceptan hiragana o katakana y `ー`. Romaji acepta cualquier texto no
  vacío dentro del límite; no se restringen alfabeto ni puntuación.
- Todos los límites de longitud se cuentan en valores escalares Unicode, no en
  unidades UTF-16. Para arrays, validar cada elemento y el número de elementos.

Los errores de duplicado usan Problem Details con extensión `code`. El duplicado
de catálogo añade `contentType` y `catalogContentId`; el duplicado propio no
revela datos de otras cuentas.

Los errores comunes son `401` sin bearer, `400` para query/campos/type/ID de
catálogo inválidos y `409` con `code=list-limit` al superar 1000 entradas. Antes
de comprobar el límite, el POST de catálogo resuelve una pertenencia existente y
responde `{item,alreadyPresent:true}`. Un duplicado oficial/custom devuelve `409`
con los códigos previamente definidos.
Los errores de validación usan Problem Details `ValidationProblemDetails`.
Un `catalog-duplicate` añade `contentType` y `catalogContentId`; `duplicate` no
incluye el payload ni el ID de otra entrada. Las operaciones POST de catálogo y
custom comprueban el tope después de detectar una pertenencia/duplicado existente.

### `DELETE /api/v1/me/known-content/{id}`

Elimina sólo la fila propia; devuelve `204 No Content` para ID propio o ausente,
sin permitir enumeración de IDs ajenos.

## Duplicados y borrado

- Catálogo: clave única `(UserId, ContentType, CatalogContentId)`; dos POST
  concurrentes deben terminar con una única fila.
- Personalizado: clave normalizada `(UserId, ContentType, normalizedWriting,
  normalizedReading)` para vocabulario y `(UserId, ContentType,
  normalizedWriting)` para kanji; una segunda escritura equivalente responde
  `409 Conflict` con error estable `duplicate`, no crea una nueva entrada.
- Antes de crear contenido propio, comparar vocabulario por escritura+lectura
  con el catálogo y kanji por carácter; devolver `409` con el tipo/ID oficial
  coincidente (`catalog-duplicate`) para que el cliente permita añadir la
  referencia canónica. Un duplicado de contenido propio devuelve `duplicate`.
- El elemento personalizado sólo existe como entrada de la lista; DELETE lo
  elimina definitivamente. No tiene entidad pública independiente.
- El ID de catálogo nunca se resuelve en campos recibidos del cliente: API lo
  resuelve en catálogo y devuelve datos canónicos.

## Servicios y organización

- `KnownContent/`: entidad, request/response DTO, servicio scoped e interfaz y
  Minimal API endpoints.
- Añadir conjunto al contexto Identity, índices, constraints y migración EF Core.
- Registrar el grupo autenticado en `Program.cs`; normalizar Problem Details y
  declarar respuestas OpenAPI.
- Reutilizar catálogos existentes para validar y resolver referencias; no alterar
  rutas públicas actuales.
- La API sirve contenido personal únicamente a la vista autenticada del
  diccionario. No se crean contratos ni proveedores para integrarlo en ejercicios.

## Decisiones de producto cerradas

- Romaji obligatorio para vocabulario personalizado; evita cambiar el formato
  actual de respuesta y no requiere añadir un conversor.
- Kanji personalizado limitado a un carácter (un valor escalar Unicode).
- Límites aceptados: escritura/kanji 100, lectura y romaji 200, significado 500,
  hasta 20 lecturas/significados y 1000 entradas por usuario.
- Rechazar duplicados propios y coincidencias exactas de catálogo normalizadas;
  una coincidencia con catálogo devuelve tipo e ID para que la web permita añadir
  la entrada oficial.
- Añadir una referencia de catálogo ya presente es idempotente y devuelve
  `200 OK`; duplicado custom responde `409 Conflict`.
- La lista paginada es el endpoint de lectura de contenido personal para la vista
  de diccionario. No hay endpoint de elementos para ejercicios.
- El significado propio es un solo texto literal sin campo de idioma ni
  traducción. Los significados de catálogo sí respetan `language` y el fallback
  actual de cada catálogo.
- Quitar una entrada personalizada la elimina definitivamente. Quitar una entrada
  de catálogo sólo elimina la pertenencia del usuario.
- POST de catálogo responde `{item, alreadyPresent}` para que la web muestre el
  estado idempotente correcto incluso si su lista local estaba desactualizada.
- La respuesta de contenido catalogado usa `language` en GET y POST de catálogo;
  la respuesta de contenido custom no cambia con ese parámetro.
- Prefijo de API fijado en `/api/v1/me/known-content`; la fase de coordinación
  congela el contrato; la implementación verifica OpenAPI en su sesión de endpoints.

## Verificación

- API: `dotnet build`; unit tests para validadores/normalización; integración con
  PostgreSQL para migración, autorización, aislamiento, constraints concurrentes,
  validación de catálogos, idempotencia, dedupe y borrado.
- Para desarrollo local, `AUTH_TEST_POSTGRES_CONNECTION` puede apuntar a la misma
  base de PostgreSQL de desarrollo usada por la API, siempre que esté dedicada a
  desarrollo/pruebas y se acepte que las pruebas apliquen migraciones y escriban
  registros. No es obligatorio crear una base paralela. La alternativa de
  PostgreSQL temporal aislado sigue disponible cuando se necesite aislamiento.
- Verificar `/openapi/v1.json` contra contratos consumidos por web y que ningún
  endpoint público expone contenido personalizado.
