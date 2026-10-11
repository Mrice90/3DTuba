# Test install of playtest/installer-board-2026-10-11 into an isolated folder (IC_TEST_ROOT), so the real install in
# %LOCALAPPDATA%\Programs\Infinite Conquest 3D, its shortcuts and Apps entry are never touched. Then: launch each
# Start menu shortcut (normal, 6x8 preview, hexagon preview) with Java off PATH and JAVA_HOME unset, capture the main
# menu, check no Java process started, close; finally run the test install's uninstaller and confirm it is gone.
$ErrorActionPreference = 'Stop'
$ic = "C:\Users\mattj\OneDrive\Documents\AI Dev\Infinite Conquest"
$setup = "$ic\playtest\installer-board-2026-10-11\InfiniteConquest-Setup.exe"
$out = "$ic\reviews\board-2026-10-11"
$test = Join-Path $env:TEMP "ic-board-installer-test"
if (Test-Path $test) { Remove-Item $test -Recurse -Force }
New-Item -ItemType Directory -Force $test | Out-Null
$realKey = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\InfiniteConquestPlaytest'
$realBefore = (Get-ItemProperty $realKey -ErrorAction SilentlyContinue).DisplayVersion
$realExe = Join-Path $env:LOCALAPPDATA "Programs\Infinite Conquest 3D\InfiniteConquestPlaytest.exe"
$realHashBefore = if (Test-Path $realExe) { (Get-FileHash $realExe).Hash } else { $null }

$env:IC_TEST_ROOT = $test; $env:IC_SILENT = '1'
$p = Start-Process $setup -Wait -PassThru
"setup exit $($p.ExitCode)"
$dest = Join-Path $test 'Infinite Conquest 3D'
$start = Join-Path $test 'StartMenu\Infinite Conquest 3D Playtest'
"installed exe: $(Test-Path "$dest\InfiniteConquestPlaytest.exe"); shortcuts: $((Get-ChildItem $start -Filter *.lnk | ForEach-Object Name) -join ' | ')"
"desktop shortcut: $(Test-Path (Join-Path $test 'Desktop\Infinite Conquest 3D Playtest.lnk'))"
"test Apps entry: $((Get-ItemProperty 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\InfiniteConquestPlaytest-Test').DisplayName)"
$javaFiles = @(Get-ChildItem $dest -Recurse -Force | Where-Object { ($_.PSIsContainer -and $_.Name -eq 'Bridge') -or $_.Extension -in '.jar', '.class' -or $_.Name -like 'java*.exe' -or $_.Name -eq 'jvm.dll' })
"java files in install: $($javaFiles.Count)"
foreach ($dll in 'Assembly-CSharp.dll', 'InfiniteConquest.RulesCore.dll', 'InfiniteConquest.Native.dll') {
    $a = (Get-FileHash "$dest\InfiniteConquestPlaytest_Data\Managed\$dll").Hash; $b = (Get-FileHash "$ic\playtest\unity-build-board-2026-10-11\InfiniteConquestPlaytest_Data\Managed\$dll").Hash
    "$dll equals package: $($a -eq $b) ($($a.Substring(0,12).ToLower()))"
}
Remove-Item Env:IC_TEST_ROOT; Remove-Item Env:IC_SILENT

[Environment]::SetEnvironmentVariable('JAVA_HOME', $null, 'Process'); [Environment]::SetEnvironmentVariable('IC_BRIDGE_CMD', $null, 'Process')
$env:PATH = ($env:PATH -split ';' | Where-Object { $_ -notmatch '(?i)java|jdk|jre|temurin|adoptium' }) -join ';'
"java on PATH: $([bool](Get-Command java -ErrorAction SilentlyContinue)); JAVA_HOME set: $([bool]$env:JAVA_HOME)"
Add-Type -AssemblyName System.Drawing
Add-Type @"
using System; using System.Runtime.InteropServices;
public static class W2 { [StructLayout(LayoutKind.Sequential)] public struct R { public int L, T, Ri, B; }
  [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out R r);
  [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr h);
  [DllImport("user32.dll")] public static extern bool PrintWindow(IntPtr h, IntPtr hdc, uint flags); }
