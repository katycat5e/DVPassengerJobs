using DV.Logic.Job;
using Newtonsoft.Json;
using PassengerJobs.Platforms;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DV.Utils;
using UnityEngine;
using DV.PointSet;

namespace PassengerJobs.Generation
{
    public static class RouteManager
    {
        private const string STATION_CONFIG_FILE = "stations.json";
        private const string DEFAULT_STATION_CONFIG_FILE = "default_stations.json";

        private const string ROUTE_CONFIG_FILE = "routes.json";
        private const string DEFAULT_ROUTE_CONFIG_FILE = "default_routes.json";

        private static StationConfig? _stationConfig = null;
        private static RouteConfig? _routeConfig = null;

        private static readonly Dictionary<string, IPassDestination> _stations = new();

        public static bool IsPassengerStation(string yardId) => _stationConfig?.cityStations.Any(p => p.yardId == yardId) == true;

        static RouteManager()
        {
            UnloadWatcher.UnloadRequested += HandleGameUnloading;
        }

        //=====================================================================================
        #region Configuration Handling

        public static bool LoadConfig()
        {
            try
            {
                _stationConfig = ReadConfigFiles<StationConfig>(STATION_CONFIG_FILE, DEFAULT_STATION_CONFIG_FILE);
            }
            catch (Exception ex)
            {
                PJMain.Error("Failed to load station config data", ex);
                return false;
            }

            try
            {
                _routeConfig = ReadConfigFiles<RouteConfig>(ROUTE_CONFIG_FILE, DEFAULT_ROUTE_CONFIG_FILE);
            }
            catch (Exception ex)
            {
                PJMain.Error("Failed to load route config data", ex);
                return false;
            }

            return true;
        }

        private static T? ReadConfigFiles<T>(string mainFilename, string defaultFilename)
            where T : class
        {
            string configPath = Path.Combine(PJMain.ModEntry.Path, mainFilename);
            T? config = null;

            if (File.Exists(configPath))
            {
                try
                {
                    config = JsonConvert.DeserializeObject<T>(File.ReadAllText(configPath));
                }
                catch (Exception ex)
                {
                    PJMain.Error($"Failed to read custom {typeof(T).Name} settings", ex);
                    config = null;
                }
            }

            if (config == null)
            {
                try
                {
                    configPath = Path.Combine(PJMain.ModEntry.Path, defaultFilename);
                    config = JsonConvert.DeserializeObject<T>(File.ReadAllText(configPath));
                }
                catch (Exception ex)
                {
                    PJMain.Error($"Failed to read default {typeof(T).Name} settings", ex);
                    throw new InvalidOperationException($"Failed to load any data from config file {defaultFilename}", ex);
                }
            }

            if (config == null)
            {
                throw new InvalidOperationException($"Failed to load any data from config file {defaultFilename}");
            }

            return config;
        }

        private static JsonSerializerSettings _jsonSettings = new()
        {
            NullValueHandling = NullValueHandling.Ignore,
            DefaultValueHandling = DefaultValueHandling.Ignore,
            Culture = System.Globalization.CultureInfo.InvariantCulture,
        };

        private static void SaveStationConfig()
        {
            try
            {
                string configPath = Path.Combine(PJMain.ModEntry.Path, DEFAULT_STATION_CONFIG_FILE);

                string serialized = JsonConvert.SerializeObject(_stationConfig, Formatting.Indented, _jsonSettings);
                File.WriteAllText(configPath, serialized);
            }
            catch (Exception ex)
            {
                PJMain.Error("Failed to save station config", ex);
            }
        }

        public static bool TryGetRuralStation(string id, out StationConfig.RuralStation? result)
        {
            foreach (var station in _stationConfig!.ruralStations)
            {
                if (station.id == id)
                {
                    result = station;
                    return true;
                }
            }
            result = null;
            return false;
        }

        public static void SaveRuralStation(string id, Vector3 location, bool hideConcrete, bool hideLamps, bool swapSides)
        {
            StationConfig.RuralStation? station = null;

            foreach (var existing in _stationConfig!.ruralStations)
            {
                if (existing.id == id)
                {
                    station = existing;
                    break;
                }
            }

            if (station is null)
            {
                station = new StationConfig.RuralStation()
                {
                    id = id,
                };
                _stationConfig.ruralStations = _stationConfig.ruralStations.Append(station).OrderBy(s => s.id).ToArray();
            }

            station.location = location;
            station.hideConcrete = hideConcrete;
            station.hideLamps = hideLamps;
            station.swapSides = swapSides;

            SaveStationConfig();
            ReloadStations();
        }

        public static void ReloadStations()
        {
            LoadConfig();
            CreateRuralStations();
        }

        #endregion

        //=====================================================================================
        #region Startup

