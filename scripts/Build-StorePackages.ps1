# 生成 Microsoft Store 提交包，不安装应用或修改证书存储。
param([string]$OutputDirectory = 'artifacts/store/0.4.0')
$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
Set-Location -LiteralPath $repoRoot
[xml]$project = Get-Content -LiteralPath 'Flux.csproj'
$version = ($project.Project.PropertyGroup | Where-Object Version | Select-Object -First 1).Version
[xml]$sourceManifest = Get-Content -LiteralPath 'Package.appxmanifest'
if ($sourceManifest.Package.Identity.Version -ne "$version.0") { throw '程序集与包版本不一致' }
$storeOutputRoot = [IO.Path]::GetFullPath((Join-Path $repoRoot $OutputDirectory))
New-Item -ItemType Directory -Path $storeOutputRoot -Force | Out-Null
function Invoke-DotNet {
    param([string[]]$Arguments)
    & dotnet @Arguments
    if ($LASTEXITCODE -ne 0) { throw "dotnet failed: $($Arguments -join ' ')" }
}
foreach ($architecture in @(
    @{ Rid = 'win-x86'; Platform = 'x86' },
    @{ Rid = 'win-x64'; Platform = 'x64' },
    @{ Rid = 'win-arm64'; Platform = 'ARM64' }
)) {
    $rid = $architecture.Rid
    $payloadRoot = Join-Path $storeOutputRoot "payload/$rid"
    $serviceOutput = Join-Path $payloadRoot 'service-payload'
    $installerOutput = Join-Path $payloadRoot 'installer-build'
    $packageOutput = (Join-Path $storeOutputRoot "packages/$rid") + [IO.Path]::DirectorySeparatorChar
    Invoke-DotNet @('publish', 'Flux.Service/Flux.Service.csproj', '-c', 'Release', '-r', $rid,
        '--self-contained', 'true', "-p:Version=$version", '-o', $serviceOutput)
    Invoke-DotNet @('publish', 'Flux.Service.Installer/Flux.Service.Installer.csproj', '-c', 'Release', '-r', $rid,
        '--self-contained', 'true', "-p:Version=$version", '-o', $installerOutput)
    Copy-Item -LiteralPath (Join-Path $installerOutput 'Flux.Service.Installer.exe') -Destination $payloadRoot
    Invoke-DotNet @('publish', 'Flux.csproj', '-c', 'Release', '-r', $rid,
        "-p:Platform=$($architecture.Platform)", "-p:Version=$version", "-p:ServicePayloadDir=$payloadRoot",
        '-p:WindowsPackageType=MSIX', '-p:EnableMsixTooling=true', '-p:UapAppxPackageBuildMode=StoreUpload',
        '-p:GenerateAppxPackageOnBuild=true', '-p:AppxBundle=Never', '-p:AppxPackageSigningEnabled=false',
        "-p:AppxPackageDir=$packageOutput")
    $packages = @(Get-ChildItem -LiteralPath $packageOutput -Filter '*.msix' -Recurse)
    if ($packages.Count -ne 1) { throw "$rid 没有生成唯一 MSIX 包" }
    $package = $packages[0]
    $zip = [IO.Compression.ZipFile]::OpenRead($package.FullName)
    try {
        foreach ($required in @('Flux.exe','Flux.dll','resources.pri','core/mihomo.exe','Assets/app.ico',
            'Flux.Service.Installer.exe','service-payload/Flux.Service.exe','service-payload/core/mihomo.exe')) {
            if (!$zip.GetEntry($required)) { throw "$rid 包缺少 $required" }
        }
        $reader = [IO.StreamReader]::new($zip.GetEntry('AppxManifest.xml').Open())
        try { [xml]$manifest = $reader.ReadToEnd() } finally { $reader.Dispose() }
        if ($manifest.Package.Identity.Version -ne "$version.0" -or
            $manifest.Package.Identity.Name -ne $sourceManifest.Package.Identity.Name -or
            $manifest.Package.Identity.Publisher -ne $sourceManifest.Package.Identity.Publisher -or
            $manifest.Package.Identity.ProcessorArchitecture -ne $architecture.Platform.ToLowerInvariant()) {
            throw "$rid 包标识、版本或架构校验失败"
        }
    } finally { $zip.Dispose() }
    Write-Host "Verified Store package: $($package.FullName)"
}
Get-ChildItem -LiteralPath (Join-Path $storeOutputRoot 'packages') -Recurse -File |
    Where-Object { $_.Extension -in '.msix', '.msixupload', '.appxupload' } |
    Get-FileHash -Algorithm SHA256 | Export-Csv -LiteralPath (Join-Path $storeOutputRoot 'SHA256.csv') -NoTypeInformation
