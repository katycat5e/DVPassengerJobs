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
                PrintError("carGuids are null or empty!");
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
                PrintError("carGuids are null or empty!");
                return null;
            }

            var trainCars = new List<TrainCar>();

            foreach( string guid in carGuids )
            {
                if( !Car.carGuidToCar.TryGetValue(guid, out Car car) || car == null )
                {
                    PrintError($"Couldn't find corresponding Car for carGuid:{guid}!");
                    return null;
                }

                if( !TrainCar.logicCarToTrainCar.TryGetValue(car, out TrainCar trainCar) || !(trainCar != null) )
                {
                    PrintError($"Couldn't find corresponding TrainCar for Car: {car.ID} with carGuid:{guid}!");
                    return null;
                }

                trainCars.Add(trainCar);
            }

            return trainCars;
        }

        private static void PrintError( string message ) => PassengerJobs.ModEntry.Logger.Error(message);

        private delegate void InitJobBookletDelegate( Job job );
        private static readonly InitJobBookletDelegate InitializeCorrespondingJobBooklet =
            AccessTools.Method("JobSaveManager:InitializeCorrespondingJobBooklet")?.CreateDelegate(typeof(InitJobBookletDelegate)) as InitJobBookletDelegate;


        static bool Prefix( JobChainSaveData chainSaveData, ref GameObject __result )
        {
            if( (chainSaveData.jobChainData.Length < 1) || !(chainSaveData is PassengerChainSaveData passChainData) )
            {
                return true;
            }

            // Figure out chain type
            PassengerChainSaveData.PassChainType chainType = passChainData.ChainType;

            if( InitializeCorrespondingJobBooklet == null )
            {
                PrintError("Failed to connect to JobSaveManager methods");
                return false;
            }

            List<TrainCar> trainCarsFromCarGuids = GetTrainCarsFromCarGuids(chainSaveData.trainCarGuids);
            if( trainCarsFromCarGuids == null )
            {
                PrintError("Couldn't find trainCarsForJobChain with trainCarGuids from chainSaveData! Skipping load of this job chain!");
                return false;
            }

            var jobChainGO = new GameObject();
            JobChainController chainController;

            if( chainType == PassengerChainSaveData.PassChainType.Transport )
            {
                // Initialize chain controller
                chainController = new PassengerTransportChainController(jobChainGO) { trainCarsForJobChain = trainCarsFromCarGuids };
                StationsChainData chainData = null;
                bool firstJob = true;

                foreach( JobDefinitionDataBase jobData in chainSaveData.jobChainData )
                {
                    // Check base job definition data
                    if( !(GetStationWithId(jobData.stationId) is Station station) )
                    {
                        PrintError($"Couldn't find corresponding Station with ID: {jobData.stationId}! Skipping load of this job chain!");
                        UnityEngine.Object.Destroy(jobChainGO);
                        return false;
                    }

                    if( (jobData.timeLimitForJob < 0f) || (jobData.initialWage < 0f) ||
                        string.IsNullOrEmpty(jobData.originStationId) || string.IsNullOrEmpty(jobData.destinationStationId) )
                    {
                        PrintError("Invalid data! Skipping load of this job chain!");
                        UnityEngine.Object.Destroy(jobChainGO);
                        return false;
                    }

                    if( !LicenseManager.IsValidForParsingToJobLicense(jobData.requiredLicenses) )
                    {
                        PrintError("Undefined job licenses requirement! Skipping load of this job chain!");
                        UnityEngine.Object.Destroy(jobChainGO);
                        return false;
                    }


                    StaticJobDefinition jobDefinition;

                    if( jobData is PassengerJobDefinitionData pjData )
                    {
                        // PASSENGER TRANSPORT JOB
                        jobDefinition = CreatePassengerTransportJob(jobChainGO, pjData);
                        chainData = new ComplexChainData(pjData.originStationId, pjData.destinationYards);
                    }
                    else if( jobData is PassAssembleJobData pajData )
                    {
                        // CONSIST ASSEMBLY JOB
                        jobDefinition = CreateConsistAssemblyJob(jobChainGO, pajData);
                        chainData = new StationsChainData(jobData.originStationId, jobData.destinationStationId);
                    }
                    else if( jobData is PassDisassembleJobData pdjData )
                    {
                        jobDefinition = CreateConsistBreakupJob(jobChainGO, pdjData);
                        chainData = new StationsChainData(jobData.originStationId, jobData.destinationStationId);
                    }
                    else
                    {
                        PrintError("Invalid job definition data type");
                        return false;
                    }

                    if( jobDefinition == null )
                    {
                        PrintError("Failed to generate job definition from save data");
                        UnityEngine.Object.Destroy(jobChainGO);
                        return false;
                    }

                    jobDefinition.PopulateBaseJobDefinition(station, jobData.timeLimitForJob, jobData.initialWage, chainData, (JobLicenses)jobData.requiredLicenses);

                    if( firstJob )
                    {
                        firstJob = false;
                        jobDefinition.ForceJobId(chainSaveData.firstJobId);
                    }

                    chainController.AddJobDefinitionToChain(jobDefinition);
                }

                jobChainGO.name = $"[LOADED] ChainJob[Passenger]: {chainData.chainOriginYardId} - {chainData.chainDestinationYardId}";
            }
            else
            {
                // COMMUTER JOB CHAIN
                StaticJobDefinition jobDefinition;
                StationsChainData chainData = null;

                chainController = new CommuterChainController(jobChainGO) { trainCarsForJobChain = trainCarsFromCarGuids };

                foreach( JobDefinitionDataBase jobDataBase in chainSaveData.jobChainData )
                {
                    if( jobDataBase is TransportJobDefinitionData jobData )
                    {
                        jobDefinition = CreateCommuterJob(jobChainGO, jobData);
                        jobDefinition.ForceJobId(chainSaveData.firstJobId);
                        chainData = new StationsChainData(jobData.originStationId, jobData.destinationStationId);
                    }
                    else
                    {
                        PrintError("Commuter chain contains invalid job type");
                        return false;
                    }

                    if( jobDefinition == null )
                    {
                        PrintError("Failed to generate job definition from save data");
                        UnityEngine.Object.Destroy(jobChainGO);
                        return false;
                    }

                    chainController.AddJobDefinitionToChain(jobDefinition);
                }

                jobChainGO.name = $"[LOADED] ChainJob[Commuter]: {chainData.chainOriginYardId} - {chainData.chainDestinationYardId}";
            }

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

        private static StaticPassengerJobDefinition CreatePassengerTransportJob( GameObject jobChainGO, PassengerJobDefinitionData jobData )
        {
            if( (jobData.destinationYards == null) || (jobData.destinationTracks == null) )
            {
                PrintError("Undefined job destination data");
                return null;
            }

            if( !(GetYardTrackWithId(jobData.startingTrack) is Track startTrack) )
            {
                PrintError($"Couldn't find corresponding start Track with ID: {jobData.startingTrack}! Skipping load of this job chain!");
                return null;
            }

            Track[] destTracks = jobData.destinationTracks?.Select(id => GetYardTrackWithId(id)).ToArray();
            if( (destTracks == null) || destTracks.Any(track => track == null) )
            {
                PrintError($"Couldn't find corresponding destination Track with ID: {string.Join<Track>(",", destTracks)}! Skipping load of this job chain!");
                return null;
            }

            string[] destYards = jobData.destinationYards;
            if( (destYards == null) || destYards.Any(yard => yard == null) )
            {
                PrintError($"Invalid destination yard IDs: {string.Join(",", destYards)}! Skipping load of this job chain!");
                return null;
            }

            if( !(GetCarsFromCarGuids(jobData.trainCarGuids) is List<Car> consist) )
            {
                PrintError("Couldn't find all carsToTransport with transportCarGuids! Skipping load of this job chain!");
                return null;
            }

            StaticPassengerJobDefinition jobDefinition = jobChainGO.AddComponent<StaticPassengerJobDefinition>();

            jobDefinition.startingTrack = startTrack;
            jobDefinition.destinationTracks = destTracks;
            jobDefinition.destinationYards = destYards;
            jobDefinition.trainCarsToTransport = consist;

            return jobDefinition;
        }

        private static StaticPassAssembleJobDefinition CreateConsistAssemblyJob( GameObject jobChainGO, PassAssembleJobData jobData )
        {
            if( !(GetYardTrackWithId(jobData.destinationTrackId) is Track destTrack) )
            {
                PrintError($"Couldn't find corresponding destination Track with ID: {jobData.destinationTrackId}! Skipping load of this job chain!");
                return null;
            }

            List<CarsPerTrack> startingCars;
            try
            {
                startingCars = jobData.carGuidsPerTrackIds.Select(Extensions.GetCarTracksByIds).ToList();
            }
            catch( ArgumentException ex )
            {
                PrintError($"Couldn't find starting tracks/cars: {ex.Message}. Skipping load of this job chain!");
                return null;
            }

            StaticPassAssembleJobDefinition jobDefinition = jobChainGO.AddComponent<StaticPassAssembleJobDefinition>();

            jobDefinition.carsPerStartingTrack = startingCars;
            jobDefinition.destinationTrack = destTrack;

            return jobDefinition;
        }

        private static StaticPassDissasembleJobDefinition CreateConsistBreakupJob( GameObject jobChainGO, PassDisassembleJobData jobData )
        {
            if( !(GetYardTrackWithId(jobData.startingTrackId) is Track startTrack) )
            {
                PrintError($"Couldn't find corresponding destination Track with ID: {jobData.startingTrackId}! Skipping load of this job chain!");
                return null;
            }

            List<CarsPerTrack> endingCars;
            try
            {
                endingCars = jobData.carGuidsPerTrackIds.Select(Extensions.GetCarTracksByIds).ToList();
            }
            catch( ArgumentException ex )
            {
                PrintError($"Couldn't find starting tracks/cars: {ex.Message}. Skipping load of this job chain!");
                return null;
            }

            StaticPassDissasembleJobDefinition jobDefinition = jobChainGO.AddComponent<StaticPassDissasembleJobDefinition>();
            jobDefinition.chainData = new StationsChainData(jobData.originStationId, jobData.destinationStationId);

            jobDefinition.carsPerDestinationTrack = endingCars;
            jobDefinition.startingTrack = startTrack;

            return jobDefinition;
        }

        private static StaticTransportJobDefinition CreateCommuterJob( GameObject jobChainGO, TransportJobDefinitionData jobData )
        {
            if( !(GetStationWithId(jobData.stationId) is Station logicStation) )
            {
                PrintError($"Couldn't find corresponding Station with ID: {jobData.stationId}! Skipping load of this job chain!");
                return null;
            }

            if( jobData.timeLimitForJob < 0f || jobData.initialWage < 0f || 
                string.IsNullOrEmpty(jobData.originStationId) || string.IsNullOrEmpty(jobData.destinationStationId) )
            {
                PrintError("Invalid data! Skipping load of this job chain!");
                return null;
            }

            if( !LicenseManager.IsValidForParsingToJobLicense(jobData.requiredLicenses) )
            {
                PrintError("Undefined job licenses requirement! Skipping load of this job chain!");
                return null;
            }

            if( !(GetYardTrackWithId(jobData.startTrackId) is Track startTrack) )
            {
                PrintError($"Couldn't find corresponding start Track with ID: {jobData.startTrackId}! Skipping load of this job chain!");
                return null;
            }

            if( !(GetYardTrackWithId(jobData.destinationTrackId) is Track destTrack) )
            {
                PrintError($"Couldn't find corresponding destination Track with ID: {jobData.destinationTrackId}! Skipping load of this job chain!");
                return null;
            }

            if( !(GetCarsFromCarGuids(jobData.transportCarGuids) is List<Car> cars) )
            {
                PrintError("Couldn't find all carsToTransport with transportCarGuids! Skipping load of this job chain!");
                return null;
            }

            if( jobData.transportedCargoPerCar.Length != cars.Count || jobData.cargoAmountPerCar.Length != cars.Count )
            {
                Debug.LogError("Unmatching number of carsToTransport and transportedCargoPerCar or cargoAmountPerCar! Skipping load of this job chain!");
                return null;
            }

            var jobDefinition = jobChainGO.AddComponent<StaticTransportJobDefinition>();

            var chainData = new StationsChainData(jobData.originStationId, jobData.destinationStationId);
            jobDefinition.PopulateBaseJobDefinition(logicStation, jobData.timeLimitForJob, jobData.initialWage, chainData, (JobLicenses)jobData.requiredLicenses);

            jobDefinition.startingTrack = startTrack;
            jobDefinition.trainCarsToTransport = cars;
            jobDefinition.transportedCargoPerCar = jobData.transportedCargoPerCar.ToList();
            jobDefinition.cargoAmountPerCar = jobData.cargoAmountPerCar.ToList();
            jobDefinition.destinationTrack = destTrack;
            jobDefinition.forceCorrectCargoStateOnCars = true;

            return jobDefinition;
        }
    }
}
