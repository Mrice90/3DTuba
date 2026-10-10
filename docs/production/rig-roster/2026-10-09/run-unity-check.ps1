# AI-060-RIG-ROSTER: isolated Unity import check. Run from playtest\rig-roster-2026-10-09\ in PowerShell.
# Creates its own throwaway project (unity-check\); never touches UnityProof or any other project.
$ErrorActionPreference = "Stop"
$here   = Split-Path -Parent $MyInvocation.MyCommand.Path
$unity  = "C:\Program Files\Unity\Hub\Editor\6000.6.3f1\Editor\Unity.exe"
if (-not (Test-Path $unity)) { $unity = (Get-ChildItem "C:\Program Files\Unity\Hub\Editor\*\Editor\Unity.exe" | Select-Object -Last 1).FullName }
$proj   = Join-Path $here "unity-check"
if (-not (Test-Path (Join-Path $proj "Assets"))) {
  & $unity -batchmode -quit -createProject $proj -logFile (Join-Path $here "unity-check-create.log") | Out-Null
}
New-Item -ItemType Directory -Force (Join-Path $proj "Assets\RigCheck"), (Join-Path $proj "Assets\Editor") | Out-Null
Copy-Item (Join-Path $here "unity-check-kit\Assets\Editor\RigCheck.cs") (Join-Path $proj "Assets\Editor\") -Force
# The Keraunos pilot is re-exported here with fixed texture names; fall back to the pilot folder copy
if (-not (Test-Path (Join-Path $here "zeus_keraunos_prime_rigged.fbx"))) { Copy-Item (Join-Path $here "..\rig-pilot-2026-10-09\zeus_keraunos_prime_rigged.fbx") (Join-Path $proj "Assets\RigCheck\") -Force }
Get-ChildItem $here -Filter "*_rigged.fbx" | Copy-Item -Destination (Join-Path $proj "Assets\RigCheck\") -Force
& $unity -batchmode -projectPath $proj -executeMethod RigCheck.Run -logFile (Join-Path $here "unity-check.log") -quit | Out-Null
Select-String -Path (Join-Path $here "unity-check.log") -Pattern "RIGCHECK" | ForEach-Object { $_.Line }
Get-Content (Join-Path $here "unity-check-report.json")
