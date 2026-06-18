<#
.SYNOPSIS
    Authenticode-sign the Vastior release binaries.
.DESCRIPTION
    Signs Vastior.exe and the four payload DLLs with a code-signing certificate
    and an RFC-3161 timestamp. Run this AFTER building, BEFORE packaging the
    release zip / generating SHA256SUMS.

    Requires:
      * signtool.exe on PATH (install the Windows SDK / "Windows SDK Signing Tools")
      * a code-signing certificate as a .pfx (or use -Thumbprint for an installed cert)

    Signing is the single biggest reduction in SmartScreen / antivirus friction for
    an injecting winmm-proxy tool. winmm_orig.dll is generated on the user's PC at
    install time and is never shipped, so it is not signed here.
.EXAMPLE
    pwsh tools/sign.ps1 -Pfx C:\path\cert.pfx -Password ****
.EXAMPLE
    pwsh tools/sign.ps1 -Thumbprint A1B2C3...   # cert already in the user store
#>
[CmdletBinding(DefaultParameterSetName = 'Pfx')]
param(
    [Parameter(Mandatory, ParameterSetName = 'Pfx')][string]$Pfx,
    [Parameter(ParameterSetName = 'Pfx')][string]$Password,
    [Parameter(Mandatory, ParameterSetName = 'Store')][string]$Thumbprint,
    [string]$TimestampUrl = 'http://timestamp.digicert.com'
)

$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent $PSScriptRoot
$signtool = (Get-Command signtool.exe -ErrorAction SilentlyContinue).Source
if (-not $signtool) { throw 'signtool.exe not found. Install the Windows SDK (Signing Tools).' }

$targets = @(
    'Vastior.exe',
    'files\Vastior.dll',
    'files\winmm.dll',
    'files\x64\Vastior.dll',
    'files\x64\winmm.dll'
) | ForEach-Object { Join-Path $repo $_ }

foreach ($t in $targets) {
    if (-not (Test-Path -LiteralPath $t)) { throw "Missing build output: $t (build first)." }
}

$common = @('sign', '/fd', 'SHA256', '/tr', $TimestampUrl, '/td', 'SHA256', '/v')
if ($PSCmdlet.ParameterSetName -eq 'Pfx') {
    $auth = @('/f', $Pfx) + $(if ($Password) { @('/p', $Password) } else { @() })
}
else {
    $auth = @('/sha1', $Thumbprint)
}

foreach ($t in $targets) {
    & $signtool ($common + $auth + @($t))
    if ($LASTEXITCODE -ne 0) { throw "signtool failed for $t" }
}

& $signtool @('verify', '/pa', '/all') $targets[0]
if ($LASTEXITCODE -ne 0) { throw 'signature verification failed' }
Write-Host 'All Vastior binaries signed and verified.' -ForegroundColor Green
