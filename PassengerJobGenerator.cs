using DV.Logic.Job;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using TMPro;
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
        public static int MAX_CARS_EXPRESS = 5;

        public const int MIN_CARS_COMMUTE = 2;
        public static int MAX_CARS_COMMUTE = 3;

        public const float BASE_WAGE_SCALE = 0.5f;
        public const float BONUS_TO_BASE_WAGE_RATIO = 2f;

        public static TrainCarType[] PassCarTypes = new TrainCarType[]
        {
            TrainCarType.PassengerRed, TrainCarType.PassengerGreen, TrainCarType.PassengerBlue
        };

        public static Dictionary<string, StationController> PassDestinations = new Dictionary<string, StationController>();

        public static Dictionary<string, string[]> CommuterDestinations = new Dictionary<string, string[]>()
        {
            { "CSW", new string[] { "SW", "FRS", "FM", "OWC" } },
            { "FF",  new string[] { "IME", "CM", "IMW" } },
            { "GF",  new string[] { "OWN", "FRC", "SM" } },
            { "HB",  new string[] { "FM", "SM" } },
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

        public static RailTrack FindRTWithId( string id )
        {
            return AllRailTracks.FirstOrDefault(rt => id.Equals(rt.logicTrack.ID.ToString()));
        }

        public static Track FindTrackWithId( string id )
        {
            return AllTracks.FirstOrDefault(t => id.Equals(t.ID.ToString()));
        }

        private static readonly System.Random Rand = new System.Random(); // seeded with current time


        #region Generator Initialization

        public static readonly Dictionary<string, HashSet<string>> StorageTrackNames = new Dictionary<string, HashSet<string>>()
        {
            { "CSW",new HashSet<string>(){ "CSW-B2SP", "CSW-B1SP" } },
            { "MF", new HashSet<string>(){ "MF-D4SP" } },
            { "FF", new HashSet<string>(){ "FF-B3SP", "FF-B5SP", "FF-B4SP" } },
            { "HB", new HashSet<string>(){ "HB-F4SP", "HB-F3SP" } },
            { "GF", new HashSet<string>(){ "GF-C1SP" } }
        };

        public static readonly Dictionary<string, HashSet<string>> PlatformTrackNames = new Dictionary<string, HashSet<string>>()
        {
            { "CSW",new HashSet<string>(){ "CSW-B6LP", "CSW-B5LP", "CSW-B4LP", "CSW-B3LP" } },
            { "MF", new HashSet<string>(){ "MF-D1LP", "MF-D2LP" } },
            { "FF", new HashSet<string>(){ "FF-B1LP", "FF-B2LP" } },
            { "HB", new HashSet<string>(){ "HB-F1LP" } }, // not enough clearance: "HB-F-2-LP"
            { "GF", new HashSet<string>(){ "GF-C3LP" } } // reserved for pass-thru: "GF-C-2-LP"
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

                // check if the player is already inside the generation zone
                float playerDist = StationRange.PlayerSqrDistanceFromStationCenter;
                PlayerWasInGenerateRange = StationRange.IsPlayerInJobGenerationZone(playerDist);

                // create loading machines
                PlatformManager.CreateMachines(Controller, PlayerWasInGenerateRange);

                var sb = new StringBuilder($"Created generator for {Controller.stationInfo.Name}:\n");
                sb.Append("Coach Storage: ");
                sb.AppendLine(string.Join(", ", StorageTracks.Select(t => t.ID)));
                sb.Append("Platforms: ");
                sb.Append(string.Join(", ", PlatformTracks.Select(t => t.ID)));

                PassengerJobs.ModEntry.Logger.Log(sb.ToString());

                RegisterStation(Controller, this);
            }
        }

        private bool PlayerWasInGenerateRange = false;
        private bool TrackSignsAreGenerated = false;

        private Coroutine GenerationRoutine = null;

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
                StartGenerationAsync();
            }

            PlatformManager.SetSignStates(Controller.stationInfo.YardID, playerInGenerateRange);

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

        public void StopGeneration()
        {
            if( GenerationRoutine != null )
            {
                StopCoroutine(GenerationRoutine);
                GenerationRoutine = null;
            }
        }

        public void StartGenerationAsync()
        {
            StopGeneration();
            GenerationRoutine = StartCoroutine(GeneratePassengerJobs());
        }

        public System.Collections.IEnumerator GeneratePassengerJobs()
        {
            PassengerJobs.ModEntry.Logger.Log($"Generating jobs at {Controller.stationInfo.Name}");

            // Create commuter hauls until >= half of storage tracks are filled
            var existingChains = Controller.ProceduralJobsController.GetCurrentJobChains();
            //int nExtantCommutes = existingChains.Count(c => c is CommuterChainController);
            int nChains = existingChains.Count;

            double totalTrackSpace = StorageTracks.Select(t => t.length).Sum();

            // generate max 3 commuter chains from each station
            //int nCommToGenerate = 3 - nExtantCommutes;
            PassengerJobs.ModEntry.Logger.Log($"{Controller.stationInfo.YardID} has {nChains} pax jobs");

            // alternate express/commuter, starting w/ express
            bool genExpress = true;
            for( int attemptCounter = 5; attemptCounter > 0; attemptCounter-- )
            {
                // break on storage tracks >70% full
                double reservedSpace = StorageTracks.Select(t => TrackOrg.GetReservedSpace(t)).Sum();
                if( (reservedSpace / totalTrackSpace) >= 0.7d ) break;

                yield return new WaitForSeconds(0.2f);

                try
                {
                    JobChainController result;
                    if( genExpress )
                    {
                        result = GenerateNewTransportJob();
                    }
                    else
                    {
                        result = GenerateNewCommuterRun();
                    }
                }
                catch( Exception ex )
                {
                    PassengerJobs.ModEntry.Logger.LogException(ex);
                }

                genExpress = !genExpress;
            }

            GenerationRoutine = null;
            yield break;
        }

        #endregion

        #region Transport Job Generation

        public PassengerTransportChainController GenerateNewTransportJob( TrainCarsPerLogicTrack consistInfo = null )
        {
            int nTotalCars;
            List<TrainCarType> jobCarTypes;
            float trainLength;
            Track startSiding;

            // Establish the starting consist and its storage location
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

                // pick start storage track
                var emptyTracks = TrackOrg.FilterOutOccupiedTracks(StorageTracks);
                startSiding = TrackOrg.GetTrackThatHasEnoughFreeSpace(emptyTracks, trainLength);

                if( startSiding == null ) startSiding = TrackOrg.GetTrackThatHasEnoughFreeSpace(StorageTracks, trainLength);
            }
            else
            {
                // Use existing consist
                nTotalCars = consistInfo.cars.Count;
                jobCarTypes = consistInfo.cars.Select(car => car.carType).ToList();
                trainLength = TrackOrg.GetTotalCarTypesLength(jobCarTypes) + TrackOrg.GetSeparationLengthBetweenCars(nTotalCars);
                startSiding = consistInfo.track;
            }

            if( startSiding == null )
            {
                //PassengerJobs.ModEntry.Logger.Log($"No available starting siding for express job at {Controller.stationInfo.Name}");
                return null;
            }

            // Try to find a loading platform to use
            PlatformDefinition loadingPlatform = PlatformManager.PickPlatform(Controller.stationInfo.YardID);

            // Choose a route
            var destPool = PassDestinations.Values.ToList();
            Track destSiding = null;
            StationController destStation = null;

            // this prevents generating jobs like "ChainJob[Passenger]: FF - FF (FF-PE-47)"
            destPool.Remove(Controller);

            while( (destSiding == null) && (destPool.Count > 0) )
            {
                // search the possible destinations 1 by 1 until we find an opening (or we don't)
                destStation = destPool.ChooseOne(Rand);

                // pick ending platform
                PassengerJobGenerator destGenerator = LinkedGenerators[destStation];
                destSiding = TrackOrg.GetTrackThatHasEnoughFreeSpace(destGenerator.StorageTracks, trainLength);

                // remove this station from the pool
                destPool.Remove(destStation);
            }

            if( destSiding == null )
            {
                //PassengerJobs.ModEntry.Logger.Log($"No available destination siding for new job at {Controller.stationInfo.Name}");
                return null;
            }

            // Try to find an unloading platform
            PlatformDefinition unloadingPlatform = PlatformManager.PickPlatform(destStation.stationInfo.YardID);

            // create job chain controller
            var chainJobObject = new GameObject($"ChainJob[Passenger]: {Controller.logicStation.ID} - {destStation.logicStation.ID}");
            chainJobObject.transform.SetParent(Controller.transform);
            var chainController = new PassengerTransportChainController(chainJobObject);

            StaticPassengerJobDefinition jobDefinition;

            //--------------------------------------------------------------------------------------------------------------------------------
            // Create transport leg job
            var chainData = new StationsChainData(Controller.stationInfo.YardID, destStation.stationInfo.YardID);
            PaymentCalculationData transportPaymentData = GetJobPaymentData(jobCarTypes);

            // calculate haul payment
            float haulDistance = JobPaymentCalculator.GetDistanceBetweenStations(Controller, destStation);
            float bonusLimit = JobPaymentCalculator.CalculateHaulBonusTimeLimit(haulDistance, false);
            float transportPayment = JobPaymentCalculator.CalculateJobPayment(JobType.Transport, haulDistance, transportPaymentData);

            // calculate additional payment for shunting
            const float shuntDistance = 500f;
            PaymentCalculationData emptyPaymentData = GetJobPaymentData(jobCarTypes, true);
            float platformBonusTime = JobPaymentCalculator.CalculateShuntingBonusTimeLimit(1) * 0.7f + PlatformController.START_XFER_DELAY;

            if( loadingPlatform?.Initialized == true )
            {
                float loadPayment = JobPaymentCalculator.CalculateJobPayment(JobType.ShuntingLoad, shuntDistance, emptyPaymentData);
                transportPayment += loadPayment;
                bonusLimit += platformBonusTime;
            }

            if( unloadingPlatform?.Initialized == true )
            {
                float unloadPayment = JobPaymentCalculator.CalculateJobPayment(JobType.ShuntingUnload, shuntDistance, emptyPaymentData);
                transportPayment += unloadPayment;
                bonusLimit += platformBonusTime;
            }

            // scale job payment depending on settings
            float wageScale = PassengerJobs.Settings.UseCustomWages ? BASE_WAGE_SCALE : 1;
            transportPayment = Mathf.Round(transportPayment * wageScale);

            if( consistInfo == null )
            {
                jobDefinition = PopulateTransportJobAndSpawn(
                    chainController, Controller.logicStation, startSiding, destSiding,
                    jobCarTypes, chainData, bonusLimit, transportPayment, true);
            }
            else
            {
                chainController.trainCarsForJobChain = consistInfo.cars;

                jobDefinition = PopulateTransportJobExistingCars(
                    chainController, Controller.logicStation, startSiding, destSiding,
                    consistInfo.LogicCars, chainData, bonusLimit, transportPayment);
            }

            if( jobDefinition == null )
            {
                PassengerJobs.ModEntry.Logger.Warning($"Failed to generate transport job definition for {chainController.jobChainGO.name}");
                chainController.DestroyChain();
                return null;
            }
            jobDefinition.subType = PassJobType.Express;

            // Setup any warehouse tasks
            if( loadingPlatform?.Initialized == true )
            {
                jobDefinition.loadMachine = loadingPlatform.Controller.LogicMachine;
            }

            if( unloadingPlatform?.Initialized == true )
            {
                jobDefinition.unloadMachine = unloadingPlatform.Controller.LogicMachine;
            }

            chainController.AddJobDefinitionToChain(jobDefinition);

            // Finalize job
            chainController.FinalizeSetupAndGenerateFirstJob();
            PassengerJobs.ModEntry.Logger.Log($"Generated new passenger haul job: {chainJobObject.name} ({chainController.currentJobInChain.ID})");

            return chainController;
        }

        private static PaymentCalculationData GetJobPaymentData( IEnumerable<TrainCarType> carTypes, bool empty = false )
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

            Dictionary<CargoType, int> cargoTypeDict;
            if( empty )
            {
                cargoTypeDict = new Dictionary<CargoType, int>(0);
            }
            else
            {
                cargoTypeDict = new Dictionary<CargoType, int>(1) { { CargoType.Passengers, totalCars } };
            }

            return new PaymentCalculationData(carTypeCount, cargoTypeDict);
        }


        private static StaticPassengerJobDefinition PopulateTransportJobAndSpawn(
            JobChainController chainController, Station startStation,
            Track startTrack, Track destTrack, List<TrainCarType> carTypes,
            StationsChainData chainData, float timeLimit, float initialPay, bool unifyConsist = false )
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

            if( unifyConsist && SkinManager_Patch.Enabled )
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
                var emptyTracks = TrackOrg.FilterOutOccupiedTracks(StorageTracks);
                startSiding = TrackOrg.GetTrackThatHasEnoughFreeSpace(emptyTracks, trainLength);

                if( startSiding == null ) startSiding = TrackOrg.GetTrackThatHasEnoughFreeSpace(StorageTracks, trainLength);

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

            // Try to find a loading platform to use
            PlatformDefinition loadingPlatform = PlatformManager.PickPlatform(Controller.stationInfo.YardID);

            // pick ending storage track
            Track destSiding = null;
            if( !CommuterDestinations.TryGetValue(Controller.stationInfo.YardID, out string[] possibleDestinations) )
            {
                PassengerJobs.ModEntry.Logger.Log("No commuter destination candidates found");
                return null;
            }

            // search through all possible destinations until we find an open siding
            var destPool = possibleDestinations.ToList();
            while( (destSiding == null) && (destPool.Count > 0) )
            {
                string destYard = destPool.ChooseOne(Rand);
                destStation = SingletonBehaviour<LogicController>.Instance.YardIdToStationController[destYard];
                destSiding = TrackOrg.GetTrackThatHasEnoughFreeSpace(destStation.logicStation.yard.StorageTracks, trainLength);

                destPool.Remove(destYard);
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

            // calculate additional payment for shunting
            const float shuntDistance = 350f;
            PaymentCalculationData emptyPaymentData = GetJobPaymentData(jobCarTypes, true);
            float platformBonusTime = JobPaymentCalculator.CalculateShuntingBonusTimeLimit(1) * 0.7f + PlatformController.START_XFER_DELAY;

            if( loadingPlatform?.Initialized == true )
            {
                float loadPayment = JobPaymentCalculator.CalculateJobPayment(JobType.ShuntingLoad, shuntDistance, emptyPaymentData);
                payment += loadPayment;
                bonusLimit += platformBonusTime;
            }

            // create job definition & spawn cars
            StaticPassengerJobDefinition jobDefinition;
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
                //PassengerJobs.ModEntry.Logger.Warning($"Failed to generate commuter haul job at {Controller.stationInfo.Name}");
                return null;
            }
            jobDefinition.subType = PassJobType.Commuter;

            // Setup warehouse task
            if( loadingPlatform?.Initialized == true )
            {
                jobDefinition.loadMachine = loadingPlatform.Controller.LogicMachine;
            }

            chainController.AddJobDefinitionToChain(jobDefinition);

            chainController.FinalizeSetupAndGenerateFirstJob(false);

            PassengerJobs.ModEntry.Logger.Log($"Generated new commuter job: {chainJobObject.name} ({chainController.currentJobInChain.ID})");
            return chainController;
        }

        public CommuterChainController GenerateCommuterReturnTrip( TrainCarsPerLogicTrack consistInfo, StationController sourceStation )
        {
            // use existing consist
            int nCars = consistInfo.cars.Count;
            List<TrainCarType> jobCarTypes = consistInfo.cars.Select(c => c.carType).ToList();
            float trainLength = TrackOrg.GetTotalTrainCarsLength(consistInfo.cars) + TrackOrg.GetSeparationLengthBetweenCars(nCars);

            Track startSiding = consistInfo.track;

            if( startSiding == null )
            {
                PassengerJobs.ModEntry.Logger.Log("Invalid start siding from parent job");
                return null;
            }

            // pick ending storage track
            Track destSiding = TrackOrg.GetTrackThatHasEnoughFreeSpace(StorageTracks, trainLength);

            if( destSiding == null )
            {
                PassengerJobs.ModEntry.Logger.Warning("No suitable destination tracks found for new commute job");
                return null;
            }

            // Try to find an unloading platform to use
            PlatformDefinition unloadPlatform = PlatformManager.PickPlatform(Controller.stationInfo.YardID);

            // create job chain controller
            var chainData = new StationsChainData(sourceStation.stationInfo.YardID, Controller.stationInfo.YardID);
            var chainJobObject = new GameObject($"ChainJob[Commuter]: {sourceStation.logicStation.ID} - {Controller.logicStation.ID}");
            chainJobObject.transform.SetParent(sourceStation.transform);
            var chainController = new CommuterChainController(chainJobObject);

            // calculate haul payment
            float haulDistance = JobPaymentCalculator.GetDistanceBetweenStations(sourceStation, Controller);
            float bonusLimit = JobPaymentCalculator.CalculateHaulBonusTimeLimit(haulDistance, false);
            float payment = JobPaymentCalculator.CalculateJobPayment(JobType.Transport, haulDistance, GetJobPaymentData(jobCarTypes));

            // calculate additional payment for shunting
            const float shuntDistance = 350f;
            PaymentCalculationData emptyPaymentData = GetJobPaymentData(jobCarTypes, true);
            float platformBonusTime = JobPaymentCalculator.CalculateShuntingBonusTimeLimit(1) * 0.7f + PlatformController.START_XFER_DELAY;

            if( unloadPlatform?.Initialized == true )
            {
                float unloadPayment = JobPaymentCalculator.CalculateJobPayment(JobType.ShuntingUnload, shuntDistance, emptyPaymentData);
                payment += unloadPayment;
                bonusLimit += platformBonusTime;
            }

            // create job definition & use existing cars
            chainController.trainCarsForJobChain = consistInfo.cars;

            StaticPassengerJobDefinition jobDefinition = PopulateTransportJobExistingCars(
                chainController, sourceStation.logicStation, startSiding, destSiding,
                consistInfo.LogicCars, chainData, bonusLimit, payment);

            if( jobDefinition == null )
            {
                chainController.DestroyChain();
                PassengerJobs.ModEntry.Logger.Warning($"Failed to generate commuter haul (return) job at {Controller.stationInfo.Name}");
                return null;
            }
            jobDefinition.subType = PassJobType.Commuter;

            // Setup warehouse task
            if( unloadPlatform?.Initialized == true )
            {
                jobDefinition.unloadMachine = unloadPlatform.Controller.LogicMachine;
            }

            chainController.AddJobDefinitionToChain(jobDefinition);

            chainController.FinalizeSetupAndGenerateFirstJob(false);

            PassengerJobs.ModEntry.Logger.Log($"Generated commuter return trip: {chainJobObject.name} ({chainController.currentJobInChain.ID})");
            return chainController;
        }

        #endregion

        #region Reset Functions
        
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

        #endregion
    }

}
