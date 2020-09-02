using System;
using System.Collections.Generic;
using System.Linq;
using DV.Logic.Job;
using Harmony12;
using UnityEngine;

namespace PassengerJobsMod
{
    [HarmonyPatch(typeof(JobSaveManager), "LoadJobChain")]
    static class JSM_LoadJobChain_Patch
    {
        private static Station GetStationWithId( string stationId )
        {
            if( SingletonBehaviour<LogicController>.Exists && 
                SingletonBehaviour<LogicController>.Instance.YardIdToStationController.TryGetValue(stationId, out StationController stationController) )
            {
                return stationController?.logicStation;
            }
            return null;
        }

        private static Track GetYardTrackWithId( string trackId )
        {
            if( YardTracksOrganizer.Instance.yardTrackIdToTrack.TryGetValue(trackId, out Track track) )
            {
                return track;
            }
            return null;
        }

        private static List<Car> GetCarsFromCarGuids( string[] carGuids )
        {
            if( carGuids == null || carGuids.Length == 0 )
            {
                PassengerJobs.ModEntry.Logger.Error("carGuids are null or empty!");
                return null;
            }

            var result = new List<Car>();
            for( int i = 0; i < carGuids.Length; i++ )
            {
                if( !Car.carGuidToCar.TryGetValue(carGuids[i], out Car car) )
                {
                    Debug.LogError("Couldn't find corresponding Car for carGuid:" + carGuids[i] + "!");
                    return null;
                }
                result.Add(car);
            }
            return result;
        }

        private static List<TrainCar> GetTrainCarsFromCarGuids( string[] carGuids )
        {
            if( carGuids == null || carGuids.Length == 0 )
            {
                PassengerJobs.ModEntry.Logger.Error("carGuids are null or empty!");
                return null;
            }

            var trainCars = new List<TrainCar>();

            foreach( string guid in carGuids )
            {
                if( !Car.carGuidToCar.TryGetValue(guid, out Car car) || car == null )
                {
                    PassengerJobs.ModEntry.Logger.Error($"Couldn't find corresponding Car for carGuid:{guid}!");
                    return null;
                }

                if( !TrainCar.logicCarToTrainCar.TryGetValue(car, out TrainCar trainCar) || !(trainCar != null) )
                {
                    PassengerJobs.ModEntry.Logger.Error($"Couldn't find corresponding TrainCar for Car: {car.ID} with carGuid:{guid}!");
                    return null;
                }

                trainCars.Add(trainCar);
            }

            return trainCars;
        }

        private delegate void InitJobBookletDelegate( Job job );
        private static readonly InitJobBookletDelegate InitializeCorrespondingJobBooklet =
            AccessTools.Method("JobSaveManager:InitializeCorrespondingJobBooklet")?.CreateDelegate(typeof(InitJobBookletDelegate)) as InitJobBookletDelegate;

