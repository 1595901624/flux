#requires -RunAsAdministrator

[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'

$runtimeArchitecture = [System.Runtime.InteropServices.RuntimeInformation]::OSArchitecture.ToString()
$runtimeIdentifier = switch ($runtimeArchitecture) {
    'X86' { 'win-x86' }
    'X64' { 'win-x64' }
    'Arm64' { 'win-arm64' }
    default { throw "不支持的 Windows 架构：$runtimeArchitecture" }
}

$certificatePath = Join-Path $PSScriptRoot 'Flux.cer'
$packageDirectory = Join-Path $PSScriptRoot "packages\\$runtimeIdentifier"
$package = Get-ChildItem -LiteralPath $packageDirectory -Filter '*.msix' -File | Select-Object -First 1

if (-not (Test-Path -LiteralPath $certificatePath -PathType Leaf)) {
    throw "未找到签名证书：$certificatePath"
}

if (-not $package) {
    throw "未找到 $runtimeIdentifier 的 MSIX 包：$packageDirectory"
}

Import-Certificate -FilePath $certificatePath -CertStoreLocation 'Cert:\LocalMachine\TrustedPeople' | Out-Null
Add-AppxPackage -Path $package.FullName

Write-Host "Flux 已安装或更新：$($package.Name)"
