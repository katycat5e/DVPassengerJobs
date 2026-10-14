using System.Collections.Generic;
using System.Linq;
using DV.ThingTypes;
using PassengerJobs.Extensions;
using PassengerJobs.Generation;
using SkinManagerMod;
using HarmonyLib;

namespace PassengerJobs.Skins;

public static class PassengerSkinChooser
{
    private static string? _chosenConsistSkin;
    private static HashSet<TrainCarLivery>? _consistLiveries;
    private static bool _initialized;

    public static void Initialize()
    {
        if (_initialized) return;
        var harmony = new Harmony("PassengerJobs.Skins");
        harmony.Patch(AccessTools.Method(typeof(SkinProvider), nameof(SkinProvider.GetNewSkin),
            new[] { typeof(TrainCarLivery) }),
            prefix: new HarmonyMethod(typeof(PassengerSkinChooser), nameof(ChooseConsistSkin)));
        // Clear the selection even when spawning throws, including older Passenger Jobs builds.
        harmony.Patch(AccessTools.Method(typeof(PassengerJobGenerator), "SpawnCars"),
            finalizer: new HarmonyMethod(typeof(PassengerSkinChooser), nameof(ClearConsistSkin)));
        PassengerJobGenerator.BeginSpawnConsist += OnBeginSpawnConsist;
        PassengerJobGenerator.AfterSpawnConsist += OnAfterSpawnConsist;
        _initialized = true;
    }

    private static void OnBeginSpawnConsist(IEnumerable<TrainCarLivery> carTypes)
    {
        ClearConsistSkin();
        var availableSkins = new HashSet<string>();
        bool first = true;

        bool preferDefaults = Main.Settings.defaultSkinsMode == DefaultSkinsMode.PreferDefaults;
        bool allowDefaults = Main.Settings.defaultSkinsMode >= DefaultSkinsMode.AllowForAllCars;
        bool allowCCLDefaults = Main.Settings.defaultSkinsMode >= DefaultSkinsMode.AllowForCustomCars;

        var uniqueCarTypes = carTypes.Distinct().ToList();

        // find skins that are applicable to the entire consist
        foreach (var livery in uniqueCarTypes)
        {
            bool isCCL = !SkinProvider.IsThemeable(livery);
            bool includeDefaults = preferDefaults || allowDefaults || (allowCCLDefaults && isCCL);

            var skinsForType = SkinProvider.GetSkinsForType(livery, includeDefaults, false);
            skinsForType.RemoveAll(s => SkinProvider.TryGetThemeSettings(s.name, out var settings)
                && settings.PreventRandomSpawning);
            if (preferDefaults)
            {
                skinsForType.RemoveAll(s => !SkinProvider.IsBuiltInTheme(s));
            }

            if (first)
            {
                availableSkins.UnionWith(skinsForType.Select(s => s.name));
                first = false;
            }
            else
            {
                availableSkins.IntersectWith(skinsForType.Select(s => s.name));
            }
        }

        var choices = availableSkins.ToList();

        if (choices.Count == 1)
        {
            _chosenConsistSkin = choices[0];
        }
        else if (choices.Count > 1)
        {
            _chosenConsistSkin = choices.PickOne();
        }
        else
        {
            _chosenConsistSkin = null;
        }

        PJMain.Log($"Chose skin {_chosenConsistSkin} for new consist");
        _consistLiveries = new HashSet<TrainCarLivery>(uniqueCarTypes);
    }
    
    private static void OnAfterSpawnConsist(IEnumerable<TrainCar>? _)
    {
        ClearConsistSkin();
    }

    private static void ClearConsistSkin()
    {
        _chosenConsistSkin = null;
        _consistLiveries = null;
    }

    private static bool ChooseConsistSkin(TrainCarLivery __0, ref string __result)
    {
        if (_chosenConsistSkin is null || _consistLiveries?.Contains(__0) != true)
            return true;
        __result = _chosenConsistSkin;
        return false;
    }
}
