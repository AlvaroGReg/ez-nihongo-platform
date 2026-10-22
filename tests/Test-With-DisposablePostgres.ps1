param(
    [string] $PostgresImage = "postgres:17.6-alpine"
)

$ErrorActionPreference = "Stop"

function Resolve-DockerCli {
    $dockerCommand = Get-Command docker -ErrorAction SilentlyContinue
    if ($dockerCommand) {
        return $dockerCommand.Source
    }

    $candidatePaths = @(
        (Join-Path $env:ProgramFiles "Docker\Docker\resources\bin\docker.exe"),
        (Join-Path $env:LOCALAPPDATA "Programs\DockerDesktop\resources\bin\docker.exe"),
        (Join-Path $env:LOCALAPPDATA "Programs\Docker\Docker\resources\bin\docker.exe")
    )

    foreach ($candidatePath in $candidatePaths) {
        if (Test-Path -LiteralPath $candidatePath -PathType Leaf) {
            return $candidatePath
        }
    }

    throw "Docker CLI was not found. Install Docker Desktop or add docker.exe to PATH."
}

$docker = Resolve-DockerCli
$containerName = "ez-nihongo-test-postgres-$([guid]::NewGuid().ToString('N'))"
$databaseName = "ez_nihongo_test"
$databaseUser = "test_user"
$databasePassword = [guid]::NewGuid().ToString("N")
$containerCreated = $false

try {
    & $docker info *> $null
    if ($LASTEXITCODE -ne 0) {
        throw "Docker CLI was found, but the Docker engine is not running or is not accessible. Start Docker Desktop and retry."
    }

    & $docker run --detach `
        --name $containerName `
        --publish "127.0.0.1::5432" `
        --env "POSTGRES_DB=$databaseName" `
        --env "POSTGRES_USER=$databaseUser" `
        --env "POSTGRES_PASSWORD=$databasePassword" `
        --health-cmd "pg_isready -U $databaseUser -d $databaseName" `
        --health-interval 1s `
        --health-timeout 3s `
        --health-retries 30 `
        $PostgresImage

    if ($LASTEXITCODE -ne 0) {
        throw "Could not start the temporary PostgreSQL container."
    }
    $containerCreated = $true

    $deadline = [DateTime]::UtcNow.AddSeconds(45)
    do {
        $health = (& $docker inspect --format '{{.State.Health.Status}}' $containerName).Trim()
        if ($LASTEXITCODE -ne 0) {
            throw "Could not inspect the temporary PostgreSQL container."
        }
        if ($health -eq "healthy") {
            break
        }
        if ($health -eq "unhealthy") {
            throw "The temporary PostgreSQL container became unhealthy."
        }
        if ([DateTime]::UtcNow -ge $deadline) {
            throw "Timed out waiting for PostgreSQL to become healthy."
        }
        Start-Sleep -Seconds 1
    } while ($true)

    $publishedPort = (& $docker port $containerName "5432/tcp").Trim()
    if ($LASTEXITCODE -ne 0 -or $publishedPort -notmatch ":(?<port>\d+)$") {
        throw "Could not determine the temporary PostgreSQL port."
    }

    $connectionString = "Host=127.0.0.1;Port=$($Matches.port);Database=$databaseName;Username=$databaseUser;Password=$databasePassword"
    $previousTestConnectionString = $env:AUTH_TEST_POSTGRES_CONNECTION
    $previousApiConnectionString = $env:Postgres__ConnectionString
    $env:AUTH_TEST_POSTGRES_CONNECTION = $connectionString
    $env:Postgres__ConnectionString = $connectionString

    try {
        $projectPath = Join-Path $PSScriptRoot "EzNihongo.Platform.Api.IntegrationTests\EzNihongo.Platform.Api.IntegrationTests.csproj"
        & dotnet test $projectPath
        $testExitCode = $LASTEXITCODE
    }
    finally {
        $env:AUTH_TEST_POSTGRES_CONNECTION = $previousTestConnectionString
        $env:Postgres__ConnectionString = $previousApiConnectionString
    }

    exit $testExitCode
}
finally {
    if ($containerCreated) {
        & $docker rm --force $containerName *> $null
    }
}
