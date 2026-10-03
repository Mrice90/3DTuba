# Removes the per-user Infinite Conquest install (files, shortcuts, Apps entry). Saved settings are kept
# in the registry under HKCU\Software\DefaultCompany so a reinstall remembers them.
Add-Type -AssemblyName System.Windows.Forms
$name = 'Infinite Conquest'
$dest = Join-Path $env:LOCALAPPDATA "Programs\$name"
if ($env:IC_SILENT -ne '1') {
    $r = [System.Windows.Forms.MessageBox]::Show("Remove $name from this PC?", "Uninstall $name", 'YesNo', 'Question')
    if ($r -ne 'Yes') { exit 0 }
}
Get-Process InfiniteConquestPlaytest -ErrorAction SilentlyContinue | Stop-Process -Force
Remove-Item (Join-Path ([Environment]::GetFolderPath('Programs')) $name) -Recurse -Force -ErrorAction SilentlyContinue
Remove-Item (Join-Path ([Environment]::GetFolderPath('Desktop')) "$name.lnk") -Force -ErrorAction SilentlyContinue
Remove-Item 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\InfiniteConquestPlaytest' -Recurse -Force -ErrorAction SilentlyContinue
# The script lives inside $dest, so delete the folder from a detached process after this one exits.
Start-Process cmd.exe -ArgumentList "/c timeout /t 2 /nobreak >nul & rmdir /s /q `"$dest`"" -WindowStyle Hidden
if ($env:IC_SILENT -ne '1') { [System.Windows.Forms.MessageBox]::Show("$name was removed.", "Uninstall $name", 'OK', 'Information') | Out-Null }
