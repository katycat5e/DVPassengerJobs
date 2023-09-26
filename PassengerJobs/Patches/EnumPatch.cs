using DV.ThingTypes;
using HarmonyLib;
using PassengerJobs.Injectors;
using System;
using static Oculus.Avatar.CAPI;

namespace PassengerJobs.Patches
{
    [HarmonyPatch(typeof(Enum))]
    internal class EnumPatch
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
    }
}
