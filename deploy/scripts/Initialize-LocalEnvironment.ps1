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
$configurationPath = Join-Path $repositoryRoot "deploy\neon\development.json"
$environmentPath = Join-Path $repositoryRoot ".env.local"
if ((Test-Path -LiteralPath $environmentPath) -and -not $Force) {
    Write-Host ".env.local already exists. Use -Force only when rotating local credentials."
    exit 0
}

$ignoredPath = & git -C $repositoryRoot check-ignore ".env.local"
if ($LASTEXITCODE -ne 0) {
    throw ".env.local must be ignored by Git before credentials can be generated."
}

$configuration = Get-Content -LiteralPath $configurationPath -Raw | ConvertFrom-Json

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

function Get-NeonConnectionString {
    param(
        [switch]$Pooled
    )

    $arguments = @(
        "--yes",
        "--package", "neonctl@$($configuration.neonCliVersion)",
        "neonctl", "connection-string", $configuration.branch,
        "--project-id", $configuration.projectId,
        "--role-name", $configuration.ownerRole,
        "--database-name", $configuration.database,
        "--ssl", "verify-full",
        "--no-color",
        "--no-analytics"
    )

    if ($Pooled) {
        $arguments += "--pooled"
    }

    $output = & npx @arguments
    if ($LASTEXITCODE -ne 0) {
        throw "Neon CLI could not provide a connection string."
    }

    $connectionString = ($output -join [Environment]::NewLine).Trim()
    if ($connectionString -notmatch "^postgresql://") {
        throw "Neon CLI returned an invalid connection string."
    }

    return $connectionString
}

$pooledConnectionString = Get-NeonConnectionString -Pooled
$directConnectionString = Get-NeonConnectionString

$lines = @(
    "DOROSAK_REDIS_PASSWORD=$(New-UrlSafeSecret)",
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
    "DOROSAK_NEON_PROJECT_ID=$($configuration.projectId)",
    "DOROSAK_NEON_OWNER_POOLED_URL=$pooledConnectionString",
    "DOROSAK_NEON_OWNER_DIRECT_URL=$directConnectionString"
)

Set-LocalSecretFile -Path $environmentPath -Lines $lines

Write-Host "Local environment created at .env.local. Secret values were not printed."
