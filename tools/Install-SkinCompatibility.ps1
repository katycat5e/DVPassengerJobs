param(
    [string]$DvInstallDir = 'W:\SteamLibrary\steamapps\common\Derail Valley',
    [string]$CecilPath = 'C:\Users\idk Ma\.nuget\packages\fody\6.8.0\netstandardtask\Mono.Cecil.dll'
)
$ErrorActionPreference = 'Stop'
if (Get-Process DerailValley -ErrorAction SilentlyContinue) { throw 'Exit Derail Valley before installing.' }
$repo = Split-Path $PSScriptRoot -Parent
& dotnet build "$PSScriptRoot\BuildSkinCompatibility.csproj" -c Release --ignore-failed-sources "-p:DvInstallDir=$DvInstallDir"
if ($LASTEXITCODE) { throw 'Adapter build failed.' }
& dotnet build "$PSScriptRoot\SkinCompatibilityTests.csproj" -c Release --ignore-failed-sources
if ($LASTEXITCODE) { throw 'Test build failed.' }
$adapter = "$PSScriptRoot\bin\Release\net48\PassengerJobs.Skins.dll"
& "$PSScriptRoot\bin\tests\net48\SkinCompatibilityTests.exe" $DvInstallDir $adapter
if ($LASTEXITCODE) { throw 'Compatibility checks failed.' }
Add-Type -Path $CecilPath
$modDir = Join-Path $DvInstallDir 'Mods\PassengerJobs'
$corePath = Join-Path $modDir 'PassengerJobs.dll'
$resolver = [Mono.Cecil.DefaultAssemblyResolver]::new()
$resolver.AddSearchDirectory((Join-Path $DvInstallDir 'DerailValley_Data\Managed'))
$resolver.AddSearchDirectory((Join-Path $DvInstallDir 'DerailValley_Data\Managed\UnityModManager'))
Get-ChildItem (Join-Path $DvInstallDir 'Mods') -Directory | ForEach-Object { $resolver.AddSearchDirectory($_.FullName) }
$parameters = [Mono.Cecil.ReaderParameters]::new()
$parameters.AssemblyResolver = $resolver
$assembly = [Mono.Cecil.AssemblyDefinition]::ReadAssembly($corePath, $parameters)
$module = $assembly.MainModule
$main = $module.Types | Where-Object FullName -eq 'PassengerJobs.PJMain'
$method = $main.Methods | Where-Object Name -eq 'TryLoadSkinManager'
if (!$method) { throw 'Unsupported Passenger Jobs: missing integration loader.' }
$alreadyPatched = @($method.Body.Instructions | Where-Object { $_.Operand -eq 'PassengerJobs.Skins.PassengerSkinChooser' }).Count -gt 0
if (!$alreadyPatched) {
    # Keep the installed main DLL version and all other methods intact. Only replace this
    # loader with the Initialize call used by the source fix in PJMain.cs.
    $findMod = ($method.Body.Instructions | Where-Object { $_.OpCode.Code -eq 'Call' -and $_.Operand -like '*::FindMod(System.String)' }).Operand
    $active = ($method.Body.Instructions | Where-Object { $_.OpCode.Code -eq 'Call' -and $_.Operand -like '*::get_Active()' }).Operand
    $bootstrap = $module.Types | Where-Object FullName -eq 'PassengerJobs.Bootstrapper'
    $initialize = $bootstrap.Methods | Where-Object Name -eq 'TryLoadInitializeMethod'
    $log = $main.Methods | Where-Object { $_.Name -eq 'Log' -and $_.Parameters.Count -eq 1 }
    $errorLog = $main.Methods | Where-Object { $_.Name -eq 'Error' -and $_.Parameters.Count -eq 2 }
    if (!$findMod -or !$active -or !$initialize -or !$log -or !$errorLog) { throw 'Unsupported loader API.' }
    $body = [Mono.Cecil.Cil.MethodBody]::new($method)
    $method.Body = $body
    $body.InitLocals = $true
    $info = [Mono.Cecil.Cil.VariableDefinition]::new($initialize.ReturnType)
    $exceptionType = $errorLog.Parameters[1].ParameterType
    $exception = [Mono.Cecil.Cil.VariableDefinition]::new($exceptionType)
    $body.Variables.Add($info)
    $body.Variables.Add($exception)
    $il = $body.GetILProcessor()
    $op = [Mono.Cecil.Cil.OpCodes]
    $done = $il.Create($op::Ret)
    $hasMod = $il.Create($op::Call, $active)
    $noMod = $il.Create($op::Ldstr, 'Skin Manager not found, skipping integration')
    $tryStart = $il.Create($op::Ldstr, 'PassengerJobs.Skins.dll')
    $catchStart = $il.Create($op::Stloc, $exception)
    $invokeType = [Mono.Cecil.TypeReference]::new('System.Reflection', 'MethodBase', $module, $module.TypeSystem.CoreLibrary)
    $invoke = [Mono.Cecil.MethodReference]::new('Invoke', $module.TypeSystem.Object, $invokeType)
    $invoke.HasThis = $true
    $invoke.Parameters.Add([Mono.Cecil.ParameterDefinition]::new($module.TypeSystem.Object))
    $invoke.Parameters.Add([Mono.Cecil.ParameterDefinition]::new([Mono.Cecil.ArrayType]::new($module.TypeSystem.Object)))
    $il.Emit($op::Ldstr, 'SkinManagerMod')
    $il.Emit($op::Call, $findMod)
    $il.Emit($op::Dup)
    $il.Emit($op::Brtrue, $hasMod)
    $il.Emit($op::Pop)
    $il.Emit($op::Br, $noMod)
    $il.Append($hasMod)
    $il.Emit($op::Brfalse, $noMod)
    $il.Emit($op::Br, $tryStart)
    $il.Append($noMod)
    $il.Emit($op::Call, $log)
    $il.Emit($op::Br, $done)
    $il.Append($tryStart)
    $il.Emit($op::Ldstr, 'PassengerJobs.Skins.PassengerSkinChooser')
    $il.Emit($op::Ldstr, 'Initialize')
    $il.Emit($op::Call, $initialize)
    $il.Emit($op::Stloc, $info)
    $il.Emit($op::Ldloc, $info)
    $hasInitializer = $il.Create($op::Ldloc, $info)
    $il.Emit($op::Brtrue, $hasInitializer)
    $il.Emit($op::Leave, $done)
    $il.Append($hasInitializer)
    $il.Emit($op::Ldnull)
    $il.Emit($op::Ldnull)
    $il.Emit($op::Callvirt, $invoke)
    $il.Emit($op::Pop)
    $il.Emit($op::Ldstr, 'Activated Skin Manager integration')
    $il.Emit($op::Call, $log)
    $il.Emit($op::Leave, $done)
    $il.Append($catchStart)
    $il.Emit($op::Ldstr, 'Skin Manager integration could not be initialized')
    $il.Emit($op::Ldloc, $exception)
    $il.Emit($op::Call, $errorLog)
    $il.Emit($op::Leave, $done)
    $il.Append($done)
    $handler = [Mono.Cecil.Cil.ExceptionHandler]::new([Mono.Cecil.Cil.ExceptionHandlerType]::Catch)
    $handler.CatchType = $exceptionType
    $handler.TryStart = $tryStart
    $handler.TryEnd = $catchStart
    $handler.HandlerStart = $catchStart
    $handler.HandlerEnd = $done
    $body.ExceptionHandlers.Add($handler)
}
$artifactDir = Join-Path $repo 'artifacts'
New-Item -ItemType Directory -Force $artifactDir | Out-Null
$patchedCore = Join-Path $artifactDir 'PassengerJobs.dll'
$assembly.Write($patchedCore)
$assembly.Dispose()
$backupDir = Join-Path $artifactDir ('backup-' + (Get-Date -Format 'yyyyMMdd-HHmmss'))
New-Item -ItemType Directory $backupDir | Out-Null
Copy-Item -LiteralPath $corePath -Destination $backupDir
Copy-Item -LiteralPath (Join-Path $modDir 'PassengerJobs.Skins.dll') -Destination $backupDir
Copy-Item -LiteralPath $patchedCore -Destination $corePath
Copy-Item -LiteralPath $adapter -Destination $modDir
Write-Output "Installed Skin Manager compatibility patch. Original DLLs: $backupDir"
