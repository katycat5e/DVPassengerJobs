using DV.Logic.Job;
using Newtonsoft.Json;
using PassengerJobs.Platforms;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace PassengerJobs.Generation
{
    public static class RouteManager
    {
        private const string ROUTE_CONFIG_FILE = "routes.json";
        private const string DEFAULT_ROUTE_CONFIG_FILE = "default_routes.json";
        private static RouteConfig? _routeConfig = null;

        private static readonly Dictionary<string, IPassDestination> _stations = new();

        public static bool IsPassengerStation(string yardId) => _routeConfig?.platforms.Any(p => p.yardId == yardId) == true;

        static RouteManager()
        {
            UnloadWatcher.UnloadRequested += HandleGameUnloading;
        }

        public static bool LoadConfig()
        {
            try
            {
                string configPath = Path.Combine(PJMain.ModEntry.Path, ROUTE_CONFIG_FILE);
                if (File.Exists(configPath))
                {
                    _routeConfig = JsonConvert.DeserializeObject<RouteConfig>(File.ReadAllText(configPath));
                }

                if (_routeConfig == null)
                {
                    configPath = Path.Combine(PJMain.ModEntry.Path, DEFAULT_ROUTE_CONFIG_FILE);
                    _routeConfig = JsonConvert.DeserializeObject<RouteConfig>(File.ReadAllText(configPath));
                }
            }
            catch (Exception ex)
            {
                PJMain.Error("Failed to load route config data", ex);
                return false;
            }

            if (_routeConfig == null)
            {
                PJMain.Error("Failed to load route config data");
                return false;
            }
            return true;
        }

        public static void OnStationControllerStart(StationController station)
        {
            string yardId = station.stationInfo.YardID;
            if (_routeConfig!.platforms.FirstOrDefault(t => t.yardId == yardId) is RouteConfig.TrackSet platforms)
            {
                var stationData = new PassStationData(station);
                stationData.AddPlatforms(platforms.tracks.Select(GetTrackById));

                var storage = _routeConfig.storage.FirstOrDefault(t => t.yardId == yardId);
                if (storage != null)
                {
                    stationData.AddStorageTracks(storage.tracks.Select(GetTrackById));
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
            foreach (var station in _routeConfig!.ruralStations)
            {
                var track = GetTrackById(station.trackId);
                var railTrack = track.GetRailTrack();

                var controller = railTrack.gameObject.AddComponent<PlatformController>();
                var platform = new RuralPlatformWrapper(station, track);
                controller.Platform = platform;

                _stations.Add(station.id, new RuralStationData(platform.LoadingMachine));

                RuralStationBuilder.GenerateDecorations(platform.LoadingMachine);

                PJMain.Log($"Created rural station {station.id} on {track.ID} {station.lowIdx}-{station.highIdx}");
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
                var routeData = _routeConfig!.expressRoutes.FirstOrDefault(t => t.start == stationData.YardID);
                if (routeData != null)
                {
                    var routeStations = GetRoutes(RouteType.Express, routeData.routes);
                    stationData.Routes.AddRange(routeStations);
                }

                var localRoutes = _routeConfig!.localRoutes.FirstOrDefault(t => t.start == stationData.YardID);
                if (localRoutes != null)
                {
                    var routeStations = GetRoutes(RouteType.Local, localRoutes.routes);
                    stationData.Routes.AddRange(routeStations);
                }
            }

            _routesInitialized = true;
        }

        private static void HandleGameUnloading()
        {
            _stations.Clear();
            _routesInitialized = false;
        }

        private static Track GetTrackById(string id)
        {
            return RailTrackRegistry.Instance.AllTracks
                .First(rt => rt.logicTrack.ID.ToString() == id)
                .logicTrack;
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

        public static RouteResult? GetExpressRoute(PassStationData startStation, IEnumerable<string> existingDests, double minLength = 0)
        {
            EnsureInitialized();

            var graph = CreateGraph(startStation.Routes, existingDests, minLength);

            if (graph[0].Weight < 0.5f) return null;

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
    }

    public sealed class RouteResult
    {
        public readonly RouteType RouteType;
        public readonly RouteTrack[] Tracks;

        public RouteResult(RouteType routeType, RouteTrack[] tracks)
        {
            RouteType = routeType;
            Tracks = tracks;
        }

        public double MinTrackLength => Tracks.Min(t => t.Length);
    }

    public readonly struct RouteTrack
    {
        public readonly IPassDestination Station;
        public readonly Track Track;
        public readonly int LowBound;
        public readonly int HighBound;

        public bool IsSegment => (LowBound >= 0) && (HighBound >= 0);

        public double Length
        {
            get
            {
                if (IsSegment)
                {
                    var rail = Track.GetRailTrack().GetPointSet();
                    return rail.points[HighBound].span - rail.points[LowBound].span;
                }
                else
                {
                    return Track.length;
                }
            }
        }

        public string PlatformID => IsSegment ? Station.YardID : Track.ID.ToString();
        public string SaveID => IsSegment ? Station.YardID : Track.ID.FullID;
        public string DisplayID => IsSegment ? $"{Station.YardID}-LP" : Track.ID.ToString();

        public RouteTrack(IPassDestination station, Track track)
        {
            Station = station;
            Track = track;
            LowBound = HighBound = -1;
        }

        public RouteTrack(IPassDestination station, Track track, int lowBound, int highBound) :
            this(station, track)
        {
            LowBound = lowBound;
            HighBound = highBound;
        }
    }

    public class RoutePath : IComparable<RoutePath>
    {
        public readonly RouteType RouteType;
        public readonly RouteNode[] Nodes;
        public readonly TrackType TrackType;
        public float Weight;

        public RoutePath(RouteData stations, TrackType trackType, double minLength = 0)
        {
            RouteType = stations.RouteType;
            Nodes = stations.Destinations.Select(s => new RouteNode(s, minLength)).ToArray();
            TrackType = trackType;

            if (Nodes.Any(n => n.Weight == 0))
            {
                Weight = 0;
            }
            else
            {
                Weight = 1 + GetWeightNoise();
            }
        }

        private static float GetWeightNoise()
        {
            // +/- 0.25
            return (UnityEngine.Random.value + 1) / 4;
        }

        public RouteTrack[] PickTracks()
        {
            var result = new RouteTrack[Nodes.Length];
            for (int i = 0; i < Nodes.Length; i++)
            {
                result[i] = Nodes[i].PickTrack();
            }
            return result;
        }

        public int CompareTo(RoutePath other)
        {
            return other.Weight.CompareTo(Weight);
        }
    }

    public readonly struct RouteNode
    {
        public readonly IPassDestination Station;
        public readonly IEnumerable<RouteTrack> Tracks;
        public readonly float Weight;

        public RouteNode(IPassDestination station, double minLength = 0)
        {
            Station = station;
            Tracks = station.GetPlatforms();
            Weight = RecalculateWeight(Tracks, minLength);
        }

        private static float RecalculateWeight(IEnumerable<RouteTrack> tracks, double minLength)
        {
            var unused = tracks.GetUnusedTracks()
                .Where(t => t.Length >= (minLength + YardTracksOrganizer.END_OF_TRACK_OFFSET_RESERVATION));

            //return ((float)unused.Count() / tracks.Count) + ();
            return unused.Any() ? 1 : 0;
        }

        public readonly RouteTrack PickTrack()
        {
            return Tracks.GetUnusedTracks().PickOneValue()!.Value;
        }
    }
}
