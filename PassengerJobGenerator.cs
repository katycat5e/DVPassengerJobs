using DV.Logic.Job;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;

namespace PassengerJobsMod
{
    class PassengerJobGenerator : MonoBehaviour
    {
        public const int MIN_CARS_TRANSPORT = 2;
        public const int MAX_CARS_TRANSPORT = 5;
        

        public TrainCarType[] PassCarTypes = new TrainCarType[]
        {
            TrainCarType.PassengerRed, TrainCarType.PassengerGreen, TrainCarType.PassengerBlue
        };

        public static List<StationController> PassDestinations = new List<StationController>();

        internal static Dictionary<StationController, PassengerJobGenerator> LinkedGenerators =
            new Dictionary<StationController, PassengerJobGenerator>();

        private static IEnumerable<Track> _AllTracks = null;
        private static IEnumerable<Track> AllTracks
        {
            get
            {
                if( _AllTracks == null ) FindAllTracks();
                return _AllTracks;
            }
        }

        private static List<RailTrack> _AllRailTracks = null;
        private static List<RailTrack> AllRailTracks
        {
            get
            {
                if( _AllRailTracks == null ) FindAllTracks();
                return _AllRailTracks;
            }
        }

        private static void FindAllTracks()
        {
            _AllRailTracks = FindObjectsOfType<RailTrack>().ToList();
            _AllTracks = _AllRailTracks.Select(rt => rt.logicTrack);
        }

        private static readonly System.Random Rand = new System.Random(); // seeded with current time

        #region Generator Initialization

        public static readonly Dictionary<string, HashSet<string>> StorageTrackNames = new Dictionary<string, HashSet<string>>()
        {
            { "CSW",new HashSet<string>(){ "CSW-B-2-SP", "CSW-B-1-SP" } },
            { "MF", new HashSet<string>(){ "MF-D-4-SP" } },
            { "FF", new HashSet<string>(){ "FF-B-3-SP", "FF-B-5-SP", "FF-B-4-SP" } },
            { "HB", new HashSet<string>(){ "HB-F-4-SP", "HB-F-3-SP" } },
            { "GF", new HashSet<string>(){ "GF-C-1-SP" } }
        };

        public static readonly Dictionary<string, HashSet<string>> PlatformTrackNames = new Dictionary<string, HashSet<string>>()
        {
            { "CSW",new HashSet<string>(){ "CSW-B-6-LP", "CSW-B-3-LP", "CSW-B-4-LP", "CSW-B-5-LP" } },
            { "MF", new HashSet<string>(){ "MF-D-1-LP", "MF-D-2-LP" } },
            { "FF", new HashSet<string>(){ "#Y-#S-354-#T", "#Y-#S-339-#T" } },
            { "HB", new HashSet<string>(){ "HB-F-1-LP", "HB-F-2-LP" } },
            { "GF", new HashSet<string>(){ "GF-C-2-LP", "GF-C-3-LP" } }
        };

        internal static List<Track> GetStorageTracks( StationController station )
        {
            var trackNames = StorageTrackNames[station.stationInfo.YardID];

            return AllTracks
                .Where(t => trackNames.Contains(t.ID.ToString()))
                .ToList();
        }

        internal static List<Track> GetLoadingTracks( StationController station )
        {
            var trackNames = PlatformTrackNames[station.stationInfo.YardID];

            var result = AllTracks.Where(t => trackNames.Contains(t.ID.ToString())).ToList();

            // fix track IDs at Food Factory
            foreach( var track in result )
            {
                if( track.ID.FullDisplayID == "#Y-#S-354-#T" )
                {
                    track.OverrideTrackID(new TrackID("FF", "B", "1", TrackID.LOADING_PASSENGER_TYPE));
                }
                else if( track.ID.FullDisplayID == "#Y-#S-339-#T" )
                {
                    track.OverrideTrackID(new TrackID("FF", "B", "2", TrackID.LOADING_PASSENGER_TYPE));
                }
            }

            return result;
        }

