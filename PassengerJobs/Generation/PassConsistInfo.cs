using DV.Logic.Job;
using System.Collections.Generic;

namespace PassengerJobs.Generation
{
    public class PassConsistInfo
    {
        public readonly RouteTrack track;
        public readonly List<Car> cars;

        public PassConsistInfo(RouteTrack track, List<Car> cars)
        {
            this.track = track;
            this.cars = cars;
        }
    }
}
