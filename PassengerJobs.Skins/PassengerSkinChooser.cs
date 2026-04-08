using System.Collections.Generic;
using System.Linq;
using DV.ThingTypes;
using PassengerJobs.Extensions;
using PassengerJobs.Generation;
using SkinManagerMod;

namespace PassengerJobs.Skins;

public class PassengerSkinChooser : SkinChooser
{
    private string? _chosenConsistSkin = null;

    public PassengerSkinChooser() : base()
    {
        PassengerJobGenerator.BeginSpawnConsist += OnBeginSpawnConsist;
        PassengerJobGenerator.AfterSpawnConsist += OnAfterSpawnConsist;
    }

    private void OnBeginSpawnConsist(IEnumerable<TrainCarLivery> carTypes)
    {
        var availableSkins = new HashSet<string>();
        bool first = true;

        bool preferDefaults = Main.Settings.defaultSkinsMode == DefaultSkinsMode.PreferDefaults;
        bool allowDefaults = Main.Settings.defaultSkinsMode >= DefaultSkinsMode.AllowForAllCars;
        bool allowCCLDefaults = Main.Settings.defaultSkinsMode >= DefaultSkinsMode.AllowForCustomCars;

        var uniqueCarTypes = carTypes.Distinct().ToList();

        // find skins that are applicable to the entire consist
        foreach (var livery in uniqueCarTypes)
        {
            bool isCCL = SkinManager.IsCCLCarType(livery);
            bool includeDefaults = preferDefaults || allowDefaults || (allowCCLDefaults && isCCL);

            var skinsForType = SkinProvider.GetSkinsForType(livery, includeDefaults, false);
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
    }
    
    private void OnAfterSpawnConsist(IEnumerable<TrainCar>? _)
    {
        _chosenConsistSkin = null;
    }

    public override bool TryChooseSkin(TrainCar car, out string? skinName)
    {
        skinName = _chosenConsistSkin;
        return _chosenConsistSkin is not null;
    }
}
