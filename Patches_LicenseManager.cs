using HarmonyLib;
using System;

namespace PassengerJobsMod
{
    // JobLicenses.DisplayName() (extension)
    [HarmonyPatch(typeof(LicenseManager))]
    static class LicenseManager_Patches
    {
        [HarmonyPatch(nameof(LicenseManager.DisplayName))]
        [HarmonyPatch(new Type[] { typeof(JobLicenses) })]
        [HarmonyPrefix]
        static bool DisplayName( JobLicenses license, ref string __result )
        {
            if( license == PassLicenses.Passengers1 )
            {
                __result = PassengerLicenseUtil.PASS1_LICENSE_NAME;
                return false;
            }
            return true;
        }

        [HarmonyPatch(nameof(LicenseManager.GetNumberOfAcquiredJobLicenses))]
        [HarmonyPostfix]
        static void CorrectNumberOfLicenses( ref int __result )
        {
            if( LicenseManager.IsJobLicenseAcquired(PassLicenses.Passengers1) )
            {
                __result += 1;
            }
        }

        [HarmonyPatch(nameof(LicenseManager.IsJobLicenseObtainable))]
        [HarmonyPrefix]
        static bool IsJobLicenseObtainable( JobLicenses license, ref bool __result )
        {
            if( license == PassLicenses.Passengers1 )
            {
                __result = LicenseManager.IsJobLicenseAcquired(JobLicenses.Shunting);
                return false;
            }
            return true;
        }

        static JobLicenses CleanJobLicenses( JobLicenses jobLicenses )
        {
            return jobLicenses & ~PassLicenses.Passengers1;
        }

        [HarmonyPatch(nameof(LicenseManager.IsValidForParsingToJobLicense))]
        [HarmonyPrefix]
        static void HideExtraLicenseForParseCheck( ref JobLicenses jobLicensesInt )
        {
            // we'll mask out the passenger license and pass on the remaining value
            jobLicensesInt = CleanJobLicenses(jobLicensesInt);
        }

        [HarmonyPatch(nameof(LicenseManager.SaveData))]
        [HarmonyPostfix]
        static void CleanExtraLicenseFromSave()
        {
            JobLicenses cleaned = CleanJobLicenses(LicenseManager.GetAcquiredJobLicenses());
            SaveGameManager.data.SetInt(SaveGameKeys.Job_Licenses, (int)cleaned);
        }

        [HarmonyPatch(nameof(LicenseManager.LoadData))]
        [HarmonyPostfix]
        static void AcquirePassLicenseLegacySave()
        {
            int? savedLicenses = SaveGameManager.data.GetInt("Job_Licenses");
            if( savedLicenses.HasValue )
            {
                JobLicenses val = (JobLicenses)savedLicenses.Value;
                if( val.HasFlag(PassLicenses.Passengers1) )
                {
                    PassengerJobs.Log("Acquiring passengers license from legacy save data");
                    LicenseManager.AcquireJobLicense(PassLicenses.Passengers1);
                }
            }
        }
    }
}
