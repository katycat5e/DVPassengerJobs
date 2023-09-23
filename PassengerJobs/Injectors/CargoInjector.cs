using DV.ThingTypes;
using DV.ThingTypes.TransitionHelpers;
using UnityEngine;

namespace PassengerJobs.Injectors
{
    public static class CargoInjector
    {
        public static CargoType_v2 PassengerCargo = null!;

        public static void RegisterPassengerCargo()
        {
            // add cargo definition
            var passCargo = ScriptableObject.CreateInstance<CargoType_v2>();
            passCargo.id = "Passengers";
            passCargo.v1 = (CargoType)1000;
            passCargo.localizationKeyFull = LocalizationKey.CARGO_NAME_FULL.K();
            passCargo.localizationKeyShort = LocalizationKey.CARGO_NAME_SHORT.K();
            passCargo.massPerUnit = 3000f;
            passCargo.fullDamagePrice = 70_000f;
            passCargo.environmentDamagePrice = 0;
            passCargo.requiredJobLicenses = new[] { LicenseInjector.License };
            passCargo.loadableCarTypes = new[]
            {
                new CargoType_v2.LoadableInfo(TrainCarType.PassengerRed.ToV2().parentType, null),
            };

            DV.Globals.G.Types.cargos.Add(passCargo);
            PassengerCargo = passCargo;
        }
    }
}
