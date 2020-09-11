using DV.Logic.Job;
using Harmony12;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEngine;

namespace PassengerJobsMod
{
    public static class PassJobType
    {
        public const JobType Express = (JobType)101;
        public const JobType Commuter = (JobType)102;
    }

    class PassengerJobGenerator : MonoBehaviour
    {
        public const int MIN_CARS_EXPRESS = 4;
        public const int MAX_CARS_EXPRESS = 5;

        public const int MIN_CARS_COMMUTE = 2;
        public const int MAX_CARS_COMMUTE = 3;

        public const float BASE_WAGE_SCALE = 0.5f;
        public const float BONUS_TO_BASE_WAGE_RATIO = 2f;

        public TrainCarType[] PassCarTypes = new TrainCarType[]
        {
            TrainCarType.PassengerRed, TrainCarType.PassengerGreen, TrainCarType.PassengerBlue
        };

        public static Dictionary<string, StationController> PassDestinations = new Dictionary<string, StationController>();

        public static Dictionary<string, string[]> TransportRoutes = new Dictionary<string, string[]>()
        {
            { "CSW", new string[] { "MF", "GF" , "HB" } },
            { "FF",  new string[] { "MF", "GF", "HB" } },
            { "GF",  new string[] { "CSW", "HB", "FF" } },
            { "HB",  new string[] { "CSW", "GF", "FF" } },
            { "MF",  new string[] { "CSW", "FF" } }
        };

