# ez-nihongo-platform

Backend platform for ez-nihongo, built with ASP.NET Core 10 Minimal APIs. It
serves vocabulary and kanji catalogs and provides account authentication and
authenticated vocabulary-progress endpoints. PostgreSQL stores Identity
accounts, persistent-login sessions, and vocabulary attempts.

The platform repository owns source catalog data in `db/`. The web client also
bundles static vocabulary and kanji catalog copies for offline dictionary and
exercise use.

## API endpoints

### Health and schema

- `GET /health` — liveness check.
- `GET /openapi/v1.json` — OpenAPI document.

### Vocabulary

- `GET /api/v1/vocabulary/levels` — vocabulary counts by JLPT level.
- `GET /api/v1/vocabulary?level=5&offset=0&limit=50&language=en` — paginated
  vocabulary. `level` is optional and must be 1–5 when supplied; `offset` must
  be non-negative; `limit` is 1–200; `language` is `en` or `es`.
- `GET /api/v1/vocabulary/{contentId}?language=en` — retrieve one vocabulary
  entry.

### Kanji

- `GET /api/v1/kanji/levels` — counts for JLPT levels and the extra collection.
- `GET /api/v1/kanji?level=5&offset=0&limit=50` — paginated kanji, optionally
  filtered by JLPT level.
- `GET /api/v1/kanji?collection=extra&offset=0&limit=50` — kanji outside JLPT
  lists. `collection` accepts `jlpt` or `extra`; `extra` cannot be combined with
  `level`.
- `GET /api/v1/kanji/{id}` — retrieve a kanji entry by its stable ID.

### Authentication and progress

- `POST /api/v1/auth/register` — create an account and return an access token.
- `POST /api/v1/auth/login` — authenticate and return an access token. Optional
  `rememberMe: true` creates a persistent session with a refresh cookie.
- `POST /api/v1/auth/refresh` — rotate the persistent-session cookie and return
  a new access token.
- `POST /api/v1/auth/logout` — revoke the session represented by the refresh
  cookie.
- `GET /api/v1/auth/me` — return the identity represented by a bearer access
  token.
- `POST /api/v1/progress/vocabulary/attempts` — record an authenticated user's
  vocabulary reading attempt.
- `GET /api/v1/progress/vocabulary/attempts?offset=0&limit=50` — retrieve the
  authenticated user's paginated vocabulary-attempt history.

Invalid query values return `400` Problem Details; unknown vocabulary or kanji
IDs return `404`. Account endpoints use rate limiting. Only the access token is
used as bearer authentication. The refresh cookie is used to refresh or log out
and may be revoked when a new login replaces the browser's previous session; it
does not authenticate other API requests.

## Run the integrated Docker development stack

The recommended local setup runs PostgreSQL, the API, and the web app together.
Follow the workspace's [Docker development instructions](../README.md#run-the-docker-development-stack)
to configure `.env` and start Compose Watch. The API is available directly at
`http://localhost:5080`, and the web app at `http://localhost:8080`.

The development Compose override configures the allowed web origin and permits
an insecure refresh cookie for local HTTP only. Production deployments must
configure their exact allowed origins and use Secure cookies.

## Run the API directly

To run the API outside Compose, provide a running PostgreSQL database and the
required configuration. The API requires a PostgreSQL connection string, JWT
signing key (at least 32 UTF-8 bytes), issuer, audience, and at least one exact
`PersistentLogin:AllowedOrigins` value. The following example configures an
HTTP-only local setup for the Vite server at `http://localhost:5173`.

From this repository root, initialize user secrets and set local values (replace
the placeholders):

```sh
dotnet user-secrets init --project src/EzNihongo.Platform.Api
dotnet user-secrets set "Postgres:ConnectionString" "Host=localhost;Database=eznihongo;Username=postgres;Password=<local-password>" --project src/EzNihongo.Platform.Api
dotnet user-secrets set "Jwt:SigningKey" "<random-secret-of-at-least-32-utf8-bytes>" --project src/EzNihongo.Platform.Api
dotnet user-secrets set "Jwt:Issuer" "ez-nihongo-local" --project src/EzNihongo.Platform.Api
dotnet user-secrets set "Jwt:Audience" "ez-nihongo-local-client" --project src/EzNihongo.Platform.Api
dotnet user-secrets set "PersistentLogin:AllowedOrigins:0" "http://localhost:5173" --project src/EzNihongo.Platform.Api
dotnet user-secrets set "PersistentLogin:AllowInsecureDevelopmentCookie" "true" --project src/EzNihongo.Platform.Api
```

