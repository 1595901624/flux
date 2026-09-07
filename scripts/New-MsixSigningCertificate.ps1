[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [string]$Publisher,

    [Parameter(Mandatory)]
    [SecureString]$Password,

    [Parameter()]
    [string]$OutputPath = (Join-Path $PSScriptRoot '..\signing\msix-signing-certificate.pfx'),

    [Parameter()]
    [ValidateRange(1, 36)]
    [int]$ValidMonths = 24
)

$ErrorActionPreference = 'Stop'

if ($Publisher -notmatch '^CN=.+') {
    throw 'Publisher 必须是 Partner Center 提供的完整发布者 DN，例如 CN=12345678-1234-1234-1234-1234567890AB。'
}

$resolvedOutputPath = $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($OutputPath)
$outputDirectory = Split-Path -Parent $resolvedOutputPath
New-Item -ItemType Directory -Path $outputDirectory -Force | Out-Null

# Store MSIX signing only needs a code-signing certificate.  The private key is
# exportable solely to create the encrypted PFX that will be stored as a secret.
$certificate = New-SelfSignedCertificate `
    -Type Custom `
    -Subject $Publisher `
    -FriendlyName 'Flux Proxy MSIX Store signing' `
    -KeyAlgorithm RSA `
    -KeyLength 3072 `
    -HashAlgorithm SHA256 `
    -KeyUsage DigitalSignature `
    -TextExtension @('2.5.29.37={text}1.3.6.1.5.5.7.3.3', '2.5.29.19={text}') `
    -KeyExportPolicy Exportable `
    -CertStoreLocation 'Cert:\CurrentUser\My' `
    -NotAfter (Get-Date).AddMonths($ValidMonths)

try {
    Export-PfxCertificate -Cert $certificate.PSPath -FilePath $resolvedOutputPath -Password $Password -ChainOption EndEntityCertOnly -Force | Out-Null
}
finally {
    Remove-Item -LiteralPath $certificate.PSPath -Force -ErrorAction SilentlyContinue
}

Write-Host "已生成加密 PFX：$resolvedOutputPath"
Write-Host '请勿提交该文件；它已被 .gitignore 排除。'
