using DV;
using DV.ThingTypes;
using DV.UI;
using DV.Utils;
using HarmonyLib;
using PassengerJobs.Injectors;
using System.Linq;

namespace PassengerJobs.Patches
{
    [HarmonyPatch(typeof(UserProfileProvider))]
    internal static class UserProfileProviderPatch
    {
        [HarmonyPatch(nameof(UserProfileProvider.IsCustomCareerUnlocked), MethodType.Getter)]
        [HarmonyPrefix]
        public static bool Get_IsCustomCareerUnlockedPrefix(UserProfileProvider __instance, ref bool __result)
        {
            bool allJobLicenses = true;
            foreach (var license in Globals.G.Types.jobLicenses)
            {
                if ((license.v1 == JobLicenses.Basic) || (license == LicenseInjector.License))
                {
                    continue;
                }

                if (!UnlockablesManager.Instance.UnlockedJobLicenses.Contains(license.v1.ToString()))
                {
                    allJobLicenses = false;
                }
            }

            int genLicenseCount = SingletonBehaviour<UnlockablesManager>.Instance.UnlockedGeneralLicenses.Count;
            __result = allJobLicenses && (genLicenseCount >= __instance.TotalGeneralLicensesCount);
            return false;
        }
    }
}
