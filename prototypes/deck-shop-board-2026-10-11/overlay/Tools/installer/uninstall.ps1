# Removes the per-user Infinite Conquest install (files, shortcuts, Apps entry). Saved settings are kept
# in the registry under HKCU\Software\DefaultCompany so a reinstall remembers them.
Add-Type -AssemblyName System.Windows.Forms
# Distinct names so the playtest never touches the 2D alpha's 'Infinite Conquest' Start menu folder.
$name = 'Infinite Conquest 3D Playtest'
$dest = Join-Path $env:LOCALAPPDATA "Programs\Infinite Conquest 3D"
$programs = [Environment]::GetFolderPath('Programs'); $desktop = [Environment]::GetFolderPath('Desktop'); $appKey = 'InfiniteConquestPlaytest'
# A test install (install.ps1 with IC_TEST_ROOT) records its root next to this script and removes only that.
$marker = Join-Path $PSScriptRoot 'test-install-root.txt'
if (Test-Path $marker) {
    $test = (Get-Content $marker -Raw).Trim()
    $dest = $PSScriptRoot; $programs = Join-Path $test 'StartMenu'; $desktop = Join-Path $test 'Desktop'; $appKey = 'InfiniteConquestPlaytest-Test'
}
if ($env:IC_SILENT -ne '1') {
    $r = [System.Windows.Forms.MessageBox]::Show("Remove $name from this PC?", "Uninstall $name", 'YesNo', 'Question')
    if ($r -ne 'Yes') { exit 0 }
}
Get-Process InfiniteConquestPlaytest -ErrorAction SilentlyContinue | Where-Object { $_.Path -and $_.Path.StartsWith($dest, [StringComparison]::OrdinalIgnoreCase) } | Stop-Process -Force
Remove-Item (Join-Path $programs $name) -Recurse -Force -ErrorAction SilentlyContinue
Remove-Item (Join-Path $desktop "$name.lnk") -Force -ErrorAction SilentlyContinue
Remove-Item "HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\$appKey" -Recurse -Force -ErrorAction SilentlyContinue
# The script lives inside $dest, so delete the folder from a detached process after this one exits.
Start-Process cmd.exe -ArgumentList "/c timeout /t 2 /nobreak >nul & rmdir /s /q `"$dest`"" -WindowStyle Hidden
if ($env:IC_SILENT -ne '1') { [System.Windows.Forms.MessageBox]::Show("$name was removed.", "Uninstall $name", 'OK', 'Information') | Out-Null }