        public static Dictionary<string, string[]> CommuterDestinations = new Dictionary<string, string[]>()
        {
            { "CSW", new string[] { "SW", "FRS", "FM", "OWC" } },
            { "FF",  new string[] { "IME", "CM" } },
            { "GF",  new string[] { "OWN", "FRC", "SM" } },
            { "HB",  new string[] { "FRS", "SM" } },
            { "MF",  new string[] { "OWC", "IMW" } }
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
        public List<Track> StartingPlatforms
        {
            get
            {
                if( _startingPlatforms == null ) _startingPlatforms = PlatformTracks.Skip(1).ToList();
                return _startingPlatforms;
            }
        }
        private List<Track> _startingPlatforms = null;

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

        public void GeneratePassengerJobs()
        {
            PassengerJobs.ModEntry.Logger.Log($"Generating jobs at {Controller.stationInfo.Name}");

            try
            {
                // Create passenger hauls until >= half the platforms are filled
                int attemptCounter = 2;
                for( ; attemptCounter > 0; attemptCounter-- )
                {
                    if( TrackOrg.FilterOutOccupiedTracks(StartingPlatforms).Count == 0 ) break;
                    GenerateNewTransportJob();
                }

                // Create commuter hauls until >= half of storage tracks are filled
                var existingChains = Controller.ProceduralJobsController.GetCurrentJobChains();
                int nExtantCommutes = existingChains.Count(c => c is CommuterChainController);

                double totalTrackSpace = StorageTracks.Select(t => t.length).Sum();

                // generate max 3 commuter chains from this station
                attemptCounter = 3 - nExtantCommutes;
                for( ; attemptCounter > 0; attemptCounter-- )
                {
                    double freeTrackSpace = StorageTracks.Select(t => TrackOrg.GetFreeSpaceOnTrack(t)).Sum();
                    if( (freeTrackSpace / totalTrackSpace) <= 0.5d ) break;

                    GenerateNewCommuterRun();
                }
            }
            catch( Exception ex )
            {
                // $"Exception encountered while generating jobs for {Controller.stationInfo.Name}:\n{ex.Message}"
                PassengerJobs.ModEntry.Logger.LogException(ex);
            }
        }

        #endregion

        #region Transport Job Generation

        public PassengerTransportChainController GenerateNewTransportJob( TrainCarsPerLogicTrack consistInfo = null )
        {
            int nTotalCars;
            List<TrainCarType> jobCarTypes;
            float trainLength;
            Track startPlatform;

            if( consistInfo == null )
            {
                // generate a consist
                nTotalCars = Rand.Next(MIN_CARS_EXPRESS, MAX_CARS_EXPRESS + 1);

                if( PassengerJobs.Settings.UniformConsists )
                {
                    TrainCarType carType = PassCarTypes.ChooseOne(Rand);
                    jobCarTypes = Enumerable.Repeat(carType, nTotalCars).ToList();
                }
                else
                {
                    jobCarTypes = PassCarTypes.ChooseMany(Rand, nTotalCars);
                }

                trainLength = TrackOrg.GetTotalCarTypesLength(jobCarTypes) + TrackOrg.GetSeparationLengthBetweenCars(nTotalCars);

                var pool = TrackOrg.FilterOutReservedTracks(TrackOrg.FilterOutOccupiedTracks(StartingPlatforms));
                if( !(TrackOrg.GetTrackThatHasEnoughFreeSpace(pool, trainLength) is Track startTrack) )
                {
                    PassengerJobs.ModEntry.Logger.Log($"Couldn't find storage track with enough free space for new job at {Controller.stationInfo.YardID}");
                    return null;
                }

                startPlatform = startTrack;
            }
            else
            {
                // Use existing consist
                nTotalCars = consistInfo.cars.Count;
                jobCarTypes = consistInfo.cars.Select(car => car.carType).ToList();
                trainLength = TrackOrg.GetTotalCarTypesLength(jobCarTypes) + TrackOrg.GetSeparationLengthBetweenCars(nTotalCars);
                startPlatform = consistInfo.track;
            }

            if( startPlatform == null )
            {
                PassengerJobs.ModEntry.Logger.Log($"No available platform for new job at {Controller.stationInfo.Name}");
                return null;
            }

            // Choose a route
            if( !TransportRoutes.TryGetValue(Controller.stationInfo.YardID, out string[] possibleDests) || (possibleDests.Length < 1) )
            {
                PassengerJobs.ModEntry.Logger.Log($"No potential routes found originating from {Controller.stationInfo.Name}");
                return null;
            }

            string destYard = possibleDests.ChooseOne(Rand);

            // pick ending platform
            Track destPlatform = null;
            PassengerJobGenerator destGenerator = null;

            if( PassDestinations.TryGetValue(destYard, out StationController destStation) )
            {
                destGenerator = LinkedGenerators[destStation];
                destPlatform = destGenerator.ArrivalTrack;

                if( TrackOrg.GetFreeSpaceOnTrack(destPlatform) < trainLength )
                {
                    PassengerJobs.ModEntry.Logger.Log($"No available destination platform for new job at {Controller.stationInfo.Name}");
                    return null;
                }
            }

            // create job chain controller
            var chainJobObject = new GameObject($"ChainJob[Passenger]: {Controller.logicStation.ID} - {destStation.logicStation.ID}");
            chainJobObject.transform.SetParent(Controller.transform);
            var chainController = new PassengerTransportChainController(chainJobObject);

            StaticPassengerJobDefinition jobDefinition;

            //--------------------------------------------------------------------------------------------------------------------------------
            // Create transport leg job
            var chainData = new StationsChainData(Controller.stationInfo.YardID, destStation.stationInfo.YardID);
            PaymentCalculationData transportPaymentData = GetJobPaymentData(jobCarTypes);
            float transportPayment = 0;
            float bonusLimit = 0;

            // calculate haul payment
            float haulDistance = JobPaymentCalculator.GetDistanceBetweenStations(Controller, destStation);
            bonusLimit += JobPaymentCalculator.CalculateHaulBonusTimeLimit(haulDistance, false);
            transportPayment += JobPaymentCalculator.CalculateJobPayment(JobType.Transport, haulDistance, transportPaymentData);

            // scale job payment depending on settings
            float wageScale = PassengerJobs.Settings.UseCustomWages ? BASE_WAGE_SCALE : 1;
            transportPayment = Mathf.Round(transportPayment * wageScale);

            if( consistInfo == null )
            {
                jobDefinition = PopulateTransportJobAndSpawn(
                    chainController, Controller.logicStation, startPlatform, destPlatform,
                    jobCarTypes, chainData, bonusLimit, transportPayment);
            }
            else
            {
                chainController.trainCarsForJobChain = consistInfo.cars;

                jobDefinition = PopulateTransportJobExistingCars(
                    chainController, Controller.logicStation, startPlatform, destPlatform,
                    consistInfo.LogicCars, chainData, bonusLimit, transportPayment);
            }

            if( jobDefinition == null )
            {
                PassengerJobs.ModEntry.Logger.Warning($"Failed to generate transport job definition for {chainController.jobChainGO.name}");
                chainController.DestroyChain();
                return null;
            }
            jobDefinition.subType = PassJobType.Express;

            chainController.AddJobDefinitionToChain(jobDefinition);

            // Finalize job
            chainController.FinalizeSetupAndGenerateFirstJob();
            PassengerJobs.ModEntry.Logger.Log($"Generated new passenger haul job: {chainJobObject.name} ({chainController.currentJobInChain.ID})");

            return chainController;
        }

        private static PaymentCalculationData GetJobPaymentData( IEnumerable<TrainCarType> carTypes )
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

            Dictionary<CargoType, int> cargoTypeDict = new Dictionary<CargoType, int>(1) { { CargoType.Passengers, totalCars } };

            return new PaymentCalculationData(carTypeCount, cargoTypeDict);
        }


