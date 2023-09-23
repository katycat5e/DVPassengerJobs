using DV.Logic.Job;
using HarmonyLib;
using PassengerJobs.Generation;
using PassengerJobs.Injectors;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace PassengerJobs.Patches
{
    [HarmonyPatch(typeof(JobSaveManager))]
    internal static class JobSaveManagerPatch
    {
        #region Loading

        [HarmonyPatch(nameof(JobSaveManager.LoadJobSaveGameData))]
        [HarmonyPrefix]
        public static void LoadJobSaveGameDataPrefix(JobsSaveGameData saveData)
        {
            SaveDataInjector.MergePassengerChainData(saveData);
        }

        [HarmonyPatch(nameof(JobSaveManager.LoadJobChain))]
        [HarmonyPrefix]
        public static bool LoadJobChainPrefix(JobChainSaveData chainSaveData, List<JobBooklet> jobBooklets, ref GameObject? __result)
        {
            if (chainSaveData == null)
            {
                __result = null;
                return false;
            }

            if ((chainSaveData.jobChainData.Length < 1) || (chainSaveData is not PassengerChainSaveData passChainData))
            {
                return true;
            }

            // instantiate chain
            var chainController = LoadPassengerChain(passChainData);
            if (chainController == null) return false;

            chainController.FinalizeSetupAndGenerateFirstJob(true);

            // pick up first job
            if (chainSaveData.jobTaken)
            {
                JobsManager.Instance.TakeJob(chainController.currentJobInChain, true);
                if (chainSaveData.currentJobTaskData != null)
                {
                    chainController.currentJobInChain.OverrideTasksStates(chainSaveData.currentJobTaskData);
                }
                else
                {
                    PJMain.Warning($"Job {chainController.currentJobInChain.ID} was taken, but there is no task data! Task data won't be loaded!");
                }

                JobSaveManager.Instance.InitializeCorrespondingJobBooklet(chainController.currentJobInChain, jobBooklets);
            }

            PJMain.Log($"Successfully loaded job chain: {chainController.jobChainGO.name}");
            __result = chainController.jobChainGO;
            return false;
        }

        public static PassengerChainController? LoadPassengerChain(PassengerChainSaveData passChainData)
        {
            var jobCars = JobSaveManager.Instance.GetTrainCarsFromCarGuids(passChainData.trainCarGuids);
            if (jobCars == null)
            {
                PJMain.Warning($"Couldn't find trainCarsForJobChain from chainSaveData {passChainData.firstJobId}! Skipping load of this job chain!");
                return null;
            }

            // create chain object
            var jobChainHolder = new GameObject();
            var chainController = new PassengerChainController(jobChainHolder)
            {
                trainCarsForJobChain = jobCars,
            };

            ExpressStationsChainData? chainData = null;

            // loop over job definitions & instantiate
            bool firstJob = true;
            foreach (var jobData in passChainData.jobChainData)
            {
                if (jobData is not ExpressJobDefinitionData expressJobSaveData)
                {
                    PJMain.Warning($"Non-passenger job in passenger chain, skipping");
                    continue;
                }

                if (LoadSavedExpressJob(jobChainHolder, expressJobSaveData) is ExpressJobDefinition definition)
                {
                    if (firstJob)
                    {
                        definition.ForceJobId(passChainData.firstJobId);
                        firstJob = false;
                    }
                    chainData = definition.ExpressChainData;
                    chainController.AddJobDefinitionToChain(definition);
                }
            }

            if (chainData == null)
            {
                PJMain.Warning("Failed to load any jobs from passenger chain");
                UnityEngine.Object.Destroy(jobChainHolder);
                return null;
            }

            jobChainHolder.name = $"[LOADED] ChainJob[Passenger]: {chainData.chainOriginYardId} - {chainData.chainViaYardId} - {chainData.chainDestinationYardId}";
            return chainController;
        }

        public static ExpressJobDefinition? LoadSavedExpressJob(GameObject jobChainHolder, ExpressJobDefinitionData jobData)
        {
            // associated station
            if (JobSaveManager.Instance.GetStationWithId(jobData.stationId) is not Station logicStation)
            {
                PJMain.Error($"Couldn't find corresponding Station with ID: {jobData.stationId}! Skipping load of this job chain!");
                return null;
            }

            // bonus time limit, base payment
            if (jobData.timeLimitForJob < 0f || jobData.initialWage < 0f ||
                string.IsNullOrEmpty(jobData.originStationId) || string.IsNullOrEmpty(jobData.destinationStationId))
            {
                PJMain.Error("Invalid data! Skipping load of this job chain!");
                return null;
            }

            // starting track
            if (JobSaveManager.Instance.GetYardTrackWithId(jobData.startingTrack) is not Track startTrack)
            {
                PJMain.Error($"Couldn't find corresponding start Track with ID: {jobData.startingTrack}! Skipping load of this job chain!");
                return null;
            }

            // via track
            if (JobSaveManager.Instance.GetYardTrackWithId(jobData.viaTrack) is not Track viaTrack)
            {
                PJMain.Error($"Couldn't find corresponding via Track with ID: {jobData.viaTrack}! Skipping load of this job chain!");
                return null;
            }

            // destination track
            if (JobSaveManager.Instance.GetYardTrackWithId(jobData.destinationTrack) is not Track destTrack)
            {
                PJMain.Error($"Couldn't find corresponding destination Track with ID: {jobData.destinationTrack}! Skipping load of this job chain!");
                return null;
            }

            // consist
            if (JobSaveManager.Instance.GetCarsFromCarGuids(jobData.TrainCarGuids) is not List<Car> consist)
            {
                PJMain.Error("Couldn't find all carsToTransport with transportCarGuids! Skipping load of this job chain!");
                return null;
            }

            ExpressJobDefinition jobDefinition = jobChainHolder.AddComponent<ExpressJobDefinition>();
            var chainData = new ExpressStationsChainData(jobData.originStationId, jobData.viaStationId, jobData.destinationStationId);
            jobDefinition.PopulateBaseJobDefinition(logicStation, jobData.timeLimitForJob, jobData.initialWage, chainData, LicenseInjector.License.v1);

            jobDefinition.StartingTrack = startTrack;
            jobDefinition.ViaTrack = viaTrack;
            jobDefinition.DestinationTrack = destTrack;
            jobDefinition.TrainCarsToTransport = consist;

            return jobDefinition;
        }

        #endregion

        #region Saving

        [HarmonyPatch(nameof(JobSaveManager.GetJobsSaveGameData))]
        [HarmonyPostfix]
        public static void GetJobsSaveGameDataPostfix(ref JobsSaveGameData __result)
        {
            // remove passenger job chains from main save data
            __result.jobChains = __result.jobChains
                .Where(c => c is not PassengerChainSaveData)
                .ToArray();
        }

        #endregion
    }
}
