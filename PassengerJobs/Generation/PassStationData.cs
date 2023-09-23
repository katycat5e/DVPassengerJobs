using DV.Logic.Job;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PassengerJobs.Generation
{
    public class PassStationData
    {
        public readonly StationController Controller;
        public string YardID => Controller.stationInfo.YardID;
        public readonly List<Track> PlatformTracks = new();
        public readonly List<Track> StorageTracks = new();

        public PassStationData(StationController controller)
        {
            Controller = controller;
        }

        public void AddPlatforms(IEnumerable<Track> platforms) => PlatformTracks.AddRange(platforms);
        public void AddStorageTracks(IEnumerable<Track> storageTracks) => StorageTracks.AddRange(storageTracks);

        public List<Track> TracksOfType(TrackType type) => 
            (type == TrackType.Platform) ? PlatformTracks : StorageTracks;

        public IEnumerable<Track> AllTracks => PlatformTracks.Concat(StorageTracks);
    }

    public enum TrackType
    {
        Platform,
        Storage,
    }
}
