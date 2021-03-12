using DV.Logic.Job;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace PassengerJobsMod
{
    [HarmonyPatch(typeof(StationProceduralJobsController), "Awake")]
    class SPJC_Awake_Patch
    {
        private static HashSet<string> ManagedYards = new HashSet<string>() { "CSW", "MF", "FF", "HB", "GF" };

        static void Postfix( StationProceduralJobsController __instance )
        {
            string yardId = __instance.stationController.stationInfo.YardID;
            if( !ManagedYards.Contains(yardId) ) return;

            var gen = __instance.gameObject.GetComponent<PassengerJobGenerator>();
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
        static void Prefix( ref List<StorageItemData> storageItemData, ref GameObject __state )
        {
            var bookProps = PassengerLicenseUtil.BookletProperties[PassBookletType.Passengers1License];

            StorageItemData itemData = storageItemData.Find(sid => bookProps.Name.Equals(sid.itemPrefabName));
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

                if( licenseObj.GetComponent<ItemSaveData>() is ItemSaveData stateSave )
                {
                    stateSave.LoadItemData(itemData.state);
                }

                // Position / rotation data
                Transform carTransform = null;
                Vector3 itemPos = new Vector3(itemData.itemPositionX, itemData.itemPositionY, itemData.itemPositionZ);

                if( !string.IsNullOrEmpty(itemData.carGuid) )
                {
                    var idGenerator = SingletonBehaviour<IdGenerator>.Instance;
                    var car = idGenerator.logicCarToTrainCar[idGenerator.carGuidToCar[itemData.carGuid]];
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

    // Job.GetPotentialBonusPaymentForTheJob()
    [HarmonyPatch(typeof(Job), nameof(Job.GetPotentialBonusPaymentForTheJob))]
    static class Job_GetBonus_Patch
    {
        static bool Prefix( Job __instance, ref float __result, float ___initialWage )
        {
            if( PassengerJobs.Settings.UseCustomWages && (__instance.jobType == PassJobType.Express) )
            {
                // it's a passenger job
                __result = ___initialWage * PassengerJobGenerator.BONUS_TO_BASE_WAGE_RATIO;
                return false;
            }
            return true;
        }
    }

    // IdGenerator.GenerateJobId()
    [HarmonyPatch(typeof(IdGenerator), nameof(IdGenerator.GenerateJobID))]
    static class IG_GenerateJobId_Patch
    {
        const string EXPRESS_TYPE = "PE";
        const string COMMUTE_TYPE = "PC";

        private static readonly System.Random idRNG = new System.Random();

        private static readonly HashSet<string> existingJobIds = 
            AccessTools.Field(typeof(IdGenerator), "existingJobIds")?.GetValue(IdGenerator.Instance) as HashSet<string>;

        static bool Prefix( IdGenerator __instance, JobType jobType, StationsChainData jobStationsInfo, ref string __result )
        {
            if( (jobType != PassJobType.Express) &&
                (jobType != PassJobType.Commuter) )
            {
                return true;
            }

            string yardId = null;
            if( jobStationsInfo != null )
            {
                yardId = jobStationsInfo.chainOriginYardId;
            }

            string typeStr = (jobType == PassJobType.Express) ? EXPRESS_TYPE : COMMUTE_TYPE;
            string idStr = FindUnusedID(typeStr, yardId);

            if( idStr != null )
            {
                __instance.RegisterJobId(idStr);
                __result = idStr;
            }
            else
            {
                PassengerJobs.ModEntry.Logger.Warning($"Couldn't find free jobId for job type: {typeStr}! Using 0 for jobId number!");
                __result = (yardId != null) ? $"{yardId}-{typeStr}-{0:D2}" : $"{typeStr}-{0:D2}";
            }

            return false;
        }

        private static string FindUnusedID( string typeStr, string yardId = null )
        {
            int idNum = idRNG.Next(0, 100);

            for( int attemptNum = 0; attemptNum < 99; attemptNum++ )
            {
                string idStr = (yardId != null) ? $"{yardId}-{typeStr}-{idNum:D2}" : $"{typeStr}-{idNum:D2}";

                if( !existingJobIds.Contains(idStr) )
                {
                    return idStr;
                }

                idNum = (idNum >= 99) ? 0 : (idNum + 1);
            }

            return null;
        }

        public static string GetNamedExpressId( SpecialTrain special )
        {
            string idStr = FindUnusedID(special.IDAbbrev);

            if( idStr != null )
            {
                //IdGenerator.Instance.RegisterJobId(idStr);
                return idStr;
            }
            else
            {
                PassengerJobs.ModEntry.Logger.Warning($"Couldn't find free jobId for express special: {special.IDAbbrev}! Using 0 for jobId number!");
                return $"{special.IDAbbrev}-{0:D2}";
            }
        }
    }

    [HarmonyPatch(typeof(JobChainController), nameof(JobChainController.GetJobChainSaveData))]
    static class JCC_GetJobChainSaveData_Patch
    {
        static void Postfix( JobChainController __instance, ref JobChainSaveData __result)
        {
            if( __instance is PassengerTransportChainController )
            {
                __result = new PassengerChainSaveData(PassengerChainSaveData.PassChainType.Transport, __result);
            }
            else if( __instance is CommuterChainController )
            {
                __result = new PassengerChainSaveData(PassengerChainSaveData.PassChainType.Commuter, __result);
            }
        }
    }

    [HarmonyPatch(typeof(JobChainController), "OnAnyJobFromChainAbandoned")]
    static class JCC_OnJobAbandoned_Patch
    {
        static void Prefix( JobChainController __instance )
        {
            if( (__instance is PassengerTransportChainController) || (__instance is CommuterChainController) )
            {
                // force deletion of cars instead of leaving them for the unused car deleter
                SingletonBehaviour<CarSpawner>.Instance.DeleteTrainCars(__instance.trainCarsForJobChain, true);
                __instance.trainCarsForJobChain.Clear();
            }
        }
    }
}
