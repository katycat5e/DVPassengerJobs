using DV.Logic.Job;
using System.Collections.Generic;
using System.Linq;

namespace PassengerJobs
{
    public class TrainCarsPerLogicTrack
    {
        public readonly List<TrainCar> cars;
        public readonly Track track;

        public CarsPerTrack CarsPerTrack => new(track, cars.Select(tc => tc.logicCar).ToList());
        public List<Car> LogicCars => cars.Select(tc => tc.logicCar).ToList();

        public TrainCarsPerLogicTrack(Track track, IEnumerable<TrainCar> cars)
        {
            this.cars = cars.ToList();
            this.track = track;
        }
    }
}
