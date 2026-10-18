$ErrorActionPreference='Stop'
$sdk='C:/Program Files/Unity/Hub/Editor/6000.6.3f1/Editor/Data/DotNetSdk/dotnet.exe'
$taskPriorEnv=@{}
foreach($taskEnvName in @('DOTNET_CLI_HOME','DOTNET_CLI_TELEMETRY_OPTOUT','DOTNET_SKIP_FIRST_TIME_EXPERIENCE','APPDATA')) {
    $taskPriorEnv[$taskEnvName]=[Environment]::GetEnvironmentVariable($taskEnvName,'Process')
}
try {
$env:DOTNET_CLI_HOME="$PSScriptRoot/.dotnet-home"
$env:DOTNET_CLI_TELEMETRY_OPTOUT='1'
$env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE='1'
$env:APPDATA="$PSScriptRoot/.appdata"
New-Item -ItemType Directory -Force "$env:APPDATA/NuGet" | Out-Null
Copy-Item "$PSScriptRoot/NuGet.Config" "$env:APPDATA/NuGet/NuGet.Config" -Force
$data='C:/Program Files/Unity/Hub/Editor/6000.6.3f1/Editor/Data'
foreach($proj in @('source/rules-core/rules-core.csproj','source/adapter/NativeRules.csproj')) {
    & $sdk restore "$PSScriptRoot/$proj" --configfile "$PSScriptRoot/NuGet.Config"
    if($LASTEXITCODE -ne 0){throw 'Restore failed'}
    & $sdk build "$PSScriptRoot/$proj" --no-restore "-p:UnityEditorData=$data" "-p:RulesCoreDll=$PSScriptRoot/source/rules-core/bin/Debug/netstandard2.1/InfiniteConquest.RulesCore.dll" -p:UseSharedCompilation=false -warnaserror
    if($LASTEXITCODE -ne 0){throw 'Build failed'}
}
} finally {
    foreach($taskEnvName in $taskPriorEnv.Keys) {[Environment]::SetEnvironmentVariable($taskEnvName,$taskPriorEnv[$taskEnvName],'Process')}
}
