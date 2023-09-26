using DV.ServicePenalty.UI;
using HarmonyLib;
using PassengerJobs.Injectors;

namespace PassengerJobs.Patches
{
    [HarmonyPatch(typeof(CareerManagerLicensesScreen))]
    internal static class CareerManagerLicensesScreenPatch
    {
        [HarmonyPatch("Awake")]
        [HarmonyPrefix]
        public static void AwakePrefix(CareerManagerLicensesScreen __instance)
        {
            var entry = new CareerManagerLicensesScreen.GeneralOrJobLicense()
            {
                jobLicense = LicenseInjector.License
            };

            __instance.licensesDisplayOrder.Add(entry);
        }
    }
}
