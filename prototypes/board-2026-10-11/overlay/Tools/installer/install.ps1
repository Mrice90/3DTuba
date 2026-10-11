# Infinite Conquest playtest installer (run by InfiniteConquest-Setup.exe, built with Windows IExpress).
# Per-user install: no admin rights. Installs to %LOCALAPPDATA%\Programs\Infinite Conquest 3D, adds Start menu and
# desktop shortcuts, and registers an uninstaller under Settings > Apps.
# AI-110: the Start menu folder also gets board-preview shortcuts (-board 6x8, -board hex).
# IC_TEST_ROOT=<dir> installs everything (files, Start menu, desktop, Apps entry 'InfiniteConquestPlaytest-Test')
# under that folder instead, so a test install never touches the real one.
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Windows.Forms
$here = Split-Path -Parent $MyInvocation.MyCommand.Path
# Distinct names so the playtest never touches the 2D alpha's 'Infinite Conquest' Start menu folder.
$name = 'Infinite Conquest 3D Playtest'
$test = $env:IC_TEST_ROOT
$dest = if ($test) { Join-Path $test 'Infinite Conquest 3D' } else { Join-Path $env:LOCALAPPDATA "Programs\Infinite Conquest 3D" }
$programs = if ($test) { Join-Path $test 'StartMenu' } else { [Environment]::GetFolderPath('Programs') }
$desktop = if ($test) { Join-Path $test 'Desktop' } else { [Environment]::GetFolderPath('Desktop') }
$appKey = if ($test) { 'InfiniteConquestPlaytest-Test' } else { 'InfiniteConquestPlaytest' }
$exe = Join-Path $dest 'InfiniteConquestPlaytest.exe'
$version = (Get-Content (Join-Path $here 'version.txt') -Raw).Trim()

try {
    # Only stop a copy running from this install folder.
    Get-Process InfiniteConquestPlaytest -ErrorAction SilentlyContinue | Where-Object { $_.Path -and $_.Path.StartsWith($dest, [StringComparison]::OrdinalIgnoreCase) } | Stop-Process -Force
    if (Test-Path $dest) { Remove-Item $dest -Recurse -Force }
    New-Item -ItemType Directory -Force $dest, $desktop | Out-Null
    Expand-Archive -Path (Join-Path $here 'payload.zip') -DestinationPath $dest -Force
    if ($test) { Set-Content (Join-Path $dest 'test-install-root.txt') $test -Encoding utf8 }

    $shell = New-Object -ComObject WScript.Shell
    $startDir = Join-Path $programs $name
    New-Item -ItemType Directory -Force $startDir | Out-Null
    $links = @(
        @{ Path = (Join-Path $startDir "$name.lnk"); Args = ''; Desc = 'Infinite Conquest playtest' },
        @{ Path = (Join-Path $desktop "$name.lnk"); Args = ''; Desc = 'Infinite Conquest playtest' },
        @{ Path = (Join-Path $startDir "Infinite Conquest (6x8 board preview).lnk"); Args = '-board 6x8'; Desc = 'Planned 6 x 8 board drawn around the engine board (centre 24 hexes playable)' },
        @{ Path = (Join-Path $startDir "Infinite Conquest (hexagon board preview).lnk"); Args = '-board hex'; Desc = '37-hex hexagon board drawn around the engine board (24 hexes playable)' })
    foreach ($l in $links) {
        $lnk = $shell.CreateShortcut($l.Path)
        $lnk.TargetPath = $exe; $lnk.Arguments = $l.Args; $lnk.WorkingDirectory = $dest; $lnk.IconLocation = "$exe,0"
        $lnk.Description = $l.Desc
        $lnk.Save()
    }
    $un = $shell.CreateShortcut((Join-Path $startDir "Uninstall $name.lnk"))
    $un.TargetPath = 'powershell.exe'
    $un.Arguments = "-NoProfile -ExecutionPolicy Bypass -File `"$dest\uninstall.ps1`""
    $un.Save()

    $key = "HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\$appKey"
    New-Item -Path $key -Force | Out-Null
    $size = [int]((Get-ChildItem $dest -Recurse -File | Measure-Object Length -Sum).Sum / 1KB)
    $props = @{
        DisplayName = $(if ($test) { "$name (test install)" } else { $name }); DisplayVersion = $version; Publisher = 'Infinite Conquest team'
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
