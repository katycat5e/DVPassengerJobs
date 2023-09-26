using DV.Logic.Job;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace PassengerJobs.Generation
{
    public static class RouteSelector
    {
        private const string ROUTE_CONFIG_FILE = "routes.json";
        private const string DEFAULT_ROUTE_CONFIG_FILE = "default_routes.json";
        private static RouteConfig? _routeConfig = null;

        private static readonly Dictionary<string, PassStationData> _stations = new();

        public static bool IsPassengerStation(string yardId) => _routeConfig?.platforms.Any(p => p.yardId == yardId) == true;

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

        public static bool Initialized { get; private set; }
        public static void Initialize()
        {
            Initialized = true;
            foreach (var station in StationController.allStations)
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

            foreach (var stationData in _stations.Values)
            {
                var routeData = _routeConfig!.expressRoutes.FirstOrDefault(t => t.start == stationData.YardID);
                if (routeData != null)
                {
                    var routeStations = routeData.routes.Select(r => r.Select(s => _stations[s]).ToArray());
                    stationData.AddRoutes(routeStations);
                }
            }
        }

        private static Track GetTrackById(string id)
        {
            return RailTrackRegistry.Instance.AllTracks
                .First(rt => rt.logicTrack.ID.ToString() == id)
                .logicTrack;
        }

        public static PassStationData GetStationData(string yardId)
        {
            if (!Initialized) Initialize();

            return _stations[yardId];
        }

        public static RouteTrack[]? GetExpressRoute(PassStationData startStation, IEnumerable<string> existingDests, double minLength = 0)
        {
            if (!Initialized) Initialize();

            var graph = CreateGraph(startStation, existingDests, minLength);

            if (graph[0].Weight < 0.5f) return null;

            return graph[0].PickTracks();
        }

        private static List<RoutePath> CreateGraph(PassStationData startStation, IEnumerable<string> existingDests, double minLength = 0)
        {
            var graph = startStation.Destinations.Select(s => new RoutePath(s, TrackType.Platform, minLength)).ToList();

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

    public readonly struct RouteTrack
    {
        public readonly PassStationData Station;
        public readonly Track Track;

        public RouteTrack(PassStationData station, Track track)
        {
            Station = station;
            Track = track;
        }
    }

    public class RoutePath : IComparable<RoutePath>
    {
        public readonly RouteNode[] Nodes;
        public readonly TrackType TrackType;
        public float Weight;

        public RoutePath(PassStationData[] stations, TrackType trackType, double minLength = 0)
        {
            Nodes = stations.Select(s => new RouteNode(s, s.TracksOfType(trackType), minLength)).ToArray();
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
        public readonly PassStationData Station;
        private readonly List<Track> Tracks;
        public readonly float Weight;

        public RouteNode(PassStationData station, List<Track> tracks, double minLength = 0)
        {
            Station = station;
            Tracks = tracks;
            Weight = RecalculateWeight(tracks, minLength);
        }

        private static float RecalculateWeight(List<Track> tracks, double minLength)
        {
            var unused = tracks.GetUnusedTracks()
                .Where(t => t.length >= (minLength + YardTracksOrganizer.END_OF_TRACK_OFFSET_RESERVATION));

            //return ((float)unused.Count() / tracks.Count) + ();
            return (unused.Count() > 0) ? 1 : 0;
        }

        public readonly RouteTrack PickTrack()
        {
            return new RouteTrack(Station, Tracks.GetUnusedTracks().PickOne()!);
        }
    }
}
