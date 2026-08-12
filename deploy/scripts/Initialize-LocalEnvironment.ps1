[CmdletBinding()]
param(
    [switch]$Force
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

Import-Module (Join-Path $PSScriptRoot "LocalSecretFile.psm1") -Force

if ($env:OS -ne "Windows_NT") {
    throw "Initialize-LocalEnvironment.ps1 currently supports Windows only."
}

$repositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path
$environmentPath = Join-Path $repositoryRoot ".env.local"
if ((Test-Path -LiteralPath $environmentPath) -and -not $Force) {
    Write-Host ".env.local already exists. Use -Force only when rotating local credentials."
    exit 0
}

$ignoredPath = & git -C $repositoryRoot check-ignore ".env.local"
if ($LASTEXITCODE -ne 0) {
    throw ".env.local must be ignored by Git before credentials can be generated."
}

function New-UrlSafeSecret {
    $bytes = [byte[]]::new(32)
    $generator = [System.Security.Cryptography.RandomNumberGenerator]::Create()

    try {
        $generator.GetBytes($bytes)
    }
    finally {
        $generator.Dispose()
    }

    return [Convert]::ToBase64String($bytes).TrimEnd("=").Replace("+", "-").Replace("/", "_")
}

function New-PostgresConnectionString {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Username,

        [Parameter(Mandatory = $true)]
        [string]$Password,

        [switch]$Migrator
    )

    $builder = [Data.Common.DbConnectionStringBuilder]::new()
    $builder["Host"] = "127.0.0.1"
    $builder["Port"] = 5432
    $builder["Database"] = "dorosak_dev"
    $builder["Username"] = $Username
    $builder["Password"] = $Password
    $builder["SSL Mode"] = "Disable"
    $builder["Channel Binding"] = "Disable"
    $builder["Include Error Detail"] = $false
    $builder["Timeout"] = 15
    $builder["Command Timeout"] = if ($Migrator) { 60 } else { 30 }
    $builder["Pooling"] = -not $Migrator

    if ($Migrator) {
        $builder["Options"] = "-c role=dorosak_schema_owner"
    }
    else {
        $builder["Minimum Pool Size"] = 0
        $builder["Maximum Pool Size"] = 20
        $builder["Connection Idle Lifetime"] = 300
        $builder["Keepalive"] = 30
    }

    return $builder.get_ConnectionString()
}

$ownerPassword = New-UrlSafeSecret
$migratorPassword = New-UrlSafeSecret
$runtimePassword = New-UrlSafeSecret
$redisPassword = New-UrlSafeSecret

$lines = @(
    "DOROSAK_POSTGRES_DATABASE=dorosak_dev",
    "DOROSAK_POSTGRES_PORT=5432",
    "DOROSAK_POSTGRES_OWNER_PASSWORD=$ownerPassword",
    "DOROSAK_POSTGRES_MIGRATOR_PASSWORD=$migratorPassword",
    "DOROSAK_POSTGRES_APP_PASSWORD=$runtimePassword",
    "",
    "DOROSAK_REDIS_PASSWORD=$redisPassword",
    "DOROSAK_REDIS_PORT=6380",
    "",
    "DOROSAK_MINIO_ROOT_USER=dorosak_local_admin",
    "DOROSAK_MINIO_ROOT_PASSWORD=$(New-UrlSafeSecret)",
    "DOROSAK_MINIO_API_PORT=9100",
    "DOROSAK_MINIO_CONSOLE_PORT=9101",
    "",
    "DOROSAK_MAILPIT_SMTP_PORT=1026",
    "DOROSAK_MAILPIT_UI_PORT=8026",
    "",
    "DOROSAK_CLAMAV_PORT=3311",
    "",
    "DOROSAK_CLOUDINARY_ENABLED=false",
    "DOROSAK_CLOUDINARY_CLOUD_NAME=",
    "DOROSAK_CLOUDINARY_API_KEY=",
    "DOROSAK_CLOUDINARY_API_SECRET=",
    "",
    "DOROSAK_API_PORT=5053",
    "DOROSAK_MEDIATR_LICENSE_KEY=",
    "DOROSAK_AUTOMAPPER_LICENSE_KEY=",
    "DOROSAK_OTEL_ENDPOINT=",
    "",
    "Migrations__ConnectionString=$(New-PostgresConnectionString -Username 'dorosak_migrator' -Password $migratorPassword -Migrator)",
    "ConnectionStrings__Database=$(New-PostgresConnectionString -Username 'dorosak_app' -Password $runtimePassword)",
    "ConnectionStrings__Redis=127.0.0.1:6380,password=$redisPassword,abortConnect=false"
)

Set-LocalSecretFile -Path $environmentPath -Lines $lines

Write-Host "Local environment created at .env.local. Secret values were not printed."
