# Skin Manager 4.3 compatibility

Skin Manager 4.3.2 no longer contains `SkinChooser` or `SkinManager.IsCCLCarType`.
The old adapter derived from the missing type, so even scanning loaded assemblies
(including the game console's startup scan) threw `ReflectionTypeLoadException`.

The adapter now explicitly initializes a Harmony prefix for
`SkinProvider.GetNewSkin(TrainCarLivery)`. It selects a theme shared by all passenger
consist liveries, respects themes marked `PreventRandomSpawning`, and overrides the
selection only during passenger spawning. A `SpawnCars` finalizer clears the state
on both success and failure. No shared theme means normal Skin Manager selection.

The regular solution uses the updated initialization in `PJMain.cs`. Add the
Skin Manager directory to `Directory.Build.targets` as shown in the example.

## Build and install against the installed version

The installed Passenger Jobs can be newer than this checkout. To preserve that
version, the standalone project compiles only the adapter against the installed
DLLs. The installer replaces only `PJMain.TryLoadSkinManager` in the main DLL to
call `PassengerSkinChooser.Initialize`, with exception handling; other methods
and the multiplayer integration remain intact. Original DLLs are backed up under
`artifacts/backup-<timestamp>/` before installation.

Exit the game and run from PowerShell:

```powershell
./tools/Install-SkinCompatibility.ps1 -DvInstallDir 'W:\SteamLibrary\steamapps\common\Derail Valley'
```

Requires .NET SDK, .NET Framework 4.8 targeting pack, and Mono.Cecil. If needed,
pass `-CecilPath` pointing to an installed Mono.Cecil DLL. The default points to
the local Fody package cache. The script builds and checks before replacing files.

Checks cover assembly type scanning, current API signatures, loader JIT validity,
scoped skin selection, unrelated liveries, and state cleanup. Harmony's wrapper
for the game spawn method requires Unity's Mono runtime, so actual initialization
and spawned train appearance must also be verified in the game.

To restore, exit the game and copy both DLLs from the backup folder back to
`Mods/PassengerJobs/`. Updating Passenger Jobs later may require rerunning the
installer.
