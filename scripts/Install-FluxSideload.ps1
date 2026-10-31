[CmdletBinding()]
param(
    # Validate an extracted distribution without changing certificate stores or installing it.
    [switch]$VerifyOnly,

    [ValidateSet('win-x86', 'win-x64', 'win-arm64')]
    [string]$RuntimeIdentifier
)

$ErrorActionPreference = 'Stop'

if ($RuntimeIdentifier -and -not $VerifyOnly) {
    throw 'RuntimeIdentifier 仅用于 VerifyOnly 校验；安装时自动选择当前系统架构。'
}

if (-not $RuntimeIdentifier) {
    $runtimeArchitecture = [System.Runtime.InteropServices.RuntimeInformation]::OSArchitecture.ToString()
    $RuntimeIdentifier = switch ($runtimeArchitecture) {
        'X86' { 'win-x86' }
        'X64' { 'win-x64' }
        'Arm64' { 'win-arm64' }
        default { throw "不支持的 Windows 架构：$runtimeArchitecture" }
    }
}

$certificatePath = Join-Path $PSScriptRoot 'Flux.cer'
$packageDirectory = Join-Path (Join-Path $PSScriptRoot 'packages') $RuntimeIdentifier

if (-not (Test-Path -LiteralPath $certificatePath -PathType Leaf)) {
    throw "未找到签名证书：$certificatePath"
}

if (-not (Test-Path -LiteralPath $packageDirectory -PathType Container)) {
    throw "未找到 $RuntimeIdentifier 的包目录，请下载与当前 Windows 架构匹配的侧载 ZIP：$packageDirectory"
}

# MSBuild places the package inside a versioned *_Test subdirectory.
$packages = @(Get-ChildItem -LiteralPath $packageDirectory -Filter '*.msix' -File -Recurse)
if ($packages.Count -eq 0) {
    throw "未找到 $RuntimeIdentifier 的 MSIX 包：$packageDirectory"
}
if ($packages.Count -ne 1) {
    throw "找到多个 $RuntimeIdentifier 的 MSIX 包，请将侧载 ZIP 解压到空目录后重试：$packageDirectory"
}
$package = $packages[0]

if ($VerifyOnly) {
    Write-Host "Flux 侧载目录校验通过：$($package.FullName)"
    return
}

$identity = [Security.Principal.WindowsIdentity]::GetCurrent()
try {
    $principal = [Security.Principal.WindowsPrincipal]::new($identity)
    if (-not $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
        throw '请以管理员身份运行 PowerShell，再执行此脚本。'
    }
}
finally {
    $identity.Dispose()
}

Import-Certificate -FilePath $certificatePath -CertStoreLocation 'Cert:\LocalMachine\TrustedPeople' | Out-Null
Add-AppxPackage -Path $package.FullName

Write-Host "Flux 已安装或更新：$($package.Name)"
