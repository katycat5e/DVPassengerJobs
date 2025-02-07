using DV.ThingTypes;
using DV.ThingTypes.TransitionHelpers;
using UnityEngine;

namespace PassengerJobs.Injectors
{
    public static class CargoInjector
    {
        public static CargoType_v2 PassengerCargo { get; private set; } = null!;

        private const int PASSENGER_CARGO_TYPE_ID = 1000;
        private const float PASSENGER_MASS_PER_UNIT = 3000f;
        private const float PASSENGER_FULL_DAMAGE_PRICE = 70_000f;
        private const float PASSENGER_ENVIRONMENT_DAMAGE_PRICE = 0f;

        public static void RegisterPassengerCargo()
        {
            var passCargo = ScriptableObject.CreateInstance<CargoType_v2>();

            passCargo.id = "Passengers";
            passCargo.v1 = (CargoType)PASSENGER_CARGO_TYPE_ID;
            passCargo.localizationKeyFull = LocalizationKey.CARGO_NAME_FULL.K();
            passCargo.localizationKeyShort = LocalizationKey.CARGO_NAME_SHORT.K();
            passCargo.massPerUnit = PASSENGER_MASS_PER_UNIT;
            passCargo.fullDamagePrice = PASSENGER_FULL_DAMAGE_PRICE;
            passCargo.environmentDamagePrice = PASSENGER_ENVIRONMENT_DAMAGE_PRICE;
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