        public static void RegisterStation( StationController controller, PassengerJobGenerator gen )
        {
            if( LinkedGenerators.ContainsKey(controller) ) return;

            LinkedGenerators.Add(controller, gen);

            if( gen.PlatformTracks.Count > 0 )
            {
                // potential destination
                PassDestinations.Add(controller);
            }
        }

        #endregion

        #region Instance Members

        public StationController Controller;
        private StationJobGenerationRange StationRange;

        public List<Track> StorageTracks;
        public List<Track> PlatformTracks;

        private readonly YardTracksOrganizer TrackOrg;

        public PassengerJobGenerator()
        {
            TrackOrg = YardTracksOrganizer.Instance;
        }

        public void Initialize()
        {
            Controller = gameObject.GetComponent<StationController>();
            if( Controller != null )
            {
                StationRange = Controller.GetComponent<StationJobGenerationRange>();
                StorageTracks = GetStorageTracks(Controller);
                PlatformTracks = GetLoadingTracks(Controller);

                // register tracks for train spawning, since they are ignored in the base game
                foreach( Track t in PlatformTracks.Union(StorageTracks) )
                {
                    YardTracksOrganizer.Instance.InitializeYardTrack(t);
                    YardTracksOrganizer.Instance.yardTrackIdToTrack.Add(t.ID.FullID, t);
                }

                var sb = new StringBuilder($"Created generator for {Controller.stationInfo.Name}:\n");
                sb.Append("Coach Storage: ");
                sb.AppendLine(string.Join(", ", StorageTracks.Select(t => t.ID)));
                sb.Append("Platforms: ");
                sb.AppendLine(string.Join(", ", PlatformTracks.Select(t => t.ID)));

                PassengerJobs.ModEntry.Logger.Log(sb.ToString());

                RegisterStation(Controller, this);
            }
        }
        
        

        private bool PlayerWasInGenerateRange = false;
        private bool TrackSignsAreGenerated = false;

        public void Update()
        {
            if( Controller.logicStation == null || !SaveLoadController.carsAndJobsLoadingFinished )
            {
                return;
            }

            if( !TrackSignsAreGenerated )
            {
                GenerateTrackSigns();
                TrackSignsAreGenerated = true;
            }

            float playerDist = StationRange.PlayerSqrDistanceFromStationCenter;
            bool playerInGenerateRange = StationRange.IsPlayerInJobGenerationZone(playerDist);

            if( playerInGenerateRange && !PlayerWasInGenerateRange )
            {
                // player entered the zone
                GeneratePassengerJobs();
            }

            PlayerWasInGenerateRange = playerInGenerateRange;
        }

        private static readonly MethodInfo GenerateTrackIdObjectMethod = typeof(StationController).GetMethod("GenerateTrackIdObject", BindingFlags.NonPublic | BindingFlags.Instance);
        private void GenerateTrackSigns()
        {
            // Use our associated station controller to create the track ID signs
            var allStationTracks = StorageTracks.Union(PlatformTracks).ToHashSet();
            var stationRailTracks = AllRailTracks.Where(rt => allStationTracks.Contains(rt.logicTrack)).ToList();

            GenerateTrackIdObjectMethod.Invoke(Controller, new object[] { stationRailTracks });
        }

