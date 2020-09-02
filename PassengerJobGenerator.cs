using DV.Logic.Job;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEngine;

namespace PassengerJobsMod
{
    class PassengerJobGenerator : MonoBehaviour
    {
        public const int MIN_CARS_TRANSPORT = 2;
        public const int MAX_CARS_TRANSPORT = 5;

        public const float BASE_WAGE_SCALE = 0.5f;
        public const float BONUS_TO_BASE_WAGE_RATIO = 2f;

        public static readonly JobType JT_Passenger = (JobType)(Enum.GetValues(typeof(JobType)).Cast<int>().Max() + 1);

        public TrainCarType[] PassCarTypes = new TrainCarType[]
        {
            TrainCarType.PassengerRed, TrainCarType.PassengerGreen, TrainCarType.PassengerBlue
        };

        public static Dictionary<string, StationController> PassDestinations = new Dictionary<string, StationController>();

        public static Dictionary<string, string[][]> TransportRoutes = new Dictionary<string, string[][]>
        {
            { "CSW", new string[][] {
                new string[] { "MF", "FF" } ,
                new string[] { "GF" } ,
                new string[] { "HB" } } },

            { "FF", new string[][] {
                new string[] { "MF", "CSW" },
                new string[] { "HB", "CSW" } } },

            { "GF", new string[][] {
                new string[] { "CSW" },
                new string[] { "HB" },
                new string[] { "FF", "MF" } } },

            { "HB", new string[][] {
                new string[] { "CSW" },
                new string[] { "GF" },
                new string[] { "FF" } } },

            { "MF", new string[][] {
                new string[] { "FF", "GF" } } }
        };

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
            { "CSW",new HashSet<string>(){ "CSW-B-6-LP", "CSW-B-3-LP" } }, // not enough clearance: "CSW-B-4-LP", "CSW-B-5-LP"
            { "MF", new HashSet<string>(){ "MF-D-1-LP", "MF-D-2-LP" } },
            { "FF", new HashSet<string>(){ "#Y-#S-168-#T", "#Y-#S-491-#T" } },
            { "HB", new HashSet<string>(){ "HB-F-1-LP" } }, // not enough clearance: "HB-F-2-LP"
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
                if( track.ID.FullDisplayID == "#Y-#S-168-#T" ) // used to be #Y-#S-354-#T
                {
                    track.OverrideTrackID(new TrackID("FF", "B", "1", TrackID.LOADING_PASSENGER_TYPE));
                }
                else if( track.ID.FullDisplayID == "#Y-#S-491-#T" ) // used to be #Y-#S-339-#T
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
                PassDestinations[controller.stationInfo.YardID] = controller;
            }
        }

        #endregion

        #region Instance Members

        public StationController Controller;
        private StationJobGenerationRange StationRange;

        public List<Track> StorageTracks;
        public List<Track> PlatformTracks;

        public Track ArrivalTrack
        {
            get => PlatformTracks.FirstOrDefault();
        }

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
                    YardTracksOrganizer.Instance.yardTrackIdToTrack[t.ID.FullID] = t;
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

        private int GetNumberTransportJobsToSpawn()
        {
            var availTracks = TrackOrg.FilterOutOccupiedTracks(PlatformTracks);
            availTracks.Remove(ArrivalTrack);

            return availTracks.Count;
        }

        private int GetNumberLogiJobsToSpawn()
        {
            var availTracks = TrackOrg.FilterOutReservedTracks(TrackOrg.FilterOutOccupiedTracks(StorageTracks));
            int targetFill = (StorageTracks.Count + 1) / 2;
            return targetFill - (StorageTracks.Count - availTracks.Count);
        }

        public void GeneratePassengerJobs()
        {
            PassengerJobs.ModEntry.Logger.Log($"Generating jobs at {Controller.stationInfo.Name}");

            try
            {
                // Create passenger hauls until >= half the platforms are filled
                int nJobSpawns = GetNumberTransportJobsToSpawn();
                for( int i = 0; i < nJobSpawns; i++ ) GenerateNewTransportJob();

                // Create logi hauls until >= half the storage tracks are filled
                nJobSpawns = GetNumberLogiJobsToSpawn();
                for( int i = 0; i < nJobSpawns; i++ ) GenerateNewLogisticHaul();
            }
            catch( Exception ex )
            {
                // $"Exception encountered while generating jobs for {Controller.stationInfo.Name}:\n{ex.Message}"
                PassengerJobs.ModEntry.Logger.LogException(ex);
            }
        }

        #endregion

        #region Transport Job Generation

        public void GenerateNewTransportJob( Tuple<StaticPassengerJobDefinition, List<TrainCar>> previousJob = null )
        {
            // generate a consist
            int nCars = Rand.Next(MIN_CARS_TRANSPORT, MAX_CARS_TRANSPORT + 1);
            List<TrainCarType> jobCarTypes;

            if( PassengerJobs.Settings.UniformConsists )
            {
                TrainCarType carType = PassCarTypes.GetRandomFromList(Rand);
                jobCarTypes = Enumerable.Repeat(carType, nCars).ToList();
            }
            else
            {
                jobCarTypes = PassCarTypes.ChooseMany(Rand, nCars);
            }

            float trainLength = TrackOrg.GetTotalCarTypesLength(jobCarTypes) + TrackOrg.GetSeparationLengthBetweenCars(nCars);

            // Choose a route
            if( !TransportRoutes.TryGetValue(Controller.stationInfo.YardID, out string[][] possibleRoutes) )
            {
                PassengerJobs.ModEntry.Logger.Log($"No potential routes found originating from {Controller.stationInfo.Name}");
                return;
            }

            string[] route = possibleRoutes.GetRandomFromList(Rand);
            if( route.Length < 1 )
            {
                PassengerJobs.ModEntry.Logger.Warning($"Selected route was empty, this shouldn't happen!");
            }

            // pick start platform
            Track startPlatform;

            if( previousJob == null )
            {
                var availTracks = TrackOrg.FilterOutOccupiedTracks(PlatformTracks);
                availTracks.Remove(ArrivalTrack);
                startPlatform = TrackOrg.GetTrackThatHasEnoughFreeSpace(availTracks, trainLength);

                if( startPlatform == null )
                {
                    PassengerJobs.ModEntry.Logger.Log($"No available platform for new job at {Controller.stationInfo.Name}");
                    return;
                }
            }
            else
            {
                startPlatform = previousJob.Item1.destinationTracks.LastOrDefault();

                if( startPlatform == null )
                {
                    PassengerJobs.ModEntry.Logger.Log($"Invalid destination platform from parent job {previousJob.Item1.gameObject.name}");
                    return;
                }
            }

            var destPlatforms = new Track[route.Length];
            var destStations = new StationController[route.Length];

            for( int i = 0; i < route.Length; i++ )
            {
                // pick ending platform
                if( PassDestinations.TryGetValue(route[i], out var destController) )
                {
                    var destGenerator = LinkedGenerators[destController];
                    destStations[i] = destController;
                    destPlatforms[i] = destGenerator.ArrivalTrack;

                    if( TrackOrg.GetFreeSpaceOnTrack(destPlatforms[i]) < trainLength ) destPlatforms = null; // check if it's actually long enough
                }
                if( destPlatforms == null )
                {
                    PassengerJobs.ModEntry.Logger.Log($"No available destination platform for new job at {Controller.stationInfo.Name}");
                    return;
                }
            }

            // create job chain controller
            var lastStation = destStations.Last();
            var chainData = new ComplexChainData(Controller.stationInfo.YardID, destStations.Select(s => s.stationInfo.YardID).ToArray());
            var chainJobObject = new GameObject($"ChainJob[Passenger]: {Controller.logicStation.ID} - {lastStation.logicStation.ID}");
            chainJobObject.transform.SetParent(Controller.transform);
            var chainController = new PassengerTransportChainController(chainJobObject);

            float payment = 0;
            float bonusLimit = 0;

            for( int i = 0; i < route.Length; i++ )
            {
                StationController fromStation = (i > 0) ? destStations[i - 1] : Controller;
                StationController toStation = destStations[i];

                // calculate haul payment
                float haulDistance = JobPaymentCalculator.GetDistanceBetweenStations(fromStation, toStation);
                bonusLimit += JobPaymentCalculator.CalculateHaulBonusTimeLimit(haulDistance, false);

                payment += JobPaymentCalculator.CalculateJobPayment(JobType.Transport, haulDistance, GetJobPaymentData(jobCarTypes));
            }

            // scale job payment depending on settings
            float wageScale = PassengerJobs.Settings.UseCustomWages ? BASE_WAGE_SCALE : 1;
            payment = Mathf.Round(payment * 0.5f * wageScale);

            // create starting job definition
            StaticPassengerJobDefinition jobDefinition;
            if( previousJob != null )
            {
                chainController.trainCarsForJobChain = previousJob.Item2;

                jobDefinition = PopulateJobExistingCars(
                    chainController, Controller.logicStation, startPlatform, destPlatforms,
                    destStations.Select(s => s.stationInfo.YardID).ToArray(),
                    previousJob.Item1.trainCarsToTransport, chainData, bonusLimit, payment);
            }
            else
            {
                jobDefinition = PopulateJobAndSpawn(
                    chainController, Controller.logicStation, startPlatform, destPlatforms,
                    destStations.Select(s => s.stationInfo.YardID).ToArray(),
                    jobCarTypes, chainData, bonusLimit, payment);
            }

            if( jobDefinition == null )
            {
                chainController.DestroyChain();
                PassengerJobs.ModEntry.Logger.Warning($"Failed to generate new job definition at {Controller.stationInfo.Name}");
                return;
            }

            // assign initial haul job to chain
            chainController.AddJobDefinitionToChain(jobDefinition);

            // Finalize job
            chainController.FinalizeSetupAndGenerateFirstJob();

            PassengerJobs.ModEntry.Logger.Log($"Generated new passenger haul job: {chainJobObject.name} ({chainController.currentJobInChain.ID})");
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

        private static StaticPassengerJobDefinition PopulateJobAndSpawn(
            JobChainController chainController, Station startStation,
            Track startTrack, Track[] destTracks, string[] destYards, List<TrainCarType> carTypes,
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

            if( SkinManager_Patch.Enabled )
            {
                SkinManager_Patch.UnifyConsist(spawnedCars);
            }

            return PopulateJobExistingCars(chainController, startStation, startTrack, destTracks, destYards, logicCars, chainData, timeLimit, initialPay);
        }

        private static StaticPassengerJobDefinition PopulateJobExistingCars(
            JobChainController chainController, Station startStation,
            Track startTrack, Track[] destTracks, string[] destYards,
            List<Car> logicCars,
            StationsChainData chainData, float timeLimit, float initialPay )
        {
            // populate the actual job
            StaticPassengerJobDefinition jobDefinition = chainController.jobChainGO.AddComponent<StaticPassengerJobDefinition>();
            jobDefinition.PopulateBaseJobDefinition(startStation, timeLimit, initialPay, chainData, PassLicenses.Passengers1);

            jobDefinition.startingTrack = startTrack;
            jobDefinition.trainCarsToTransport = logicCars;
            jobDefinition.destinationTracks = destTracks;
            jobDefinition.destinationYards = destYards;

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
            var possibleDestinations = PassDestinations.Values.ToList();
            possibleDestinations.Remove(Controller);

            for( int i = 0; (destSiding == null) && (i < 5); i++ )
            {
                destStation = possibleDestinations.GetRandomFromList(Rand);
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

            PassengerJobs.ModEntry.Logger.Log($"Generated new logi haul job: {chainJobObject.name} ({chainController.currentJobInChain.ID})");
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
