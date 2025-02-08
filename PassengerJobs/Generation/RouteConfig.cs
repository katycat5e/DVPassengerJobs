#nullable disable

using UnityEngine;

namespace PassengerJobs.Generation
{
    public class StationConfig
    {
        public CityStation[] cityStations;
        public RuralStation[] ruralStations;

        public class CityStation
        {
            public string yardId;
            public string[] platforms;
            public string[] terminusTracks;
            public string[] storage;
        }

        public class RuralStation
        {
            public string id;
            public string name;
            public string trackId;
            public int lowIdx;
            public int highIdx;

            public Vector3? platformOffset;
            public Vector3? platformRotation;
            public float? markerAngle;
        }
    }

    public class RouteConfig
    {
        public RouteSet[] expressRoutes;
        public RouteSet[] localRoutes;

        public class RouteSet
        {
            public string start;
            public string[][] routes;
        }
    }
}