        public void GeneratePassengerJobs()
        {
            PassengerJobs.ModEntry.Logger.Log($"Generating jobs at {Controller.stationInfo.Name}");

            try
            {
                // Create passenger hauls until <= half the platforms are filled
                var availTracks = TrackOrg.FilterOutReservedTracks(TrackOrg.FilterOutOccupiedTracks(PlatformTracks));
                int nJobSpawns = availTracks.Count - ((PlatformTracks.Count + 1) / 2);

                for( int i = 0; i < nJobSpawns; i++ ) GenerateNewRoundTripJob();

                // Create logi hauls until <= half the storage tracks are filled
                availTracks = TrackOrg.FilterOutReservedTracks(TrackOrg.FilterOutOccupiedTracks(StorageTracks));
                nJobSpawns = availTracks.Count - ((StorageTracks.Count + 1)/ 2);

                for( int i = 0; i < nJobSpawns; i++ ) GenerateNewLogisticHaul();
            }
            catch( Exception ex )
            {
                PassengerJobs.ModEntry.Logger.Error($"Exception encountered while generating jobs for {Controller.stationInfo.Name}:\n{ex.Message}");
            }
        }

        #endregion

        #region Transport Job Generation

        public void GenerateNewRoundTripJob()
        {
            StationController destStation = null;

            // generate a consist
            int nCars = Rand.Next(MIN_CARS_TRANSPORT, MAX_CARS_TRANSPORT + 1);
            var jobCarTypes = PassCarTypes.ChooseMany(Rand, nCars);

            float trainLength = TrackOrg.GetTotalCarTypesLength(jobCarTypes) + TrackOrg.GetSeparationLengthBetweenCars(nCars);

            // pick start platform
            var availTracks = TrackOrg.FilterOutReservedTracks(TrackOrg.FilterOutOccupiedTracks(PlatformTracks));
            Track startPlatform = TrackOrg.GetTrackThatHasEnoughFreeSpace(availTracks, trainLength);

            if( startPlatform == null ) return;

            // pick ending platform
            Track destPlatform = null;
            for( int i = 0; (destPlatform == null) && (i < 5); i++ )
            {
                destStation = PassDestinations.GetRandomFromList(Rand, Controller);
                var destGenerator = LinkedGenerators[destStation];
                destPlatform = TrackOrg.GetTrackThatHasEnoughFreeSpace(TrackOrg.FilterOutOccupiedTracks(destGenerator.PlatformTracks), trainLength);
            }
            if( destPlatform == null ) return;

            // create job chain controller
            var chainData = new StationsChainData(Controller.stationInfo.YardID, destStation.stationInfo.YardID);
            var chainJobObject = new GameObject($"ChainJob[{JobType.Transport}]: {Controller.logicStation.ID} - {destStation.logicStation.ID}");
            chainJobObject.transform.SetParent(Controller.transform);
            var chainController = new JobChainControllerWithEmptyHaulGeneration(chainJobObject);

            // calculate haul payment
            // divided in half for out, half for return trip
            float haulDistance = JobPaymentCalculator.GetDistanceBetweenStations(Controller, destStation);
            float bonusLimit = Mathf.Floor(JobPaymentCalculator.CalculateHaulBonusTimeLimit(haulDistance, false) / 2);
            float payment = Mathf.Floor(JobPaymentCalculator.CalculateJobPayment(JobType.Transport, haulDistance, GetJobPaymentData(jobCarTypes)) / 2);

            // create starting job definition
            var jobDefinition = PopulateJobDefinition(chainController, Controller.logicStation, startPlatform, destPlatform, jobCarTypes, chainData, bonusLimit, payment);

            if( jobDefinition == null )
            {
                chainController.DestroyChain();
                PassengerJobs.ModEntry.Logger.Warning($"Failed to generate job at {Controller.stationInfo.Name}");
                return;
            }

            // assign initial haul job to chain
            chainController.AddJobDefinitionToChain(jobDefinition);

            // try to create return trip job definition
            var returnJobDefinition = PopulateJobExistingCars(
                chainController, destStation.logicStation, destPlatform, startPlatform,
                jobDefinition.trainCarsToTransport, jobCarTypes, chainData, bonusLimit, payment);

            if( returnJobDefinition == null )
            {
                PassengerJobs.ModEntry.Logger.Warning($"Failed to generate return trip for \"{chainJobObject.name}\"");
            }
            else
            {
                chainController.AddJobDefinitionToChain(returnJobDefinition);
            }

            // Finalize job
            chainController.FinalizeSetupAndGenerateFirstJob();

            PassengerJobs.ModEntry.Logger.Log($"Generated new transport job: {chainJobObject.name}");
        }

