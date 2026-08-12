[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

Import-Module (Join-Path $PSScriptRoot "LocalSecretFile.psm1") -Force

if ($env:OS -ne "Windows_NT") {
    throw "Initialize-LocalPostgresDatabase.ps1 currently supports Windows only."
}

$repositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path
$environmentPath = Join-Path $repositoryRoot ".env.local"
$bootstrapSqlPath = Join-Path $repositoryRoot "deploy\neon\bootstrap-development-database.sql"

if (-not (Test-Path -LiteralPath $environmentPath -PathType Leaf)) {
    throw ".env.local does not exist. Run Initialize-LocalEnvironment.ps1 first."
}

Assert-LocalSecretFileSecurity -Path $environmentPath

$values = @{}
foreach ($line in [IO.File]::ReadAllLines($environmentPath)) {
    if ([string]::IsNullOrWhiteSpace($line) -or $line.StartsWith("#")) {
        continue
    }

    $parts = $line.Split("=", 2)
    if ($parts.Count -eq 2) {
        $values[$parts[0]] = $parts[1]
    }
}

function Get-RequiredValue {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Name
    )

    if (-not $values.ContainsKey($Name) -or [string]::IsNullOrWhiteSpace($values[$Name])) {
        throw "Required local environment value '$Name' is missing. Regenerate .env.local with Initialize-LocalEnvironment.ps1 -Force."
    }

    return [string]$values[$Name]
}

function ConvertTo-Base64 {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Value
    )

    return [Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes($Value))
}

$dockerCommand = Get-Command docker -ErrorAction SilentlyContinue
$dockerPath = if ($dockerCommand) {
    $dockerCommand.Source
}
else {
    "C:\Program Files\Docker\Docker\resources\bin\docker.exe"
}

if (-not (Test-Path -LiteralPath $dockerPath)) {
    throw "Docker CLI was not found."
}

$composeArguments = @(
    "compose",
    "--project-name", "dorosak",
    "--env-file", $environmentPath
)
$database = Get-RequiredValue -Name "DOROSAK_POSTGRES_DATABASE"
$ownerPassword = Get-RequiredValue -Name "DOROSAK_POSTGRES_OWNER_PASSWORD"
$migratorPassword = Get-RequiredValue -Name "DOROSAK_POSTGRES_MIGRATOR_PASSWORD"
$runtimePassword = Get-RequiredValue -Name "DOROSAK_POSTGRES_APP_PASSWORD"
$migratorConnection = Get-RequiredValue -Name "Migrations__ConnectionString"

Push-Location $repositoryRoot
try {
    & $dockerPath @composeArguments up --detach --wait postgres
    if ($LASTEXITCODE -ne 0) {
        throw "Local PostgreSQL could not be started."
    }

    $historyExists = (& $dockerPath @composeArguments exec -T postgres `
        psql --username dorosak_owner --dbname $database --no-psqlrc -qAt `
        -c "SELECT to_regclass('migrations.__ef_migrations_history') IS NOT NULL;").Trim()
    if ($LASTEXITCODE -ne 0) {
        throw "Local PostgreSQL schema state could not be read."
    }

    if ($historyExists -eq "f") {
        Get-Content -LiteralPath $bootstrapSqlPath -Raw | & $dockerPath @composeArguments exec -T postgres `
            psql --username dorosak_owner --dbname $database --no-psqlrc -q -v ON_ERROR_STOP=1
        if ($LASTEXITCODE -ne 0) {
            throw "Local PostgreSQL role bootstrap failed."
        }
    }

    $passwordSql = @(
        "ALTER ROLE dorosak_owner PASSWORD '$ownerPassword';",
        "ALTER ROLE dorosak_migrator PASSWORD '$migratorPassword';",
        "ALTER ROLE dorosak_app PASSWORD '$runtimePassword';"
    ) -join [Environment]::NewLine
    $encodedPasswordSql = ConvertTo-Base64 -Value $passwordSql
    & $dockerPath @composeArguments exec -T postgres sh -eu -c `
        "printf '%s' '$encodedPasswordSql' | base64 -d | psql --username dorosak_owner --dbname '$database' --no-psqlrc -q -v ON_ERROR_STOP=1"
    if ($LASTEXITCODE -ne 0) {
        throw "Local PostgreSQL application credentials could not be configured."
    }

    $backendPath = Join-Path $repositoryRoot "backend"
    $hadPreviousConnection = Test-Path Env:\Migrations__ConnectionString
    $previousConnection = $env:Migrations__ConnectionString
    Push-Location $backendPath
    try {
        $env:Migrations__ConnectionString = $migratorConnection
        & dotnet tool restore
        if ($LASTEXITCODE -ne 0) {
            throw "The local .NET tools could not be restored."
        }

        & dotnet tool run dotnet-ef database update `
            --project "src/Dorosak.Infrastructure/Dorosak.Infrastructure.csproj" `
            --startup-project "src/Dorosak.Api/Dorosak.Api.csproj" `
            --context DorosakDbContext
        if ($LASTEXITCODE -ne 0) {
            throw "Local PostgreSQL migrations failed."
        }
    }
    finally {
        if ($hadPreviousConnection) {
            $env:Migrations__ConnectionString = $previousConnection
        }
        else {
            Remove-Item Env:\Migrations__ConnectionString -ErrorAction SilentlyContinue
        }
        Pop-Location
    }

    $runtimeSql = "SELECT current_user || '|' || has_schema_privilege(current_user, 'app', 'USAGE') || '|' || has_schema_privilege(current_user, 'app', 'CREATE') || '|' || (to_regclass('operations.schema_compatibility') IS NOT NULL);"
    $encodedRuntimeSql = ConvertTo-Base64 -Value $runtimeSql
    $runtimeProbe = "printf '%s' '$encodedRuntimeSql' | base64 -d | PGPASSWORD='$runtimePassword' psql --host localhost --username dorosak_app --dbname '$database' --no-psqlrc -qAt -v ON_ERROR_STOP=1"
    $runtimeResult = (& $dockerPath @composeArguments exec -T postgres sh -eu -c $runtimeProbe).Trim()
    if ($LASTEXITCODE -ne 0 -or $runtimeResult -ne "dorosak_app|true|false|true") {
        throw "Local PostgreSQL runtime role verification returned an unexpected result."
    }

    Write-Host "Local PostgreSQL is initialized and migrated. Secret values were not printed."
}
finally {
    Pop-Location
}
