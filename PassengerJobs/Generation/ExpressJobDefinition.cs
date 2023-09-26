using DV.Logic.Job;
using DV.ThingTypes;
using PassengerJobs.Injectors;
using PassengerJobs.Platforms;
using System;
using System.Collections;
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

        private List<Car>? _cars;
        public List<Car>? TrainCarsToTransport
        {
            get => _cars;
            set
            {
                _cars = value;
                _cargoList = (_cars != null) ? Enumerable.Repeat(CargoInjector.PassengerCargo.v1, _cars.Count).ToList() : null;
            }
        }

        private List<CargoType>? _cargoList = null;
        public Track? StartingTrack = null;
        public Track[]? DestinationTracks = null;

        public override JobDefinitionDataBase GetJobDefinitionSaveData()
        {
            if (GetGuidsFromCars(TrainCarsToTransport) is not string[] guidsFromCars)
            {
                throw new Exception("Couldn't extract transportCarsGuids");
            }

            return new ExpressJobDefinitionData(
                timeLimitForJob, initialWage, logicStation.ID, ExpressChainData!.chainOriginYardId, ExpressChainData.destinationYardIds,
                (int)requiredLicenses, guidsFromCars, StartingTrack!.ID.FullID, DestinationTracks!.Select(t => t.ID.FullID).ToArray());
        }

        public override List<TrackReservation> GetRequiredTrackReservations()
        {
            float reservedLength = CarSpawner.Instance.GetTotalCarsLength(TrainCarsToTransport, true);

            return DestinationTracks.Select(t => new TrackReservation(t, reservedLength)).ToList();
        }

        public override void GenerateJob(Station jobOriginStation, float timeLimit = 0, float initialWage = 0, string? forcedJobId = null, JobLicenses requiredLicenses = JobLicenses.Basic)
        {
            if ((TrainCarsToTransport == null) || (TrainCarsToTransport.Count == 0) ||
                (StartingTrack == null) || (DestinationTracks == null))
            {
                TrainCarsToTransport = null;
                StartingTrack = null;
                DestinationTracks = null;

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
            var sourceTrack = StartingTrack;
            for (int i = 0; i < DestinationTracks.Length; i++)
            {
                bool isLast = (i == (DestinationTracks.Length - 1));
                var leg = CreateTransportLeg(sourceTrack, DestinationTracks[i]);
                taskList.Add(leg);

                var loadTask = CreateBoardingTask(DestinationTracks[i], totalCapacity, isLast);
                taskList.Add(loadTask);

                sourceTrack = DestinationTracks[i];
            }

            var superTask = new SequentialTasks(taskList);
            job = new Job(superTask, PassJobType.Express, timeLimit, initialWage, chainData, forcedJobId, requiredLicenses);

            // add to signs along route
            PlatformController.GetControllerForTrack(StartingTrack).AddOutgoingJobToSigns(job);
            for (int i = 0; i < DestinationTracks.Length - 1; i++)
            {
                job.JobTaken += PlatformController.GetControllerForTrack(DestinationTracks[i]).AddOutgoingJobToSigns;
            }
            job.JobTaken += PlatformController.GetControllerForTrack(DestinationTracks.Last()).AddIncomingJobToSigns;

            jobOriginStation.AddJobToStation(job);
        }

        private Task CreateTransportLeg(Track sourceTrack, Track destinationTrack)
        {
            return JobsGenerator.CreateTransportTask(TrainCarsToTransport, destinationTrack, sourceTrack, _cargoList);
        }

        private Task CreateBoardingTask(Track platform, float totalCapacity, bool isFinal)
        {
            var warehouse = PlatformController.GetControllerForTrack(platform).Warehouse;
            var taskType = isFinal ? WarehouseTaskType.Unloading : WarehouseTaskType.Loading;

            return new WarehouseTask(TrainCarsToTransport, taskType, warehouse, CargoInjector.PassengerCargo.v1, totalCapacity, isLastTask: isFinal);
        }
    }

    public class ExpressStationsChainData : StationsChainData
    {
        public string[] destinationYardIds;

        public ExpressStationsChainData(string chainOriginYardId, string[] chainDestinationYardIds) 
            : base(chainOriginYardId, chainDestinationYardIds.Last())
        {
            destinationYardIds = chainDestinationYardIds;
        }
    }

    public class ExpressJobDefinitionData : JobDefinitionDataBase
    {
        public string[] TrainCarGuids;
        public string[] destinationStationIds;
        public string startingTrack;
        public string[] destinationTracks;

        public ExpressJobDefinitionData(
            float timeLimitForJob, float initialWage, string stationId, string originStationId, string[] destinationStationIds, int requiredLicenses,
            string[] transportCarGuids, string startTrackId, string[] destTrackIds) :
            base(timeLimitForJob, initialWage, stationId, originStationId, destinationStationIds.Last(), requiredLicenses)
        {
            TrainCarGuids = transportCarGuids;
            this.destinationStationIds = destinationStationIds;
            startingTrack = startTrackId;
            destinationTracks = destTrackIds;
        }
    }
}
