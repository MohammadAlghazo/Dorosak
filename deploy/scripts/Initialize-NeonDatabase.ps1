[CmdletBinding()]
param(
    [switch]$RotateCredentials
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

Import-Module (Join-Path $PSScriptRoot "LocalSecretFile.psm1") -Force

if ($env:OS -ne "Windows_NT") {
    throw "Initialize-NeonDatabase.ps1 currently supports Windows only."
}

$repositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path
$environmentPath = Join-Path $repositoryRoot ".env.local"
$configurationPath = Join-Path $repositoryRoot "deploy\neon\development.json"
$bootstrapSqlPath = Join-Path $repositoryRoot "deploy\neon\bootstrap-development-database.sql"

if (-not (Test-Path -LiteralPath $environmentPath -PathType Leaf)) {
    throw ".env.local does not exist. Run Initialize-LocalEnvironment.ps1 first."
}

Assert-LocalSecretFileSecurity -Path $environmentPath

$ignoredPath = & git -C $repositoryRoot check-ignore ".env.local"
if ($LASTEXITCODE -ne 0) {
    throw ".env.local must be ignored by Git before database credentials can be generated."
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

$environmentLines = [IO.File]::ReadAllLines($environmentPath)
$values = @{}
foreach ($line in $environmentLines) {
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

    return [string]$values[$Name]
}

function New-UrlSafeSecret {
    $bytes = [byte[]]::new(32)
    $generator = [Security.Cryptography.RandomNumberGenerator]::Create()

    try {
        $generator.GetBytes($bytes)
    }
    finally {
        $generator.Dispose()
    }

    return [Convert]::ToBase64String($bytes).TrimEnd("=").Replace("+", "-").Replace("/", "_")
}

function Get-ExistingPassword {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Name
    )

    if ($RotateCredentials -or -not $values.ContainsKey($Name)) {
        return New-UrlSafeSecret
    }

    $builder = [Data.Common.DbConnectionStringBuilder]::new()
    $builder.set_ConnectionString([string]$values[$Name])
    if (-not $builder.ContainsKey("Password") -or [string]::IsNullOrWhiteSpace([string]$builder["Password"])) {
        throw "Existing connection string '$Name' does not contain a password."
    }

    return [string]$builder["Password"]
}

function New-NpgsqlConnectionString {
    param(
        [Parameter(Mandatory = $true)]
        [Uri]$Endpoint,

        [Parameter(Mandatory = $true)]
        [string]$Username,

        [Parameter(Mandatory = $true)]
        [string]$Password,

        [switch]$Migrator
    )

    $database = [Uri]::UnescapeDataString($Endpoint.AbsolutePath.TrimStart("/"))
    $port = if ($Endpoint.Port -gt 0) { $Endpoint.Port } else { 5432 }
    $builder = [Data.Common.DbConnectionStringBuilder]::new()
    $builder["Host"] = $Endpoint.DnsSafeHost
    $builder["Port"] = $port
    $builder["Database"] = $database
    $builder["Username"] = $Username
    $builder["Password"] = $Password
    $builder["SSL Mode"] = "VerifyFull"
    $builder["Channel Binding"] = "Require"
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

function New-PostgresUri {
    param(
        [Parameter(Mandatory = $true)]
        [Uri]$Endpoint,

        [Parameter(Mandatory = $true)]
        [string]$Username,

        [Parameter(Mandatory = $true)]
        [string]$Password
    )

    $database = $Endpoint.AbsolutePath.TrimStart("/")
    $port = if ($Endpoint.Port -gt 0) { $Endpoint.Port } else { 5432 }
    $encodedUsername = [Uri]::EscapeDataString($Username)
    $encodedPassword = [Uri]::EscapeDataString($Password)
    return "postgresql://${encodedUsername}:${encodedPassword}@$($Endpoint.DnsSafeHost):${port}/${database}?sslmode=verify-full&channel_binding=require&sslrootcert=system"
}

function ConvertTo-Base64 {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Value
    )

    return [Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes($Value))
}

function Invoke-DatabaseBootstrap {
    param(
        [Parameter(Mandatory = $true)]
        [string]$OwnerConnection,

        [Parameter(Mandatory = $true)]
        [string]$PasswordSql
    )

    $containerScript = @'
IFS= read -r connection_b64
IFS= read -r password_sql_b64
connection="$(printf '%s' "$connection_b64" | base64 -d)"
psql "$connection" --no-psqlrc -q -v ON_ERROR_STOP=1 -f /bootstrap.sql
printf '%s' "$password_sql_b64" | base64 -d | psql "$connection" --no-psqlrc -q -v ON_ERROR_STOP=1
'@
    $mount = "type=bind,source=$bootstrapSqlPath,target=/bootstrap.sql,readonly"
    $payload = @((ConvertTo-Base64 $OwnerConnection), (ConvertTo-Base64 $PasswordSql))
    $encodedContainerScript = ConvertTo-Base64 $containerScript
    $command = "printf '%s' '$encodedContainerScript' | base64 -d > /tmp/bootstrap.sh; sh -eu /tmp/bootstrap.sh"
    $payload | & $dockerPath run --rm -i --mount $mount $postgresClientImage sh -c $command | Out-Null

    if ($LASTEXITCODE -ne 0) {
        throw "Neon database bootstrap failed."
    }
}

function Invoke-DatabaseScalar {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Connection,

        [Parameter(Mandatory = $true)]
        [string]$Sql
    )

    $containerScript = @'
IFS= read -r connection_b64
IFS= read -r sql_b64
connection="$(printf '%s' "$connection_b64" | base64 -d)"
printf '%s' "$sql_b64" | base64 -d | psql "$connection" --no-psqlrc -qAt -v ON_ERROR_STOP=1
'@
    $payload = @((ConvertTo-Base64 $Connection), (ConvertTo-Base64 $Sql))
    $encodedContainerScript = ConvertTo-Base64 $containerScript
    $command = "printf '%s' '$encodedContainerScript' | base64 -d > /tmp/query.sh; sh -eu /tmp/query.sh"
    $result = $payload | & $dockerPath run --rm -i $postgresClientImage sh -c $command

    if ($LASTEXITCODE -ne 0) {
        throw "Neon database verification failed."
    }

    return ($result -join "`n").Trim()
}

