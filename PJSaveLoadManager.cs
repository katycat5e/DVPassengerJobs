using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DV.JObjectExtstensions;
using HarmonyLib;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace PassengerJobsMod
{
    public static class PJSaveLoadManager
    {
        public static readonly string SAVE_FOLDER = "saves";
        public static readonly string SAVE_FILE_NAME = "pj_savedata.json";
        public static readonly string SAVE_BACKUP_PREFIX = "pj_savedata_backup_";
        public static readonly string SAVE_BACKUP_FORMAT = SAVE_BACKUP_PREFIX + "{0}.json";

        public static string SaveDirectory => Path.Combine(PassengerJobs.ModEntry.Path, SAVE_FOLDER);
        public static string SaveFilePath => Path.Combine(PassengerJobs.ModEntry.Path, SAVE_FOLDER, SAVE_FILE_NAME);

        private const string PJ_DATA_KEY = "passengers_mod";
        private const string HAS_LICENSE_P1_KEY = "pass1_obtained";
        private const string VERSION_KEY = "version";

        public const int CURRENT_DATA_VERSION = 2;

        private static JObject loadedData = null;

        private static IEnumerable<StationProceduralJobsController> ProceduralJobsControllers
        {
            get => StationController.allStations.Select(sc => sc.ProceduralJobsController);
        }

        public static void Save( SaveGameData mainGameData )
        {
            var pjSaveData = new JObject();

            // jobs
            JobChainSaveData[] chainData =
                ProceduralJobsControllers
                .SelectMany(controller => controller.GetCurrentJobChains())
                .Where(IsPassengerChain)
                .Select(chain => chain.GetJobChainSaveData())
                .ToArray();

            var jobsData = new JobsSaveGameData(chainData, 0);
            pjSaveData.SetObjectViaJSON(SaveGameKeys.Jobs, jobsData, JobSaveManager.serializeSettings);

            // licenses
            pjSaveData.SetBool(HAS_LICENSE_P1_KEY, LicenseManager.IsJobLicenseAcquired(PassLicenses.Passengers1));

            pjSaveData.SetInt(VERSION_KEY, CURRENT_DATA_VERSION);

            // add to base game data
            mainGameData.SetJObject(PJ_DATA_KEY, pjSaveData);
        }

        private static bool IsPassengerChain( JobChainController jobChain )
        {
            return (jobChain is PassengerTransportChainController) || (jobChain is CommuterChainController);
        }

        private static JObject MigrateV3Data()
        {
            if( File.Exists(SaveFilePath) )
            {
                JObject data;

                try
                {
                    using( var saveFile = File.OpenText(SaveFilePath) )
                    {
                        using( var jtr = new JsonTextReader(saveFile) )
                        {
                            data = JToken.ReadFrom(jtr) as JObject;
                            if( data == null )
                            {
                                PassengerJobs.Error("Save file contained invalid JSON");
                            }
                        }
                    }

                    File.Delete(SaveFilePath);
                    return data;
                }
                catch( Exception ex )
                {
                    PassengerJobs.Warning("Couldn't read save v3 file:\n" + ex.Message);
                    return null;
                }
            }

            return null;
        }

        public static void Load()
        {
            loadedData = SaveGameManager.data.GetJObject(PJ_DATA_KEY);
            if( loadedData == null )
            {
                loadedData = MigrateV3Data();
                if( loadedData != null ) PassengerJobs.Log("Migrated data from v3 save");
            }

            if( loadedData != null )
            {
                PassengerJobs.Log("Found save data, attempting to load...");

                if( loadedData.GetInt(VERSION_KEY) == CURRENT_DATA_VERSION )
                {
                    if( loadedData.GetBool(HAS_LICENSE_P1_KEY) == true )
                    {
                        PassengerJobs.Log("Acquiring passengers license");
                        LicenseManager.AcquireJobLicense(PassLicenses.Passengers1);

                        Inventory.Instance.RemoveMoney(PassengerLicenseUtil.PASS1_COST);
                    }
                }
                else
                {
                    PassengerJobs.Warning("Save file contains incompatible data version");
                    loadedData = null;
                }
            }
            else
            {
                PassengerJobs.Log("No save data found");
            }
        }

        public static void InjectJobChains( JobsSaveGameData mainJobData )
        {
            // inject job chains into main game data
            if( loadedData?.GetObjectViaJSON<JobsSaveGameData>(SaveGameKeys.Jobs, JobSaveManager.serializeSettings) is JobsSaveGameData jobData )
            {
                JobChainSaveData[] combinedChains = mainJobData.jobChains
                    .Concat(jobData.jobChains)
                    .ToArray();

                mainJobData.jobChains = combinedChains;
            }
        }

        public static void PurgeSaveData()
        {
            // we'll keep the backups just in case
            if( File.Exists(SaveFilePath) )
            {
                try
                {
                    File.Delete(SaveFilePath);
                }
                catch( Exception ex )
                {
                    PassengerJobs.Error("Failed to delete save file:\n" + ex.Message);
                }
            }
        }
    }

    [HarmonyPatch(typeof(SaveGameManager))]
    static class SaveGameManager_Patches
    {
        [HarmonyPatch("DoSaveIO")]
        [HarmonyPrefix]
        static void InjectPassengerSaveData( SaveGameData data )
        {
            PJSaveLoadManager.Save(data);

            // refund license in case mod is uninstalled
            if( LicenseManager.IsJobLicenseAcquired(PassLicenses.Passengers1) )
            {
                float? money = data.GetFloat(SaveGameKeys.Player_money);
                if( money.HasValue )
                {
                    float newBalance = money.Value + PassengerLicenseUtil.PASS1_COST;
                    data.SetFloat(SaveGameKeys.Player_money, newBalance);
                }
            }
        }

        [HarmonyPatch("DoLoadIO")]
        [HarmonyPostfix]
        static void OnSaveLoaded( bool __result )
        {
            if( !__result ) return;

            PJSaveLoadManager.Load();
        }
    }

    [HarmonyPatch(typeof(JobChainController), nameof(JobChainController.GetJobChainSaveData))]
    static class JCC_GetJobChainSaveData_Patch
    {
        static void Postfix( JobChainController __instance, ref JobChainSaveData __result )
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

    // Filter save game job data to exclude passenger values
    [HarmonyPatch(typeof(JobSaveManager))]
    static class JobSaveManager_Patches
    {
        static bool IsNotPassengerChainData( JobChainSaveData data )
        {
            return !(data is PassengerChainSaveData);
        }

        [HarmonyPatch(nameof(JobSaveManager.GetJobsSaveGameData))]
        [HarmonyPostfix]
        static void FilterPassengerJobChains( ref JobsSaveGameData __result )
        {
            __result.jobChains = __result.jobChains
                .Where(IsNotPassengerChainData)
                .ToArray();
        }

        [HarmonyPatch(nameof(JobSaveManager.LoadJobSaveGameData))]
        [HarmonyPrefix]
        static void LoadPassengerChains( JobsSaveGameData saveData )
        {
            PJSaveLoadManager.InjectJobChains(saveData);
        }
    }
}
