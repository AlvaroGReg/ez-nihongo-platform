[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [ValidateNotNullOrEmpty()]
    [string]$UserName,

    [Parameter(Mandatory = $false)]
    [Security.SecureString]$Password
)

$ErrorActionPreference = 'Stop'

if (-not $Password) {
    $Password = Read-Host 'Nueva contraseña' -AsSecureString
}

$passwordPointer = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($Password)
try {
    $plainPassword = [Runtime.InteropServices.Marshal]::PtrToStringBSTR($passwordPointer)
    [Environment]::SetEnvironmentVariable('EZNIHONGO_RESET_PASSWORD', $plainPassword, 'Process')
} finally {
    [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($passwordPointer)
}

try {
    $platformRoot = Split-Path -Parent $PSScriptRoot
    $workspaceRoot = Split-Path -Parent $platformRoot
    $envFile = Join-Path $workspaceRoot '.env'
    if (-not [Environment]::GetEnvironmentVariable('Postgres__ConnectionString', 'Process')) {
        if (-not (Test-Path -LiteralPath $envFile)) {
            throw "No Postgres__ConnectionString is set and workspace .env was not found at '$envFile'."
        }

        $passwordLine = [IO.File]::ReadAllLines($envFile) |
            Where-Object { $_ -match '^\s*POSTGRES_PASSWORD\s*=' } |
            Select-Object -Last 1
        if (-not $passwordLine) {
            throw "POSTGRES_PASSWORD was not found in '$envFile'."
        }

        $postgresPassword = ($passwordLine -replace '^\s*POSTGRES_PASSWORD\s*=\s*', '').Trim()
        if ($postgresPassword.Length -ge 2 -and $postgresPassword.StartsWith('"') -and $postgresPassword.EndsWith('"')) {
            $postgresPassword = $postgresPassword.Substring(1, $postgresPassword.Length - 2)
        } elseif ($postgresPassword.Length -ge 2 -and $postgresPassword.StartsWith("'") -and $postgresPassword.EndsWith("'")) {
            $postgresPassword = $postgresPassword.Substring(1, $postgresPassword.Length - 2)
        }

        if ([string]::IsNullOrWhiteSpace($postgresPassword)) {
            throw "POSTGRES_PASSWORD is empty in '$envFile'."
        }

        [Environment]::SetEnvironmentVariable('EZNIHONGO_POSTGRES_PASSWORD', $postgresPassword, 'Process')
    }

    $toolProject = Join-Path $PSScriptRoot 'SetLocalUserPassword/SetLocalUserPassword.csproj'
    dotnet run --project $toolProject --configuration Release -- $UserName
    if ($LASTEXITCODE -ne 0) {
        throw "Password reset failed (dotnet exit code $LASTEXITCODE)."
    }
} finally {
    [Environment]::SetEnvironmentVariable('EZNIHONGO_RESET_PASSWORD', $null, 'Process')
    [Environment]::SetEnvironmentVariable('EZNIHONGO_POSTGRES_PASSWORD', $null, 'Process')
    $plainPassword = $null
    $postgresPassword = $null
}
