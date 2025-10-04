using DV.Logic.Job;
using Newtonsoft.Json;
using PassengerJobs.Platforms;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DV.Utils;
using UnityEngine;
using PassengerJobs.Config;

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

        private static RouteGraph _localRoutes = new(RouteType.Local);

        private static readonly Dictionary<string, IPassDestination> _stations = new();

        public static StationConfig.CityStation[]? CityStations => _stationConfig?.cityStations?.ToArray();
        public static StationConfig.RuralStation[]? RuralStations => _stationConfig?.ruralStations?.ToArray();
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
            ApplyPlatformData();
        }

        public static void SetStations(StationConfig.CityStation[]? newCityStations, StationConfig.RuralStation[]? newRuralStations)
        {

            if(newCityStations == null && newRuralStations == null)
            {
                PJMain.Warning($"Tried to set stations, but new stations are null");
                return;
            }

            newCityStations ??= Array.Empty<StationConfig.CityStation>();
            newRuralStations ??= Array.Empty<StationConfig.RuralStation>();

            _stationConfig ??= new StationConfig();

            _stationConfig.cityStations = newCityStations;
            _stationConfig.ruralStations = newRuralStations;

            //CreateRuralStations();
            //ApplyPlatformData();
        }

        public static void SavePlatformConfig(string platformId, Vector3 cornerA, Vector3 cornerB, float depth)
        {
            string stationId = platformId.Split('-')[0];
            var station = _stationConfig!.cityStations.FirstOrDefault(s => s.yardId == stationId);

            if (station is null)
            {
                PJMain.Error($"Station {stationId} does not exist in the config");
                return;
            }

            StationConfig.CityPlatform? platform = station.platforms.FirstOrDefault(p => p.id == platformId);
            
            if (platform is null)
            {
                platform = new StationConfig.CityPlatform()
                {
                    id = platformId
                };

                station.platforms = station.platforms.Append(platform).ToArray();
            }

            platform.spawnZoneA = cornerA;
            platform.spawnZoneB = cornerB;
            platform.spawnZoneDepth = depth;

            SaveStationConfig();
            ReloadStations();
        }

        public static void ApplyPlatformData()
        {
            foreach (var station in _stationConfig!.cityStations)
            {
                foreach (var platform in station.platforms)
                {
                    if (PlatformController.TryGetControllerForTrack(platform.id, out var controller))
                    {
                        var platformData = new PassStationData.PlatformData(GetTrackById(platform.id)!, platform);
                        controller!.PlatformData = platformData;
                    }
                }
            }
        }

        #endregion

        //=====================================================================================
        #region Startup

        public static void OnStationControllerStart(StationController station)
        {
            string yardId = station.stationInfo.YardID;

            IEnumerable<Track> ParseTracks(IEnumerable<string> ids)
            {
                foreach (string id in ids)
                {
                    if (GetTrackById(id) is Track track)
                    {
                        yield return track;
                    }
                    else
                    {
                        PJMain.Error($"Invalid track id in config for station {yardId}: {id}");
                    }
                }
            }

            if (_stationConfig!.cityStations.FirstOrDefault(t => t.yardId == yardId) is StationConfig.CityStation config)
            {
                var stationData = new PassStationData(station);

                foreach (var platformConfig in config.platforms)
                {
                    if (GetTrackById(platformConfig.id) is Track track)
                    {
                        stationData.AddPlatform(track, platformConfig);
                    }
                    else
                    {
                        PJMain.Error($"Invalid track id in config for station {yardId}: {platformConfig.id}");
                    }
                }

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
                    stationData.AddTerminusTracks(stationData.Platforms.Select(p => p.Track));
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
                    PJMain.Log($"Created rural station {station.id} on {newStation.Platform.WarehouseTrack.ID}");
                }
                else
                {
                    PJMain.Log($"Created rural station {station.id} on {newStation.Platform.WarehouseTrack.ID} {newStation.Platform.LowerBound}-{newStation.Platform.UpperBound}");
                }
            }
        }

        private static bool _routesInitialized = false;
        public static void EnsureInitialized()
        {
            if (_routesInitialized) return;

            static IEnumerable<RouteData> GetRoutes(RouteType routeType, IEnumerable<string[]> destIds)
            {
                foreach (var route in destIds)
                {
                    yield return new RouteData(routeType, route.Select(s => _stations[s]).ToArray());
                }
            }

            _localRoutes.MinLength = _routeConfig!.minLocalLength;
            _localRoutes.MaxLength = _routeConfig!.maxLocalLength;

            BuildRouteGraph(_localRoutes, _routeConfig.localNodes);

            foreach (var stationData in _stations.Values.OfType<PassStationData>())
            {
                var routeData = _routeConfig!.expressRoutes?.FirstOrDefault(t => t.start == stationData.YardID);
                if (routeData != null)
                {
                    var routeStations = GetRoutes(RouteType.Express, routeData.routes);
                    stationData.ExpressRoutes.AddRange(routeStations);
                }

                // dynamically generated routes from station nodes
                var localRouteData = GetRoutes(RouteType.Local, _localRoutes.GetAllRoutes(stationData.YardID));
                stationData.RegionalRoutes.AddRange(localRouteData);

                // extra statically defined routes
                var localRoutes = _routeConfig!.localRoutes?.FirstOrDefault(t => t.start == stationData.YardID);
                if (localRoutes != null)
                {
                    localRouteData = GetRoutes(RouteType.Local, localRoutes.routes);
                    stationData.RegionalRoutes.AddRange(localRouteData);
                }
            }

            _routesInitialized = true;
        }

        private static void BuildRouteGraph(RouteGraph graph, IEnumerable<RouteConfig.Node> nodeData)
        {
            graph.Reset();

            foreach (var node in nodeData)
            {
                if (!_stations.TryGetValue(node.id, out var stationData))
                {
                    PJMain.Warning($"Couldn't match route node {node.id} with any station");
                    continue;
                }

                bool isTerminus = stationData.GetPlatforms(true).Any();
                graph.CreateNode(node.id, isTerminus);
            }

            foreach (var node in nodeData)
            {
                if (node.linkA is not null)
                {
                    foreach (var toConnect in node.linkA)
                    {
                        graph.CreateLink(node.id, toConnect, RouteGraph.LinkSide.A);
                    }
                }
                else
                {
                    PJMain.Warning($"No links specified for route node {node.id}");
                }

                if (node.linkB is not null)
                {
                    foreach (var toConnect in node.linkB)
                    {
                        graph.CreateLink(node.id, toConnect, RouteGraph.LinkSide.B);
                    }
                }
            }
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
