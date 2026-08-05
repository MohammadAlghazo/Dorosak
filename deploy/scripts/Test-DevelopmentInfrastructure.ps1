[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$repositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path
$environmentPath = Join-Path $repositoryRoot ".env.local"
$configurationPath = Join-Path $repositoryRoot "deploy\neon\development.json"

if (-not (Test-Path -LiteralPath $environmentPath)) {
    throw ".env.local does not exist. Run Initialize-LocalEnvironment.ps1 first."
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

$configuration = Get-Content -LiteralPath $configurationPath -Raw | ConvertFrom-Json
$postgresClientImage = [string]$configuration.postgresClientImage
if ($postgresClientImage -notmatch "^postgres:18\.4-alpine@sha256:[a-f0-9]{64}$") {
    throw "The PostgreSQL client image must be pinned by version and digest."
}

$composeArguments = @("compose", "--project-name", "dorosak", "--env-file", $environmentPath)

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
        throw "Required local environment value '$Name' is missing."
    }

    return $values[$Name]
}

function Test-TcpPort {
    param(
        [Parameter(Mandatory = $true)]
        [int]$Port
    )

    $client = [Net.Sockets.TcpClient]::new()
    try {
        $task = $client.ConnectAsync("127.0.0.1", $Port)
        if (-not $task.Wait(5000) -or -not $client.Connected) {
            throw "TCP port $Port is not reachable."
        }
    }
    finally {
        $client.Dispose()
    }
}

function Get-ContainerEnvironment {
    param(
        [Parameter(Mandatory = $true)]
        [string]$ContainerId
    )

    $json = & $dockerPath inspect $ContainerId --format "{{json .Config.Env}}"
    if ($LASTEXITCODE -ne 0) {
        throw "Could not inspect a development container."
    }

    $environment = @{}
    foreach ($entry in ($json | ConvertFrom-Json)) {
        $parts = $entry.Split("=", 2)
        if ($parts.Count -eq 2) {
            $environment[$parts[0]] = $parts[1]
        }
    }

    return $environment
}