        private static PaymentCalculationData GetJobPaymentData( IEnumerable<TrainCarType> carTypes, bool logisticHaul = false )
        {
            var carTypeCount = new Dictionary<TrainCarType, int>();
            int totalCars = 0;

            foreach( TrainCarType type in carTypes )
            {
                if( carTypeCount.TryGetValue(type, out int curCount) )
                {
                    carTypeCount[type] = curCount + 1;
                }
                else carTypeCount[type] = 1;

                totalCars += 1;
            }

            // If the job is logistic haul, the cargo is 0
            Dictionary<CargoType, int> cargoTypeDict;
            if( logisticHaul )
            {
                cargoTypeDict = new Dictionary<CargoType, int>();
            }
            else
            {
                cargoTypeDict = new Dictionary<CargoType, int>(1) { { CargoType.Passengers, totalCars } };
            }

            return new PaymentCalculationData(carTypeCount, cargoTypeDict);
        }

        private static StaticTransportJobDefinition PopulateJobDefinition(
            JobChainController chainController, Station startStation,
            Track startTrack, Track destTrack, List<TrainCarType> carTypes,
            StationsChainData chainData, float timeLimit, float initialPay )
        {
            // Spawn the cars
            RailTrack startRT = SingletonBehaviour<LogicController>.Instance.LogicToRailTrack[startTrack];
            var spawnedCars = CarSpawner.SpawnCarTypesOnTrack(carTypes, startRT, true, 0, false, true);

            if( spawnedCars == null ) return null;

            chainController.trainCarsForJobChain = spawnedCars;
            var logicCars = TrainCar.ExtractLogicCars(spawnedCars);
            if( logicCars == null )
            {
                PassengerJobs.ModEntry.Logger.Error("Couldn't extract logic cars, deleting spawned cars");
                SingletonBehaviour<CarSpawner>.Instance.DeleteTrainCars(spawnedCars, true);
                return null;
            }

            return PopulateJobExistingCars(chainController, startStation, startTrack, destTrack, logicCars, carTypes, chainData, timeLimit, initialPay);
        }

        private static StaticTransportJobDefinition PopulateJobExistingCars(
            JobChainController chainController, Station startStation,
            Track startTrack, Track destTrack,
            List<Car> logicCars, List<TrainCarType> carTypes,
            StationsChainData chainData, float timeLimit, float initialPay )
        {
            // populate the actual job
            StaticTransportJobDefinition jobDefinition = chainController.jobChainGO.AddComponent<StaticTransportJobDefinition>();
            jobDefinition.PopulateBaseJobDefinition(startStation, timeLimit, initialPay, chainData, PassLicenses.Passengers1);

            jobDefinition.startingTrack = startTrack;
            jobDefinition.trainCarsToTransport = logicCars;
            jobDefinition.transportedCargoPerCar = carTypes.Select(ct => CargoType.Passengers).ToList();
            jobDefinition.cargoAmountPerCar = carTypes.Select(ct => 1f).ToList();
            jobDefinition.forceCorrectCargoStateOnCars = true;
            jobDefinition.destinationTrack = destTrack;

            return jobDefinition;
        }

        #endregion

        #region Logistic Haul Generation

