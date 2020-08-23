using Harmony12;
using System;

namespace PassengerJobsMod
{
    // LicenseManager.GetNumberOfAcquiredJobLicenses()
    [HarmonyPatch(typeof(LicenseManager), nameof(LicenseManager.GetNumberOfAcquiredJobLicenses))]
    class LM_GetAcquiredLicenses_Patch
    {
        public void Postfix( ref int __result )
        {
            if( LicenseManager.IsJobLicenseAcquired(PassLicenses.Passengers1) )
            {
                __result += 1;
            }
        }
    }

    // LicenseManager.GetRequiredLicensesInOrderToAcquireJobLicense()
    [HarmonyPatch(typeof(LicenseManager), nameof(LicenseManager.GetRequiredLicensesInOrderToAcquireJobLicense))]
    class LM_GetPrereqLicenses_Patch
    {
        public bool Prefix( JobLicenses license, ref ValueTuple<GeneralLicenseType, JobLicenses> __result )
        {
            if( license == PassLicenses.Passengers1 )
            {
                __result = new ValueTuple<GeneralLicenseType, JobLicenses>(GeneralLicenseType.NotSet, JobLicenses.Basic);
                return false;
            }
            return true;
        }
    }

    // LicenseManager.IsJobLicenseObtainable()
    [HarmonyPatch(typeof(LicenseManager), nameof(LicenseManager.IsJobLicenseObtainable))]
    class LM_IsLicenseObtainable_Patch
    {
        public bool Prefix( JobLicenses license, ref bool __result )
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
        public void Prefix( ref JobLicenses jobLicensesInt )
        {
            // we'll mask out the passenger license and pass on the remaining value
            jobLicensesInt &= ~PassLicenses.Passengers1;
        }
    }

    // LicenseManager.LoadData()
    [HarmonyPatch(typeof(LicenseManager), nameof(LicenseManager.LoadData))]
    class LM_LoadData_Patch
    {
        public void Postfix()
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
