using DV.Logic.Job;
using DV.ThingTypes;
using MPAPI;
using PassengerJobs.Injectors;
using PassengerJobs.Platforms;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace PassengerJobs.Generation
{
    public class PassengerJobGenerator : MonoBehaviour
    {
        private static readonly Dictionary<string, PassengerJobGenerator> _instances = new();
        private const float BASE_WAGE_SCALE = 0.5f;
        private const float BASE_TO_BONUS_MULTIPLIER = 2;

        private const int MAX_REGIONAL_CARS = 4;

        public static bool TryGetInstance(string yardId, out PassengerJobGenerator generator) =>
            _instances.TryGetValue(yardId, out generator);

        static PassengerJobGenerator()
        {
            UnloadWatcher.UnloadRequested += HandleGameUnloading;
        }

        private static void HandleGameUnloading()
        {
            foreach (var instance in _instances.Values)
            {
                Destroy(instance);
            }

            _instances.Clear();

            PlatformController.HandleGameUnloading();
        }

        public static float GetBonusPayment(float basePayment)
        {
            if (PJMain.Settings.UseCustomWages)
            {
                return basePayment * BASE_TO_BONUS_MULTIPLIER;
            }
            else
            {
                return basePayment * 0.5f;
            }
        }

        public StationController Controller = null!;
        public readonly List<PlatformController> PlatformControllers = new();
        private StationJobGenerationRange _stationRange = null!;
        private PassStationData _stationData = null!;
        private Coroutine? _generationRoutine = null;

        private bool _playerWasInRange = false;
        private JobType _nextJobType;

        private bool PlayerIsInRange
        {
            get
            {
                if (MultiplayerAPI.IsMultiplayerLoaded && !MultiplayerAPI.Instance.IsHost)
                    return false;
                
                float playerDist = _stationRange.PlayerSqrDistanceFromStationCenter;

                return _stationRange.IsPlayerInJobGenerationZone(playerDist);
            }
        }

        public void Awake()
        {
            Controller = GetComponent<StationController>();
            if (!Controller)
            {
                PJMain.Error("Can't find StationController when creating PassengerJobGenerator");
                enabled = false;
                return;
            }

            _stationRange = GetComponent<StationJobGenerationRange>();
            _stationData = RouteManager.GetStationData(Controller.stationInfo.YardID);

            // check if the player is already inside the generation zone
            float playerDist = _stationRange.PlayerSqrDistanceFromStationCenter;
            _playerWasInRange = _stationRange.IsPlayerInJobGenerationZone(playerDist);

            _nextJobType = (new[] { PassJobType.Express, PassJobType.Local }).PickOneValue()!.Value;

            // create warehouse/sign controllers
            foreach (var platform in _stationData.Platforms)
            {
                var holder = new GameObject($"Platform_{platform.Track.ID}");
                holder.transform.SetParent(Controller.transform, false);

                var platformController = holder.AddComponent<PlatformController>();
                platformController.Platform = new StationPlatformWrapper(platform.Track);
                platformController.PlatformData = platform;
                platformController.SetDecorationsEnabled(_playerWasInRange);
                PlatformControllers.Add(platformController);

                PJMain.Log($"Successfully created platform controller for track {platform.Track.ID}");
            }

            var sb = new StringBuilder($"Created generator for {Controller.stationInfo.Name}:\n");
            sb.Append("Coach Storage: ");
            sb.AppendLine(string.Join(", ", _stationData.StorageTracks.Select(t => t.ID)));
            sb.Append("Platforms: ");
            sb.Append(string.Join(", ", _stationData.Platforms.Select(t => t.Track.ID)));
            PJMain.Log(sb.ToString());

            _instances.Add(_stationData.YardID, this);
        }

        public void Start()
        {
            GenerateTrackSigns();
        }

        private void GenerateTrackSigns()
        {
            // Use our associated station controller to create the track ID signs
            var allStationTracks = _stationData.AllTracks.ToHashSet();
            var stationRailTracks = RailTrackRegistry.Instance.AllTracks.Where(rt => allStationTracks.Contains(rt.LogicTrack())).ToList();

            Controller.GenerateTrackIdObject(stationRailTracks);
        }

        public void Update()
        {
            if (Controller.logicStation == null || !AStartGameData.carsAndJobsLoadingFinished)
            {
                return;
            }

            bool nowInRange = PlayerIsInRange;
            if (nowInRange && !_playerWasInRange)
            {
                StartGenerationAsync();
            }

            if (_playerWasInRange != nowInRange)
            {
                foreach (var platform in PlatformControllers)
                {
                    platform.SetDecorationsEnabled(nowInRange);
                }
            }

            _playerWasInRange = nowInRange;
        }

        public void StopGeneration()
        {
            if (_generationRoutine != null)
            {
                StopCoroutine(_generationRoutine);
                _generationRoutine = null;
            }
        }

        public void StartGenerationAsync()
        {
            StopGeneration();
            _generationRoutine = StartCoroutine(GeneratePassengerJobs());
        }

        private static readonly WaitForSeconds _generationDelay = WaitFor.Seconds(0.2f);

        private IEnumerator GeneratePassengerJobs()
        {
            int watchdog = 15;
            while ((watchdog > 0) && _stationData.TerminusTracks.GetUnusedTracks().Any())
            {
                yield return _generationDelay;

                try
                {
                    GenerateJob(_nextJobType);
                    _nextJobType = (_nextJobType == PassJobType.Express) ? PassJobType.Local : PassJobType.Express;
                }
                catch (Exception ex)
                {
                    PJMain.Error($"Error generating job @ {_stationData.YardID}", ex);
                }

                watchdog--;
            }

            _generationRoutine = null;
        }

        public PassengerChainController? GenerateJob(JobType jobType, PassConsistInfo? consistInfo = null)
        {
            int nTotalCars;
            List<TrainCarLivery> jobCarTypes;
            
            RouteTrack startPlatform;
            RouteResult? destinations;

            var currentDests = Controller.logicStation.availableJobs
                .Where(j => PassJobType.IsPJType(j.jobType))
                .Select(j => j.chainData.chainDestinationYardId);

            // Establish the starting consist and its storage location
            if (consistInfo == null)
            {
                // generate a new consist
                var potentialStart = _stationData.TerminusTracks.GetUnusedTracks().PickOne();
                if (potentialStart == null) return null;
                startPlatform = new RouteTrack(_stationData, potentialStart);

                destinations = RouteManager.GetRoute(_stationData, jobType.GetRouteType(), currentDests);
                if (destinations == null) return null;

                double minLength = Math.Min(startPlatform.Length, destinations.MinTrackLength);

                TrainCarLivery livery = ConsistManager.GetPassengerCars().PickOne()!;
                double carLength = CarSpawner.Instance.carLiveryToCarLength[livery];
                nTotalCars = (int)Math.Floor((minLength + CarSpawner.SEPARATION_BETWEEN_TRAIN_CARS) / (carLength + CarSpawner.SEPARATION_BETWEEN_TRAIN_CARS));

                if (jobType == PassJobType.Local)
                {
                    nTotalCars = Math.Min(nTotalCars, MAX_REGIONAL_CARS);
                }
                else
                {
                    nTotalCars -= 2;
                }

                jobCarTypes = Enumerable.Repeat(livery, nTotalCars).ToList();
            }
            else
            {
                // use existing consist
                nTotalCars = consistInfo.cars.Count;
                startPlatform = consistInfo.track;

                double consistLength = CarSpawner.Instance.GetTotalCarsLength(consistInfo.cars, true);
                destinations = RouteManager.GetRoute(_stationData, jobType.GetRouteType(), currentDests, consistLength);
                if (destinations == null) return null;

                jobCarTypes = consistInfo.cars.Select(c => c.carType).ToList();
            }

            // create job chain controller
            string destString = string.Join(" - ", destinations.Tracks.Select(d => d.Station.YardID));
            var chainJobObject = new GameObject($"ChainJob[Passenger]: {Controller.stationInfo.YardID} - {destString}");
            chainJobObject.transform.SetParent(Controller.transform);
            var chainController = new PassengerChainController(chainJobObject);


            //--------------------------------------------------------------------------------------------------------------------------------
            // Create multi stage transport job
            var chainData = new ExpressStationsChainData(Controller.stationInfo.YardID, destinations.Tracks.Select(d => d.Station.YardID).ToArray());
            PaymentCalculationData transportPaymentData = GetJobPaymentData(jobCarTypes);

            // calculate haul payment
            float haulDistance = GetTotalHaulDistance(Controller, destinations.Tracks);
            float bonusLimit = JobPaymentCalculator.CalculateHaulBonusTimeLimit(haulDistance, false);

            var equivalentPayJobType = (jobType == PassJobType.Local) ? JobType.EmptyHaul : JobType.Transport;
            float transportPayment = JobPaymentCalculator.CalculateJobPayment(equivalentPayJobType, haulDistance, transportPaymentData);


            // scale job payment depending on settings
            float wageScale = PJMain.Settings.UseCustomWages ? BASE_WAGE_SCALE : 1;
            transportPayment = Mathf.Round(transportPayment * wageScale);

            PassengerHaulJobDefinition? jobDefinition;
            if (consistInfo == null)
            {
                jobDefinition = PopulateExpressJobAndSpawn(
                    chainController, Controller.logicStation, startPlatform, destinations,
                    jobCarTypes, chainData, bonusLimit, transportPayment);
            }
            else
            {
                chainController.carsForJobChain = consistInfo.cars.ToList();

                jobDefinition = PopulateExpressJobExistingCars(
                    chainController, Controller.logicStation, startPlatform, destinations,
                    consistInfo.cars, chainData, bonusLimit, transportPayment);
            }

            if (jobDefinition == null)
            {
                PJMain.Warning($"Failed to generate transport job definition for {chainController.jobChainGO.name}");
                chainController.DestroyChain();
                return null;
            }

            chainController.AddJobDefinitionToChain(jobDefinition);

            // Finalize job
            chainController.FinalizeSetupAndGenerateFirstJob();
            PJMain.Log($"Generated new passenger haul job: {chainJobObject.name} ({chainController.currentJobInChain.ID})");

            return chainController;
        }

        private static float GetTotalHaulDistance(StationController startStation, IEnumerable<RouteTrack> destinations)
        {
            float totalDistance = 0;
            StationController source = startStation;

            foreach (var station in destinations.Select(t => t.Station).OfType<PassStationData>())
            {
                totalDistance += JobPaymentCalculator.GetDistanceBetweenStations(source, station.Controller);
                source = station.Controller;
            }

            return totalDistance;
        }

        private static PaymentCalculationData GetJobPaymentData(IEnumerable<TrainCarLivery> carTypes, bool empty = false)
        {
            var carTypeCount = new Dictionary<TrainCarLivery, int>();
            int totalCars = 0;

            foreach (var type in carTypes)
            {
                if (carTypeCount.TryGetValue(type, out int curCount))
                {
                    carTypeCount[type] = curCount + 1;
                }
                else carTypeCount[type] = 1;

                totalCars += 1;
            }

            Dictionary<CargoType, int> cargoTypeDict;
            if (empty)
            {
                cargoTypeDict = new Dictionary<CargoType, int>(0);
            }
            else
            {
                cargoTypeDict = new Dictionary<CargoType, int>(1) { { CargoInjector.PassengerCargo.v1, totalCars } };
            }

            return new PaymentCalculationData(carTypeCount, cargoTypeDict);
        }

        private static PassengerHaulJobDefinition? PopulateExpressJobAndSpawn(
            JobChainController chainController, Station startStation,
            RouteTrack startTrack, RouteResult route, List<TrainCarLivery> carTypes,
            ExpressStationsChainData chainData, float timeLimit, float initialPay)
        {
            // Spawn the cars
            RailTrack startRT = startTrack.Track.RailTrack();
            
            var spawnedCars = CarSpawner.Instance.SpawnCarTypesOnTrackRandomOrientation(carTypes, startRT, true, 
                true,0, false, false);

            if (spawnedCars == null) return null;

            chainController.carsForJobChain = spawnedCars.Select(c => c.logicCar).ToList();
            var logicCars = TrainCar.ExtractLogicCars(spawnedCars);
            if (logicCars == null)
            {
                PJMain.Error("Couldn't extract logic cars, deleting spawned cars");
                CarSpawner.Instance.DeleteTrainCars(spawnedCars, true);
                return null;
            }

            return PopulateExpressJobExistingCars(chainController, startStation,
                startTrack, route, logicCars,
                chainData, timeLimit, initialPay);
        }

        private static PassengerHaulJobDefinition PopulateExpressJobExistingCars(
            JobChainController chainController, Station startStation,
            RouteTrack startTrack, RouteResult route, List<Car> logicCars,
            StationsChainData chainData, float timeLimit, float initialPay)
        {
            // populate the actual job
            PassengerHaulJobDefinition jobDefinition = chainController.jobChainGO.AddComponent<PassengerHaulJobDefinition>();
            jobDefinition.PopulateBaseJobDefinition(startStation, timeLimit, initialPay, chainData, LicenseInjector.License.v1);

            jobDefinition.RouteType = route.RouteType;
            jobDefinition.TrainCarsToTransport = logicCars;
            jobDefinition.StartingTrack = startTrack;
            jobDefinition.DestinationTracks = route.Tracks;

            return jobDefinition;
        }
    }
}
