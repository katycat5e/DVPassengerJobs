using HarmonyLib;
using PassengerJobs.Generation;
using PassengerJobs.Injectors;

namespace PassengerJobs.Patches
{
    [HarmonyPatch(typeof(StartingItemsController), nameof(StartingItemsController.AddStartingItems))]
    internal class StartingItemsControllerPatch
    {
        public static void Postfix()
        {
            BundleLoader.HandleSaveReload();
            SaveDataInjector.AcquirePassengerLicense();
            RouteManager.CreateRuralStations();
        }
    }
}
