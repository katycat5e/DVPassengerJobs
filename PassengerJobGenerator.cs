using DV.Logic.Job;
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
        public const JobType ConsistAssemble = (JobType)103;
        public const JobType ConsistDissasemble = (JobType)104;
    }

    class PassengerJobGenerator : MonoBehaviour
    {
        public const int MIN_CARS_TRANSPORT = 4;
        public const int MAX_CARS_TRANSPORT = 5;

        public const float BASE_WAGE_SCALE = 0.5f;
        public const float BONUS_TO_BASE_WAGE_RATIO = 2f;

        public TrainCarType[] PassCarTypes = new TrainCarType[]
        {
            TrainCarType.PassengerRed, TrainCarType.PassengerGreen, TrainCarType.PassengerBlue
        };

        public static Dictionary<string, StationController> PassDestinations = new Dictionary<string, StationController>();

        public static Dictionary<string, string[][]> TransportRoutes = new Dictionary<string, string[][]>()
        {
            { "CSW", new string[][] {
                new string[] { "MF", "FF" } ,
                new string[] { "GF" } ,
                new string[] { "HB" } } },

            { "FF", new string[][] {
                new string[] { "MF", "CSW" },
                new string[] { "HB", } } },

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

        public static Dictionary<Job, JobChainController> CommuterJobDict = new Dictionary<Job, JobChainController>();



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

        public void GeneratePassengerJobs()
        {
            PassengerJobs.ModEntry.Logger.Log($"Generating jobs at {Controller.stationInfo.Name}");

            try
            {
                // Create passenger hauls until >= half the platforms are filled
                double totalTrackSpace = StorageTracks.Select(t => t.length).Sum();
                bool genExpress = true;

                foreach( Job job in PlayerJobs.Instance.currentJobs )
                {
                    if( CommuterJobDict.TryGetValue(job, out var chainController) && 
                        (job.chainData.chainDestinationYardId == Controller.stationInfo.YardID) )
                    {
                        // commuter job that's heading here
                        List<TrainCar> jobCars = chainController.trainCarsForJobChain;
                        if( job.GetJobData().FirstOrDefault() is TaskData transportTask )
                        {
                            var segment = new TrainCarsPerLogicTrack(transportTask.destinationTrack, jobCars);
                            var newChain = GenerateNewTransportJob(new List<TrainCarsPerLogicTrack>() { segment }, true);
                            if( newChain != null )
                            {
                                // register the new chain for delayed initialization
                                PendingGenerationDict[job] = newChain;
                                job.JobCompleted += OnIncomingJobCompleted;
                                job.JobAbandoned += OnIncomingJobAbandoned;
                                genExpress = false;
                            }
                            break;
                        }
                        else
                        {
                            PassengerJobs.ModEntry.Logger.Warning($"Couldn't get tasks for incoming commuter job to {Controller.stationInfo.Name}");
                            break;
                        }
                    }
                }

                for( int attemptCounter = 5; attemptCounter > 0; attemptCounter-- )
                {
                    double freeTrackSpace = StorageTracks.Select(t => TrackOrg.GetFreeSpaceOnTrack(t)).Sum();
                    if( (freeTrackSpace / totalTrackSpace) <= 0.5d ) break;

                    if( genExpress )
                    {
                        GenerateNewTransportJob();
                    }
                    else GenerateNewCommuterRun();

                    genExpress = !genExpress;
                }
            }
            catch( Exception ex )
            {
                // $"Exception encountered while generating jobs for {Controller.stationInfo.Name}:\n{ex.Message}"
                PassengerJobs.ModEntry.Logger.LogException(ex);
            }
        }

        private readonly Dictionary<Job, JobChainController> PendingGenerationDict = new Dictionary<Job, JobChainController>();

        private void OnIncomingJobCompleted( Job incoming )
        {
            if( PendingGenerationDict.TryGetValue(incoming, out var chainController) )
            {
                incoming.JobCompleted -= OnIncomingJobCompleted;
                incoming.JobAbandoned -= OnIncomingJobAbandoned;

                chainController.FinalizeSetupAndGenerateFirstJob();
                PendingGenerationDict.Remove(incoming);
            }
        }

        private void OnIncomingJobAbandoned( Job incoming )
        {
            if( PendingGenerationDict.TryGetValue(incoming, out var chainController) )
            {
                incoming.JobCompleted -= OnIncomingJobCompleted;
                incoming.JobAbandoned -= OnIncomingJobAbandoned;

                chainController.trainCarsForJobChain.Clear();
                chainController.DestroyChain();
                PendingGenerationDict.Remove(incoming);
            }
        }

        #endregion

        #region Transport Job Generation

        public PassengerTransportChainController GenerateNewTransportJob( List<TrainCarsPerLogicTrack> consistInfo = null, bool delayedInit = false )
        {
            int nTotalCars;
            List<ConsistSegment> startSubConsists;
            List<TrainCarType> jobCarTypes;
            float trainLength;

            if( consistInfo == null )
            {
                // generate a consist
                nTotalCars = Rand.Next(MIN_CARS_TRANSPORT, MAX_CARS_TRANSPORT + 1);
                int c1Count = Rand.Next(1, nTotalCars);
                int c2Count = nTotalCars - c1Count;

                if( PassengerJobs.Settings.UniformConsists )
                {
                    jobCarTypes = new List<TrainCarType>();

                    // subconsist 1
                    TrainCarType carType = PassCarTypes.ChooseOne(Rand);
                    jobCarTypes.AddRange(Enumerable.Repeat(carType, c1Count));

                    // subconsist 2
                    carType = PassCarTypes.ChooseOne(Rand);
                    jobCarTypes.AddRange(Enumerable.Repeat(carType, c2Count));
                }
                else
                {
                    jobCarTypes = PassCarTypes.ChooseMany(Rand, nTotalCars);
                }

                float c1Length = TrackOrg.GetTotalCarTypesLength(jobCarTypes.GetRange(0, c1Count).ToList()) + TrackOrg.GetSeparationLengthBetweenCars(c1Count);
                float c2Length = TrackOrg.GetTotalCarTypesLength(jobCarTypes.GetRange(c1Count, c2Count).ToList()) + TrackOrg.GetSeparationLengthBetweenCars(c2Count);
                trainLength = TrackOrg.GetTotalCarTypesLength(jobCarTypes) + TrackOrg.GetSeparationLengthBetweenCars(nTotalCars);

                var sidingPool = StorageTracks.ToList();
                if( !(TrackOrg.GetTrackThatHasEnoughFreeSpace(sidingPool, c1Length) is Track c1Track) )
                {
                    PassengerJobs.ModEntry.Logger.Log($"Couldn't find storage track with enough free space for new job at {Controller.stationInfo.YardID}");
                    return null;
                }

                sidingPool.Remove(c1Track);
                Track c2Track = null;
                bool useSingleTrack = false;

                if( TrackOrg.GetTrackThatHasEnoughFreeSpace(sidingPool, c2Length) is Track c2Track_tmp )
                {
                    c2Track = c2Track_tmp;
                }
                else
                {
                    // no second track with enough free space
                    if( TrackOrg.GetFreeSpaceOnTrack(c1Track) >= trainLength )
                    {
                        useSingleTrack = true;
                    }
                    else
                    {
                        PassengerJobs.ModEntry.Logger.Log($"Couldn't find storage tracks with enough free space for new job at {Controller.stationInfo.YardID}");
                        return null;
                    }
                }

                if( useSingleTrack )
                {
                    startSubConsists = new List<ConsistSegment>()
                    {
                        new ConsistSegment(nTotalCars, c1Track)
                    };
                }
                else
                {
                    startSubConsists = new List<ConsistSegment>()
                    {
                        new ConsistSegment(c1Count, c1Track),
                        new ConsistSegment(c2Count, c2Track)
                    };
                }
            }
            else
            {
                // Use existing consist
                nTotalCars = consistInfo.Sum(cpt => cpt.cars.Count);

                startSubConsists = new List<ConsistSegment>();
                jobCarTypes = new List<TrainCarType>();

                foreach( var cut in consistInfo )
                {
                    startSubConsists.Add(new ConsistSegment(cut.cars, cut.track));
                    jobCarTypes.AddRange(cut.cars.Select(car => car.carType));
                }

                // Make sure our consist is a respectable size
                if( nTotalCars < MIN_CARS_TRANSPORT )
                {
                    // if 3 or less cars
                    int nNewCars = Rand.Next(1, MAX_CARS_TRANSPORT + 1 - nTotalCars); // we want to spawn at least 1, but not have more than MAX total
                    
                    List<TrainCarType> newTypes;
                    if( PassengerJobs.Settings.UniformConsists )
                    {
                        newTypes = Enumerable.Repeat(PassCarTypes.ChooseOne(Rand), nNewCars).ToList();
                    }
                    else
                    {
                        newTypes = PassCarTypes.ChooseMany(Rand, nNewCars);
                    }

                    float newLength = TrackOrg.GetTotalCarTypesLength(newTypes) + TrackOrg.GetSeparationLengthBetweenCars(nNewCars);
                    if( !(TrackOrg.GetTrackThatHasEnoughFreeSpace(StorageTracks, newLength) is Track newTrack) )
                    {
                        PassengerJobs.ModEntry.Logger.Log("Tried to expand consist of new job with existing cars, but not enough space available");
                    }
                    else
                    {
                        jobCarTypes.AddRange(newTypes);
                        startSubConsists.Add(new ConsistSegment(nNewCars, newTrack));
                        nTotalCars += nNewCars;
                    }
                }

                trainLength = TrackOrg.GetTotalCarTypesLength(jobCarTypes) + TrackOrg.GetSeparationLengthBetweenCars(nTotalCars);
            }

            // Choose a route
            if( !TransportRoutes.TryGetValue(Controller.stationInfo.YardID, out string[][] possibleRoutes) )
            {
                PassengerJobs.ModEntry.Logger.Log($"No potential routes found originating from {Controller.stationInfo.Name}");
                return null;
            }

            string[] route = possibleRoutes.ChooseOne(Rand);
            if( route.Length < 1 )
            {
                PassengerJobs.ModEntry.Logger.Warning($"Selected route was empty, this shouldn't happen!");
            }

            // pick start platform
            Track startPlatform;

            var availTracks = TrackOrg.FilterOutOccupiedTracks(PlatformTracks);
            availTracks.Remove(ArrivalTrack);
            startPlatform = TrackOrg.GetTrackThatHasEnoughFreeSpace(availTracks, trainLength);

            if( startPlatform == null )
            {
                PassengerJobs.ModEntry.Logger.Log($"No available platform for new job at {Controller.stationInfo.Name}");
                return null;
            }

            // Pick transport checkpoint platforms
            var destPlatforms = new Track[route.Length];
            var destStations = new StationController[route.Length];
            PassengerJobGenerator destGenerator = null;

            for( int i = 0; i < route.Length; i++ )
            {
                // pick ending platform
                if( PassDestinations.TryGetValue(route[i], out var destController) )
                {
                    destGenerator = LinkedGenerators[destController];
                    destStations[i] = destController;
                    destPlatforms[i] = destGenerator.ArrivalTrack;

                    if( TrackOrg.GetFreeSpaceOnTrack(destPlatforms[i]) < trainLength ) destPlatforms = null; // check if it's actually long enough
                }
                if( destPlatforms == null )
                {
                    PassengerJobs.ModEntry.Logger.Log($"No available destination platform for new job at {Controller.stationInfo.Name}");
                    return null;
                }
            }

            // pick ending sidings
            List<Tuple<int, Track>> endingSidingAssignments;

            // if train is 2 or less cars or there is only one storage track available
            if( (jobCarTypes.Count <= 2) || (destGenerator.StorageTracks.Count < 2) )
            {
                Track destTrack = TrackOrg.GetTrackThatHasEnoughFreeSpace(destGenerator.StorageTracks, trainLength);
                if( destTrack == null )
                {
                    PassengerJobs.ModEntry.Logger.Log($"No free storage space at {destGenerator.Controller.stationInfo.Name} for inbound job of {jobCarTypes.Count} coaches");
                    return null;
                }

                endingSidingAssignments = new List<Tuple<int, Track>>() 
                { 
                    new Tuple<int, Track>(jobCarTypes.Count, destTrack) 
                };
            }
            else
            {
                endingSidingAssignments = new List<Tuple<int, Track>>();
                var pool = destGenerator.StorageTracks.ToList();

                int startIdx = 0;
                foreach( ConsistSegment consist in startSubConsists )
                {
                    var carsForTrack = jobCarTypes.GetRange(startIdx, consist.CarCount);

                    float consistSize = TrackOrg.GetTotalCarTypesLength(carsForTrack) + TrackOrg.GetSeparationLengthBetweenCars(consist.CarCount);
                    
                    Track destTrack = TrackOrg.GetTrackThatHasEnoughFreeSpace(pool, consistSize);
                    if( destTrack == null )
                    {
                        PassengerJobs.ModEntry.Logger.Log($"No free storage space for 1 or more subconsist at {destGenerator.Controller.stationInfo.Name} for inbound job of {jobCarTypes.Count} coaches");
                        return null;
                    }

                    pool.Remove(destTrack);
                    endingSidingAssignments.Add(new Tuple<int, Track>(consist.CarCount, destTrack));
                    startIdx += consist.CarCount;
                }
            }

            // create job chain controller
            var lastStation = destStations.Last();
            var chainJobObject = new GameObject($"ChainJob[Passenger]: {Controller.logicStation.ID} - {lastStation.logicStation.ID}");
            chainJobObject.transform.SetParent(Controller.transform);
            var chainController = new PassengerTransportChainController(chainJobObject);

            StaticJobDefinition jobDefinition;

            //--------------------------------------------------------------------------------------------------------------------------------
            // Create consist assembly job
            PaymentCalculationData emptyPaymentData = GetJobPaymentData(jobCarTypes, true);
            float shuntingDistance = startSubConsists.Count * 250f;
            float shuntingPay = JobPaymentCalculator.CalculateJobPayment(JobType.ShuntingLoad, shuntingDistance, emptyPaymentData);

            StationsChainData chainData = new StationsChainData(Controller.stationInfo.YardID, lastStation.stationInfo.YardID);

            // spawn any new cars and register existing ones
            jobDefinition = PopulateAssemblyJobAndSpawn(
                chainController, Controller.logicStation, jobCarTypes, startSubConsists, startPlatform, chainData, shuntingPay);

            if( jobDefinition == null )
            {
                chainController.DestroyChain();
                PassengerJobs.ModEntry.Logger.Warning($"Failed to generate consist assembly job definition at {Controller.stationInfo.Name}");
                return null;
            }

            chainController.AddJobDefinitionToChain(jobDefinition);

            // get spawned cars for succesive jobs
            List<Car> chainLogicCars = chainController.trainCarsForJobChain.Select(tc => tc.logicCar).ToList();

            //--------------------------------------------------------------------------------------------------------------------------------
            // Create transport leg job
            var transChainData = new ComplexChainData(Controller.stationInfo.YardID, destStations.Select(s => s.stationInfo.YardID).ToArray());
            PaymentCalculationData transportPaymentData = GetJobPaymentData(jobCarTypes, false);
            float transportPayment = 0;
            float bonusLimit = 0;

            // calculate haul payment for all legs
            for( int i = 0; i < route.Length; i++ )
            {
                StationController fromStation = (i > 0) ? destStations[i - 1] : Controller;
                StationController toStation = destStations[i];

                float haulDistance = JobPaymentCalculator.GetDistanceBetweenStations(fromStation, toStation);
                bonusLimit += JobPaymentCalculator.CalculateHaulBonusTimeLimit(haulDistance, false);
                transportPayment += JobPaymentCalculator.CalculateJobPayment(JobType.Transport, haulDistance, transportPaymentData);
            }

            // scale job payment depending on settings
            float wageScale = PassengerJobs.Settings.UseCustomWages ? BASE_WAGE_SCALE : 1;
            transportPayment = Mathf.Round(transportPayment * 0.5f * wageScale);

            jobDefinition = PopulateTransportJobExistingCars(
                chainController, Controller.logicStation, startPlatform, destPlatforms,
                destStations.Select(s => s.stationInfo.YardID).ToArray(),
                chainLogicCars, transChainData, bonusLimit, transportPayment);

            if( jobDefinition == null )
            {
                PassengerJobs.ModEntry.Logger.Warning($"Failed to generate transport job definition for {chainController.jobChainGO.name}");
                chainController.DestroyChain();
                return null;
            }

            chainController.AddJobDefinitionToChain(jobDefinition);

            //--------------------------------------------------------------------------------------------------------------------------------
            // Create consist breakup job
            Track endPlatform = destPlatforms.Last();

            List<CarsPerTrack> finalPositions = new List<CarsPerTrack>();
            int offset = 0;
            foreach( Tuple<int, Track> consist in endingSidingAssignments )
            {
                List<Car> cars = chainLogicCars.Skip(offset).Take(consist.Item1).ToList();
                finalPositions.Add(new CarsPerTrack(consist.Item2, cars));
                offset += consist.Item1;
            }

            jobDefinition = PopulateBreakupJobExistingCars(
                chainController, lastStation.logicStation, endPlatform,
                finalPositions, chainData, shuntingPay);

            if( jobDefinition == null )
            {
                PassengerJobs.ModEntry.Logger.Warning($"Failed to generate consist breakup job definition for {chainController.jobChainGO.name}");
                chainController.DestroyChain();
                return null;
            }

            chainController.AddJobDefinitionToChain(jobDefinition);

            // Finalize job
            if( delayedInit )
            {
                PassengerJobs.ModEntry.Logger.Log($"Generated new passenger haul job: {chainJobObject.name} ({chainController.currentJobInChain.ID}) [waiting for incoming cars]");
            }
            else
            {
                chainController.FinalizeSetupAndGenerateFirstJob();
                PassengerJobs.ModEntry.Logger.Log($"Generated new passenger haul job: {chainJobObject.name} ({chainController.currentJobInChain.ID})");
            }

            return chainController;
        }

        private static PaymentCalculationData GetJobPaymentData( IEnumerable<TrainCarType> carTypes, bool emptyHaul = false )
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
            if( emptyHaul )
            {
                cargoTypeDict = new Dictionary<CargoType, int>();
            }
            else
            {
                cargoTypeDict = new Dictionary<CargoType, int>(1) { { CargoType.Passengers, totalCars } };
            }

            return new PaymentCalculationData(carTypeCount, cargoTypeDict);
        }

        private static StaticPassAssembleJobDefinition PopulateAssemblyJobAndSpawn(
            JobChainController chainController, Station station,
            List<TrainCarType> carTypes, IEnumerable<ConsistSegment> consistSegments,
            Track destinationTrack, StationsChainData chainData, float initialPay )
        {
            List<TrainCar> allTrainCars = new List<TrainCar>();
            List<CarsPerTrack> spawnedConsists = new List<CarsPerTrack>();
            int curIdx = 0;

            // get spawn data for each sub-consist
            var spawnData = new List<CarSpawner.SpawnData>();
            foreach( var subConsist in consistSegments )
            {
                if( subConsist.AlreadySpawned )
                {
                    allTrainCars.AddRange(subConsist.ExistingCars);
                }
                else
                {
                    RailTrack startRT = SingletonBehaviour<LogicController>.Instance.LogicToRailTrack[subConsist.Track];
                    List<TrainCarType> consistTypes = carTypes.GetRange(curIdx, subConsist.CarCount);

                    var data = CarSpawner.GetTrackMiddleBasedSpawnData(consistTypes, startRT, 0, false, true);
                    if( data.result != CarSpawner.SpawnDataResult.OK )
                    {
                        PassengerJobs.ModEntry.Logger.Warning($"Couldn't spawn coaches on track {subConsist.Track.ID.FullID}: result = {data.result}");
                        return null;
                    }

                    spawnData.Add(data);
                }
            }

            // try to do the actual spawning
            foreach( var consistSpawnData in spawnData )
            {
                Track logicTrack = consistSpawnData.track.logicTrack;
                var spawnedCars = CarSpawner.SpawnCars(consistSpawnData, true);

                if( spawnedCars == null )
                {
                    PassengerJobs.ModEntry.Logger.Warning($"Failed to spawn coaches on track {logicTrack.ID.FullID}, deleting all job cars");
                    SingletonBehaviour<CarSpawner>.Instance.DeleteTrainCars(allTrainCars, true);
                    return null;
                }

                var logicCars = TrainCar.ExtractLogicCars(spawnedCars);
                if( spawnedCars == null )
                {
                    PassengerJobs.ModEntry.Logger.Warning($"Failed to extract logic cars, deleting all job cars");
                    SingletonBehaviour<CarSpawner>.Instance.DeleteTrainCars(allTrainCars, true);
                    return null;
                }

                if( SkinManager_Patch.Enabled )
                {
                    SkinManager_Patch.UnifyConsist(spawnedCars);
                }

                allTrainCars.AddRange(spawnedCars);
                spawnedConsists.Add(new CarsPerTrack(logicTrack, logicCars));
            }

            chainController.trainCarsForJobChain = allTrainCars;
            return PopulateAssemblyJobExistingCars(chainController, station, destinationTrack, spawnedConsists, chainData, initialPay);
        }

        private static StaticPassAssembleJobDefinition PopulateAssemblyJobExistingCars(
            JobChainController chainController, Station station, Track destinationTrack,
            List<CarsPerTrack> startingCars, StationsChainData chainData, float initialPay )
        {
            StaticPassAssembleJobDefinition jobDefinition = chainController.jobChainGO.AddComponent<StaticPassAssembleJobDefinition>();
            jobDefinition.PopulateBaseJobDefinition(station, 0, initialPay, chainData, JobLicenses.Shunting | PassLicenses.Passengers1);

            jobDefinition.carsPerStartingTrack = startingCars;
            jobDefinition.destinationTrack = destinationTrack;

            return jobDefinition;
        }

        private static StaticPassDissasembleJobDefinition PopulateBreakupJobExistingCars(
            JobChainController chainController, Station station, Track startingTrack,
            List<CarsPerTrack> endingCars, StationsChainData chainData, float initialPay )
        {
            StaticPassDissasembleJobDefinition jobDefinition = chainController.jobChainGO.AddComponent<StaticPassDissasembleJobDefinition>();
            jobDefinition.PopulateBaseJobDefinition(station, 0, initialPay, chainData, JobLicenses.Shunting | PassLicenses.Passengers1);

            jobDefinition.startingTrack = startingTrack;
            jobDefinition.carsPerDestinationTrack = endingCars;

            return jobDefinition;
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

            return PopulateTransportJobExistingCars(chainController, startStation, startTrack, destTracks, destYards, logicCars, chainData, timeLimit, initialPay);
        }

        private static StaticPassengerJobDefinition PopulateTransportJobExistingCars(
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

        #region Commuter Haul Generation

        public void GenerateNewCommuterRun( TrainCarsPerLogicTrack consistInfo = null )
        {
            StationController destStation = null;
            Track startSiding;
            int nCars;
            float trainLength;
            List<TrainCarType> jobCarTypes;

            if( consistInfo == null )
            {
                // generate a consist
                nCars = Rand.Next(MIN_CARS_TRANSPORT, MAX_CARS_TRANSPORT + 1);
                jobCarTypes = PassCarTypes.ChooseMany(Rand, nCars);

                trainLength = TrackOrg.GetTotalCarTypesLength(jobCarTypes) + TrackOrg.GetSeparationLengthBetweenCars(nCars);

                // pick start storage track
                var availTracks = TrackOrg.FilterOutReservedTracks(TrackOrg.FilterOutOccupiedTracks(StorageTracks));
                startSiding = TrackOrg.GetTrackThatHasEnoughFreeSpace(availTracks, trainLength);

                if( startSiding == null )
                {
                    PassengerJobs.ModEntry.Logger.Log($"No available siding for new job at {Controller.stationInfo.Name}");
                    return;
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
                    return;
                }
            }

            // pick ending storage track
            Track destSiding = null;
            if( !CommuterDestinations.TryGetValue(Controller.stationInfo.YardID, out string[] possibleDestinations) )
            {
                PassengerJobs.ModEntry.Logger.Log("No commuter destination candidates found");
                return;
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
                return;
            }

            // create job chain controller
            var chainData = new StationsChainData(Controller.stationInfo.YardID, destStation.stationInfo.YardID);
            var chainJobObject = new GameObject($"ChainJob[Commuter]: {Controller.logicStation.ID} - {destStation.logicStation.ID}");
            chainJobObject.transform.SetParent(Controller.transform);
            var chainController = new CommuterChainController(chainJobObject);

            // calculate haul payment
            float haulDistance = JobPaymentCalculator.GetDistanceBetweenStations(Controller, destStation);
            float bonusLimit = JobPaymentCalculator.CalculateHaulBonusTimeLimit(haulDistance, false);
            float payment = JobPaymentCalculator.CalculateJobPayment(JobType.EmptyHaul, haulDistance, GetJobPaymentData(jobCarTypes, true));

            // create job definition & spawn cars
            StaticCommuterJobDefinition jobDefinition, returnJobDefinition;
            if( consistInfo != null )
            {
                // use existing cars
                chainController.trainCarsForJobChain = consistInfo.cars;

                jobDefinition = PopulateCommuterJobExistingCars(
                    chainController, Controller.logicStation, startSiding, destSiding,
                    consistInfo.cars.Select(tc => tc.logicCar).ToList(),
                    chainData, bonusLimit, payment);
            }
            else
            {
                // spawn cars & populate
                jobDefinition = PopulateCommuterJobAndSpawn(
                    chainController, Controller.logicStation, startSiding, destSiding, jobCarTypes, chainData, bonusLimit, payment);
            }

            if( jobDefinition == null )
            {
                chainController.DestroyChain();
                PassengerJobs.ModEntry.Logger.Warning($"Failed to generate commuter haul job at {Controller.stationInfo.Name}");
                return;
            }

            chainController.AddJobDefinitionToChain(jobDefinition);

            // generate return trip
            var logicCars = chainController.trainCarsForJobChain.Select(tc => tc.logicCar).ToList();
            var returnChain = new StationsChainData(chainData.chainDestinationYardId, chainData.chainOriginYardId);
            returnJobDefinition = PopulateCommuterJobExistingCars(chainController, destStation.logicStation, destSiding, startSiding, logicCars, returnChain, bonusLimit, payment);
            chainController.AddJobDefinitionToChain(returnJobDefinition);

            chainController.FinalizeSetupAndGenerateFirstJob(false);

            PassengerJobs.ModEntry.Logger.Log($"Generated new commuter job: {chainJobObject.name} ({chainController.currentJobInChain.ID})");
        }

        private static StaticCommuterJobDefinition PopulateCommuterJobAndSpawn(
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

            return PopulateCommuterJobExistingCars(chainController, startStation, startTrack, destTrack, logicCars, chainData, timeLimit, initialPay);
        }

        private static StaticCommuterJobDefinition PopulateCommuterJobExistingCars(
            JobChainController chainController, Station startStation,
            Track startTrack, Track destTrack, List<Car> logicCars,
            StationsChainData chainData, float timeLimit, float initialPay )
        {
            var jobDefinition = chainController.jobChainGO.AddComponent<StaticCommuterJobDefinition>();
            jobDefinition.PopulateBaseJobDefinition(startStation, timeLimit, initialPay, chainData, PassLicenses.Passengers1);
            jobDefinition.startingTrack = startTrack;
            jobDefinition.trainCarsToTransport = logicCars;
            jobDefinition.destinationTrack = destTrack;

            return jobDefinition;
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
    }

}