function Set-EnvironmentValues {
    param(
        [Parameter(Mandatory = $true)]
        [Collections.IDictionary]$Updates,

        [string[]]$Remove = @()
    )

    $remaining = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
    $removed = [Collections.Generic.HashSet[string]]::new($Remove, [StringComparer]::Ordinal)
    foreach ($key in $Updates.Keys) {
        [void]$remaining.Add([string]$key)
    }

    $updatedLines = [Collections.Generic.List[string]]::new()
    foreach ($line in $environmentLines) {
        $parts = $line.Split("=", 2)
        if ($parts.Count -eq 2 -and $removed.Contains($parts[0])) {
            continue
        }

        if ($parts.Count -eq 2 -and $remaining.Contains($parts[0])) {
            $updatedLines.Add("$($parts[0])=$($Updates[$parts[0]])")
            [void]$remaining.Remove($parts[0])
        }
        else {
            $updatedLines.Add($line)
        }
    }

    if ($remaining.Count -gt 0 -and $updatedLines.Count -gt 0 -and $updatedLines[$updatedLines.Count - 1] -ne "") {
        $updatedLines.Add("")
    }

    foreach ($key in $Updates.Keys) {
        if ($remaining.Contains([string]$key)) {
            $updatedLines.Add("$key=$($Updates[$key])")
        }
    }

    Set-LocalSecretFile -Path $environmentPath -Lines $updatedLines.ToArray()
}

