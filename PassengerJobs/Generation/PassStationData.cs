using DV.Logic.Job;
using PassengerJobs.Platforms;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PassengerJobs.Generation
{
    public interface IPassDestination
    {
        string YardID { get; }
        IEnumerable<RouteTrack> GetPlatforms(bool onlyTerminusTracks = false);
        IEnumerable<Track> AllTracks { get; }
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
        public readonly List<Track> PlatformTracks = new();
        public readonly List<Track> StorageTracks = new();
        public readonly List<Track> TerminusTracks = new();

        public readonly List<RouteData> ExpressRoutes = new();
        public readonly List<RouteData> RegionalRoutes = new();

        public PassStationData(StationController controller)
        {
            Controller = controller;
        }

        public void AddPlatforms(IEnumerable<Track> platforms) => PlatformTracks.AddRange(platforms);
        public void AddTerminusTracks(IEnumerable<Track> terminusTracks) => TerminusTracks.AddRange(terminusTracks);
        public void AddStorageTracks(IEnumerable<Track> storageTracks) => StorageTracks.AddRange(storageTracks);

        public IEnumerable<RouteTrack> GetPlatforms(bool onlyTerminusTracks = false)
        {
            var options = onlyTerminusTracks ? TerminusTracks : PlatformTracks;
            return options.Select(t => new RouteTrack(this, t));
        }

        public IEnumerable<Track> AllTracks => PlatformTracks.Concat(StorageTracks);
    }

    public class RuralStationData : IPassDestination
    {
        public readonly RuralLoadingMachine Platform;

        public string YardID => Platform.Id;

        public RuralStationData(RuralLoadingMachine platform)
        {
            Platform = platform;
        }

        public IEnumerable<RouteTrack> GetPlatforms(bool onlyTerminusTracks)
        {
            if (onlyTerminusTracks)
            {
                return Enumerable.Empty<RouteTrack>();
            }
            return new[] { new RouteTrack(this, Platform.Track, Platform.LowerBound, Platform.UpperBound) };
        }

        public IEnumerable<Track> AllTracks => new Track[] { Platform.Track };
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
