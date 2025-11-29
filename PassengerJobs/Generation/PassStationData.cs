using DV.Logic.Job;
using PassengerJobs.Config;
using PassengerJobs.Platforms;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace PassengerJobs.Generation
{
    public interface IPassDestination
    {
        string YardID { get; }
        IEnumerable<RouteTrack> GetPlatforms(bool onlyTerminusTracks = false);
        IEnumerable<Track> AllTracks { get; }

        Vector3 GetLocation();
    }

    public class RouteData
    {
        public readonly RouteType RouteType;
        public readonly IPassDestination[] Destinations;

        public RouteData(RouteType routeType, IPassDestination[] destinations)
        {
            RouteType = routeType;
            Destinations = destinations;
        }
    }

    public class PassStationData : IPassDestination
    {
        public readonly StationController Controller;
        public string YardID => Controller.stationInfo.YardID;
        public readonly List<PlatformData> Platforms = new();
        public readonly List<Track> StorageTracks = new();
        public readonly List<Track> TerminusTracks = new();

        public readonly List<RouteData> ExpressRoutes = new();
        public readonly List<RouteData> RegionalRoutes = new();

        public PassStationData(StationController controller)
        {
            Controller = controller;
        }

        public Vector3 GetLocation()
        {
            return Controller.transform.position;
        }

        public void AddPlatform(Track track, StationConfig.CityPlatform config) => Platforms.Add(new(track, config));
        public void AddTerminusTracks(IEnumerable<Track> terminusTracks) => TerminusTracks.AddRange(terminusTracks);
        public void AddStorageTracks(IEnumerable<Track> storageTracks) => StorageTracks.AddRange(storageTracks);

        public IEnumerable<RouteTrack> GetPlatforms(bool onlyTerminusTracks = false)
        {
            if (onlyTerminusTracks)
            {
                return TerminusTracks.Select(t => new RouteTrack(this, t));
            }
            else
            {
                return Platforms.Select(p => new RouteTrack(this, p.Track));
            }
        }

        public IEnumerable<Track> AllTracks => Platforms.Select(p => p.Track).Concat(StorageTracks);

        public class PlatformData
        {
            public readonly Track Track;

            public readonly bool HasSpawnZone;
            public readonly Vector3 CornerA;
            public readonly Vector3 CornerB;
            public readonly float Depth;
            public readonly float? PeepSpacing;

            public PlatformData(Track track, StationConfig.CityPlatform config)
            {
                Track = track;

                if (config.spawnZoneA.HasValue && config.spawnZoneB.HasValue && config.spawnZoneDepth.HasValue)
                {
                    HasSpawnZone = true;
                    CornerA = config.spawnZoneA.Value;
                    CornerB = config.spawnZoneB.Value;
                    Depth = config.spawnZoneDepth.Value;
                    PeepSpacing = config.spacing;
                }
            }
        }
    }

    public class RuralStationData : IPassDestination
    {
        public readonly RuralLoadingMachine Platform;

        public readonly PlatformController Controller;

        public string YardID => Platform.Id;

        public RuralStationData(RuralLoadingMachine platform, PlatformController controller)
        {
            Platform = platform;
            Controller = controller;
        }

        public Vector3 GetLocation()
        {
            return Controller.transform.position;
        }

        public IEnumerable<RouteTrack> GetPlatforms(bool onlyTerminusTracks)
        {
            if (onlyTerminusTracks)
            {
                return Enumerable.Empty<RouteTrack>();
            }
            
            if (Platform.IsYardTrack)
            {
                return new[] { new RouteTrack(this, Platform.WarehouseTrack) };
            }

            return new[] { new RouteTrack(this, Platform.WarehouseTrack, Platform.LowerBound, Platform.UpperBound) };
        }

        public IEnumerable<Track> AllTracks => new Track[] { Platform.WarehouseTrack };
    }

    public enum TrackType
    {
        Platform,
        Storage,
    }

    public enum RouteType
    {
        Express,
        Local,
    }
}
