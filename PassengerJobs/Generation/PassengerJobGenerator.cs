using DV.Logic.Job;
using DV.ThingTypes;
using PassengerJobs.Injectors;
using PassengerJobs.Platforms;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

using ThreadTask = System.Threading.Tasks.Task;

namespace PassengerJobs.Generation
{
    public class PassengerJobGenerator : MonoBehaviour
    {
        private class DelayedGenTracker
        {
            public bool Finished = false;
            public PassengerChainController? Controller;
            public int FrameCount = 0;

            public void Finish(PassengerChainController? controller)
            {
                Finished = true;
                Controller = controller;
            }
        }

        private static readonly Dictionary<string, PassengerJobGenerator> _instances = new();
        private const float BASE_WAGE_SCALE = 0.5f;
        private const float BASE_TO_BONUS_MULTIPLIER = 2;
        private const float WIGGLE_DISTANCE = 4;
        private const float LENGTH_MULTIPLIER = 0.95f;
        // Time taken for a stop along the route.
        // This should count both the unloading/loading + stopping and going.
        private const float STOPPING_TIME = 90f;

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

        public readonly List<PlatformController> PlatformControllers = new();
        public StationController Controller = null!;

        private readonly Dictionary<PassConsistInfo, Coroutine> _consistGenRoutines = new();
        private StationJobGenerationRange _stationRange = null!;
        private PassStationData _stationData = null!;
        private Coroutine? _generationRoutine = null;
        private bool _playerWasInRange = false;
        private JobType _nextJobType;

