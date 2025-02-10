using DV.Teleporters;
using UnityEngine;

namespace PassengerJobs.Platforms
{
    public class RuralStationFastTravelDestination : StationFastTravelDestination
    {
        public string StationName { get; private set; } = string.Empty;
        public string ID { get; private set; } = string.Empty;

        public override string MarkerName => StationName;

        public void Init(Transform anchor, string localisedStationName, string id)
        {
            playerTeleportAnchor = anchor;
            mapMarkerAnchor = anchor;
            StationName = localisedStationName;
            ID = id;
        }
    }
}
