using DV.Booklets;
using DV.Logic.Job;
using DV.ThingTypes;
using System.Collections.Generic;

namespace PassengerJobs.Platforms
{
    public class RuralLoadingTask : WarehouseTask
    {
        public const TaskType TaskType = (TaskType)42;
        public override TaskType InstanceTaskType => TaskType;
        public List<Car> Cars => cars;
        public bool IsLoading => warehouseTaskType == WarehouseTaskType.Loading;

        public RuralLoadingTask(List<Car> cars, WarehouseTaskType warehouseTaskType, RuralLoadingMachine warehouseMachine, CargoType cargoType, float cargoAmount, long timeLimit = 0L, bool isLastTask = false)
            : base(cars, warehouseTaskType, warehouseMachine, cargoType, cargoAmount, timeLimit, isLastTask)
        {
        }

        public override float GetTaskPrice() => 0;


        public override TaskData GetTaskData()
        {
            return new RuralTaskData(this);
        }


    }
    public class RuralTaskData : TaskData
    {
        public RuralTaskData(RuralLoadingTask task) : base(
            RuralLoadingTask.TaskType,
            task.state,
            task.taskStartTime,
            task.taskFinishTime,
            task.Cars,
            null,
            task.warehouseMachine.WarehouseTrack,
            WarehouseTaskType.None,
            null,
            task.cargoAmount,
            null,
            false,
            false)
        {
        }
    }


    public class RuralTask_data : Task_data
    {
        public string stationId;
        public bool isLoading;

        public RuralTask_data(RuralLoadingTask task) : base(task)
        {
            stationId = task.warehouseMachine.ID;
            isLoading = task.IsLoading;
        }
    }

}