        private static StaticPassengerJobDefinition PopulateTransportJobAndSpawn(
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

            if( SkinManager_Patch.Enabled )
            {
                SkinManager_Patch.UnifyConsist(spawnedCars);
            }

            return PopulateTransportJobExistingCars(chainController, startStation, startTrack, destTrack, logicCars, chainData, timeLimit, initialPay);
        }

        private static StaticPassengerJobDefinition PopulateTransportJobExistingCars(
            JobChainController chainController, Station startStation,
            Track startTrack, Track destTrack, List<Car> logicCars,
            StationsChainData chainData, float timeLimit, float initialPay )
        {
            // populate the actual job
            StaticPassengerJobDefinition jobDefinition = chainController.jobChainGO.AddComponent<StaticPassengerJobDefinition>();
            jobDefinition.PopulateBaseJobDefinition(startStation, timeLimit, initialPay, chainData, PassLicenses.Passengers1);

            jobDefinition.startingTrack = startTrack;
            jobDefinition.trainCarsToTransport = logicCars;
            jobDefinition.destinationTrack = destTrack;

            return jobDefinition;
        }

        #endregion

        #region Commuter Haul Generation

        public CommuterChainController GenerateNewCommuterRun( TrainCarsPerLogicTrack consistInfo = null )
        {
            StationController destStation = null;
            Track startSiding;
            int nCars;
            float trainLength;
            List<TrainCarType> jobCarTypes;

            if( consistInfo == null )
            {
                // generate a consist
                nCars = Rand.Next(MIN_CARS_COMMUTE, MAX_CARS_COMMUTE + 1);
                jobCarTypes = PassCarTypes.ChooseMany(Rand, nCars);

                trainLength = TrackOrg.GetTotalCarTypesLength(jobCarTypes) + TrackOrg.GetSeparationLengthBetweenCars(nCars);

                // pick start storage track
                var availTracks = TrackOrg.FilterOutReservedTracks(StorageTracks);
                startSiding = TrackOrg.GetTrackThatHasEnoughFreeSpace(availTracks, trainLength);

                if( startSiding == null )
                {
                    //PassengerJobs.ModEntry.Logger.Log($"No available siding for new job at {Controller.stationInfo.Name}");
                    return null;
                }
            }
            else
            {
                // use existing consist
                nCars = consistInfo.cars.Count;
                jobCarTypes = consistInfo.cars.Select(c => c.carType).ToList();
                trainLength = TrackOrg.GetTotalTrainCarsLength(consistInfo.cars) + TrackOrg.GetSeparationLengthBetweenCars(nCars);

                startSiding = consistInfo.track;

                if( startSiding == null )
                {
                    PassengerJobs.ModEntry.Logger.Log("Invalid start siding from parent job");
                    return null;
                }
            }

            // pick ending storage track
            Track destSiding = null;
            if( !CommuterDestinations.TryGetValue(Controller.stationInfo.YardID, out string[] possibleDestinations) )
            {
                PassengerJobs.ModEntry.Logger.Log("No commuter destination candidates found");
                return null;
            }

            for( int i = 0; (destSiding == null) && (i < 5); i++ )
            {
                string destYard = possibleDestinations.ChooseOne(Rand);
                destStation = SingletonBehaviour<LogicController>.Instance.YardIdToStationController[destYard];
                destSiding = TrackOrg.GetTrackThatHasEnoughFreeSpace(destStation.logicStation.yard.StorageTracks, trainLength);
            }

            if( destSiding == null )
            {
                PassengerJobs.ModEntry.Logger.Warning("No suitable destination tracks found for new commute job");
                return null;
            }

            // create job chain controller
            var chainData = new StationsChainData(Controller.stationInfo.YardID, destStation.stationInfo.YardID);
            var chainJobObject = new GameObject($"ChainJob[Commuter]: {Controller.logicStation.ID} - {destStation.logicStation.ID}");
            chainJobObject.transform.SetParent(Controller.transform);
            var chainController = new CommuterChainController(chainJobObject);

            // calculate haul payment
            float haulDistance = JobPaymentCalculator.GetDistanceBetweenStations(Controller, destStation);
            float bonusLimit = JobPaymentCalculator.CalculateHaulBonusTimeLimit(haulDistance, false);
            float payment = JobPaymentCalculator.CalculateJobPayment(JobType.Transport, haulDistance, GetJobPaymentData(jobCarTypes));

            // create job definition & spawn cars
            StaticPassengerJobDefinition jobDefinition, returnJobDefinition;
            if( consistInfo != null )
            {
                // use existing cars
                chainController.trainCarsForJobChain = consistInfo.cars;

                jobDefinition = PopulateTransportJobExistingCars(
                    chainController, Controller.logicStation, startSiding, destSiding,
                    consistInfo.LogicCars, chainData, bonusLimit, payment);
            }
            else
            {
                // spawn cars & populate
                jobDefinition = PopulateTransportJobAndSpawn(
                    chainController, Controller.logicStation, startSiding, destSiding, jobCarTypes, chainData, bonusLimit, payment);
            }

            if( jobDefinition == null )
            {
                chainController.DestroyChain();
                PassengerJobs.ModEntry.Logger.Warning($"Failed to generate commuter haul job at {Controller.stationInfo.Name}");
                return null;
            }
            jobDefinition.subType = PassJobType.Commuter;

            chainController.AddJobDefinitionToChain(jobDefinition);

            // generate return trip
            var logicCars = chainController.trainCarsForJobChain.Select(tc => tc.logicCar).ToList();
            var returnChain = new StationsChainData(chainData.chainDestinationYardId, chainData.chainOriginYardId);
            returnJobDefinition = PopulateTransportJobExistingCars(chainController, destStation.logicStation, destSiding, startSiding, logicCars, returnChain, bonusLimit, payment);
            chainController.AddJobDefinitionToChain(returnJobDefinition);

            chainController.FinalizeSetupAndGenerateFirstJob(false);

            PassengerJobs.ModEntry.Logger.Log($"Generated new commuter job: {chainJobObject.name} ({chainController.currentJobInChain.ID})");
            return chainController;
        }

