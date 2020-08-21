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
        public const int MAX_PASSENGER_JOBS = 2;
        public const int MAX_CARS_PER_HAUL = 4;
        

        public TrainCarType[] PassCarTypes = new TrainCarType[]
        {
            TrainCarType.PassengerRed, TrainCarType.PassengerGreen, TrainCarType.PassengerBlue
        };

        public static List<StationController> PassDestinations = new List<StationController>();

        internal static Dictionary<StationController, PassengerJobGenerator> LinkedGenerators =
            new Dictionary<StationController, PassengerJobGenerator>();

        private static List<Track> _AllTracks = null;
        private static List<Track> AllTracks
        {
            get
            {
                if( _AllTracks == null )
                {
                    _AllTracks = FindObjectsOfType<RailTrack>().Select(rt => rt.logicTrack).ToList();
                }
                return _AllTracks;
            }
        }

        private static System.Random Rand = new System.Random(); // seeded with current time

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
            //Regex reg = new Regex("\\[Y\\]_\\[" + station.stationInfo.YardID + "\\]_\\[[a-z0-9]+-(\\d+)-SP\\]", RegexOptions.IgnoreCase);

            var trackNames = StorageTrackNames[station.stationInfo.YardID];

            return AllTracks
                .Where(t => trackNames.Contains(t.ID.ToString()))
                .ToList();
        }

        internal static List<Track> GetLoadingTracks( StationController station )
        {
            //Regex reg = new Regex("\\[Y\\]_\\[" + station.stationInfo.YardID + "\\]_\\[[a-z0-9]+-(\\d+)-LP\\]", RegexOptions.IgnoreCase);

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
        
        private static T GetRandomFromList<T>( IList<T> list, T toExclude = default )
        {
            if( list == null || list.Count == 0 ) return default;

            T result;

            do
            {
                int i = Rand.Next(list.Count);
                result = list[i];
            }
            while( Equals(result, toExclude) );

            return result;
        }

        private static List<T> ChooseFromList<T>( IList<T> source, int count )
        {
            var result = new List<T>(count);

            for( int i = 0; i < count; i++ )
            {
                result.Add(GetRandomFromList(source));
            }

            return result;
        }

        private bool PlayerWasInGenerateRange = false;
        public void Update()
        {
            if( Controller.logicStation == null || !SaveLoadController.carsAndJobsLoadingFinished )
            {
                return;
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

        public void GeneratePassengerJobs()
        {
            PassengerJobs.ModEntry.Logger.Log($"Generating jobs at {Controller.stationInfo.Name}");

            StationController destStation = null;
            PassengerJobGenerator destGenerator = null;

            // generate a consist
            int nCars = Rand.Next(MAX_CARS_PER_HAUL) + 1;
            var jobCarTypes = ChooseFromList(PassCarTypes, nCars);

            float trainLength = TrackOrg.GetTotalCarTypesLength(jobCarTypes) + TrackOrg.GetSeparationLengthBetweenCars(nCars);

            // pick starting
            var availTracks = TrackOrg.FilterOutReservedTracks(TrackOrg.FilterOutOccupiedTracks(PlatformTracks));
            Track startTrack = TrackOrg.GetTrackThatHasEnoughFreeSpace(availTracks, trainLength);

            if( startTrack == null ) return;

            // pick ending track
            Track destTrack = null;
            for( int i = 0; (destTrack == null) && (i < 5); i++ )
            {
                destStation = GetRandomFromList(PassDestinations, Controller);
                destGenerator = LinkedGenerators[destStation];
                destTrack = TrackOrg.GetTrackThatHasEnoughFreeSpace(TrackOrg.FilterOutOccupiedTracks(destGenerator.PlatformTracks), trainLength);
            }
            if( destTrack == null ) return;

            // create job chain controller
            var chainData = new StationsChainData(Controller.stationInfo.YardID, destStation.stationInfo.YardID);
            var chainJobObject = new GameObject($"ChainJob[{JobType.Transport}]: {Controller.logicStation.ID} - {destStation.logicStation.ID}");
            chainJobObject.transform.SetParent(Controller.transform);
            var chainController = new JobChainControllerWithEmptyHaulGeneration(chainJobObject);

            // calculate payment
            float haulDistance = JobPaymentCalculator.GetDistanceBetweenStations(Controller, destStation);
            float bonusLimit = JobPaymentCalculator.CalculateHaulBonusTimeLimit(haulDistance, true);
            float payment = JobPaymentCalculator.CalculateJobPayment(JobType.Transport, haulDistance, GetPaymentData(jobCarTypes));

            // create job definition
            var jobDefinition = PopulateJobDefinition(chainController, Controller.logicStation, startTrack, destTrack, jobCarTypes, chainData, bonusLimit, payment);

            if( jobDefinition == null )
            {
                chainController.DestroyChain();
                PassengerJobs.ModEntry.Logger.Log($"Failed to generate job at {Controller.stationInfo.Name}");
                return;
            }

            // assign job to chain and finalize
            chainController.AddJobDefinitionToChain(jobDefinition);
            chainController.FinalizeSetupAndGenerateFirstJob();
        }

        private static PaymentCalculationData GetPaymentData( IEnumerable<TrainCarType> carTypes )
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

            var cargoTypeDict = new Dictionary<CargoType, int>(1) { { CargoType.Passengers, totalCars } };
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

            // populate the actual job
            StaticTransportJobDefinition jobDefinition = chainController.jobChainGO.AddComponent<StaticTransportJobDefinition>();
            jobDefinition.PopulateBaseJobDefinition(startStation, timeLimit, initialPay, chainData);

            jobDefinition.startingTrack = startTrack;
            jobDefinition.trainCarsToTransport = logicCars;
            jobDefinition.transportedCargoPerCar = carTypes.Select(ct => CargoType.Passengers).ToList();
            jobDefinition.cargoAmountPerCar = carTypes.Select(ct => 0.36f).ToList(); // 0.36 is ~27 avg humans
            jobDefinition.forceCorrectCargoStateOnCars = true;
            jobDefinition.destinationTrack = destTrack;

            return jobDefinition;
        }

        #endregion
    }

}
