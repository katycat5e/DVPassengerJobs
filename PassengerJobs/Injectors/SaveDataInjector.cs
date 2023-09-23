using DV.InventorySystem;
using DV.JObjectExtstensions;
using Newtonsoft.Json.Linq;
using PassengerJobs.Generation;
using System.Collections.Generic;
using System.Linq;

namespace PassengerJobs.Injectors
{
    public static class SaveDataInjector
    {
        private const string PJ_DATA_KEY = "passengers_mod";
        private const string HAS_LICENSE_P1_KEY = "pass1_obtained";
        private const string VERSION_KEY = "version";

        public const int CURRENT_DATA_VERSION = 4;

        public static JObject? loadedData;

        private static IEnumerable<StationProceduralJobsController> ProceduralJobsControllers
        {
            get => StationController.allStations.Select(sc => sc.ProceduralJobsController);
        }
        
        public static void InjectDataIntoSaveGame(SaveGameData mainGameData)
        {
            var pjSaveData = new JObject();

            // jobs
            JobChainSaveData[] chainData = ProceduralJobsControllers
                .SelectMany(controller => controller.GetCurrentJobChains())
                .OfType<PassengerChainController>()
                .Select(chain => chain.GetJobChainSaveData())
                .ToArray();

            var jobsData = new JobsSaveGameData(chainData, 0);
            pjSaveData.SetObjectViaJSON(SaveGameKeys.Jobs, jobsData, JobSaveManager.serializeSettings);

            // licenses
            pjSaveData.SetBool(HAS_LICENSE_P1_KEY, LicenseManager.Instance.IsJobLicenseAcquired(LicenseInjector.License));

            pjSaveData.SetInt(VERSION_KEY, CURRENT_DATA_VERSION);

            // add to base game data
            mainGameData.SetJObject(PJ_DATA_KEY, pjSaveData);
        }

        public static void ExtractDataFromSaveGame(SaveGameData mainGameData)
        {
            loadedData = mainGameData.GetJObject(PJ_DATA_KEY);

            if (loadedData != null)
            {
                PJMain.Log("Found injected save data, attempting to load...");
                if (loadedData.GetInt(VERSION_KEY) != CURRENT_DATA_VERSION)
                {
                    PJMain.Warning("Save file contains incompatible data version");
                    loadedData = null;
                }
            }
            else
            {
                PJMain.Log("No save data found");
            }
        }

        public static void MergePassengerChainData(JobsSaveGameData mainJobData)
        {
            if (loadedData?.GetObjectViaJSON<JobsSaveGameData>(SaveGameKeys.Jobs, JobSaveManager.serializeSettings) is JobsSaveGameData jobData)
            {
                JobChainSaveData[] combinedChains = mainJobData.jobChains
                    .Concat(jobData.jobChains)
                    .ToArray();

                mainJobData.jobChains = combinedChains;
            }
        }

        public static void AcquirePassengerLicense()
        {
            if ((loadedData != null) && (loadedData.GetBool(HAS_LICENSE_P1_KEY) == true) && Inventory.Instance)
            {
                PJMain.Log("Acquiring passengers license");
                LicenseManager.Instance.AcquireJobLicense(new[] { LicenseInjector.License });

                Inventory.Instance.RemoveMoney(LicenseData.Cost);
            }
        }
    }
}