function Invoke-EntityFrameworkMigrations {
    param(
        [Parameter(Mandatory = $true)]
        [string]$ConnectionString
    )

    $backendPath = Join-Path $repositoryRoot "backend"
    $hadPreviousConnection = Test-Path Env:\Migrations__ConnectionString
    $previousConnection = $env:Migrations__ConnectionString

    Push-Location $backendPath
    try {
        $env:Migrations__ConnectionString = $ConnectionString
        & dotnet tool restore
        if ($LASTEXITCODE -ne 0) {
            throw "The local .NET tools could not be restored."
        }

        & dotnet tool run dotnet-ef database update `
            --project "src/Dorosak.Infrastructure/Dorosak.Infrastructure.csproj" `
            --startup-project "src/Dorosak.Api/Dorosak.Api.csproj" `
            --context DorosakDbContext
        if ($LASTEXITCODE -ne 0) {
            throw "The Neon database migration failed."
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
}

$ownerDirectUrl = Get-RequiredValue -Name "DOROSAK_NEON_OWNER_DIRECT_URL"
$ownerPooledUrl = Get-RequiredValue -Name "DOROSAK_NEON_OWNER_POOLED_URL"
$ownerDirectEndpoint = [Uri]$ownerDirectUrl
$ownerPooledEndpoint = [Uri]$ownerPooledUrl
$ownerConnection = if ($ownerDirectUrl.Contains("?")) {
    "$ownerDirectUrl&sslrootcert=system"
}
else {
    "$ownerDirectUrl?sslrootcert=system"
}

$migratorConnectionKey = if ($values.ContainsKey("Migrations__ConnectionString")) {
    "Migrations__ConnectionString"
}
elseif ($values.ContainsKey("DOROSAK_NEON_MIGRATOR_CONNECTION")) {
    "DOROSAK_NEON_MIGRATOR_CONNECTION"
}
else {
    "Migrations__ConnectionString"
}
$migratorPassword = Get-ExistingPassword -Name $migratorConnectionKey
$runtimePassword = Get-ExistingPassword -Name "ConnectionStrings__Database"
$passwordSql = @(
    "ALTER ROLE dorosak_migrator PASSWORD '$migratorPassword';",
    "ALTER ROLE dorosak_app PASSWORD '$runtimePassword';"
) -join [Environment]::NewLine

Invoke-DatabaseBootstrap -OwnerConnection $ownerConnection -PasswordSql $passwordSql

$migratorUri = New-PostgresUri -Endpoint $ownerDirectEndpoint -Username "dorosak_migrator" -Password $migratorPassword
$runtimeUri = New-PostgresUri -Endpoint $ownerPooledEndpoint -Username "dorosak_app" -Password $runtimePassword
$migratorResult = Invoke-DatabaseScalar -Connection $migratorUri `
    -Sql "SET ROLE dorosak_schema_owner; SELECT current_user || '|' || session_user || '|' || has_schema_privilege(current_user, 'app', 'CREATE');"
$runtimeResult = Invoke-DatabaseScalar -Connection $runtimeUri `
    -Sql "SELECT current_user || '|' || session_user || '|' || has_schema_privilege(current_user, 'app', 'USAGE') || '|' || has_schema_privilege(current_user, 'operations', 'USAGE') || '|' || has_schema_privilege(current_user, 'app', 'CREATE') || '|' || has_schema_privilege(current_user, 'operations', 'CREATE') || '|' || has_schema_privilege(current_user, 'migrations', 'USAGE') || '|' || has_schema_privilege(current_user, 'public', 'USAGE') || '|' || has_database_privilege(current_user, current_database(), 'TEMPORARY') || '|' || pg_has_role(current_user, 'dorosak_schema_owner', 'SET') || '|' || current_setting('statement_timeout');"

if ($migratorResult -ne "dorosak_schema_owner|dorosak_migrator|true") {
    throw "Migrator role verification returned an unexpected result."
}
if ($runtimeResult -ne "dorosak_app|dorosak_app|true|true|false|false|false|false|false|false|30s") {
    throw "Runtime role verification returned an unexpected result."
}

$updates = [ordered]@{
    "Migrations__ConnectionString" = New-NpgsqlConnectionString `
        -Endpoint $ownerDirectEndpoint -Username "dorosak_migrator" -Password $migratorPassword -Migrator
    "ConnectionStrings__Database" = New-NpgsqlConnectionString `
        -Endpoint $ownerPooledEndpoint -Username "dorosak_app" -Password $runtimePassword
    "ConnectionStrings__Redis" = "127.0.0.1:$(Get-RequiredValue -Name 'DOROSAK_REDIS_PORT'),password=$(Get-RequiredValue -Name 'DOROSAK_REDIS_PASSWORD'),abortConnect=false"
}

Set-EnvironmentValues -Updates $updates -Remove @("DOROSAK_NEON_MIGRATOR_CONNECTION")
Invoke-EntityFrameworkMigrations -ConnectionString $updates["Migrations__ConnectionString"]

$runtimePrivilegeSql = @'
SELECT
    (to_regclass('operations.outbox_messages') IS NOT NULL
    AND has_table_privilege(current_user, 'operations.outbox_messages', 'SELECT')
    AND NOT has_table_privilege(current_user, 'operations.outbox_messages', 'TRUNCATE')
    AND has_table_privilege(current_user, 'operations.schema_compatibility', 'SELECT')
    AND NOT has_table_privilege(current_user, 'operations.schema_compatibility', 'UPDATE')
    AND has_schema_privilege(current_user, 'engagement', 'USAGE')
    AND has_table_privilege(current_user, 'engagement.content_reports', 'SELECT')
    AND has_table_privilege(current_user, 'engagement.content_reports', 'INSERT')
    AND NOT has_table_privilege(current_user, 'engagement.content_reports', 'UPDATE')
    AND NOT has_table_privilege(current_user, 'engagement.content_reports', 'DELETE')
    AND NOT has_table_privilege(current_user, 'engagement.content_reports', 'TRUNCATE')
    AND has_column_privilege(current_user, 'engagement.content_reports', 'status', 'UPDATE')
    AND has_column_privilege(current_user, 'engagement.content_reports', 'updated_at', 'UPDATE')
    AND has_column_privilege(current_user, 'engagement.content_reports', 'closed_at', 'UPDATE')
    AND NOT has_column_privilege(current_user, 'engagement.content_reports', 'id', 'UPDATE')
    AND NOT has_column_privilege(current_user, 'engagement.content_reports', 'reporter_user_id', 'UPDATE')
    AND NOT has_column_privilege(current_user, 'engagement.content_reports', 'course_id', 'UPDATE')
    AND NOT has_column_privilege(current_user, 'engagement.content_reports', 'review_id', 'UPDATE')
    AND NOT has_column_privilege(current_user, 'engagement.content_reports', 'comment_id', 'UPDATE')
    AND NOT has_column_privilege(current_user, 'engagement.content_reports', 'reported_user_id', 'UPDATE')
    AND NOT has_column_privilege(current_user, 'engagement.content_reports', 'reason', 'UPDATE')
    AND NOT has_column_privilege(current_user, 'engagement.content_reports', 'details', 'UPDATE')
    AND NOT has_column_privilege(current_user, 'engagement.content_reports', 'created_at', 'UPDATE')
    AND has_table_privilege(current_user, 'engagement.moderation_cases', 'SELECT')
    AND has_table_privilege(current_user, 'engagement.moderation_cases', 'INSERT')
    AND NOT has_table_privilege(current_user, 'engagement.moderation_cases', 'UPDATE')
    AND NOT has_table_privilege(current_user, 'engagement.moderation_cases', 'DELETE')
    AND NOT has_table_privilege(current_user, 'engagement.moderation_cases', 'TRUNCATE')
    AND has_column_privilege(current_user, 'engagement.moderation_cases', 'status', 'UPDATE')
    AND has_column_privilege(current_user, 'engagement.moderation_cases', 'assigned_to_user_id', 'UPDATE')
    AND has_column_privilege(current_user, 'engagement.moderation_cases', 'version', 'UPDATE')
    AND has_column_privilege(current_user, 'engagement.moderation_cases', 'updated_at', 'UPDATE')
    AND has_column_privilege(current_user, 'engagement.moderation_cases', 'closed_at', 'UPDATE')
    AND NOT has_column_privilege(current_user, 'engagement.moderation_cases', 'id', 'UPDATE')
    AND NOT has_column_privilege(current_user, 'engagement.moderation_cases', 'report_id', 'UPDATE')
    AND NOT has_column_privilege(current_user, 'engagement.moderation_cases', 'created_at', 'UPDATE')
    AND has_table_privilege(current_user, 'engagement.moderation_actions', 'SELECT')
    AND has_table_privilege(current_user, 'engagement.moderation_actions', 'INSERT')
    AND NOT has_table_privilege(current_user, 'engagement.moderation_actions', 'UPDATE')
    AND NOT has_table_privilege(current_user, 'engagement.moderation_actions', 'DELETE')
    AND NOT has_table_privilege(current_user, 'engagement.moderation_actions', 'TRUNCATE')
    AND NOT has_column_privilege(current_user, 'engagement.moderation_actions', 'id', 'UPDATE')
    AND NOT has_column_privilege(current_user, 'engagement.moderation_actions', 'case_id', 'UPDATE')
     AND NOT has_column_privilege(current_user, 'engagement.moderation_actions', 'actor_user_id', 'UPDATE')
     AND NOT has_column_privilege(current_user, 'engagement.moderation_actions', 'action_type', 'UPDATE')
     AND NOT has_column_privilege(current_user, 'engagement.moderation_actions', 'reason', 'UPDATE')
     AND NOT has_column_privilege(current_user, 'engagement.moderation_actions', 'created_at', 'UPDATE')
     AND has_schema_privilege(current_user, 'communication', 'USAGE')
     AND has_table_privilege(current_user, 'communication.conversations', 'SELECT,INSERT')
     AND has_table_privilege(current_user, 'communication.messages', 'SELECT,INSERT')
     AND has_table_privilege(current_user, 'communication.notification_sequences', 'SELECT,INSERT')
     AND has_table_privilege(current_user, 'communication.notifications', 'SELECT,INSERT')
     AND has_table_privilege(current_user, 'communication.announcements', 'SELECT,INSERT')
     AND has_table_privilege(current_user, 'communication.announcement_targets', 'SELECT,INSERT')
     AND NOT has_table_privilege(current_user, 'communication.conversations', 'UPDATE,DELETE,TRUNCATE')
     AND NOT has_table_privilege(current_user, 'communication.messages', 'UPDATE,DELETE,TRUNCATE')
     AND NOT has_table_privilege(current_user, 'communication.notification_sequences', 'UPDATE,DELETE,TRUNCATE')
     AND NOT has_table_privilege(current_user, 'communication.notifications', 'UPDATE,DELETE,TRUNCATE')
     AND NOT has_table_privilege(current_user, 'communication.announcements', 'UPDATE,DELETE,TRUNCATE')
     AND NOT has_table_privilege(current_user, 'communication.announcement_targets', 'UPDATE,DELETE,TRUNCATE')
     AND has_column_privilege(current_user, 'communication.conversations', 'updated_at', 'UPDATE')
     AND has_column_privilege(current_user, 'communication.conversations', 'last_sequence', 'UPDATE')
     AND has_column_privilege(current_user, 'communication.notification_sequences', 'last_sequence', 'UPDATE')
     AND has_column_privilege(current_user, 'communication.notifications', 'is_read', 'UPDATE')
     AND has_column_privilege(current_user, 'communication.notifications', 'read_at', 'UPDATE')
     AND has_column_privilege(current_user, 'communication.announcements', 'title', 'UPDATE')
     AND has_column_privilege(current_user, 'communication.announcements', 'body', 'UPDATE')
     AND has_column_privilege(current_user, 'communication.announcements', 'version', 'UPDATE')
     AND has_column_privilege(current_user, 'communication.announcements', 'updated_at', 'UPDATE')
     AND has_column_privilege(current_user, 'communication.announcements', 'deleted_at', 'UPDATE')
     AND has_column_privilege(current_user, 'communication.announcements', 'deleted_by_user_id', 'UPDATE')
     AND NOT has_column_privilege(current_user, 'communication.notifications', 'user_id', 'UPDATE')
     AND NOT has_column_privilege(current_user, 'communication.notifications', 'sequence', 'UPDATE')
     AND NOT has_column_privilege(current_user, 'communication.notifications', 'message_id', 'UPDATE')
     AND NOT has_column_privilege(current_user, 'communication.notifications', 'announcement_id', 'UPDATE')
     AND NOT has_column_privilege(current_user, 'communication.announcement_targets', 'user_id', 'UPDATE')
     AND NOT has_column_privilege(current_user, 'communication.announcement_targets', 'notification_id', 'UPDATE'))::text;
'@
$runtimePrivilegeResult = Invoke-DatabaseScalar -Connection $runtimeUri -Sql $runtimePrivilegeSql
if ($runtimePrivilegeResult -ne "true") {
    throw "Runtime table privilege verification returned an unexpected result."
}

Write-Host "Neon roles, schema privileges, migrations, and local application connections are ready. Secret values were not printed."
