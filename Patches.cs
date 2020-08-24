using Harmony12;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace PassengerJobsMod
{
    [HarmonyPatch(typeof(StationProceduralJobsController), "Awake")]
    class StationController_Start_Patch
    {
        private static HashSet<string> ManagedYards = new HashSet<string>() { "CSW", "MF", "FF", "HB", "GF" };

        static void Postfix( StationProceduralJobsController __instance )
        {
            string yardId = __instance.stationController.stationInfo.YardID;
            if( !ManagedYards.Contains(yardId) ) return;

            var gen = __instance.GetComponent<PassengerJobGenerator>();
            if( gen == null )
            {
                gen = __instance.gameObject.AddComponent<PassengerJobGenerator>();
                gen.Initialize();
            }
        }
    }

    // InventoryStartingItems.InstantiateStorageItemsWorld()
    [HarmonyPatch(typeof(InventoryStartingItems), "InstantiateStorageItemsWorld")]
    static class ISI_InstantiateItemsWorld_Patch
    {
        static void Prefix( List<StorageItemData> storageItemData, ref GameObject __state )
        {
            var bookProps = PassengerLicenseUtil.BookletProperties[PassBookletType.Passengers1License];

            StorageItemData itemData = storageItemData.Find(sid => bookProps.Name == sid.itemPrefabName);
            if( itemData != null )
            {
                storageItemData.Remove(itemData);

                GameObject licenseObj = Resources.Load(BC_CreateLicense_Patch.COPIED_PREFAB_NAME) as GameObject;
                if( licenseObj == null )
                {
                    PassengerJobs.ModEntry.Logger.Error("Couldn't spawn saved Passengers 1 license");
                    return;
                }

                // GameObject properties and state
                licenseObj = UnityEngine.Object.Instantiate(licenseObj);
                PassengerLicenseUtil.SetLicenseObjectProperties(licenseObj, PassBookletType.Passengers1License);
                licenseObj.GetComponent<InventoryItemSpec>().belongsToPlayer = itemData.belongsToPlayer;

                if( licenseObj.GetComponent<IStateSave>() is IStateSave stateSave )
                {
                    stateSave.SetStateSaveData(itemData.state);
                }

                // Position / rotation data
                Transform carTransform = null;
                Vector3 itemPos = new Vector3(itemData.itemPositionX, itemData.itemPositionY, itemData.itemPositionZ);

                if( !string.IsNullOrEmpty(itemData.carGuid) )
                {
                    var car = TrainCar.GetTrainCarByCarGuid(itemData.carGuid);
                    if( car )
                    {
                        if( car.GetComponent<TrainPhysicsLod>() is TrainPhysicsLod carPhysics )
                        {
                            carPhysics.ForceItemUpdate(false);
                        }
                        else
                        {
                            Debug.LogError($"Car {car.name} doesn't have TrainPhysicsLod component. Skipping.");
                        }

                        carTransform = car.interior;
                        itemPos += new Vector3(0, 0.3f, 0);
                    }
                }

                licenseObj.transform.position = itemPos;
                licenseObj.transform.rotation = new Quaternion(itemData.itemRotationX, itemData.itemRotationY, itemData.itemRotationZ, itemData.itemRotationW);

                Transform worldTransform = SingletonBehaviour<WorldMover>.Exists ? SingletonBehaviour<WorldMover>.Instance.originShiftParent : null;
                licenseObj.transform.SetParent(carTransform ?? worldTransform, true);

                __state = licenseObj;
            }
            else
            {
                // itemData == null
                __state = null;
            }
        }

        static void Postfix( List<GameObject> __result, ref GameObject __state )
        {
            if( __state != null )
            {
                __result.Add(__state);
            }
        }
    }
}
