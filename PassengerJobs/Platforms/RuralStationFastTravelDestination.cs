using DV.Teleporters;
using UnityEngine;

namespace PassengerJobs.Platforms;

public class RuralStationFastTravelDestination : StationFastTravelDestination
{
    public string StationName { get; private set; } = string.Empty;
    public string ID { get; private set; } = string.Empty;


    public override string MarkerName
    {
        get
        {
            return StationName;
        }
    }

    public void Init(Transform teleportAnchor, Transform markerAnchor, string localisedStationName, string id)
    {
        playerTeleportAnchor = teleportAnchor;
        mapMarkerAnchor = markerAnchor;
        StationName = localisedStationName;
        ID = id;
    }
}
