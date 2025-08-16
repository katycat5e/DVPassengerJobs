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
    public class PassengerHaulJobDefinition : StaticJobDefinition
    {
        public RouteType RouteType = RouteType.Express;

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
        public RouteTrack? StartingTrack = null;
        public RouteTrack[]? DestinationTracks = null;

        public override JobDefinitionDataBase GetJobDefinitionSaveData()
        {
            if (GetGuidsFromCars(TrainCarsToTransport) is not string[] guidsFromCars)
            {
                throw new Exception("Couldn't extract transportCarsGuids");
            }

            return new ExpressJobDefinitionData(
                timeLimitForJob, initialWage, logicStation.ID, ExpressChainData!.chainOriginYardId, ExpressChainData.destinationYardIds,
                (int)requiredLicenses, guidsFromCars, StartingTrack!.Value.PlatformID, DestinationTracks!.Select(t => t.PlatformID).ToArray(),
                (int)RouteType);
        }

        public override List<TrackReservation> GetRequiredTrackReservations()
        {
            float reservedLength = CarSpawner.Instance.GetTotalCarsLength(TrainCarsToTransport, true);

            return DestinationTracks
                .Where(t => !t.IsSegment)
                .Select(t => new TrackReservation(t.Track, (float)YardTracksOrganizer.Instance.GetFreeSpaceOnTrack(t.Track) - 1)).ToList();
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
            
            // initial boarding
            var initialLoad = CreateBoardingTask(StartingTrack.Value, totalCapacity, true, false);
            taskList.Add(initialLoad);

            // actual move between stations
            //var sourceTrack = StartingTrack;
            for (int i = 0; i < DestinationTracks.Length; i++)
            {
                bool isLast = (i == (DestinationTracks.Length - 1));
                //var leg = CreateTransportLeg(sourceTrack, DestinationTracks[i]);
                //taskList.Add(leg);

                var unloadTask = CreateBoardingTask(DestinationTracks[i], totalCapacity, false, isLast);
                taskList.Add(unloadTask);

                if (!isLast)
                {
                    var loadTask = CreateBoardingTask(DestinationTracks[i], totalCapacity, true, false);
                    taskList.Add(loadTask);
                }

                //sourceTrack = DestinationTracks[i];
            }

            var superTask = new SequentialTasks(taskList);
            var jobType = (RouteType == RouteType.Express) ? PassJobType.Express : PassJobType.Local;
            job = new Job(superTask, jobType, timeLimit, initialWage, chainData, forcedJobId, requiredLicenses);

            // add to signs along route
            PlatformController.GetControllerForTrack(StartingTrack.Value.PlatformID).RegisterOutgoingJob(job);
            for (int i = 0; i < DestinationTracks.Length - 1; i++)
            {
                job.JobTaken += PlatformController.GetControllerForTrack(DestinationTracks[i].PlatformID).RegisterOutgoingJob;
            }
            job.JobTaken += PlatformController.GetControllerForTrack(DestinationTracks.Last().PlatformID).RegisterIncomingJob;

            jobOriginStation.AddJobToStation(job);
        }

        private Task CreateTransportLeg(Track sourceTrack, Track destinationTrack)
        {
            return JobsGenerator.CreateTransportTask(TrainCarsToTransport, destinationTrack, sourceTrack, _cargoList);
        }

        private Task CreateBoardingTask(RouteTrack platform, float totalCapacity, bool loading, bool isFinal)
        {
            //var warehouse = PlatformController.GetControllerForTrack(platform).Warehouse;

            //return new WarehouseTask(TrainCarsToTransport, taskType, warehouse, CargoInjector.PassengerCargo.v1, totalCapacity, isLastTask: isFinal);
            var wrapper = PlatformController.GetControllerForTrack(platform.PlatformID).Platform;
            return wrapper.GenerateBoardingTask(TrainCarsToTransport!, loading, totalCapacity, isFinal);
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
        public int routeType;
        public string[] TrainCarGuids;
        public string[] destinationStationIds;
        public string startingTrack;
        public string[] destinationTracks;

        public ExpressJobDefinitionData(
            float timeLimitForJob, float initialWage, string stationId, string originStationId, string[] destinationStationIds, int requiredLicenses,
            string[] transportCarGuids, string startTrackId, string[] destTrackIds, int routeType) :
            base(timeLimitForJob, initialWage, stationId, originStationId, destinationStationIds.Last(), requiredLicenses)
        {
            TrainCarGuids = transportCarGuids;
            this.destinationStationIds = destinationStationIds;
            startingTrack = startTrackId;
            destinationTracks = destTrackIds;
            this.routeType = routeType;
        }
    }
}
