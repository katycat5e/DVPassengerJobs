using DV.Logic.Job;
using DV.ThingTypes;
using PassengerJobs.Injectors;
using System;
using System.Collections.Generic;
using System.Linq;

namespace PassengerJobs.Generation
{
    public class ExpressJobDefinition : StaticJobDefinition
    {
        public ExpressStationsChainData? ExpressChainData
        {
            get => chainData as ExpressStationsChainData;
            set => chainData = value;
        }

        public List<Car>? TrainCarsToTransport = null!;
        public Track? StartingTrack = null!;
        public Track? ViaTrack = null!;
        public Track? DestinationTrack = null!;

        public override JobDefinitionDataBase GetJobDefinitionSaveData()
        {
            if (GetGuidsFromCars(TrainCarsToTransport) is not string[] guidsFromCars)
            {
                throw new Exception("Couldn't extract transportCarsGuids");
            }

            return new ExpressJobDefinitionData(
                timeLimitForJob, initialWage, logicStation.ID, ExpressChainData!.chainOriginYardId, ExpressChainData.chainViaYardId, ExpressChainData.chainDestinationYardId,
                (int)requiredLicenses, guidsFromCars, StartingTrack!.ID.FullID, ViaTrack!.ID.FullID, DestinationTrack!.ID.FullID);
        }

        public override List<TrackReservation> GetRequiredTrackReservations()
        {
            float reservedLength = CarSpawner.Instance.GetTotalCarsLength(TrainCarsToTransport, true);

            return new List<TrackReservation>
            {
                new TrackReservation(ViaTrack, reservedLength),
                new TrackReservation(DestinationTrack, reservedLength),
            };
        }

        public override void GenerateJob(Station jobOriginStation, float timeLimit = 0, float initialWage = 0, string? forcedJobId = null, JobLicenses requiredLicenses = JobLicenses.Basic)
        {
            if ((TrainCarsToTransport == null) || (TrainCarsToTransport.Count == 0) ||
                (StartingTrack == null) || (ViaTrack == null) || (DestinationTrack == null))
            {
                TrainCarsToTransport = null;
                StartingTrack = null;
                ViaTrack = null;
                DestinationTrack = null;

                PJMain.Warning("Failed to generate passengers job, bad data");
                return;
            }

            // Get total cargo capacity
            float totalCapacity = 0;
            foreach (var car in TrainCarsToTransport)
            {
                //car.DumpCargo();
                totalCapacity += car.capacity;
            }

            var taskList = new List<Task>();

            // actual move between stations
            Task firstLeg = JobsGenerator.CreateTransportTask(
                TrainCarsToTransport, ViaTrack, StartingTrack,
                Enumerable.Repeat(CargoInjector.PassengerCargo.v1, TrainCarsToTransport.Count).ToList());
            taskList.Add(firstLeg);

            Task secondLeg = JobsGenerator.CreateTransportTask(
                TrainCarsToTransport, DestinationTrack, ViaTrack,
                Enumerable.Repeat(CargoInjector.PassengerCargo.v1, TrainCarsToTransport.Count).ToList(), true);
            taskList.Add(secondLeg);

            var superTask = new SequentialTasks(taskList);
            job = new Job(superTask, PassJobType.Express, timeLimit, initialWage, chainData, forcedJobId, requiredLicenses);

            jobOriginStation.AddJobToStation(job);
        }
    }

    public class ExpressStationsChainData : StationsChainData
    {
        public string chainViaYardId;

        public ExpressStationsChainData(string chainOriginYardId, string chainViaYardId, string chainDestinationYardId) : base(chainOriginYardId, chainDestinationYardId)
        {
            this.chainViaYardId = chainViaYardId;
        }
    }

    public class ExpressJobDefinitionData : JobDefinitionDataBase
    {
        public string[] TrainCarGuids;
        public string viaStationId;
        public string startingTrack;
        public string viaTrack;
        public string destinationTrack;

        public ExpressJobDefinitionData(
            float timeLimitForJob, float initialWage, string stationId, string originStationId, string viaStationId, string destinationStationId, int requiredLicenses,
            string[] transportCarGuids, string startTrackId, string viaTrackId, string destTrackId) :
            base(timeLimitForJob, initialWage, stationId, originStationId, destinationStationId, requiredLicenses)
        {
            TrainCarGuids = transportCarGuids;
            this.viaStationId = viaStationId;
            startingTrack = startTrackId;
            viaTrack = viaTrackId;
            destinationTrack = destTrackId;
        }
    }
}
