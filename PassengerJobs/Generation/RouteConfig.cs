#nullable disable

using UnityEngine;

namespace PassengerJobs.Generation
{
    public class StationConfig
    {
        public TrackSet[] platforms;
        public TrackSet[] storage;

        public RuralStation[] ruralStations;

        public class TrackSet
        {
            public string yardId;
            public string[] tracks;
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
