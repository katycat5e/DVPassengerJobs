using DV.Logic.Job;
using System;
using System.Collections.Generic;
using System.Linq;

namespace PassengerJobs.Generation
{
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
                    var rail = Track.RailTrack().GetUnkinkedPointSet();
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
            TrackType = trackType;

            Nodes = new RouteNode[stations.Destinations.Length];
            for (int i = 0; i < Nodes.Length; i++)
            {
                bool isFinal = (i == Nodes.Length - 1);
                Nodes[i] = new RouteNode(stations.Destinations[i], minLength, isFinal);
            }

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
        public readonly double MinLength;
        public readonly float Weight;

        public RouteNode(IPassDestination station, double minLength, bool isFinal)
        {
            Station = station;
            MinLength = minLength;

            Tracks = station.GetPlatforms(isFinal)
                .GetUnusedTracks()
                .Where(t => t.Length >= (minLength + YardTracksOrganizer.END_OF_TRACK_OFFSET_RESERVATION));

            //return ((float)unused.Count() / tracks.Count) + ();
            Weight = Tracks.Any() ? 1 : 0;
        }

        public readonly RouteTrack PickTrack()
        {
            return Tracks.PickOneValue()!.Value;
        }
    }
}
