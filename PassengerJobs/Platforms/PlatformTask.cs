using DV.Logic.Job;
using System;
using System.Collections.Generic;
using System.Linq;

namespace PassengerJobs.Platforms
{
    public abstract class PlatformTask
    {
        public readonly Task Task;

        public PlatformTask(Task task)
        {
            Task = task;
        }

        public Job Job => Task.Job;
        public TaskState State
        {
            get => Task.state;
            set => Task.state = value;
        }

        public abstract List<Car> Cars { get; }
        public abstract bool IsLoadTask { get; }
    }

    public abstract class PlatformTask<T> : PlatformTask
        where T : Task
    {
        public readonly T TypedTask;

        public PlatformTask(T typedTask) : base(typedTask)
        {
            TypedTask = typedTask;
        }
    }

    public sealed class CityLoadTaskWrapper : PlatformTask<CityLoadingTask>
    {
        public CityLoadTaskWrapper(CityLoadingTask task) : base(task) { }

        public override List<Car> Cars => TypedTask.cars;
        public override bool IsLoadTask => TypedTask.warehouseTaskType == WarehouseTaskType.Loading;
    }

    public sealed class RuralLoadTaskWrapper : PlatformTask<RuralLoadingTask>
    {
        public RuralLoadTaskWrapper(RuralLoadingTask task) : base(task) { }

        public override List<Car> Cars => TypedTask.Cars;
        public override bool IsLoadTask => TypedTask.warehouseTaskType == WarehouseTaskType.Loading;
    }
}
