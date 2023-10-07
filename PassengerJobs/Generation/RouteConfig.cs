#nullable disable

using UnityEngine;

namespace PassengerJobs.Generation
{
    public class RouteConfig
    {
        public TrackSet[] platforms;
        public TrackSet[] storage;
        public RouteSet[] expressRoutes;

        public RuralStation[] ruralStations;
        public RouteSet[] localRoutes;

        public class TrackSet
        {
            public string yardId;
            public string[] tracks;
        }

        public class RouteSet
        {
            public string start;
            public string[][] routes;
        }

        public class RuralStation
        {
            public string id;
            public string trackId;
            public int lowIdx;
            public int highIdx;

            public Vector3? platformOffset;
            public Vector3? platformRotation;
        }
    }
}