        private bool PlayerIsInRange
        {
            get
            {
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

        #region Regular Generation

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
            _generationRoutine = StartCoroutine(GeneratePassengerJobsRoutine());
        }

        private IEnumerator GeneratePassengerJobsRoutine()
        {
            int watchdog = 15;

            while ((watchdog > 0) && _stationData.TerminusTracks.GetUnusedTracks().Any())
            {
                yield return WaitFor.FixedUpdate;

                var sw = System.Diagnostics.Stopwatch.StartNew();
                var tracker = new DelayedGenTracker();

                while (!tracker.Finished)
                {
                    yield return GenerateJobDelayed(_nextJobType, tracker);
                }

                _nextJobType = (_nextJobType == PassJobType.Express) ? PassJobType.Local : PassJobType.Express;
                watchdog--;
                sw.Stop();

                if (tracker.Controller == null) PJMain.Log($"No job was generated");
                PJMain.Log($"Total gen time: {sw.Elapsed.TotalSeconds:F4}s / Frames: {tracker.FrameCount}");
            }

            _generationRoutine = null;
        }

        private IEnumerator GenerateJobDelayed(JobType jobType, DelayedGenTracker tracker)
        {
            bool randomOrientation = true;
            List<TrainCarLivery> jobCarTypes;
            RouteTrack startPlatform;
            RouteResult? destinations = null;

            var currentDests = Controller.logicStation.availableJobs
                .Where(j => PassJobType.IsPJType(j.jobType))
                .Select(j => j.chainData.chainDestinationYardId);

            // Establish the starting consist and its storage location.
            var potentialStart = _stationData.TerminusTracks.GetUnusedTracks().PickOne();
            if (potentialStart == null) goto ReturnEmpty;
            startPlatform = new RouteTrack(_stationData, potentialStart);

            var routeType = jobType.GetRouteType();
            var routeWorking = true;

            // Execute this generation in a separate thread as it is expensive.
            ThreadTask.Run(() =>
            {
                destinations = RouteManager.GetRoute(_stationData, routeType, currentDests);
                routeWorking = false;
            });

            while (routeWorking)
            {
                yield return null;
                tracker.FrameCount++;
            }

            if (destinations == null) goto ReturnEmpty;

            double maxAllowedLength = Math.Min(startPlatform.Length, destinations.MinTrackLength);
            maxAllowedLength = (maxAllowedLength * LENGTH_MULTIPLIER) - WIGGLE_DISTANCE;

            TrainCarLivery livery = ConsistManager.GetFilteredPassengerCars(routeType, maxAllowedLength).PickOne()!;

            if (CCLIntegration.TryGetTrainset(livery, out var trainset) && CCLIntegration.IsTrainsetEnabled(trainset))
            {
                // Use the trainset itself directly. Length has already been checked, so it fits.
                jobCarTypes = trainset.ToList();
                randomOrientation = false;

                // Check if it's possible to have more of the trainset spawn.
                var count = CCLIntegration.GetMaxRepeatedSpawn(livery);
                var current = 1;
                var length = CarSpawner.Instance.GetTotalCarLiveriesLength(jobCarTypes, true);
                var total = length + CarSpawner.SEPARATION_BETWEEN_TRAIN_CARS + length;

                while (total < maxAllowedLength && current < count)
                {
                    jobCarTypes.AddRange(trainset);
                    current++;
                    total += CarSpawner.SEPARATION_BETWEEN_TRAIN_CARS + length;
                }
            }
            else
            {
                // Regular single livery consist.
                double carLength = CarSpawner.Instance.carLiveryToCarLength[livery];
                int nTotalCars = (int)Math.Floor(maxAllowedLength / (carLength + CarSpawner.SEPARATION_BETWEEN_TRAIN_CARS));

                if (jobType == PassJobType.Local)
                {
                    nTotalCars = Math.Min(nTotalCars, MAX_REGIONAL_CARS);
                }
                else if (nTotalCars > 6)
                {
                    nTotalCars -= 2;
                }

                nTotalCars = Mathf.Min(nTotalCars, CCLIntegration.GetMaxRepeatedSpawn(livery), Controller.proceduralJobsRuleset.maxCarsPerJob);
                jobCarTypes = Enumerable.Repeat(livery, nTotalCars).ToList();
            }

            var chainController = GenerateController(jobType, null, jobCarTypes, startPlatform, destinations, randomOrientation);
            tracker.Finish(chainController);
            yield break;

        ReturnEmpty:
            tracker.Finish(null);
            yield break;
        }

        #endregion

        #region Consist Generation

        public void StartGenerationFromConsistAsync(JobType jobType, PassConsistInfo consistInfo)
        {
            if (_consistGenRoutines.TryGetValue(consistInfo, out var coroutine))
            {
                StopCoroutine(coroutine);
            }

            _consistGenRoutines[consistInfo] = StartCoroutine(GeneratePassengerJobsFromConsistRoutine(jobType, consistInfo));
        }

        public PassengerChainController? GenerateJobFromConsist(JobType jobType, PassConsistInfo consistInfo)
        {
            var currentDests = Controller.logicStation.availableJobs
                .Where(j => PassJobType.IsPJType(j.jobType))
                .Select(j => j.chainData.chainDestinationYardId);

            // Use existing consist.
            double consistLength = CarSpawner.Instance.GetTotalCarsLength(consistInfo.cars, true);
            var destinations = RouteManager.GetRoute(_stationData, jobType.GetRouteType(), currentDests, consistLength);

            if (destinations == null) return null;
            List<TrainCarLivery> jobCarTypes = consistInfo.cars.Select(c => c.carType).ToList();

            return GenerateController(jobType, consistInfo, jobCarTypes, consistInfo.track, destinations, false);
        }

        private IEnumerator GeneratePassengerJobsFromConsistRoutine(JobType jobType, PassConsistInfo consistInfo)
        {
            int watchdog = 5;

            while (watchdog > 0)
            {
                yield return WaitFor.FixedUpdate;

                var sw = System.Diagnostics.Stopwatch.StartNew();
                var tracker = new DelayedGenTracker();

                while (!tracker.Finished)
                {
                    yield return GenerateJobFromConsistDelayed(jobType, consistInfo, tracker);
                }

                watchdog--;
                sw.Stop();

                if (tracker.Controller != null)
                {
                    PJMain.Log($"Job generation was successful");
                    PJMain.Log($"Total gen time: {sw.Elapsed.TotalSeconds:F4}s / Frames: {tracker.FrameCount}");
                    break;
                }

                PJMain.Log($"No job for consist was generated");
                PJMain.Log($"Total gen time: {sw.Elapsed.TotalSeconds:F4}s / Frames: {tracker.FrameCount}");
            }

            _consistGenRoutines.Remove(consistInfo);
        }

        private IEnumerator GenerateJobFromConsistDelayed(JobType jobType, PassConsistInfo consistInfo, DelayedGenTracker tracker)
        {
            var currentDests = Controller.logicStation.availableJobs
                .Where(j => PassJobType.IsPJType(j.jobType))
                .Select(j => j.chainData.chainDestinationYardId);

            // Use existing consist.
            double consistLength = CarSpawner.Instance.GetTotalCarsLength(consistInfo.cars, true);
            RouteResult? destinations = null;
            var routeWorking = true;

            // Execute this generation in a separate thread as it is expensive.
            ThreadTask.Run(() =>
            {
                destinations = RouteManager.GetRoute(_stationData, jobType.GetRouteType(), currentDests, consistLength);
                routeWorking = false;
            });

            while (routeWorking)
            {
                yield return null;
                tracker.FrameCount++;
            }

            if (destinations == null)
            {
                tracker.Finish(null);
                yield break;
            }

            List<TrainCarLivery> jobCarTypes = consistInfo.cars.Select(c => c.carType).ToList();

            var chainController = GenerateController(jobType, null, jobCarTypes, consistInfo.track, destinations, false);
            tracker.Finish(chainController);
            yield break;
        }

        #endregion

        private PassengerChainController? GenerateController(JobType jobType, PassConsistInfo? consistInfo, List<TrainCarLivery> jobCarTypes,
            RouteTrack startPlatform, RouteResult destinations, bool randomOrientation)
        {
            // Create job chain controller.
            string destString = string.Join(" - ", destinations.Tracks.Select(d => d.Station.YardID));
            var chainJobObject = new GameObject($"ChainJob[Passenger]: {Controller.stationInfo.YardID} - {destString}");
            chainJobObject.transform.SetParent(Controller.transform);
            var chainController = new PassengerChainController(chainJobObject);

            //--------------------------------------------------------------------------------------------------------------------------------
            // Create multi stage transport job.
            var chainData = new ExpressStationsChainData(Controller.stationInfo.YardID, destinations.Tracks.Select(d => d.Station.YardID).ToArray());
            PaymentCalculationData transportPaymentData = GetJobPaymentData(jobCarTypes);

            // Calculate haul payment.
            float haulDistance = GetTotalHaulDistance(Controller, destinations.Tracks);
            float bonusLimit = JobPaymentCalculator.CalculateHaulBonusTimeLimit(haulDistance, false) * GetTimeMultiplier(jobType) * PJMain.Settings.TimeScale;
            bonusLimit += GetTimeForStops(destinations);

            float transportPayment = JobPaymentCalculator.CalculateJobPayment(JobType.Transport, haulDistance, transportPaymentData);

            // Scale job payment depending on settings.
            float wageScale = PJMain.Settings.UseCustomWages ? BASE_WAGE_SCALE : 1;
            transportPayment = Mathf.Round(transportPayment * wageScale);

            PassengerHaulJobDefinition? jobDefinition;
            if (consistInfo == null)
            {
                jobDefinition = PopulateExpressJobAndSpawn(
                    chainController, Controller.logicStation, startPlatform, destinations,
                    jobCarTypes, chainData, bonusLimit, transportPayment, randomOrientation);
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

            // Finalize job.
            chainController.FinalizeSetupAndGenerateFirstJob();
            PJMain.Log($"Generated new passenger haul job: {chainJobObject.name} ({chainController.currentJobInChain.ID})");
            return chainController;
        }

        private static float GetTotalHaulDistance(StationController startStation, IEnumerable<RouteTrack> destinations)
        {
            float totalDistance = 0;
            Vector3 sourceLocation = startStation.transform.position;

            foreach (var station in destinations.Select(t => t.Station))
            {
                totalDistance += Vector3.Distance(sourceLocation, station.GetLocation());
                sourceLocation = station.GetLocation();
            }

            return totalDistance;
        }

        // Express jobs were much easier to complete on time than locals, so this artifically changes the balance.
        private static float GetTimeMultiplier(JobType jobType) => jobType switch
        {
            PassJobType.Local => 1.1f,
            PassJobType.Express => 0.8f,
            _ => 1.0f,
        };

        // The regular time calculation doesn't account for stops during the route, which this should help with.
        private static float GetTimeForStops(RouteResult route) => Mathf.Max(0, route.Tracks.Length - 1) * STOPPING_TIME;

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

            Dictionary<CargoType, int> cargoTypeDict = empty ? new(0) : new(1) { { CargoInjector.PassengerCargo.v1, totalCars } };
            return new PaymentCalculationData(carTypeCount, cargoTypeDict);
        }

        private static PassengerHaulJobDefinition? PopulateExpressJobAndSpawn(
            JobChainController chainController, Station startStation,
            RouteTrack startTrack, RouteResult route, List<TrainCarLivery> carTypes,
            ExpressStationsChainData chainData, float timeLimit, float initialPay, bool randomOrientation)
        {
            // Spawn the cars.
            RailTrack startRT = startTrack.Track.RailTrack();

            var spawnedCars = SpawnCars();

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

            List<TrainCar> SpawnCars()
            {
                bool flipConsist = UnityEngine.Random.value <= 0.5f;

                // Bias toward buffers/track end. This looks better on terminal stations like CW.
                if (!startRT.inIsConnected)
                {
                    return CarSpawner.Instance.SpawnCarTypesOnTrackStrict(carTypes, startRT, true, true, WIGGLE_DISTANCE,
                        flipConsist, randomOrientation, false);
                }

                // Same but the other side.
                if (!startRT.outIsConnected)
                {
                    // It's defined by the distance from the start of the "in" of the track, so position must be reversed.
                    var position = startTrack.Length - CarSpawner.Instance.GetTotalCarLiveriesLength(carTypes, true);
                    return CarSpawner.Instance.SpawnCarTypesOnTrackStrict(carTypes, startRT, true, true, position - WIGGLE_DISTANCE,
                        flipConsist, randomOrientation, false);
                }

                // Else use the regular middle based spawn data.
                if (randomOrientation)
                {
                    return CarSpawner.Instance.SpawnCarTypesOnTrackRandomOrientation(carTypes, startRT,
                        true, true, 0, flipConsist, false);
                }
                else
                {
                    return CarSpawner.Instance.SpawnCarTypesOnTrack(carTypes, Enumerable.Repeat(false, carTypes.Count).ToList(), startRT,
                        true, true, 0, flipConsist, false);
                }
            }
        }

        private static PassengerHaulJobDefinition PopulateExpressJobExistingCars(
            JobChainController chainController, Station startStation,
            RouteTrack startTrack, RouteResult route, List<Car> logicCars,
            StationsChainData chainData, float timeLimit, float initialPay)
        {
            // populate the actual job
            PassengerHaulJobDefinition jobDefinition = chainController.jobChainGO.AddComponent<PassengerHaulJobDefinition>();
            JobLicenses requiredLicenses = JobLicenses.Fragile;
            requiredLicenses |= (route.RouteType == RouteType.Express) ? LicenseInjector.License2.v1 : LicenseInjector.License1.v1;
            requiredLicenses |= (logicCars.Count > 10) ? JobLicenses.TrainLength2 : 0;
            requiredLicenses |= (logicCars.Count > 5 && logicCars.Count <= 10) ? JobLicenses.TrainLength1 : 0;

            jobDefinition.PopulateBaseJobDefinition(startStation, timeLimit, initialPay, chainData, requiredLicenses);

            jobDefinition.RouteType = route.RouteType;
            jobDefinition.TrainCarsToTransport = logicCars;
            jobDefinition.StartingTrack = startTrack;
            jobDefinition.DestinationTracks = route.Tracks;

            return jobDefinition;
        }
    }
}
