#nullable disable
using UnityEngine;

namespace PassengerJobs.Config
{
    public class StationConfig
    {
        public CityStation[] cityStations;
        public RuralStation[] ruralStations;

        public class CityStation
        {
            public string yardId;
            public CityPlatform[] platforms;
            public string[] terminusTracks;
            public string[] storage;
        }

        public class CityPlatform
        {
            public string id;
            public Vector3? spawnZoneA;
            public Vector3? spawnZoneB;
            public float? spawnZoneDepth;
            public float? spacing;
        }

        public class RuralStation
        {
            public string id;

            public Vector3 location;
            public bool swapSides;

            // Platform configuration
            public bool hideConcrete;
            public bool hideLamps;
            public float extraHeight;

            public float? markerAngle;
        }
    }
}
