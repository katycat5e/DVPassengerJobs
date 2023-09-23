using DV.Logic.Job;
using System;
using System.Collections.Generic;
using System.Linq;

namespace PassengerJobs.Generation
{
    public static class RouteSelector
    {
        public static readonly Dictionary<string, string[]> StorageTrackNames = new()
        {
            { "CSW",new[] { "CSW-B2SP", "CSW-B1SP" } },
            { "MF", new[] { "MF-D4SP" } },
            { "FF", new[] { "FF-B3SP", "FF-B5SP", "FF-B4SP" } },
            { "HB", new[] { "HB-F4SP", "HB-F3SP" } },
            { "GF", new[] { "GF-C1SP" } }
        };

        public static readonly Dictionary<string, string[]> PlatformTrackNames = new()
        {
            { "CSW",new[] { "CSW-B6LP", "CSW-B5LP", "CSW-B4LP", "CSW-B3LP" } },
            { "MF", new[] { "MF-D1LP", "MF-D2LP" } },
            { "FF", new[] { "FF-B1LP", "FF-B2LP" } },
            { "HB", new[] { "HB-F1LP", "HB-F2LP" } },
            { "GF", new[] { "GF-C3LP" } } // reserved for pass-thru: "GF-C2LP"
        };

        private static readonly Dictionary<string, PassStationData> _stations = new();
        private static readonly List<RouteNode> _expressGraph = new();

        public static bool Initialized { get; private set; }
        public static void Initialize()
        {
            Initialized = true;
            foreach (var station in StationController.allStations)
            {
                string yardId = station.stationInfo.YardID;
                if (PlatformTrackNames.TryGetValue(yardId, out string[] platformIds))
                {
                    var stationData = new PassStationData(station);
                    stationData.AddPlatforms(platformIds.Select(GetTrackById));
                    stationData.AddStorageTracks(StorageTrackNames[yardId].Select(GetTrackById));
                    _stations.Add(yardId, stationData);

                    _expressGraph.Add(new RouteNode(stationData, TrackType.Platform));
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

        public static RouteTrack[]? GetExpressRoute(PassStationData startStation, double minLength = 0)
        {
            const int NUM_DESTINATIONS = 2;

            if (!Initialized) Initialize();

            RecalculateGraph(_expressGraph, minLength);

            var result = new RouteTrack[NUM_DESTINATIONS];
            int sourceIdx = 0;

            for (int targetIdx = 0; targetIdx < NUM_DESTINATIONS; targetIdx++)
            {
                if (_expressGraph[sourceIdx].Weight < 0.5) return null;
                if (_expressGraph[sourceIdx].Station.YardID == startStation.YardID)
                {
                    sourceIdx++;
                }

                result[targetIdx] = _expressGraph[sourceIdx].PickTrack();

                sourceIdx++;
            }

            SortRouteByDistance(startStation, result);
            return result;
        }

        private static void SortRouteByDistance(PassStationData startStation, RouteTrack[] tracks)
        {
            float distanceCurrent = 
                JobPaymentCalculator.GetDistanceBetweenStations(startStation.Controller, tracks[0].Station.Controller) +
                JobPaymentCalculator.GetDistanceBetweenStations(tracks[0].Station.Controller, tracks[1].Station.Controller);

            float distanceSwapped = 
                JobPaymentCalculator.GetDistanceBetweenStations(startStation.Controller, tracks[1].Station.Controller) +
                JobPaymentCalculator.GetDistanceBetweenStations(tracks[1].Station.Controller, tracks[0].Station.Controller);

            if (distanceCurrent > distanceSwapped)
            {
                (tracks[1], tracks[0]) = (tracks[0], tracks[1]);
            }
        }

        public static double GetMaxTrainLength(Track[] tracks)
        {
            return tracks.Min(t => YardTracksOrganizer.Instance.GetUnreservedSpace(t));
        }

        private static void RecalculateGraph(List<RouteNode> graph, double minLength = 0)
        {
            foreach (var node in graph)
            {
                node.RecalculateWeight(minLength);
            }

            graph.Sort();
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

    public class RouteNode : IComparable<RouteNode>
    {
        public readonly PassStationData Station;
        public readonly TrackType TargetTrackType;

        public float Weight { get; private set; }

        private readonly List<Track> Tracks;

        public RouteNode(PassStationData station, TrackType targetType)
        {
            Station = station;
            TargetTrackType = targetType;
            Tracks = Station.TracksOfType(TargetTrackType);
            Weight = 0;
        }

        public void RecalculateWeight(double minLength = 0)
        {
            var unused = Tracks.GetUnusedTracks()
                .Where(t => t.length >= (minLength + YardTracksOrganizer.END_OF_TRACK_OFFSET_RESERVATION));

            Weight = ((float)unused.Count() / Tracks.Count) + ((UnityEngine.Random.value + 1) / 4);
        }

        public RouteTrack PickTrack()
        {
            return new RouteTrack(Station, Tracks.GetUnusedTracks().PickOne()!);
        }

        public int CompareTo(RouteNode other)
        {
            return other.Weight.CompareTo(Weight);
        }
    }
}
