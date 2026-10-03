# Infinite Conquest playtest installer (run by InfiniteConquest-Setup.exe, built with Windows IExpress).
# Per-user install: no admin rights. Installs to %LOCALAPPDATA%\Programs\Infinite Conquest, adds Start menu and
# desktop shortcuts, and registers an uninstaller under Settings > Apps.
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Windows.Forms
$here = Split-Path -Parent $MyInvocation.MyCommand.Path
$name = 'Infinite Conquest'
$dest = Join-Path $env:LOCALAPPDATA "Programs\$name"
$exe = Join-Path $dest 'InfiniteConquestPlaytest.exe'
$version = (Get-Content (Join-Path $here 'version.txt') -Raw).Trim()

try {
    Get-Process InfiniteConquestPlaytest -ErrorAction SilentlyContinue | Stop-Process -Force
    if (Test-Path $dest) { Remove-Item $dest -Recurse -Force }
    New-Item -ItemType Directory -Force $dest | Out-Null
    Expand-Archive -Path (Join-Path $here 'payload.zip') -DestinationPath $dest -Force

    $shell = New-Object -ComObject WScript.Shell
    $startDir = Join-Path ([Environment]::GetFolderPath('Programs')) $name
    New-Item -ItemType Directory -Force $startDir | Out-Null
    foreach ($lnkPath in @((Join-Path $startDir "$name.lnk"), (Join-Path ([Environment]::GetFolderPath('Desktop')) "$name.lnk"))) {
        $lnk = $shell.CreateShortcut($lnkPath)
        $lnk.TargetPath = $exe; $lnk.WorkingDirectory = $dest; $lnk.IconLocation = "$exe,0"
        $lnk.Description = 'Infinite Conquest playtest'
        $lnk.Save()
    }
    $un = $shell.CreateShortcut((Join-Path $startDir "Uninstall $name.lnk"))
    $un.TargetPath = 'powershell.exe'
    $un.Arguments = "-NoProfile -ExecutionPolicy Bypass -File `"$dest\uninstall.ps1`""
    $un.Save()

    $key = "HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\InfiniteConquestPlaytest"
    New-Item -Path $key -Force | Out-Null
    $size = [int]((Get-ChildItem $dest -Recurse -File | Measure-Object Length -Sum).Sum / 1KB)
    $props = @{
        DisplayName = $name; DisplayVersion = $version; Publisher = 'Infinite Conquest team'
        InstallLocation = $dest; DisplayIcon = "$exe,0"; EstimatedSize = $size; NoModify = 1; NoRepair = 1
        UninstallString = "powershell.exe -NoProfile -ExecutionPolicy Bypass -File `"$dest\uninstall.ps1`""
    }
    foreach ($k in $props.Keys) {
        $type = if ($props[$k] -is [int]) { 'DWord' } else { 'String' }
        New-ItemProperty -Path $key -Name $k -Value $props[$k] -PropertyType $type -Force | Out-Null
    }

    if ($env:IC_SILENT -ne '1') {
        $r = [System.Windows.Forms.MessageBox]::Show("$name $version is installed.`n`nPlay now?", "$name setup", 'YesNo', 'Information')
        if ($r -eq 'Yes') { Start-Process $exe -WorkingDirectory $dest }
    }
} catch {
    if ($env:IC_SILENT -ne '1') { [System.Windows.Forms.MessageBox]::Show("Setup failed: $($_.Exception.Message)", "$name setup", 'OK', 'Error') | Out-Null }
    exit 1
}
