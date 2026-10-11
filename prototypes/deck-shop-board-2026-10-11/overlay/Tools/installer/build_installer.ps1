# Packages a built playtest folder (exe + Data with the native C# rules) into InfiniteConquest-Setup.exe using
# Windows' built-in IExpress. usage: build_installer.ps1 -BuildDir <dir> -OutDir <dir> -Version <x.y.z>
# The rules run in-process (InfiniteConquest.RulesCore/Native.dll), so no Bridge, Java runtime or JAR is shipped.
param([Parameter(Mandatory)][string]$BuildDir, [Parameter(Mandatory)][string]$OutDir, [string]$Version = '0.1.0')
$ErrorActionPreference = 'Stop'
$here = Split-Path -Parent $MyInvocation.MyCommand.Path
$BuildDir = (Resolve-Path $BuildDir).Path
New-Item -ItemType Directory -Force $OutDir | Out-Null
$OutDir = (Resolve-Path $OutDir).Path
$stage = Join-Path $env:TEMP "ic-installer-$([guid]::NewGuid().ToString('N'))"
$payloadSrc = Join-Path $stage 'game'
New-Item -ItemType Directory -Force $payloadSrc | Out-Null

foreach ($need in 'InfiniteConquestPlaytest.exe', 'InfiniteConquestPlaytest_Data',
                 'InfiniteConquestPlaytest_Data\Managed\InfiniteConquest.RulesCore.dll',
                 'InfiniteConquestPlaytest_Data\Managed\InfiniteConquest.Native.dll',
                 'InfiniteConquestPlaytest_Data\StreamingAssets\NativeRules\structure-slots.properties') {
    if (-not (Test-Path (Join-Path $BuildDir $need))) { throw "Build is missing $need (needs a native C# rules build)" }
}
# Refuse a Java-bridge build: the installer ships the native build only.
$java = @(Get-ChildItem $BuildDir -Recurse -Force | Where-Object {
    ($_.PSIsContainer -and $_.Name -eq 'Bridge') -or $_.Extension -in '.jar', '.class' -or $_.Name -like 'java*.exe' -or $_.Name -eq 'jvm.dll'
})
if ($java.Count) { throw "Build contains Java bridge files ($($java[0].FullName)); use the native build" }
# Ship the game, not the dev leftovers (Unity's debug-symbol backup folder, screenshots, smoke logs).
Get-ChildItem $BuildDir | Where-Object {
    $_.Name -notlike '*_BackUpThisFolder_ButDontShipItWithYourGame' -and $_.Name -notmatch '\.(png|log|jsonl)$' -and
    $_.Name -notin @('playtest-smoke.json', 'PLAY-INFINITE-CONQUEST.bat', 'PLAY-INFINITE-CONQUEST.cmd', 'checks',
                     'PLAY-BOARD-6x8-PREVIEW.cmd', 'PLAY-BOARD-HEX-PREVIEW.cmd',
                     'audio-build-validation.json', 'SHA256SUMS.txt', 'README.md')
} | Copy-Item -Destination $payloadSrc -Recurse
Copy-Item (Join-Path $here 'uninstall.ps1') $payloadSrc

$zip = Join-Path $stage 'payload.zip'
Add-Type -AssemblyName System.IO.Compression.FileSystem
[IO.Compression.ZipFile]::CreateFromDirectory($payloadSrc, $zip, [IO.Compression.CompressionLevel]::Fastest, $false)
Copy-Item (Join-Path $here 'install.ps1'), (Join-Path $here 'install.cmd') $stage
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
AppLaunched=install.cmd
PostInstallCmd=<None>
AdminQuietInstCmd=
UserQuietInstCmd=
SourceFiles=SourceFiles
[Strings]
FILE0="install.ps1"
FILE1="payload.zip"
FILE2="version.txt"
FILE3="install.cmd"
[SourceFiles]
SourceFiles0=$stage\
[SourceFiles0]
%FILE0%=
%FILE1%=
%FILE2%=
%FILE3%=
"@
$sedPath = Join-Path $stage 'setup.sed'
Set-Content $sedPath $sed -Encoding ascii
$p = Start-Process "$env:WINDIR\System32\iexpress.exe" -ArgumentList '/N', '/Q', $sedPath -WindowStyle Hidden -Wait -PassThru
if (-not (Test-Path $target)) { throw "IExpress failed (exit $($p.ExitCode))" }
$resolvedStage = [IO.Path]::GetFullPath($stage)
$resolvedTemp = [IO.Path]::GetFullPath($env:TEMP).TrimEnd([IO.Path]::DirectorySeparatorChar) + [IO.Path]::DirectorySeparatorChar
if (-not $resolvedStage.StartsWith($resolvedTemp, [StringComparison]::OrdinalIgnoreCase)) { throw "Unexpected staging cleanup path" }
Remove-Item -LiteralPath $resolvedStage -Recurse -Force
"INSTALLER $target $([math]::Round((Get-Item $target).Length / 1MB)) MB"

