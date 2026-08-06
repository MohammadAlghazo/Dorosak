Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

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
        [void]$security.AddAccessRule($rule)
    }

    return $security
}

function Assert-LocalSecretFileSecurity {
    [CmdletBinding()]
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
    [CmdletBinding()]
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

function Set-LocalSecretFile {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path,

        [Parameter(Mandatory = $true)]
        [AllowEmptyString()]
        [string[]]$Lines
    )

    $temporaryPath = "$Path.$([Guid]::NewGuid().ToString('N')).tmp"

    try {
        Write-LocalSecretFile -Path $temporaryPath -Lines $Lines
        Move-Item -LiteralPath $temporaryPath -Destination $Path -Force
        Assert-LocalSecretFileSecurity -Path $Path
    }
    finally {
        if (Test-Path -LiteralPath $temporaryPath) {
            Remove-Item -LiteralPath $temporaryPath -Force
        }
    }
}

Export-ModuleMember -Function Assert-LocalSecretFileSecurity, Write-LocalSecretFile, Set-LocalSecretFile
