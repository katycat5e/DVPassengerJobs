using DV.Logic.Job;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace PassengerJobsMod
{
    class StaticPassengerJobDefinition : StaticJobDefinition
    {
        public JobType subType;
        public List<Car> trainCarsToTransport;
        public Track startingTrack;
        public Track destinationTrack;

        public override JobDefinitionDataBase GetJobDefinitionSaveData()
        {
            string[] guidsFromCars = GetGuidsFromCars(trainCarsToTransport);
            if( guidsFromCars == null )
            {
                throw new Exception("Couldn't extract transportCarsGuids");
            }

            return new PassengerJobDefinitionData(
                timeLimitForJob, initialWage, logicStation.ID, chainData.chainOriginYardId, chainData.chainDestinationYardId, 
                (int)requiredLicenses, guidsFromCars, startingTrack.ID.FullID, destinationTrack.ID.FullID, (int)subType);
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
            }

            // Force cargo state
            foreach( var car in trainCarsToTransport )
            {
                car.DumpCargo();
                car.LoadCargo(car.capacity, CargoType.Passengers);
            }

            // Initialize tasks
            Task transportTask = JobsGenerator.CreateTransportTask(
                trainCarsToTransport, destinationTrack, startingTrack,
                Enumerable.Repeat(CargoType.Passengers, trainCarsToTransport.Count).ToList());

            job = new Job(transportTask, subType, jobTimeLimit, initialWage, chainData, forcedJobId, requiredLicenses);
            jobOriginStation.AddJobToStation(job);
        }
    }

    class PassengerJobDefinitionData : JobDefinitionDataBase
    {
        public int subType;
        public string[] trainCarGuids;
        public string startingTrack;
        public string destinationTrack;

        public PassengerJobDefinitionData( 
            float timeLimitForJob, float initialWage, string stationId, string originStationId, string destinationStationId, int requiredLicenses,
            string[] transportCarGuids, string startTrackId, string destTrackId, int jobType) :
            base(timeLimitForJob, initialWage, stationId, originStationId, destinationStationId, requiredLicenses)
        {
            trainCarGuids = transportCarGuids;
            startingTrack = startTrackId;
            destinationTrack = destTrackId;
            subType = jobType;
        }
    }
}
