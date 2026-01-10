using DV.Booklets;
using DV.ThingTypes;
using HarmonyLib;
using PassengerJobs.Generation;
using PassengerJobs.Injectors;
using System;
using UnityEngine;

namespace PassengerJobs.Patches
{
    [HarmonyPatch(typeof(StartingItemsController))]
    internal class StartingItemsControllerPatch
    {
        [HarmonyPatch(nameof(StartingItemsController.AddStartingItems))]
        [HarmonyPostfix]
        public static void AddStartingItemsPostfix()
        {
            BundleLoader.HandleSaveReload();
            SaveDataInjector.AcquirePassengerLicense();
            RouteManager.CreateRuralStations();
        }
        
        [HarmonyPatch(nameof(StartingItemsController.InstantiateItem))]
        [HarmonyPrefix]
        private static bool InstantiateItemPrefix(StartingItemsController __instance, StorageItemData itemData, ref GameObject __result)
        {
            var prefabName = itemData.itemPrefabName;
            if (string.IsNullOrEmpty(prefabName)) return true;

            var license = TryGetLicense(prefabName, out var isInfo);
            if (license == null) return true;

            var position =
                StartingItemsController.ITEM_INSTANTIATION_SAFETY_OFFSET * __instance.instantiatedItemCount +
                StartingItemsController.ITEM_INSTANTIATION_SAFETY_POSITION;
            __instance.instantiatedItemCount++;
            
            try
            {
                __result = isInfo
                    ? BookletCreator_Licenses.CreateLicenseInfo(license, position, Quaternion.identity)
                    : BookletCreator_Licenses.CreateLicense(license, position, Quaternion.identity);
            }
            catch (Exception ex)
            {
                PJMain.Warning($"Failed to restore passenger license booklet '{prefabName}' from save: {ex.Message}");
            }

            return false;
        }

        private static JobLicenseType_v2? TryGetLicense(string prefabName, out bool isInfo)
        {
            (var license, isInfo) = prefabName switch
            {
                _ when prefabName == LicenseInjector.License1Data.PrefabName => (LicenseInjector.License1, false),
                _ when prefabName == LicenseInjector.License2Data.PrefabName => (LicenseInjector.License2, false),
                _ when prefabName == LicenseInjector.License1Data.SamplePrefabName => (LicenseInjector.License1, true),
                _ when prefabName == LicenseInjector.License2Data.SamplePrefabName => (LicenseInjector.License2, true),
                _ => (null, false)
            };

            return license;
        }
    }
}
