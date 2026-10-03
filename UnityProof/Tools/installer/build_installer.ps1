# Packages a built playtest folder (exe + Data + Bridge with jre) into InfiniteConquest-Setup.exe using
# Windows' built-in IExpress. usage: build_installer.ps1 -BuildDir <dir> -OutDir <dir> -Version <x.y.z>
param([Parameter(Mandatory)][string]$BuildDir, [Parameter(Mandatory)][string]$OutDir, [string]$Version = '0.1.0')
$ErrorActionPreference = 'Stop'
$here = Split-Path -Parent $MyInvocation.MyCommand.Path
$BuildDir = (Resolve-Path $BuildDir).Path
New-Item -ItemType Directory -Force $OutDir | Out-Null
$OutDir = (Resolve-Path $OutDir).Path
$stage = Join-Path $env:TEMP "ic-installer-$([guid]::NewGuid().ToString('N'))"
$payloadSrc = Join-Path $stage 'game'
New-Item -ItemType Directory -Force $payloadSrc | Out-Null

foreach ($need in 'InfiniteConquestPlaytest.exe', 'InfiniteConquestPlaytest_Data', 'Bridge\jre\bin\java.exe', 'Bridge\classes\RulesBridge.class') {
    if (-not (Test-Path (Join-Path $BuildDir $need))) { throw "Build is missing $need" }
}
# Ship the game, not the dev leftovers (Unity's debug-symbol backup folder, screenshots, smoke logs).
Get-ChildItem $BuildDir | Where-Object {
    $_.Name -notlike '*_BackUpThisFolder_ButDontShipItWithYourGame' -and $_.Name -notmatch '\.(png|log|jsonl)$' -and
    $_.Name -notin @('playtest-smoke.json', 'PLAY-INFINITE-CONQUEST.bat')
} | Copy-Item -Destination $payloadSrc -Recurse
Copy-Item (Join-Path $here 'uninstall.ps1') $payloadSrc

$zip = Join-Path $stage 'payload.zip'
Add-Type -AssemblyName System.IO.Compression.FileSystem
[IO.Compression.ZipFile]::CreateFromDirectory($payloadSrc, $zip, [IO.Compression.CompressionLevel]::Fastest, $false)
Copy-Item (Join-Path $here 'install.ps1') $stage
Set-Content (Join-Path $stage 'version.txt') $Version -Encoding ascii

$target = Join-Path $OutDir 'InfiniteConquest-Setup.exe'
$sed = @"
[Version]
Class=IEXPRESS
SEDVersion=3
[Options]
PackagePurpose=InstallApp
ShowInstallProgramWindow=1
HideExtractAnimation=0
UseLongFileName=1
InsideCompressed=0
CAB_FixedSize=0
CAB_ResvCodeSigning=0
RebootMode=N
InstallPrompt=
DisplayLicense=
FinishMessage=
TargetName=$target
FriendlyName=Infinite Conquest Setup
AppLaunched=cmd /c powershell.exe -NoProfile -ExecutionPolicy Bypass -WindowStyle Hidden -File install.ps1
PostInstallCmd=<None>
AdminQuietInstCmd=
UserQuietInstCmd=
SourceFiles=SourceFiles
[Strings]
FILE0="install.ps1"
FILE1="payload.zip"
FILE2="version.txt"
[SourceFiles]
SourceFiles0=$stage\
[SourceFiles0]
%FILE0%=
%FILE1%=
%FILE2%=
"@
$sedPath = Join-Path $stage 'setup.sed'
Set-Content $sedPath $sed -Encoding ascii
$p = Start-Process "$env:WINDIR\System32\iexpress.exe" -ArgumentList '/N', '/Q', $sedPath -Wait -PassThru
if (-not (Test-Path $target)) { throw "IExpress failed (exit $($p.ExitCode))" }
Remove-Item $stage -Recurse -Force
"INSTALLER $target $([math]::Round((Get-Item $target).Length / 1MB)) MB"
