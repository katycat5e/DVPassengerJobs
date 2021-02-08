using DV.Logic.Job;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace PassengerJobsMod
{
    class StaticPassengerJobDefinition : StaticJobDefinition
    {
        public JobType subType = PassJobType.Commuter;
        public List<Car> trainCarsToTransport;
        public Track startingTrack;
        public Track destinationTrack;

        public WarehouseMachine loadMachine = null;
        public WarehouseMachine unloadMachine = null;

        public SpecialTrain specialDefinition = null;

        public override JobDefinitionDataBase GetJobDefinitionSaveData()
        {
            string[] guidsFromCars = GetGuidsFromCars(trainCarsToTransport);
            if( guidsFromCars == null )
            {
                throw new Exception("Couldn't extract transportCarsGuids");
            }

            return new PassengerJobDefinitionData(
                timeLimitForJob, initialWage, logicStation.ID, chainData.chainOriginYardId, chainData.chainDestinationYardId, 
                (int)requiredLicenses, guidsFromCars, startingTrack.ID.FullID, destinationTrack.ID.FullID, (int)subType,
                loadMachine?.WarehouseTrack.ID.FullID, unloadMachine?.WarehouseTrack.ID.FullID, specialDefinition?.Name);
        }

        public override List<TrackReservation> GetRequiredTrackReservations()
        {
            float reservedLength =
                YardTracksOrganizer.Instance.GetTotalCarsLength(trainCarsToTransport) + 
                YardTracksOrganizer.Instance.GetSeparationLengthBetweenCars(trainCarsToTransport.Count);
            
            return new List<TrackReservation>
            {
                new TrackReservation(destinationTrack, reservedLength)
            };
        }

        protected override void GenerateJob( Station jobOriginStation, float jobTimeLimit = 0, float initialWage = 0, string forcedJobId = null, JobLicenses requiredLicenses = JobLicenses.Basic )
        {
            if( (trainCarsToTransport == null) || (trainCarsToTransport.Count == 0) ||
                (startingTrack == null) || (destinationTrack == null) )
            {
                trainCarsToTransport = null;
                startingTrack = null;
                destinationTrack = null;
                return;
            }

            // Get total cargo capacity
            float totalCapacity = 0;
            foreach( var car in trainCarsToTransport )
            {
                //car.DumpCargo();
                totalCapacity += car.capacity;
            }

            Track departTrack = startingTrack;
            Track arriveTrack = destinationTrack;
            var taskList = new List<Task>();

            // Check for loading task
            PlatformController loadPlatform = null;
            if( loadMachine != null )
            {
                departTrack = loadMachine.WarehouseTrack;

                Task stageCarsTask = JobsGenerator.CreateTransportTask(trainCarsToTransport, departTrack, startingTrack);
                taskList.Add(stageCarsTask);

                Task loadTask = new WarehouseTask(trainCarsToTransport, WarehouseTaskType.Loading, loadMachine, CargoType.Passengers, totalCapacity);
                taskList.Add(loadTask);

                // check to register for unloading display
                if( (PlatformManager.GetPlatformByTrackId(loadMachine.WarehouseTrack.ID.FullID) is PlatformDefinition pdef) && pdef.Initialized )
                {
                    loadPlatform = pdef.Controller;
                }
            }
            else
            {
                foreach( var car in trainCarsToTransport )
                {
                    car.LoadCargo(car.capacity, CargoType.Passengers);
                }
            }

            if( unloadMachine != null ) arriveTrack = unloadMachine.WarehouseTrack;

            // actual move between stations
            Task transportTask = JobsGenerator.CreateTransportTask(
                trainCarsToTransport, arriveTrack, departTrack,
                Enumerable.Repeat(CargoType.Passengers, trainCarsToTransport.Count).ToList());

            taskList.Add(transportTask);

            // check for unloading task
            PlatformController unloadPlatform = null;
            if( unloadMachine != null )
            {
                Task unloadTask = new WarehouseTask(trainCarsToTransport, WarehouseTaskType.Unloading, unloadMachine, CargoType.Passengers, totalCapacity);
                taskList.Add(unloadTask);

                Task storeCarsTask = JobsGenerator.CreateTransportTask(trainCarsToTransport, destinationTrack, arriveTrack);
                taskList.Add(storeCarsTask);

                // check to register for unloading display
                if( (PlatformManager.GetPlatformByTrackId(unloadMachine.WarehouseTrack.ID.FullID) is PlatformDefinition pdef) && pdef.Initialized )
                {
                    unloadPlatform = pdef.Controller;
                }
            }

            Task superTask = new SequentialTasks(taskList);

            // check if we should generate a special job ID
            if( string.IsNullOrEmpty(forcedJobId) && (specialDefinition != null) )
            {
                forcedJobId = IG_GenerateJobId_Patch.GetNamedExpressId(specialDefinition);
            }

            job = new Job(superTask, subType, jobTimeLimit, initialWage, chainData, forcedJobId, requiredLicenses);

            // set up platform displays
            if( loadPlatform != null ) loadPlatform.AddOutgoingJobToDisplay(job);
            if( unloadPlatform != null ) job.JobTaken += unloadPlatform.AddIncomingJobToDisplay;

            // track the job if it's a special, for booklet info etc
            if( specialDefinition != null )
            {
                SpecialConsistManager.JobToSpecialMap.Add(job.ID, specialDefinition);
            }

            jobOriginStation.AddJobToStation(job);
        }
    }

    class PassengerJobDefinitionData : JobDefinitionDataBase
    {
        public int subType;
        public string[] trainCarGuids;
        public string startingTrack;
        public string destinationTrack;

        public string loadingTrackId;
        public string unloadingTrackId;

        public string specialName;

        public PassengerJobDefinitionData( 
            float timeLimitForJob, float initialWage, string stationId, string originStationId, string destinationStationId, int requiredLicenses,
            string[] transportCarGuids, string startTrackId, string destTrackId, int jobType, string loadTrack, string unloadTrack, string special) :
            base(timeLimitForJob, initialWage, stationId, originStationId, destinationStationId, requiredLicenses)
        {
            trainCarGuids = transportCarGuids;
            startingTrack = startTrackId;
            destinationTrack = destTrackId;
            subType = jobType;

            loadingTrackId = loadTrack;
            unloadingTrackId = unloadTrack;

            specialName = special;
        }
    }
}
