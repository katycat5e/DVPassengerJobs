using DV.ThingTypes;
using HarmonyLib;
using PassengerJobs.Injectors;
using System;
using System.Collections.Generic;
using static Oculus.Avatar.CAPI;

namespace PassengerJobs.Patches
{
    [HarmonyPatch(typeof(Enum))]
    internal static class EnumPatch
    {
        [HarmonyPatch(nameof(Enum.GetValues))]
        [HarmonyPostfix]
        public static void GetValuesPostfix(Type enumType, ref Array __result)
        {
            if (enumType == typeof(JobLicenses))
            {
                __result = ExtendArray(__result, LicenseInjector.License.v1);
            }
            else if (enumType == typeof(CargoType))
            {
                __result = ExtendArray(__result, CargoInjector.PassengerCargo.v1);
            }
        }

        private static Array ExtendArray<T>(Array source, params T[] newValues)
        {
            var result = Array.CreateInstance(typeof(T), source.Length + newValues.Length);
            Array.Copy(source, result, source.Length);
            Array.Copy(newValues, 0, result, source.Length, newValues.Length);
            return result;
        }

        [HarmonyPatch(nameof(Enum.IsDefined))]
        [HarmonyPrefix]
        public static bool IsDefinedPrefix(Type enumType, object value, ref bool __result)
        {
            if (enumType == typeof(CargoType))
            {
                if (((value is int iVal) && ((CargoType)iVal == CargoInjector.PassengerCargo.v1)) ||
                    ((value is CargoType cVal) && (cVal == CargoInjector.PassengerCargo.v1)))
                {
                    __result = true;
                    return false;
                }
            }
            return true;
        }
    }

    [HarmonyPatch(typeof(JobLicenseType_v2))]
    internal static class JobLicenseType_v2Patch
    {
        [HarmonyPatch(nameof(JobLicenseType_v2.ToV2List))]
        [HarmonyPostfix]
        public static void ToV2ListPostfix(JobLicenses flags, ref List<JobLicenseType_v2> __result)
        {
            if ((flags & LicenseInjector.License.v1) != JobLicenses.Basic)
            {
                __result.Add(LicenseInjector.License);
            }
        }
    }
}
