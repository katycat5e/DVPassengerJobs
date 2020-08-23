using Harmony12;
using System;

namespace PassengerJobsMod
{
    // JobLicenses.DisplayName() (extension)
    [HarmonyPatch(typeof(LicenseManager), nameof(LicenseManager.DisplayName))]
    [HarmonyPatch(new Type[] { typeof(JobLicenses) })]
    class LM_DisplayName_Patch
    {
        static bool Prefix( JobLicenses license, ref string __result )
        {
            if( license == PassLicenses.Passengers1 )
            {
                __result = "Passengers 1";
                return false;
            }
            return true;
        }
    }

    // LicenseManager.GetNumberOfAcquiredJobLicenses()
    [HarmonyPatch(typeof(LicenseManager), nameof(LicenseManager.GetNumberOfAcquiredJobLicenses))]
    class LM_GetAcquiredLicenses_Patch
    {
        static void Postfix( ref int __result )
        {
            if( LicenseManager.IsJobLicenseAcquired(PassLicenses.Passengers1) )
            {
                __result += 1;
            }
        }
    }

    // LicenseManager.IsJobLicenseObtainable()
    [HarmonyPatch(typeof(LicenseManager), nameof(LicenseManager.IsJobLicenseObtainable))]
    class LM_IsLicenseObtainable_Patch
    {
        static bool Prefix( JobLicenses license, ref bool __result )
        {
            if( license == PassLicenses.Passengers1 )
            {
                __result = true;
                return false;
            }
            return true;
        }
    }

    // LicenseManager.IsValidForParsingToJobLicense()
    [HarmonyPatch(typeof(LicenseManager), nameof(LicenseManager.IsValidForParsingToJobLicense))]
    class LM_IsValidLicenses_Patch
    {
        static void Prefix( ref JobLicenses jobLicensesInt )
        {
            // we'll mask out the passenger license and pass on the remaining value
            jobLicensesInt &= ~PassLicenses.Passengers1;
        }
    }

    // LicenseManager.LoadData()
    [HarmonyPatch(typeof(LicenseManager), nameof(LicenseManager.LoadData))]
    class LM_LoadData_Patch
    {
        static void Postfix()
        {
            int? savedLicenses = SaveGameManager.data.GetInt("Job_Licenses");
            if( savedLicenses.HasValue )
            {
                JobLicenses val = (JobLicenses)savedLicenses.Value;
                if( val.HasFlag(PassLicenses.Passengers1) )
                {
                    LicenseManager.AcquireJobLicense(PassLicenses.Passengers1);
                }
            }
        }
    }
}
