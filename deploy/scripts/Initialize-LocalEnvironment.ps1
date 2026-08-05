[CmdletBinding()]
param(
    [switch]$Force
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

if ($env:OS -ne "Windows_NT") {
    throw "Initialize-LocalEnvironment.ps1 currently supports Windows only."
}

$repositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path
$configurationPath = Join-Path $repositoryRoot "deploy\neon\development.json"
$environmentPath = Join-Path $repositoryRoot ".env.local"
$temporaryEnvironmentPath = "$environmentPath.$([Guid]::NewGuid().ToString('N')).tmp"

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

function New-LocalSecretFileSecurity {
    $windowsIdentity = [Security.Principal.WindowsIdentity]::GetCurrent()
    $identities = @(
        $windowsIdentity.User,
        [Security.Principal.SecurityIdentifier]::new("S-1-5-18"),
        [Security.Principal.SecurityIdentifier]::new("S-1-5-32-544")
    )
    $security = [Security.AccessControl.FileSecurity]::new()
    $security.SetAccessRuleProtection($true, $false)

    foreach ($identity in $identities) {
        $rule = [Security.AccessControl.FileSystemAccessRule]::new(
            $identity,
            [Security.AccessControl.FileSystemRights]::FullControl,
            [Security.AccessControl.AccessControlType]::Allow
        )
        $security.AddAccessRule($rule)
    }

    return $security
}

function Assert-LocalSecretFileSecurity {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path
    )

    $currentUserSid = [Security.Principal.WindowsIdentity]::GetCurrent().User.Value
    $allowedSids = @($currentUserSid, "S-1-5-18", "S-1-5-32-544")
    $security = Get-Acl -LiteralPath $Path
    $actualSids = @()
    $owner = [Security.Principal.NTAccount]::new($security.Owner)
    $ownerSid = $owner.Translate([Security.Principal.SecurityIdentifier]).Value

    if (-not $security.AreAccessRulesProtected) {
        throw ".env.local must not inherit Windows access rules."
    }
    if ($ownerSid -ne $currentUserSid) {
        throw ".env.local must be owned by the current Windows user."
    }

    foreach ($rule in $security.Access) {
        $sid = $rule.IdentityReference.Translate([Security.Principal.SecurityIdentifier]).Value
        $hasFullControl = ($rule.FileSystemRights -band [Security.AccessControl.FileSystemRights]::FullControl) `
            -eq [Security.AccessControl.FileSystemRights]::FullControl

        if ($sid -notin $allowedSids -or `
            $rule.AccessControlType -ne [Security.AccessControl.AccessControlType]::Allow -or `
            -not $hasFullControl) {
            throw ".env.local contains an unexpected Windows access rule."
        }

        $actualSids += $sid
    }

    foreach ($allowedSid in $allowedSids) {
        if ($allowedSid -notin $actualSids) {
            throw ".env.local is missing a required Windows access rule."
        }
    }
}

function Write-LocalSecretFile {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path,

        [Parameter(Mandatory = $true)]
        [AllowEmptyString()]
        [string[]]$Lines
    )

    $security = New-LocalSecretFileSecurity
    $stream = [IO.FileStream]::new(
        $Path,
        [IO.FileMode]::CreateNew,
        [Security.AccessControl.FileSystemRights]::FullControl,
        [IO.FileShare]::None,
        4096,
        [IO.FileOptions]::WriteThrough,
        $security
    )
    $writer = $null

    try {
        $writer = [IO.StreamWriter]::new($stream, [Text.UTF8Encoding]::new($false))
        foreach ($line in $Lines) {
            $writer.WriteLine($line)
        }
    }
    finally {
        if ($writer) {
            $writer.Dispose()
        }
        else {
            $stream.Dispose()
        }
    }

    Assert-LocalSecretFileSecurity -Path $Path
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

try {
    if (Test-Path -LiteralPath $temporaryEnvironmentPath) {
        Remove-Item -LiteralPath $temporaryEnvironmentPath -Force
    }

    Write-LocalSecretFile -Path $temporaryEnvironmentPath -Lines $lines
    Move-Item -LiteralPath $temporaryEnvironmentPath -Destination $environmentPath -Force
    Assert-LocalSecretFileSecurity -Path $environmentPath
}
finally {
    if (Test-Path -LiteralPath $temporaryEnvironmentPath) {
        Remove-Item -LiteralPath $temporaryEnvironmentPath -Force
    }
}

Write-Host "Local environment created at .env.local. Secret values were not printed."