Run the API in the Development environment:

```powershell
$env:ASPNETCORE_ENVIRONMENT = "Development"
dotnet run --project src/EzNihongo.Platform.Api -- --urls http://localhost:5080
```

Do not use insecure cookies or development secrets outside local development.
The API checks database connectivity and applies pending EF Core migrations
before it starts serving requests. For a normal web development session, start
the Vite app from `ez-nihongo-web/`; its proxy forwards `/api` to port 5080.

The access-token lifetime defaults to 15 minutes and is configurable with
`Jwt:AccessTokenMinutes`. Authentication throttling is configured with
`AuthRateLimit:PermitLimit` (default 5) and
`AuthRateLimit:WindowMinutes` (default 1). Environment variables use double
underscores, such as `Jwt__SigningKey` and `Postgres__ConnectionString`. Use a
high-entropy signing key from a secret manager in deployed environments.

## Persistent login behavior

Persistent login lasts up to 30 days from the original login. The refresh token
is opaque, stored server-side only as a hash, and delivered only in the
`ez-nihongo-refresh` HttpOnly cookie. The cookie is host-only, Secure outside
the explicitly enabled Development-only HTTP exception, SameSite=Strict, and
scoped to `/api/v1/auth`.

Refresh and logout require JSON, an exact allowed `Origin`, and
`X-EZ-Nihongo-Request: 1`. `PersistentLogin:AllowedOrigins` must be configured
for every deployment. Do not enable wildcard credentialed CORS.

`auth.http` contains request examples. Refresh and logout examples require the
HTTP client to retain the cookie set by a persistent login. Use a REST client
such as the VS Code REST Client, and replace the sample password before using
the registration request.

## Database and local account maintenance

The API applies checked-in EF Core migrations automatically at startup. To add
a schema change, create a versioned migration:

```sh
dotnet ef migrations add <MigrationName> --project src/EzNihongo.Platform.Api --startup-project src/EzNihongo.Platform.Api
```

To change a local account password without deleting its account or progress,
run this helper from the repository root while the workspace Compose database
is running:

```powershell
.\tools\Set-LocalUserPassword.ps1 -UserName "user@example.com"
```

The script prompts for the password without echoing it and updates it through
ASP.NET Core Identity, which validates the password policy and stores the hash.
It reads the workspace `.env` or a configured `Postgres__ConnectionString`.

To delete all local database data and initialize a fresh database, run
`docker compose -f compose.yaml -f compose.dev.yaml down --volumes` from the
workspace root. This removes local accounts and progress as well as the
database volume.

## Tests

Run the API test suite from this repository root:

```sh
dotnet test tests/EzNihongo.Platform.Api.IntegrationTests
```

PostgreSQL integration tests require `AUTH_TEST_POSTGRES_CONNECTION` to point to
a disposable database. Without it, those tests are skipped and unit tests still
run. The suite covers authentication and persistent login, vocabulary progress,
rate limiting, Problem Details, and public health/catalog access.

On Windows with Docker Desktop, run the PowerShell helper to start a temporary
PostgreSQL container, run the suite against it, and remove the container:

```powershell
.\tests\Test-With-DisposablePostgres.ps1
```

## Catalog data and tooling

### Vocabulary

The API loads `db/jlpt-vocabulary/n1.json` through `n5.json`. The aggregate
`all.json` is maintained for existing TypeScript consumers and can be
regenerated with Yarn:

```sh
yarn install
yarn build
```

Vocabulary entries preserve available English and Spanish translations. API
responses expose stable `contentId` values, using source UUIDs when available
and a word/reading identity otherwise. Example:

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

The vocabulary data originates from
[tanos.co.uk/jlpt](https://www.tanos.co.uk/jlpt/) and is attributed to Jonathan
Waller under Creative Commons Attribution (CC BY). Local modifications retain
the Japanese words, readings, romaji, and JLPT levels and add Spanish
translations where available.

### Kana and kanji

Kana source catalogs are `db/kana/hiragana.json` and
`db/kana/katakana.json`; corresponding characters share an `id`.

Kanji catalogs are `db/kanji/n1.json` through `n5.json` and `extra.json`. API
responses preserve the web catalog field names, including readings and bilingual
meaning arrays. Extra kanji have `jlpt: null` and IDs prefixed with `extra-`;
filter them with `collection=extra`. Omitting both `level` and `collection`
includes all catalogs.
