using DV.ServicePenalty.UI;
using Harmony12;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace PassengerJobsMod
{
    static class CM_Constants
    {
        internal const int N_BUILTIN_LICENSES = 19;
        internal const int N_PASS_LICENSES = 1;
        internal static FieldInfo nSlotsField = typeof(CareerManagerLicensesScreen).GetField("numberOfSlots", BindingFlags.NonPublic | BindingFlags.Instance);
        internal static FieldInfo licenseEntryField = typeof(CareerManagerLicensesScreen).GetField("licenseEntries", BindingFlags.NonPublic | BindingFlags.Instance);
        internal static Type licenseEntryType = typeof(CareerManagerLicensesScreen).GetNestedType("LicenseEntry", BindingFlags.NonPublic);
        internal static MethodInfo updateLicenseMethod = licenseEntryType.GetMethod("UpdateJobLicenseData", BindingFlags.Public | BindingFlags.Instance);
    }

    [HarmonyPatch(typeof(CareerManagerLicensesScreen))]
    [HarmonyPatch("HighestFirstDisplayedLicenseIndex", MethodType.Getter)]
    class CM_HighestStartIndex_Patch
    {
        static bool Prefix( CareerManagerLicensesScreen __instance, ref int __result )
        {
            int nSlots = (int)CM_Constants.nSlotsField.GetValue(__instance);

            __result = Mathf.Max(CM_Constants.N_BUILTIN_LICENSES + CM_Constants.N_PASS_LICENSES - nSlots, 0);

            return false;
        }
    }

    [HarmonyPatch(typeof(CareerManagerLicensesScreen), "PopulateLicensesTextsFromIndex")]
    class CM_PopulateLicenses_Patch
    {

        // oh god why
        static void Postfix( int startingIndex, CareerManagerLicensesScreen __instance )
        {
            int nSlots = (int)CM_Constants.nSlotsField.GetValue(__instance);

            // offset from first slot in screen
            int passLicenseSlot = CM_Constants.N_BUILTIN_LICENSES - startingIndex;
            if( passLicenseSlot < nSlots )
            {
                Type entryListType = typeof(List<>).MakeGenericType(CM_Constants.licenseEntryType);
                PropertyInfo indexProp = entryListType.GetProperty("Item", BindingFlags.Public | BindingFlags.Instance);

                object licenseEntryList = CM_Constants.licenseEntryField.GetValue(__instance);
                object licenseEntry = indexProp.GetValue(licenseEntryList, new object[] { passLicenseSlot });

                CM_Constants.updateLicenseMethod.Invoke(licenseEntry, new object[] { PassLicenses.Passengers1 });
            }
        }
    }
}