        public void GenerateNewLogisticHaul()
        {
            StationController destStation = null;

            // generate a consist
            int nCars = Rand.Next(MIN_CARS_TRANSPORT, MAX_CARS_TRANSPORT + 1);
            var jobCarTypes = PassCarTypes.ChooseMany(Rand, nCars);

            float trainLength = TrackOrg.GetTotalCarTypesLength(jobCarTypes) + TrackOrg.GetSeparationLengthBetweenCars(nCars);

            // pick start storage track
            var availTracks = TrackOrg.FilterOutReservedTracks(TrackOrg.FilterOutOccupiedTracks(StorageTracks));
            Track startSiding = TrackOrg.GetTrackThatHasEnoughFreeSpace(availTracks, trainLength);

            if( startSiding == null ) return;

            // pick ending storage track
            Track destSiding = null;
            for( int i = 0; (destSiding == null) && (i < 5); i++ )
            {
                destStation = PassDestinations.GetRandomFromList(Rand, Controller);
                var destGenerator = LinkedGenerators[destStation];
                destSiding = TrackOrg.GetTrackThatHasEnoughFreeSpace(TrackOrg.FilterOutOccupiedTracks(destGenerator.StorageTracks), trainLength);
            }
            if( destSiding == null ) return;

            // create job chain controller
            var chainData = new StationsChainData(Controller.stationInfo.YardID, destStation.stationInfo.YardID);
            var chainJobObject = new GameObject($"ChainJob[{JobType.EmptyHaul}]: {Controller.logicStation.ID} - {destStation.logicStation.ID}");
            chainJobObject.transform.SetParent(Controller.transform);
            var chainController = new JobChainController(chainJobObject);

            // calculate haul payment
            float haulDistance = JobPaymentCalculator.GetDistanceBetweenStations(Controller, destStation);
            float bonusLimit = JobPaymentCalculator.CalculateHaulBonusTimeLimit(haulDistance, false);
            float payment = JobPaymentCalculator.CalculateJobPayment(JobType.EmptyHaul, haulDistance, GetJobPaymentData(jobCarTypes, true));

            // create job definition & spawn cars
            var jobDefinition = PopulateLogisticJob(
                chainController, Controller.logicStation, startSiding, destSiding, jobCarTypes, chainData, bonusLimit, payment);

            if( jobDefinition == null )
            {
                chainController.DestroyChain();
                PassengerJobs.ModEntry.Logger.Warning($"Failed to generate logistic haul job at {Controller.stationInfo.Name}");
                return;
            }

            chainController.AddJobDefinitionToChain(jobDefinition);
            chainController.FinalizeSetupAndGenerateFirstJob(false);

            PassengerJobs.ModEntry.Logger.Log($"Generated new logi haul job: {chainJobObject.name}");
        }

        private static StaticEmptyHaulJobDefinition PopulateLogisticJob(
            JobChainController chainController, Station startStation,
            Track startTrack, Track destTrack, List<TrainCarType> carTypes,
            StationsChainData chainData, float timeLimit, float initialPay )
        {
            // Spawn the cars
            RailTrack startRT = SingletonBehaviour<LogicController>.Instance.LogicToRailTrack[startTrack];
            var spawnedCars = CarSpawner.SpawnCarTypesOnTrack(carTypes, startRT, true, 0, false, true);

            if( spawnedCars == null ) return null;

            chainController.trainCarsForJobChain = spawnedCars;
            var logicCars = TrainCar.ExtractLogicCars(spawnedCars);
            if( logicCars == null )
            {
                PassengerJobs.ModEntry.Logger.Error("Couldn't extract logic cars, deleting spawned cars");
                SingletonBehaviour<CarSpawner>.Instance.DeleteTrainCars(spawnedCars, true);
                return null;
            }

            var jobDefinition = chainController.jobChainGO.AddComponent<StaticEmptyHaulJobDefinition>();
            jobDefinition.PopulateBaseJobDefinition(startStation, timeLimit, initialPay, chainData, JobLicenses.LogisticalHaul | PassLicenses.Passengers1);
            jobDefinition.startingTrack = startTrack;
            jobDefinition.trainCarsToTransport = logicCars;
            jobDefinition.destinationTrack = destTrack;

            return jobDefinition;
        }

        #endregion
    }

}