        static bool Prefix( JobChainSaveData chainSaveData, ref GameObject __result )
        {
            if( (chainSaveData.jobChainData.Length < 1) || !(chainSaveData.jobChainData[0] is PassengerJobDefinitionData jobData) )
            {
                return true;
            }

            if( InitializeCorrespondingJobBooklet == null )
            {
                PassengerJobs.ModEntry.Logger.Error("Failed to connect to JobSaveManager methods");
                return false;
            }

            List<TrainCar> trainCarsFromCarGuids = GetTrainCarsFromCarGuids(chainSaveData.trainCarGuids);
            if( trainCarsFromCarGuids == null )
            {
                PassengerJobs.ModEntry.Logger.Error("Couldn't find trainCarsForJobChain with trainCarGuids from chainSaveData! Skipping load of this job chain!");
                return false;
            }

            var jobChainGO = new GameObject();

            var jobDefinition = jobChainGO.AddComponent<StaticPassengerJobDefinition>();
            jobDefinition.ForceJobId(chainSaveData.firstJobId);

            // Populate base job definition
            if( !(GetStationWithId(jobData.stationId) is Station station) )
            {
                PassengerJobs.ModEntry.Logger.Error($"Couldn't find corresponding Station with ID: {jobData.stationId}! Skipping load of this job chain!");
                UnityEngine.Object.Destroy(jobChainGO);
                return false;
            }

            if( (jobData.timeLimitForJob < 0f) || (jobData.initialWage < 0f) || 
                string.IsNullOrEmpty(jobData.originStationId) || string.IsNullOrEmpty(jobData.destinationStationId) )
            {
                PassengerJobs.ModEntry.Logger.Error("Invalid data! Skipping load of this job chain!");
                UnityEngine.Object.Destroy(jobChainGO);
                return false;
            }

            if( !LicenseManager.IsValidForParsingToJobLicense(jobData.requiredLicenses) )
            {
                PassengerJobs.ModEntry.Logger.Error("Undefined job licenses requirement! Skipping load of this job chain!");
                UnityEngine.Object.Destroy(jobChainGO);
                return false;
            }

            if( (jobData.destinationYards == null) || (jobData.destinationTracks == null) )
            {
                PassengerJobs.ModEntry.Logger.Error("Undefined job destination data");
                UnityEngine.Object.Destroy(jobChainGO);
                return false;
            }

            var chainData = new ComplexChainData(jobData.originStationId, jobData.destinationYards);
            jobDefinition.PopulateBaseJobDefinition(station, jobData.timeLimitForJob, jobData.initialWage, chainData, (JobLicenses)jobData.requiredLicenses);

            // Populate extended job definition
            if( !(GetYardTrackWithId(jobData.startingTrack) is Track startTrack) )
            {
                PassengerJobs.ModEntry.Logger.Error($"Couldn't find corresponding start Track with ID: {jobData.startingTrack}! Skipping load of this job chain!");
                UnityEngine.Object.Destroy(jobChainGO);
                return false;
            }

            Track[] destTracks = jobData.destinationTracks?.Select(id => GetYardTrackWithId(id)).ToArray();
            if( (destTracks == null) || destTracks.Any(track => track == null) )
            {
                PassengerJobs.ModEntry.Logger.Error($"Couldn't find corresponding destination Track with ID: {string.Join<Track>(",", destTracks)}! Skipping load of this job chain!");
                UnityEngine.Object.Destroy(jobChainGO);
                return false;
            }

            string[] destYards = jobData.destinationYards;
            if( (destYards == null) || destYards.Any(yard => yard == null) )
            {
                PassengerJobs.ModEntry.Logger.Error($"Invalid destination yard IDs: {string.Join(",", destYards)}! Skipping load of this job chain!");
                UnityEngine.Object.Destroy(jobChainGO);
                return false;
            }

            if( !(GetCarsFromCarGuids(jobData.trainCarGuids) is List<Car> consist) )
            {
                PassengerJobs.ModEntry.Logger.Error("Couldn't find all carsToTransport with transportCarGuids! Skipping load of this job chain!");
                UnityEngine.Object.Destroy(jobChainGO);
                return false;
            }

            jobDefinition.startingTrack = startTrack;
            jobDefinition.destinationTracks = destTracks;
            jobDefinition.destinationYards = destYards;
            jobDefinition.trainCarsToTransport = consist;

            // Initialize chain controller
            var chainController = new PassengerTransportChainController(jobChainGO) { trainCarsForJobChain = trainCarsFromCarGuids };
            chainController.AddJobDefinitionToChain(jobDefinition);

            jobChainGO.name = $"[LOADED] ChainJob[Passenger]: {chainData.chainOriginYardId} - {chainData.chainDestinationYardId}";
            chainController.FinalizeSetupAndGenerateFirstJob(true);

            if( chainSaveData.jobTaken )
            {
                PlayerJobs.Instance.TakeJob(chainController.currentJobInChain, true);
                if( chainSaveData.currentJobTaskData != null )
                {
                    chainController.currentJobInChain.OverrideTasksStates(chainSaveData.currentJobTaskData);
                }
                else
                {
                    Debug.LogError("Job from chain was taken, but there is no task data! Task data won't be loaded!");
                }

                InitializeCorrespondingJobBooklet(chainController.currentJobInChain);
            }

            __result = chainController.jobChainGO;
            return false;
        }
    }
}
