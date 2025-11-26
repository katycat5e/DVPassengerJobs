using DV.Booklets;
using DV.ThingTypes;
using HarmonyLib;
using PassengerJobs.Injectors;
using System;
using UnityEngine;

namespace PassengerJobs.Patches
{
    [HarmonyPatch(typeof(BookletCreator_Licenses))]
    internal static class BookletCreator_LicensesPatch
    {
        [HarmonyPatch(nameof(BookletCreator_Licenses.CreateLicense))]
        [HarmonyPatch(new Type[] { typeof(JobLicenseType_v2), typeof(Vector3), typeof(Quaternion), typeof(Transform), typeof(bool)})]
        [HarmonyPostfix]
        public static void CreateLicensePostfix(JobLicenseType_v2 license, ref GameObject __result)
        {
            if (license == LicenseInjector.License1)
            {
                LicenseInjector.SetLicense1Properties(__result);
            }
            if (license == LicenseInjector.License2)
            {
                LicenseInjector.SetLicense2Properties(__result);
            }
        }

        [HarmonyPatch(nameof(BookletCreator_Licenses.CreateLicenseInfo))]
        [HarmonyPatch(new Type[] { typeof(JobLicenseType_v2), typeof(Vector3), typeof(Quaternion), typeof(Transform), typeof(bool) })]
        [HarmonyPostfix]
        public static void CreateLicenseInfoPostfix(JobLicenseType_v2 license, ref GameObject __result)
        {
            if (license == LicenseInjector.License1)
            {
                LicenseInjector.SetLicense1SampleProperties(__result);
            }
            if (license == LicenseInjector.License2)
            {
                LicenseInjector.SetLicense2SampleProperties(__result);
            }
        }
    }
}
