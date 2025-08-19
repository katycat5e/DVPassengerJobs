#nullable disable

namespace PassengerJobs.Config
{
    public class RouteConfig
    {
        public int minLocalLength = 1;
        public int maxLocalLength = 5;
        public Node[] localNodes;

        public class Node
        {
            public string id;
            public string[] linkA;
            public string[] linkB;
        }

        public RouteSet[] expressRoutes;
        public RouteSet[] localRoutes;

        public class RouteSet
        {
            public string start;
            public string[][] routes;
        }
    }
}
