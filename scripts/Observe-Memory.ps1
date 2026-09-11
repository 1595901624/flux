# Observe-Memory.ps1 - Run Flux for one hour and sample memory (plan section 6)
param(
    [int]$Minutes = 60,
    [int]$IntervalSeconds = 15
)
$ErrorActionPreference = 'Continue'
$root = Split-Path -Parent $PSScriptRoot
Set-Location $root

$exe = Join-Path $root 'artifacts\observe\Flux.exe'
if (-not (Test-Path $exe)) {
    Write-Host 'Publishing win-x64 portable for observation...'
    dotnet publish Flux.csproj -c Release -r win-x64 -o artifacts\observe
    if ($LASTEXITCODE -ne 0) { throw 'publish failed' }
}
if (-not (Test-Path $exe)) { throw 'Flux.exe not found after publish' }

$csv = 'artifacts\memory-observation.csv'
New-Item -ItemType Directory -Force (Split-Path $csv) | Out-Null

# If a Flux instance is already running, observe it; otherwise start one.
$flux = Get-Process -Name Flux -ErrorAction SilentlyContinue | Select-Object -First 1
if ($flux) {
    Write-Host "Flux already running (PID $($flux.Id)); observing existing instance"
} else {
    Write-Host "Starting $exe"
    Start-Process -FilePath $exe | Out-Null
    Start-Sleep -Seconds 12
}

$deadline = (Get-Date).AddMinutes($Minutes)
'timestamp,flux_pid,flux_ws_mb,flux_private_mb,core_pid,core_ws_mb' | Out-File $csv -Encoding utf8

$samples = 0
while ((Get-Date) -lt $deadline) {
    $flux = Get-Process -Name Flux -ErrorAction SilentlyContinue | Select-Object -First 1
    $core = Get-Process -Name mihomo -ErrorAction SilentlyContinue | Select-Object -First 1
    if ($flux) {
        $corePid = if ($core) { $core.Id } else { '' }
        $coreWs = if ($core) { [math]::Round($core.WorkingSet64 / 1MB, 1) } else { '' }
        $line = "{0},{1},{2},{3},{4},{5}" -f (Get-Date).ToString('yyyy-MM-dd HH:mm:ss'), $flux.Id, [math]::Round($flux.WorkingSet64 / 1MB, 1), [math]::Round($flux.PrivateMemorySize64 / 1MB, 1), $corePid, $coreWs
        $line | Out-File $csv -Append -Encoding utf8
        $samples++
    } else {
        Write-Host 'Flux process not present (may have been closed by the user); waiting...'
    }
    Start-Sleep -Seconds $IntervalSeconds
}

# Summary: first 20 samples vs last 20 samples average
$rows = Import-Csv $csv | ForEach-Object {
    [pscustomobject]@{
        Time = [datetime]$_.timestamp
        WS = [double]$_.'flux_ws_mb'
        Private = [double]$_.'flux_private_mb'
    }
}
if ($rows.Count -ge 2) {
    $first = $rows | Select-Object -First 20
    $last = $rows | Select-Object -Last 20
    $firstWs = ($first | Measure-Object WS -Average).Average
    $lastWs = ($last | Measure-Object WS -Average).Average
    $firstPriv = ($first | Measure-Object Private -Average).Average
    $lastPriv = ($last | Measure-Object Private -Average).Average
    $growthWs = [math]::Round($lastWs - $firstWs, 1)
    $growthPriv = [math]::Round($lastPriv - $firstPriv, 1)
    Write-Host "Samples: $samples"
    Write-Host ("Working set: first-avg {0} MB -> last-avg {1} MB (growth {2} MB)" -f $firstWs, $lastWs, $growthWs)
    Write-Host ("Private memory: first-avg {0} MB -> last-avg {1} MB (growth {2} MB)" -f $firstPriv, $lastPriv, $growthPriv)
    if ($growthPriv -lt 50) { Write-Host 'VERDICT: no sustained memory growth [OK]' -ForegroundColor Green }
    else { Write-Host 'VERDICT: private memory grew more than 50 MB; manual review needed [WARN]' -ForegroundColor Yellow }
} else {
    Write-Host "Not enough samples ($samples)"
}
