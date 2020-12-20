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
                if( !SingletonBehaviour<IdGenerator>.Instance.carGuidToCar.TryGetValue(carGuids[i], out Car car) )
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
                if( !SingletonBehaviour<IdGenerator>.Instance.carGuidToCar.TryGetValue(guid, out Car car) || car == null )
                {
                    PrintError($"Couldn't find corresponding Car for carGuid:{guid}!");
                    return null;
                }

                if( !SingletonBehaviour<IdGenerator>.Instance.logicCarToTrainCar.TryGetValue(car, out TrainCar trainCar) || !(trainCar != null) )
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
            if( chainSaveData.jobChainData.Length < 1 )
            {
                return true;
            }

            JobChainController chainController = null;
            if( chainSaveData is PassengerChainSaveData passChainData )
            {
                chainController = CreateSavedJobChain(passChainData);
            }
            else
            {
                var firstJobData = chainSaveData.jobChainData.First();
                if( ((JobLicenses)firstJobData.requiredLicenses).HasFlag(PassLicenses.Passengers1) )
                {
                    DeleteOldChain(chainSaveData);
                }
                else return true; // pass to base game
            }

            if( chainController == null ) return false;

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
                    PrintError("Job from chain was taken, but there is no task data! Task data won't be loaded!");
                }

                InitializeCorrespondingJobBooklet(chainController.currentJobInChain);
            }

            PassengerJobs.ModEntry.Logger.Log($"Successfully loaded job chain: {chainController.jobChainGO.name}");

            __result = chainController.jobChainGO;
            return false;
        }

        private static void DeleteOldChain( JobChainSaveData chainSaveData )
        {
            var cars = GetTrainCarsFromCarGuids(chainSaveData.trainCarGuids);

            if( cars != null )
            {
                PassengerJobs.ModEntry.Logger.Log("Deleting pre v2.0 job chain");
                SingletonBehaviour<CarSpawner>.Instance.DeleteTrainCars(cars, true);
            }
        }

        private static JobChainController CreateSavedJobChain( PassengerChainSaveData passChainData )
        {
            // Figure out chain type
            PassengerChainSaveData.PassChainType chainType = passChainData.ChainType;

            if( InitializeCorrespondingJobBooklet == null )
            {
                PrintError("Failed to connect to JobSaveManager methods");
                return null;
            }

            List<TrainCar> trainCarsFromCarGuids = GetTrainCarsFromCarGuids(passChainData.trainCarGuids);
            if( trainCarsFromCarGuids == null )
            {
                PrintError("Couldn't find trainCarsForJobChain with trainCarGuids from chainSaveData! Skipping load of this job chain!");
                return null;
            }

            var jobChainGO = new GameObject();
            JobChainController chainController;

            if( chainType == PassengerChainSaveData.PassChainType.Transport )
            {
                // PASSENGER TRANSPORT (EXPRESS) CHAIN
                StaticJobDefinition jobDefinition;
                StationsChainData chainData = null;
                chainController = new PassengerTransportChainController(jobChainGO) { trainCarsForJobChain = trainCarsFromCarGuids };

                foreach( JobDefinitionDataBase jobData in passChainData.jobChainData )
                {

                    if( jobData is PassengerJobDefinitionData pjData )
                    {
                        jobDefinition = CreateSavedPassengerJob(jobChainGO, pjData);
                        jobDefinition?.ForceJobId(passChainData.firstJobId);
                        chainData = new StationsChainData(jobData.originStationId, jobData.destinationStationId);
                    }
                    else
                    {
                        PrintError("Express pax chain contains invalid job type");
                        return null;
                    }

                    if( jobDefinition == null )
                    {
                        PrintError("Failed to generate job definition from save data");
                        UnityEngine.Object.Destroy(jobChainGO);
                        return null;
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
                bool first = true;

                chainController = new CommuterChainController(jobChainGO) { trainCarsForJobChain = trainCarsFromCarGuids };

                foreach( JobDefinitionDataBase jobDataBase in passChainData.jobChainData )
                {
                    if( jobDataBase is PassengerJobDefinitionData jobData )
                    {
                        jobDefinition = CreateSavedPassengerJob(jobChainGO, jobData);

                        if( first )
                        {
                            chainData = new StationsChainData(jobData.originStationId, jobData.destinationStationId);
                            jobDefinition?.ForceJobId(passChainData.firstJobId);
                            first = false;
                        }
                    }
                    else
                    {
                        PrintError("Commuter chain contains invalid job type");
                        return null;
                    }

                    if( jobDefinition == null )
                    {
                        PrintError("Failed to generate job definition from save data");
                        UnityEngine.Object.Destroy(jobChainGO);
                        return null;
                    }

                    chainController.AddJobDefinitionToChain(jobDefinition);
                }

                jobChainGO.name = $"[LOADED] ChainJob[Commuter]: {chainData.chainOriginYardId} - {chainData.chainDestinationYardId}";
            }

            return chainController;
        }

        private static StaticPassengerJobDefinition CreateSavedPassengerJob( GameObject jobChainGO, PassengerJobDefinitionData jobData )
        {
            // associated station
            if( !(GetStationWithId(jobData.stationId) is Station logicStation) )
            {
                PrintError($"Couldn't find corresponding Station with ID: {jobData.stationId}! Skipping load of this job chain!");
                return null;
            }

            // bonus time limit, base payment
            if( jobData.timeLimitForJob < 0f || jobData.initialWage < 0f ||
                string.IsNullOrEmpty(jobData.originStationId) || string.IsNullOrEmpty(jobData.destinationStationId) )
            {
                PrintError("Invalid data! Skipping load of this job chain!");
                return null;
            }

            // license requirements
            if( !LicenseManager.IsValidForParsingToJobLicense(jobData.requiredLicenses) )
            {
                PrintError("Undefined job licenses requirement! Skipping load of this job chain!");
                return null;
            }

            // starting track
            if( !(GetYardTrackWithId(jobData.startingTrack) is Track startTrack) )
            {
                PrintError($"Couldn't find corresponding start Track with ID: {jobData.startingTrack}! Skipping load of this job chain!");
                return null;
            }

            // destination track
            if( !(GetYardTrackWithId(jobData.destinationTrack) is Track destTrack) )
            {
                PrintError($"Couldn't find corresponding destination Track with ID: {jobData.destinationTrack}! Skipping load of this job chain!");
                return null;
            }

            // consist
            if( !(GetCarsFromCarGuids(jobData.trainCarGuids) is List<Car> consist) )
            {
                PrintError("Couldn't find all carsToTransport with transportCarGuids! Skipping load of this job chain!");
                return null;
            }

            // loading platform
            WarehouseMachine loadMachine = null;
            if( !string.IsNullOrEmpty(jobData.loadingTrackId) )
            {
                if( PlatformManager.GetPlatformByTrackId(jobData.loadingTrackId) is PlatformDefinition pd )
                {
                    loadMachine = pd.Controller.LogicMachine;
                }
            }

            // unloading platform
            WarehouseMachine unloadMachine = null;
            if( !string.IsNullOrEmpty(jobData.unloadingTrackId) )
            {
                if( PlatformManager.GetPlatformByTrackId(jobData.unloadingTrackId) is PlatformDefinition pd )
                {
                    unloadMachine = pd.Controller.LogicMachine;
                }
            }

            StaticPassengerJobDefinition jobDefinition = jobChainGO.AddComponent<StaticPassengerJobDefinition>();
            var chainData = new StationsChainData(jobData.originStationId, jobData.destinationStationId);
            jobDefinition.PopulateBaseJobDefinition(logicStation, jobData.timeLimitForJob, jobData.initialWage, chainData, (JobLicenses)jobData.requiredLicenses);

            jobDefinition.subType = (JobType)jobData.subType;
            jobDefinition.startingTrack = startTrack;
            jobDefinition.destinationTrack = destTrack;
            jobDefinition.trainCarsToTransport = consist;
            jobDefinition.loadMachine = loadMachine;
            jobDefinition.unloadMachine = unloadMachine;
            
            return jobDefinition;
        }
    }
}
