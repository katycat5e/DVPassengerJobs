using DV.ThingTypes;
using PassengerJobs.Injectors;
using System.Collections.Generic;
using System.Linq;

namespace PassengerJobs.Generation
{
    public static class ConsistManager
    {
        private const string ID_RED = "PassengerRed";
        private const string ID_BLUE = "PassengerBlue";
        private const string ID_GREEN = "PassengerGreen";

        public static IEnumerable<TrainCarLivery> GetAllPassengerCars()
        {
            return CargoInjector.PassengerCargo.loadableCarTypes.SelectMany(info => info.carType.liveries);
        }

        public static IEnumerable<TrainCarLivery> GetFilteredPassengerCars(RouteType route, double maxTrainsetLength = double.PositiveInfinity)
        {
            var liveries = GetAllPassengerCars();

            // Skip any more checks if CCL isn't loaded.
            if (!CCLIntegration.Loaded) return liveries;

            // Get passenger carrying cars, then filter out things from CCL.
            var filtered = liveries.Where(livery => CCLIntegration.IsLiveryEnabled(livery, route) && TrainsetCheck(livery, maxTrainsetLength));

            if (CCLIntegration.IsCCLPrefered() && filtered.Count() > 3)
            {
                // If CCL is prefered and there are valid entries besides the vanilla ones, remove those.
                // If there isn't any custom car loaded, filtered will only be the original 3 so we don't go in here.
                return filtered.Where(x => x.id is not ID_RED and not ID_BLUE and not ID_GREEN);
            }

            return filtered;

            static bool TrainsetCheck(TrainCarLivery livery, double maxLength)
            {
                // If stuff from trainsets is allowed to spawn alone, no need to do trainset checks.
                if (PJMain.Settings.AllowCCLTrainsetAlone) return true;

                // If it is not part of a CCL set, then it's a lone livery, no need to check more.
                if (!CCLIntegration.TryGetTrainset(livery, out var trainset)) return true;

                // Finally check if the trainset is enabled (all liveries in it active), and if the
                // length of it fits within the requested limits.
                return CCLIntegration.IsTrainsetEnabled(trainset) &&
                    CarSpawner.Instance.GetTotalCarLiveriesLength(trainset.ToList(), true) <= maxLength;
            }
        }
    }
}
