# ez-nihongo-platform

Backend platform for ez-nihongo. It currently exposes the JLPT vocabulary
catalog over an ASP.NET Core 10 API. The source vocabulary is maintained in
`db/` and can be rebuilt from the Anki exports with the scripts below.

## API

Run the API from this repository root:

```sh
dotnet run --project src/EzNihongo.Platform.Api -- --urls http://localhost:5080
```

Endpoints:

- `GET /health` — liveness check.
- `GET /api/v1/vocabulary/levels` — vocabulary counts by JLPT level.
- `GET /api/v1/vocabulary?level=5&offset=0&limit=50` — paginated vocabulary;
  `level` is optional and `limit` is between 1 and 200.
- `GET /api/v1/vocabulary/{contentId}` — retrieve one vocabulary entry.
- `GET /openapi/v1.json` — machine-readable API schema.

Invalid query parameters return `400` Problem Details; an unknown content ID
returns `404`.

## Build Command

```
yarn
yarn build
```

## Build Flow

- Anki files based on JLPT Level
- ↓ `anki2tabs.sh`
- Tabs files based on JLPT Level
- ↓ `tabs2json.js`
- JSON files based on JLPT Level
- ↓ `json2db.js`
- All in one JSON file

## Data Structure

```json
{
  "word": "毎朝",
  "meaning": {
    "en": "every morning",
    "es": ""
  },
  "furigana": "まいあさ",
  "romaji": "maiasa",
  "level": 5
}
```

`meaning.en` preserves the English meaning from the source dataset. `meaning.es`
is reserved for the Spanish translation and is currently empty. The API returns
this data through stable `contentId` values, using source UUIDs when available
and a word/reading identity otherwise.

## Attribution and modifications

The vocabulary data originates from [tanos.co.uk/jlpt](https://www.tanos.co.uk/jlpt/)
and is attributed to Jonathan Waller under Creative Commons Attribution (CC BY).
Our local modification keeps the English meaning under `meaning.en` and adds an empty `meaning.es` field for future translation; Japanese words, readings, romaji, and JLPT levels are retained.

## Run the complete workspace

From the `ez-nihongo-project` workspace root, `./deploy.ps1` builds and starts
the API and web containers with Docker Compose. The API reads these same `db/`
files inside its container; the browser communicates with the API through the
web container's `/api` reverse proxy.