        public static void OnStationControllerStart(StationController station)
        {
            static IEnumerable<Track> ParseTracks(IEnumerable<string> ids)
            {
                return ids.Select(GetTrackById).Where(t => t is not null)!;
            }

            string yardId = station.stationInfo.YardID;
            if (_stationConfig!.cityStations.FirstOrDefault(t => t.yardId == yardId) is StationConfig.CityStation config)
            {
                var stationData = new PassStationData(station);
                stationData.AddPlatforms(ParseTracks(config.platforms));

                if (config.storage is not null)
                {
                    stationData.AddStorageTracks(ParseTracks(config.storage));
                }

                if (config.terminusTracks is not null)
                {
                    stationData.AddTerminusTracks(ParseTracks(config.terminusTracks));
                }
                else
                {
                    stationData.AddTerminusTracks(stationData.PlatformTracks);
                }

                _stations.Add(yardId, stationData);

                // add tracks to yard organizer
                foreach (var track in stationData.AllTracks)
                {
                    YardTracksOrganizer.Instance.InitializeYardTrack(track);
                    YardTracksOrganizer.Instance.yardTrackIdToTrack[track.ID.FullID] = track;
                }
            }
        }

        public static void CreateRuralStations()
        {
            //only generate rural stations on the default map
            if (SingletonBehaviour<SaveGameManager>.Instance.IsMapifyMap())
            {
                PJMain.Log("Skipping rural stations generation because this is a Mapify map");
                return;
            }

            foreach (var station in _stationConfig!.ruralStations)
            {
                RuralStationData? newStation;

                if (_stations.TryGetValue(station.id, out var stationData) && (stationData is RuralStationData existing))
                {
                    RuralStationBuilder.DestroyDecorations(existing.Platform);
                    newStation = RuralStationBuilder.CreateStation(station, existing);
                }
                else
                {
                    newStation = RuralStationBuilder.CreateStation(station);
                }

                if (newStation is null)
                {
                    _stations.Remove(station.id);
                    continue;
                }

                _stations[station.id] = newStation;

                if (newStation.Platform.IsYardTrack)
                {
                    PJMain.Log($"Created rural station {station.id} on {newStation.Platform.Track.ID}");
                }
                else
                {
                    PJMain.Log($"Created rural station {station.id} on {newStation.Platform.Track.ID} {newStation.Platform.LowerBound}-{newStation.Platform.UpperBound}");
                }
            }
        }

        private static bool _routesInitialized = false;
        public static void EnsureInitialized()
        {
            if (_routesInitialized) return;

            static IEnumerable<RouteData> GetRoutes(RouteType routeType, string[][] destIds)
            {
                foreach (var route in destIds)
                {
                    yield return new RouteData(routeType, route.Select(s => _stations[s]).ToArray());
                }
            }

            foreach (var stationData in _stations.Values.OfType<PassStationData>())
            {
                var routeData = _routeConfig!.expressRoutes?.FirstOrDefault(t => t.start == stationData.YardID);
                if (routeData != null)
                {
                    var routeStations = GetRoutes(RouteType.Express, routeData.routes);
                    stationData.ExpressRoutes.AddRange(routeStations);
                }

                var localRoutes = _routeConfig!.localRoutes?.FirstOrDefault(t => t.start == stationData.YardID);
                if (localRoutes != null)
                {
                    var routeStations = GetRoutes(RouteType.Local, localRoutes.routes);
                    stationData.RegionalRoutes.AddRange(routeStations);
                }
            }

            _routesInitialized = true;
        }

        private static void HandleGameUnloading()
        {
            _stations.Clear();
            _routesInitialized = false;
        }

        #endregion

        //=====================================================================================
        #region Route Calculation

        private static Track? GetTrackById(string id)
        {
            var railTrack = RailTrackRegistry.Instance.AllTracks
                .FirstOrDefault(rt => rt.LogicTrack().ID.ToString() == id);

            if (railTrack is null)
            {
                PJMain.Error($"GetTrackById({id}) not found");
            }

            return railTrack?.LogicTrack();
        }

        public static RouteTrack? GetRouteTrackById(string trackId)
        {
            return _stations.Values
                .SelectMany(s => s.GetPlatforms())
                .Cast<RouteTrack?>()
                .FirstOrDefault(t => t!.Value.PlatformID == trackId);
        }

        public static PassStationData GetStationData(string yardId)
        {
            return (PassStationData)_stations[yardId];
        }

        public static RouteResult? GetRoute(PassStationData startStation, RouteType routeType, IEnumerable<string> existingDests, double minLength = 0)
        {
            EnsureInitialized();
            
            var routeOptions = (routeType == RouteType.Express) ? startStation.ExpressRoutes : startStation.RegionalRoutes;
            var graph = CreateGraph(routeOptions, existingDests, minLength);
            
            if ((graph.Count == 0) || (graph[0].Weight < 0.5f)) return null;

            return new RouteResult(graph[0].RouteType, graph[0].PickTracks());
        }

        private static List<RoutePath> CreateGraph(IEnumerable<RouteData> routes, IEnumerable<string> existingDests, double minLength = 0)
        {
            var graph = routes.Select(s => new RoutePath(s, TrackType.Platform, minLength)).ToList();

            foreach (var route in graph)
            {
                // give extra weight to unused stations
                if (!existingDests.Contains(route.Nodes.Last().Station.YardID))
                {
                    route.Weight *= 2;
                }
            }

            graph.Sort();
            return graph;
        }

        #endregion
    }
}
