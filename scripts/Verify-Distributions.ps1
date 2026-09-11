# Verify-Distributions.ps1 — 本地复现 CI 打包并验证发布物完整性
# 用法:
#   .\scripts\Verify-Distributions.ps1 -Mode Portable            # 三架构便携 ZIP
#   .\scripts\Verify-Distributions.ps1 -Mode Msix                # 三架构未签名 MSIX
#   .\scripts\Verify-Distributions.ps1 -Mode All
param(
    [ValidateSet('Portable', 'Msix', 'All')]
    [string]$Mode = 'All',
    [string]$Version = '0.3.0'
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
Set-Location $root

$architectures = @(
    @{ Rid = 'win-x86'; Platform = 'x86' },
    @{ Rid = 'win-x64'; Platform = 'x64' },
    @{ Rid = 'win-arm64'; Platform = 'ARM64' }
)

# 期望的发布物内容（便携与 MSIX 包都必须包含）
$expectedFiles = @('Flux.exe', 'Flux.dll', 'resources.pri', 'core/mihomo.exe', 'Assets/app.ico')

function Test-PackageContent {
    param([string]$PackagePath, [string]$Label)
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $zip = [System.IO.Compression.ZipFile]::OpenRead($PackagePath)
    try {
        $names = $zip.Entries | ForEach-Object { $_.FullName }
        $problems = @()
        foreach ($expected in $expectedFiles) {
            $pattern = '^' + [regex]::Escape($expected).Replace('/', '[/\\]') + '$'
            if (-not ($names | Where-Object { $_ -match $pattern })) {
                $problems += "缺少 $expected"
            }
        }
        # i18n: resources.pri 必须存在且包含多语言标记
        $pri = $zip.Entries | Where-Object { $_.FullName -eq 'resources.pri' }
        if ($pri) {
            $stream = $pri.Open()
            $ms = New-Object System.IO.MemoryStream
            $stream.CopyTo($ms)
            $bytes = $ms.ToArray()
            $stream.Dispose(); $ms.Dispose()
            # PRI 内语言标记可能是 ASCII 或 UTF-16LE；转十六进制后搜索两种形态（避免编码空值问题）
            $hex = [System.BitConverter]::ToString($bytes).Replace('-', '')
            foreach ($lang in 'zh-CN', 'en-US', 'ja', 'ar', 'tt') {
                $asciiHex = [System.BitConverter]::ToString([System.Text.Encoding]::ASCII.GetBytes($lang)).Replace('-', '')
                $utf16Hex = [System.BitConverter]::ToString([System.Text.Encoding]::Unicode.GetBytes($lang)).Replace('-', '')
                if (-not ($hex.Contains($asciiHex) -or $hex.Contains($utf16Hex))) {
                    $problems += "resources.pri 缺少语言标记 $lang"
                }
            }
        }
        if ($problems.Count -gt 0) {
            Write-Host "  [FAIL] $Label" -ForegroundColor Red
            $problems | ForEach-Object { Write-Host "    - $_" -ForegroundColor Red }
            throw "$Label 内容校验失败"
        }
        Write-Host "  [OK] $Label ($($names.Count) 项)" -ForegroundColor Green
    }
    finally { $zip.Dispose() }
}

if ($Mode -in 'Portable', 'All') {
    Write-Host '=== 便携 ZIP（三架构，self-contained）===' -ForegroundColor Cyan
    foreach ($arch in $architectures) {
        $rid = $arch.Rid
        $output = "artifacts\verify\portable\$rid"
        if (Test-Path $output) { Remove-Item $output -Recurse -Force }
        Write-Host "发布 $rid ..."
        dotnet publish Flux.csproj -c Release -r $rid --self-contained true -p:WindowsPackageType=None -o $output
        if ($LASTEXITCODE -ne 0) { throw "publish $rid 失败" }

        $zipPath = "artifacts\verify\Flux-$rid-v$Version.zip"
        if (Test-Path $zipPath) { Remove-Item $zipPath -Force }
        Compress-Archive -Path "$output\*" -DestinationPath $zipPath
        Test-PackageContent -PackagePath $zipPath -Label "便携 $rid"
    }
}

if ($Mode -in 'Msix', 'All') {
    Write-Host '=== MSIX（三架构，SideloadOnly，未签名）===' -ForegroundColor Cyan
    foreach ($arch in $architectures) {
        $rid = $arch.Rid
        $pkgDir = "$root\artifacts\verify\msix\$rid\"
        if (Test-Path $pkgDir) { Remove-Item $pkgDir -Recurse -Force }
        Write-Host "打包 $rid ..."
        dotnet publish Flux.csproj -c Release -r $rid -p:Platform=$($arch.Platform) `
            -p:WindowsPackageType=MSIX -p:EnableMsixTooling=true `
            -p:UapAppxPackageBuildMode=SideloadOnly -p:GenerateAppxPackageOnBuild=true `
            -p:AppxBundle=Never -p:AppxPackageSigningEnabled=false `
            -p:AppxPackageDir=$pkgDir
        if ($LASTEXITCODE -ne 0) { throw "MSIX 打包 $rid 失败" }

        $msix = Get-ChildItem $pkgDir -Filter '*.msix' -Recurse | Select-Object -First 1
        if (-not $msix) { throw "$rid 未生成 .msix" }
        Test-PackageContent -PackagePath $msix.FullName -Label "MSIX $rid"
    }
}

Write-Host '=== 全部发布物验证通过 ===' -ForegroundColor Green
