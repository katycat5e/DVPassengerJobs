using System;
using System.Collections.Generic;
using System.Linq;
using DV.Logic.Job;

namespace PassengerJobsMod
{
    class StaticPassAssembleJobDefinition : StaticJobDefinition
    {
        public List<CarsPerTrack> carsPerStartingTrack;
        public Track destinationTrack;

        public override JobDefinitionDataBase GetJobDefinitionSaveData()
        {
            return new PassAssembleJobData(
                timeLimitForJob, initialWage,
                logicStation.ID, chainData.chainOriginYardId, chainData.chainDestinationYardId,
                (int)requiredLicenses, destinationTrack.ID.FullID, carsPerStartingTrack.Select(Extensions.GetIdData).ToArray());
        }

        public override List<TrackReservation> GetRequiredTrackReservations()
        {
            var allCars = carsPerStartingTrack.SelectMany(cpt => cpt.cars).ToList();

            float length = YardTracksOrganizer.Instance.GetTotalCarsLength(allCars);
            length += YardTracksOrganizer.Instance.GetSeparationLengthBetweenCars(allCars.Count);

            return new List<TrackReservation>()
            {
                new TrackReservation(destinationTrack, length)
            };
        }

        protected override void GenerateJob( Station jobOriginStation, float timeLimit = 0, float initialWage = 0, string forcedJobId = null, JobLicenses requiredLicenses = JobLicenses.Basic )
        {
            if( (carsPerStartingTrack == null) || (carsPerStartingTrack.Count == 0) )
            {
                carsPerStartingTrack = null;
                destinationTrack = null;
                job = null;
                PassengerJobs.ModEntry.Logger.Error("Passenger consist assemble job not created, null or empty car starting tracks");
                return;
            }

            if( destinationTrack == null )
            {
                carsPerStartingTrack = null;
                destinationTrack = null;
                job = null;
                PassengerJobs.ModEntry.Logger.Error("Passenger consist assemble job not created, no destination track given");
                return;
            }

            var collectionTasks = new List<Task>();
            foreach( CarsPerTrack cpt in carsPerStartingTrack )
            {
                // passengers are foreboden for shunting
                foreach( Car car in cpt.cars )
                {
                    car.DumpCargo();
                }
                collectionTasks.Add(JobsGenerator.CreateTransportTask(cpt.cars, destinationTrack, cpt.track));
            }

            job = new Job(new ParallelTasks(collectionTasks, 0), PassJobType.ConsistAssemble, timeLimit, initialWage, chainData, forcedJobId, requiredLicenses);

            jobOriginStation.AddJobToStation(job);
        }
    }

    class PassAssembleJobData : JobDefinitionDataBase
    {
        public PassAssembleJobData( float timeLimitForJob, float initialWage, string stationId, string originStationId, string destinationStationId, int requiredLicenses, string destTrackId, CarGuidsPerTrackId[] carsPerStartingTrack ) :
            base(timeLimitForJob, initialWage, stationId, originStationId, destinationStationId, requiredLicenses)
        {
            destinationTrackId = destTrackId;
            carGuidsPerTrackIds = carsPerStartingTrack;
        }

        public string destinationTrackId;
        public CarGuidsPerTrackId[] carGuidsPerTrackIds;
    }

    class StaticPassDissasembleJobDefinition : StaticJobDefinition
    {
        public Track startingTrack;
        public List<CarsPerTrack> carsPerDestinationTrack;

        public override JobDefinitionDataBase GetJobDefinitionSaveData()
        {
            return new PassDisassembleJobData(
                timeLimitForJob, initialWage, logicStation.ID,
                chainData.chainOriginYardId, chainData.chainDestinationYardId,
                (int)requiredLicenses, startingTrack.ID.FullID, carsPerDestinationTrack.Select(Extensions.GetIdData).ToArray());
        }

        public override List<TrackReservation> GetRequiredTrackReservations()
        {
            return carsPerDestinationTrack.Select(cpt =>
            {
                float length = YardTracksOrganizer.Instance.GetTotalCarsLength(cpt.cars);
                length += YardTracksOrganizer.Instance.GetSeparationLengthBetweenCars(cpt.cars.Count);
                return new TrackReservation(cpt.track, length);
            }).ToList();
        }

        protected override void GenerateJob( Station jobOriginStation, float timeLimit = 0, float initialWage = 0, string forcedJobId = null, JobLicenses requiredLicenses = JobLicenses.Basic )
        {
            if( (carsPerDestinationTrack == null) || (carsPerDestinationTrack.Count == 0) )
            {
                carsPerDestinationTrack = null;
                startingTrack = null;
                job = null;
                PassengerJobs.ModEntry.Logger.Error("Passenger consist disassemble job not created, null or empty car destination tracks");
                return;
            }

            if( startingTrack == null )
            {
                carsPerDestinationTrack = null;
                startingTrack = null;
                job = null;
                PassengerJobs.ModEntry.Logger.Error("Passenger consist disassemble job not created, no starting track given");
                return;
            }

            var depositTasks = new List<Task>();
            foreach( CarsPerTrack cpt in carsPerDestinationTrack )
            {
                // passengers are foreboden for shunting
                foreach( Car car in cpt.cars )
                {
                    car.DumpCargo();
                }
                depositTasks.Add(JobsGenerator.CreateTransportTask(cpt.cars, cpt.track, startingTrack));
            }

            job = new Job(new ParallelTasks(depositTasks), PassJobType.ConsistDissasemble, timeLimit, initialWage, chainData, forcedJobId, requiredLicenses);
            jobOriginStation.AddJobToStation(job);
        }
    }

    class PassDisassembleJobData : JobDefinitionDataBase
    {
        public PassDisassembleJobData( float timeLimitForJob, float initialWage, string stationId, string originStationId, string destinationStationId, int requiredLicenses, string startTrackId, CarGuidsPerTrackId[] carsPerDestinationTrack ) :
            base(timeLimitForJob, initialWage, stationId, originStationId, destinationStationId, requiredLicenses)
        {
            startingTrackId = startTrackId;
            carGuidsPerTrackIds = carsPerDestinationTrack;
        }

        public string startingTrackId;
        public CarGuidsPerTrackId[] carGuidsPerTrackIds;
    }
}
