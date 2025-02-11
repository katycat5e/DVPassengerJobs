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
            
            public Vector3 location;
            public bool swapSides;
            public bool noDecoration;

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
