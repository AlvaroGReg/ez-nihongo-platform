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
- `POST /api/v1/auth/register` — create an account and return a bearer token.
- `POST /api/v1/auth/login` — authenticate and return a bearer token.
- `GET /api/v1/auth/me` — return the identity associated with a bearer token.

Invalid query parameters return `400` Problem Details; an unknown content ID
returns `404`.

## Identity persistence (development)

The API requires PostgreSQL and valid JWT settings at startup. For local
development with the integrated workspace stack, copy `.env.example` to `.env`
at the workspace root and set `POSTGRES_PASSWORD` and `JWT_SIGNING_KEY` to local
random values. Start the stack with `./deploy.ps1`; PostgreSQL is exposed on
`localhost:5432` and the API waits until it is healthy. The `.env` file is
ignored by Git and must not contain production credentials.

For running the API directly outside Compose, set the connection string and JWT
values through user secrets (never commit credentials):

```sh
dotnet user-secrets init --project src/EzNihongo.Platform.Api
dotnet user-secrets set "Postgres:ConnectionString" "Host=localhost;Database=eznihongo;Username=postgres;Password=<local-password>" --project src/EzNihongo.Platform.Api
dotnet user-secrets set "Jwt:SigningKey" "<random-secret-of-at-least-32-utf8-bytes>" --project src/EzNihongo.Platform.Api
dotnet user-secrets set "Jwt:Issuer" "ez-nihongo-local" --project src/EzNihongo.Platform.Api
dotnet user-secrets set "Jwt:Audience" "ez-nihongo-local-client" --project src/EzNihongo.Platform.Api
```

The access-token lifetime defaults to 15 minutes and can be changed with
`Jwt:AccessTokenMinutes`. Authentication throttling is controlled by
`AuthRateLimit:PermitLimit` (default 5) and `AuthRateLimit:WindowMinutes`
(default 1). Environment variables use the corresponding double-underscore
form, such as `Jwt__SigningKey` and `Postgres__ConnectionString`. Use a
high-entropy signing key from a secret manager in deployed environments.

The Compose stack creates the PostgreSQL database, but schema updates remain an
explicit deployment step. After starting the stack, apply the versioned Identity
schema from this repository root:

```sh
$env:Postgres__ConnectionString = "Host=localhost;Database=eznihongo;Username=eznihongo;Password=<POSTGRES_PASSWORD-from-workspace-.env>"
dotnet tool restore
dotnet ef database update --project src/EzNihongo.Platform.Api --startup-project src/EzNihongo.Platform.Api
```

To add a later migration, run
`dotnet ef migrations add <MigrationName> --project src/EzNihongo.Platform.Api --startup-project src/EzNihongo.Platform.Api`
with `Postgres__ConnectionString` set. The API does not automatically apply
migrations at startup; deploy schema changes explicitly before starting a new
API version.

### Authentication requests

`auth.http` contains registration, login, `/me`, and error request examples.
Start the API and select individual requests in an HTTP client such as the
VS Code REST Client. Replace the sample password and paste the returned access
token into `@accessToken` before requesting `/me`.

### Tests

Run unit tests and the PostgreSQL integration suite from the repository root:

```sh
dotnet test tests/EzNihongo.Platform.Api.IntegrationTests
```

Set `AUTH_TEST_POSTGRES_CONNECTION` to a disposable PostgreSQL database before
running the command to enable integration tests. The suite applies the checked-in
migrations and creates accounts with randomized email addresses. Without that
variable, PostgreSQL integration tests are reported as skipped; unit tests still
run. Integration coverage includes registration and concurrent duplicate
registration, password hashing, login, bearer validation, Problem Details,
rate limiting, and anonymous health/vocabulary access.

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
