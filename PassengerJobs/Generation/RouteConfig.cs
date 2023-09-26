#nullable disable

namespace PassengerJobs.Generation
{
    public class RouteConfig
    {
        public TrackSet[] platforms;
        public TrackSet[] storage;
        public ExpressRoute[] expressRoutes;

        public class TrackSet
        {
            public string yardId;
            public string[] tracks;
        }

        public class ExpressRoute
        {
            public string start;
            public string[][] routes;
        }
    }
}
