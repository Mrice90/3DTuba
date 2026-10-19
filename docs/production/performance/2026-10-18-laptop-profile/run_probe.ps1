# AI-209: launch a probe build windowed at its default settings, record wall-clock load time and peak memory,
# and collect PerfProbe's JSON. Usage: run_probe.ps1 -Exe <path> -Out <json> [-Seconds 30] [-Extra "..."]
param([string]$Exe, [string]$Out, [int]$Seconds = 30, [string]$Extra = "")
$ErrorActionPreference = "Stop"
if (Test-Path $Out) { Remove-Item $Out }
$log = [IO.Path]::ChangeExtension($Out, ".player.log")
$argList = "-playback -perfProbe -perfSeconds $Seconds -perfResult `"$Out`" -logFile `"$log`" $Extra"
$sw = [Diagnostics.Stopwatch]::StartNew()
$p = Start-Process -FilePath $Exe -ArgumentList $argList -WorkingDirectory (Split-Path $Exe) -PassThru
$peakWs = 0; $peakPriv = 0; $samples = @()
while (-not $p.HasExited) {
    $p.Refresh()
    try { $ws = $p.WorkingSet64; $pv = $p.PrivateMemorySize64 } catch { $ws = 0; $pv = 0 }
    if ($ws -gt $peakWs) { $peakWs = $ws }; if ($pv -gt $peakPriv) { $peakPriv = $pv }
    $samples += [pscustomobject]@{ t = [math]::Round($sw.Elapsed.TotalSeconds, 1); wsMb = [math]::Round($ws / 1MB) }
    Start-Sleep -Milliseconds 500
    if ($sw.Elapsed.TotalSeconds -gt ($Seconds * 3 + 240)) { Stop-Process -Id $p.Id -Force; throw "probe timed out" }
}
$wall = $sw.Elapsed.TotalSeconds
$probe = if (Test-Path $Out) { Get-Content $Out -Raw | ConvertFrom-Json } else { $null }
$summary = [pscustomobject]@{
    exe = $Exe; exitCode = $p.ExitCode; wallSeconds = [math]::Round($wall, 1)
    peakWorkingSetMb = [math]::Round($peakWs / 1MB); peakPrivateMb = [math]::Round($peakPriv / 1MB)
    memoryTimeline = $samples | Where-Object { $_.t % 5 -lt 0.5 }
    probe = $probe
}
$summary | ConvertTo-Json -Depth 8 | Set-Content -Encoding utf8 ([IO.Path]::ChangeExtension($Out, ".summary.json"))
"exit=$($p.ExitCode) wall=$([math]::Round($wall,1))s peakWS=$([math]::Round($peakWs/1MB))MB peakPrivate=$([math]::Round($peakPriv/1MB))MB"