"@
$all = $true
foreach ($s in @(@{ Lnk = 'Infinite Conquest 3D Playtest.lnk'; Shot = 'installed-menu.png' },
                 @{ Lnk = 'Infinite Conquest (6x8 board preview).lnk'; Shot = 'installed-menu-6x8.png' },
                 @{ Lnk = 'Infinite Conquest (hexagon board preview).lnk'; Shot = 'installed-menu-hex.png' })) {
    $javaBefore = @(Get-Process java, javaw -ErrorAction SilentlyContinue | ForEach-Object Id)
    $launched = Get-Date; Start-Process -FilePath (Join-Path $start $s.Lnk)
    $proc = $null
    for ($i = 0; $i -lt 60 -and $proc -eq $null; $i++) { Start-Sleep -Seconds 1; $proc = Get-Process InfiniteConquestPlaytest -ErrorAction SilentlyContinue | Where-Object { $_.MainWindowHandle -ne 0 -and $_.Path -eq "$dest\InfiniteConquestPlaytest.exe" -and $_.StartTime -ge $launched } | Select-Object -First 1 }
    if ($proc -eq $null) { "$($s.Lnk): NO_WINDOW"; $all = $false; continue }
    $cmdline = (Get-CimInstance Win32_Process -Filter "ProcessId=$($proc.Id)").CommandLine
    Start-Sleep -Seconds 15; $proc.Refresh()
    [W2]::SetForegroundWindow($proc.MainWindowHandle) | Out-Null; Start-Sleep -Seconds 2
    $r = New-Object W2+R; [W2]::GetWindowRect($proc.MainWindowHandle, [ref]$r) | Out-Null
    $w = $r.Ri - $r.L; $h = $r.B - $r.T
    $bmp = New-Object System.Drawing.Bitmap $w, $h
    $g = [System.Drawing.Graphics]::FromImage($bmp); $hdc = $g.GetHdc(); $ok = [W2]::PrintWindow($proc.MainWindowHandle, $hdc, 2); $g.ReleaseHdc($hdc); $g.Dispose()
    $bmp.Save((Join-Path $out $s.Shot), [System.Drawing.Imaging.ImageFormat]::Png); $bmp.Dispose()
    $javaNew = @(Get-Process java, javaw -ErrorAction SilentlyContinue | Where-Object { $_.Id -notin $javaBefore })
    "$($s.Lnk): window $w x $h, responding=$($proc.Responding), printwindow=$ok, new java processes=$($javaNew.Count), command line: $cmdline"
    if (-not $proc.Responding -or $javaNew.Count) { $all = $false }
    $proc.CloseMainWindow() | Out-Null; Start-Sleep -Seconds 3
    if (!$proc.HasExited) { Stop-Process -Id $proc.Id -Force }
}

$env:IC_SILENT = '1'
& powershell.exe -NoProfile -ExecutionPolicy Bypass -File "$dest\uninstall.ps1"
Remove-Item Env:IC_SILENT
Start-Sleep -Seconds 5
"after uninstall: files=$(Test-Path $dest) startmenu=$(Test-Path $start) desktop=$(Test-Path (Join-Path $test 'Desktop\Infinite Conquest 3D Playtest.lnk')) testAppsEntry=$(Test-Path 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\InfiniteConquestPlaytest-Test')"
$realAfter = (Get-ItemProperty $realKey -ErrorAction SilentlyContinue).DisplayVersion
$realHashAfter = if (Test-Path $realExe) { (Get-FileHash $realExe).Hash } else { $null }
"real install untouched: Apps version $realBefore -> $realAfter, exe hash same: $($realHashBefore -eq $realHashAfter)"
"ALL_LAUNCHES_OK=$all"
