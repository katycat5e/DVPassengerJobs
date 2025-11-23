using DV.InventorySystem;
using DV.JObjectExtstensions;
using DV.ThingTypes;
using DV.ThingTypes.TransitionHelpers;
using Newtonsoft.Json.Linq;
using PassengerJobs.Generation;
using System;
using System.Collections.Generic;
using System.Linq;
using VLB;
using static DV.RenderTextureSystem.BookletRender.VehicleCatalogPageTemplatePaper;

namespace PassengerJobs.Injectors
{
    public static class SaveDataInjector
    {
        private const string PJ_DATA_KEY = "passengers_mod";
        private const string HAS_LICENSE_P1_KEY = "pass1_obtained";
        private const string HAS_LICENSE_P2_KEY = "pass2_obtained";
        private const string VERSION_KEY = "version";

        public const int CURRENT_DATA_VERSION = 5;

        public static JObject? loadedData;

        private static readonly Dictionary<int, Action<SaveGameData>> SaveDataMigrations = new()
        {
            {4, MigrateV4ToV5}
        };

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
            pjSaveData.SetBool(HAS_LICENSE_P1_KEY, LicenseManager.Instance.IsJobLicenseAcquired(LicenseInjector.License1));
            pjSaveData.SetBool(HAS_LICENSE_P2_KEY, LicenseManager.Instance.IsJobLicenseAcquired(LicenseInjector.License2));

            pjSaveData.SetInt(VERSION_KEY, CURRENT_DATA_VERSION);

            // add to base game data
            mainGameData.SetJObject(PJ_DATA_KEY, pjSaveData);
        }

        public static void ExtractDataFromSaveGame(SaveGameData mainGameData)
        {
            loadedData = mainGameData.GetJObject(PJ_DATA_KEY);

            PJMain.Log($"Current Passenger Save Version: {loadedData.GetInt(VERSION_KEY)}");

            if (loadedData != null)
            {
                PJMain.Log("Found injected save data, attempting to load...");
                switch (loadedData.GetInt(VERSION_KEY))
                {
                    case null:
                        PJMain.Warning("Passenger save data version key not found");
                        loadedData = null;
                        break;
                    case int version when version != CURRENT_DATA_VERSION:
                        try
                        {
                            RunMigrations(version, mainGameData);
                            PJMain.Log("Passenger save data migrated successfully.");
                        }
                        catch (Exception ex)
                        {
                            PJMain.Warning($"Passenger save data migration failed: {ex.Message}");
                            loadedData = null;
                        }
                        break;
                }
            }
            else
            {
                PJMain.Log("No save data found");
            }
        }

        public static void MergePassengerChainData(JobsSaveGameData mainJobData)
        {
            try
            {
                if (loadedData?.GetObjectViaJSON<JobsSaveGameData>(SaveGameKeys.Jobs, JobSaveManager.serializeSettings) is JobsSaveGameData jobData)
                {
                    JobChainSaveData[] combinedChains = mainJobData.jobChains
                        .Concat(jobData.jobChains)
                        .ToArray();

                    mainJobData.jobChains = combinedChains;
                }
            }
            catch (Exception ex)
            {
                PJMain.Error("Failed to extract passenger jobs save data", ex);
            }
        }

        public static void AcquirePassengerLicense()
        {
            if ((loadedData != null) && (loadedData.GetBool(HAS_LICENSE_P1_KEY) == true) && Inventory.Instance)
            {
                PJMain.Log("Acquiring passengers1 license");
                LicenseManager.Instance.AcquireJobLicense(new[] { LicenseInjector.License1 });

                Inventory.Instance.RemoveMoney(LicenseInjector.License1Data.Cost);
            }
            if ((loadedData != null) && (loadedData.GetBool(HAS_LICENSE_P2_KEY) == true) && Inventory.Instance)
            {
                PJMain.Log("Acquiring passengers2 license");
                LicenseManager.Instance.AcquireJobLicense(new[] { LicenseInjector.License2 });

                Inventory.Instance.RemoveMoney(LicenseInjector.License2Data.Cost);
            }
        }

        private static void RunMigrations(int version, SaveGameData mainGameData)
        {
            if (version > CURRENT_DATA_VERSION)
                throw new Exception($"Save version {version} is newer than supported version {CURRENT_DATA_VERSION}");

            for (int i = version; i < CURRENT_DATA_VERSION; i++)
            {
                if (!SaveDataMigrations.TryGetValue(i, out var migrate))
                {
                    throw new Exception($"No migration path for v{i}");
                }
                PJMain.Log($"Migrating save data from v{version} to v{version + 1}");
                migrate(mainGameData);
                loadedData.SetInt(VERSION_KEY, version + 1);
            }
        }

        private static void MigrateV4ToV5(SaveGameData mainGameData)
        {
            const int fromVersion = 4;
            const int toVersion = 5;
            const float v4License1Cost = 100_000f;
            const float v5License1Cost = 40_000f;

            float refundAmount;
            bool hasLicense1 = loadedData.GetBool(HAS_LICENSE_P1_KEY) == true;
            bool hasFragileLicense = LicenseManager.Instance.IsJobLicenseAcquired(JobLicenses.Fragile.ToV2());

            if (!hasLicense1)
            { 
                return;
            }

            if (hasFragileLicense)
            {
                refundAmount = v4License1Cost - v5License1Cost;
                PJMain.Log($"Refunding ${refundAmount} for passengers 1 license due to cost reduction between v{fromVersion} and v{toVersion}");
            }
            else
            {
                refundAmount = v4License1Cost;
                loadedData.SetBool(HAS_LICENSE_P1_KEY, false);
                PJMain.Log($"Refunding ${refundAmount} for passengers 1 license due to new unmet license requirement in v{toVersion}");
            }

            float? money = mainGameData.GetFloat(SaveGameKeys.Player_money);
            if (money.HasValue)
            {
                float newBalance = money.Value + refundAmount;
                mainGameData.SetFloat(SaveGameKeys.Player_money, newBalance);
            }
        }
    }
}
