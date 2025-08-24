using DV.Logic.Job;
using DV.ThingTypes;
using System.Collections.Generic;

namespace PassengerJobs.Platforms;

public class CityLoadingTask : WarehouseTask
{
    public const TaskType TaskType = (TaskType)43;
    public override TaskType InstanceTaskType => TaskType;
    public List<Car> Cars => cars;

    public CityLoadingTask(List<Car> cars, WarehouseTaskType warehouseTaskType, WarehouseMachine warehouseMachine, CargoType cargoType, float cargoAmount, long timeLimit = 0L, bool isLastTask = false)
        : base(cars, warehouseTaskType, warehouseMachine, cargoType, cargoAmount, timeLimit, isLastTask)
    {
    }
}