function Test-NeonConnection {
    param(
        [Parameter(Mandatory = $true)]
        [string]$EnvironmentKey,

        [Parameter(Mandatory = $true)]
        [string]$Label
    )

    $connectionString = Get-RequiredValue -Name $EnvironmentKey
    $psqlScript = @(
        'connection_string="$(base64 -d)";',
        'case "$connection_string" in *\?*) separator="&" ;; *) separator="?" ;; esac;',
        'psql "${connection_string}${separator}sslrootcert=system" --no-psqlrc',
        '-Atc "SELECT current_database(), current_user; SHOW server_version;"'
    ) -join " "
    $encodedScript = [Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes($psqlScript))
    $encodedConnectionString = [Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes($connectionString))

    $result = $encodedConnectionString | & $dockerPath run --rm -i $postgresClientImage `
        sh -c "printf '%s' '$encodedScript' | base64 -d > /tmp/test-neon.sh; sh /tmp/test-neon.sh"

    if ($LASTEXITCODE -ne 0 -or $result[0] -ne "dorosak_dev|dorosak_owner") {
        throw "$Label Neon connection test failed."
    }

    Write-Host "$Label Neon connection passed ($($result[1]))."
}

Push-Location $repositoryRoot
try {
    & $dockerPath @composeArguments config --quiet
    if ($LASTEXITCODE -ne 0) {
        throw "Docker Compose validation failed."
    }

    $services = @("redis", "minio", "mailpit", "clamav")
    $containerIds = @{}
    foreach ($service in $services) {
        $containerId = (& $dockerPath @composeArguments ps --quiet $service).Trim()
        if ([string]::IsNullOrWhiteSpace($containerId)) {
            throw "The $service container is not running."
        }
        $containerIds[$service] = $containerId

        $healthTemplate = "{{if .State.Health}}{{.State.Health.Status}}{{else}}{{.State.Status}}{{end}}"
        $health = (& $dockerPath inspect $containerId --format $healthTemplate).Trim()
        if ($health -ne "healthy") {
            throw "The $service container is not healthy. Current status: $health."
        }
    }

    $redisEnvironment = Get-ContainerEnvironment -ContainerId $containerIds["redis"]
    $minioEnvironment = Get-ContainerEnvironment -ContainerId $containerIds["minio"]
    if ($redisEnvironment["REDISCLI_AUTH"] -cne (Get-RequiredValue -Name "DOROSAK_REDIS_PASSWORD")) {
        throw "Redis is running with credentials that do not match .env.local."
    }
    if ($minioEnvironment["MINIO_ROOT_USER"] -cne (Get-RequiredValue -Name "DOROSAK_MINIO_ROOT_USER") -or `
        $minioEnvironment["MINIO_ROOT_PASSWORD"] -cne (Get-RequiredValue -Name "DOROSAK_MINIO_ROOT_PASSWORD")) {
        throw "MinIO is running with credentials that do not match .env.local."
    }

    $setResult = (& $dockerPath @composeArguments exec -T redis `
        redis-cli SET dorosak:health:test ok EX 60).Trim()
    $getResult = (& $dockerPath @composeArguments exec -T redis `
        redis-cli GET dorosak:health:test).Trim()
    & $dockerPath @composeArguments exec -T redis `
        redis-cli DEL dorosak:health:test | Out-Null

    if ($setResult -ne "OK" -or $getResult -ne "ok") {
        throw "Redis read/write test failed."
    }
    Write-Host "Redis read/write test passed."

    $minioPort = [int](Get-RequiredValue -Name "DOROSAK_MINIO_API_PORT")
    $mailpitUiPort = [int](Get-RequiredValue -Name "DOROSAK_MAILPIT_UI_PORT")
    $mailpitSmtpPort = [int](Get-RequiredValue -Name "DOROSAK_MAILPIT_SMTP_PORT")
    $clamavPort = [int](Get-RequiredValue -Name "DOROSAK_CLAMAV_PORT")

    $minio = Invoke-WebRequest -UseBasicParsing -Uri "http://127.0.0.1:$minioPort/minio/health/live" -TimeoutSec 10
    $mailpit = Invoke-WebRequest -UseBasicParsing -Uri "http://127.0.0.1:$mailpitUiPort/livez" -TimeoutSec 10
    if ($minio.StatusCode -ne 200 -or $mailpit.StatusCode -ne 200) {
        throw "MinIO or Mailpit HTTP health test failed."
    }
    Test-TcpPort -Port $mailpitSmtpPort
    Test-TcpPort -Port $clamavPort
    Write-Host "MinIO, Mailpit, and ClamAV endpoint tests passed."

    $eicar = 'X5O!P%@AP[4\PZX54(P^)7CC)7}$EICAR-STANDARD-ANTIVIRUS-TEST-FILE!$H+H*'
    $encodedEicar = [Convert]::ToBase64String([Text.Encoding]::ASCII.GetBytes($eicar))
    & $dockerPath @composeArguments exec -T clamav `
        sh -c "printf '%s' '$encodedEicar' | base64 -d > /tmp/eicar.com"
    if ($LASTEXITCODE -ne 0) {
        throw "Could not create the EICAR test file."
    }

    $scan = & $dockerPath @composeArguments exec -T clamav `
        clamdscan /tmp/eicar.com
    $scanExitCode = $LASTEXITCODE
    & $dockerPath @composeArguments exec -T clamav `
        rm -f /tmp/eicar.com | Out-Null
    if ($scanExitCode -ne 1 -or ($scan -join "`n") -notmatch "FOUND") {
        throw "ClamAV EICAR behavior test failed."
    }
    Write-Host "ClamAV EICAR detection test passed."

    Test-NeonConnection -EnvironmentKey "DOROSAK_NEON_OWNER_DIRECT_URL" -Label "Direct owner"
    Test-NeonConnection -EnvironmentKey "DOROSAK_NEON_OWNER_POOLED_URL" -Label "Pooled owner"

    Write-Host "All development infrastructure checks passed."
}
finally {
    Pop-Location
}
