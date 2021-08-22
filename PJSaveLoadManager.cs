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
        
        private const string HAS_LICENSE_P1_KEY = "pass1_obtained";
        private const string VERSION_KEY = "version";

        public const int CURRENT_DATA_VERSION = 2;

        private static JObject loadedData = null;

        private static IEnumerable<StationProceduralJobsController> ProceduralJobsControllers
        {
            get => StationController.allStations.Select(sc => sc.ProceduralJobsController);
        }

        public static void Save()
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

            try
            {
                if( !Directory.Exists(SaveDirectory) ) Directory.CreateDirectory(SaveDirectory);

                using( var outFile = File.CreateText(SaveFilePath) )
                {
                    using( var jtw = new JsonTextWriter(outFile) )
                    {
                        jtw.Formatting = Formatting.Indented;
                        pjSaveData.WriteTo(jtw);
                    }
                }
            }
            catch( Exception ex )
            {
                PassengerJobs.Error("Couldn't open/create save file:\n" + ex.Message);
                return;
            }
        }

        private static bool IsPassengerChain( JobChainController jobChain )
        {
            return (jobChain is PassengerTransportChainController) || (jobChain is CommuterChainController);
        }

        public static void Load( JobsSaveGameData mainJobData )
        {
            if( File.Exists(SaveFilePath) )
            {
                // load data off disk
                PassengerJobs.Log("Found save data file, attempting to load...");

                try
                {
                    using( var saveFile = File.OpenText(SaveFilePath) )
                    {
                        using( var jtr = new JsonTextReader(saveFile) )
                        {
                            loadedData = JToken.ReadFrom(jtr) as JObject;
                            if( loadedData == null )
                            {
                                PassengerJobs.Error("Save file contained invalid JSON");
                            }
                        }
                    }
                }
                catch( Exception ex )
                {
                    PassengerJobs.Error("Couldn't read save file:\n" + ex.Message);
                    return;
                }

                if( loadedData.GetInt(VERSION_KEY) == CURRENT_DATA_VERSION )
                {
                    if( loadedData.GetBool(HAS_LICENSE_P1_KEY) == true )
                    {
                        PassengerJobs.Log("Acquiring passengers license");
                        LicenseManager.AcquireJobLicense(PassLicenses.Passengers1);
                    }

                    // inject job chains into main game data
                    if( loadedData.GetObjectViaJSON<JobsSaveGameData>(SaveGameKeys.Jobs, JobSaveManager.serializeSettings) is JobsSaveGameData jobData )
                    {
                        JobChainSaveData[] combinedChains = mainJobData.jobChains
                            .Concat(jobData.jobChains)
                            .ToArray();

                        mainJobData.jobChains = combinedChains;
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
                // no save file exists
                PassengerJobs.Log("No save file found, skipping load");
            }
        }

        public static void CreateSaveBackup()
        {
            string dateString = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
            string backupFileName = string.Format(SAVE_BACKUP_FORMAT, dateString);
            string backupPath = Path.Combine(SaveDirectory, backupFileName);

            if( !File.Exists(SaveFilePath) || File.Exists(backupPath) )
            {
                PassengerJobs.Log("Skipping save backup, no existing save or backup already exists");
                return;
            }

            try
            {
                File.Copy(SaveFilePath, backupPath);
            }
            catch( Exception ex )
            {
                PassengerJobs.Error("Failed to create save file backup:\n" + ex.Message);
                return;
            }

            PassengerJobs.Log("Successfully backed up save data");
            DeleteOldBackups();
        }

        public static void DeleteOldBackups()
        {
            string saveDir = SaveDirectory;

            var oldBackups = Directory.EnumerateFiles(saveDir)
                .Where(name => name.StartsWith(SAVE_BACKUP_PREFIX))
                .Select(name => new FileInfo(Path.Combine(saveDir, name)))
                .OrderByDescending(file => file.CreationTime)
                .Skip(5);

            int deletedCount = 0;
            foreach( FileInfo file in oldBackups )
            {
                file.Delete();
                deletedCount++;
            }

            if( deletedCount > 0 )
            {
                PassengerJobs.Log($"Deleted {deletedCount} old passenger saves");
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

    [HarmonyPatch(typeof(SaveGameManager), nameof(SaveGameManager.Save))]
    static class SaveGameManager_Save_Patch
    {
        static void Postfix()
        {
            PJSaveLoadManager.Save();
        }
    }

    [HarmonyPatch(typeof(SaveGameManager), "MakeBackupFile")]
    static class SaveGameManager_MakeBackup_Patch
    {
        static void Postfix()
        {
            PJSaveLoadManager.CreateSaveBackup();
        }
    }

    // Prevent regular saving of passenger job chains
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
            PJSaveLoadManager.Load(saveData);
        }
    }
}