        #endregion

        private class ConsistSegment
        {
            public int CarCount;
            public Track Track;
            public List<TrainCar> ExistingCars = null;

            public bool AlreadySpawned => ExistingCars != null;

            public ConsistSegment( IEnumerable<TrainCar> extantCars, Track track )
            {
                if( extantCars == null ) throw new ArgumentException("Existing car list can't be null");

                ExistingCars = extantCars.ToList();
                CarCount = ExistingCars.Count;
                Track = track;
            }

            public ConsistSegment( int nCars, Track track )
            {
                CarCount = nCars;
                Track = track;
            }
        }

        private static readonly FieldInfo spawnedOverviewsField = AccessTools.Field(typeof(StationController), "spawnedJobOverviews");

        public static void PurgePassengerJobChains()
        {
            foreach( var kvp in PassDestinations )
            {
                var controller = kvp.Value;
                var chainList = controller.ProceduralJobsController.GetCurrentJobChains().ToList(); // cache locally since we're modifying the collection

                List<JobOverview> spawnedOverviews = null;
                if( spawnedOverviewsField?.GetValue(controller) is List<JobOverview> ovList )
                {
                    spawnedOverviews = ovList;
                }
                else
                {
                    PassengerJobs.ModEntry.Logger.Warning($"Couldn't get list of job overviews to delete at {controller.stationInfo.Name}");
                }

                foreach( JobChainController chain in chainList )
                {
                    if( (chain is PassengerTransportChainController) || (chain is CommuterChainController) )
                    {
                        PassengerJobs.ModEntry.Logger.Log($"Deleting passenger chaincontroller {chain.jobChainGO?.name}");
                        var cars = chain.trainCarsForJobChain;

                        if( (spawnedOverviews?.Find(ov => ov.job == chain.currentJobInChain) is JobOverview overview) )
                        {
                            PassengerJobs.ModEntry.Logger.Log($"Destroying job booklet for job {chain.currentJobInChain.ID}");
                            overview.DestroyJobOverview();
                        }

                        chain.currentJobInChain.AbandonJob();
                        SingletonBehaviour<CarSpawner>.Instance.DeleteTrainCars(cars);
                    }
                }
            }
        }
    }

}
