using DV.Logic.Job;
using DV.ThingTypes;
using MPAPI.Types;
using MPAPI.Util;
using MPAPI;
using PassengerJobs.Platforms;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System;

namespace PassengerJobs.MP.Multiplayer.Serializers;

public class CityLoadingTaskData : TaskNetworkData<CityLoadingTaskData>
{
    public ushort[]? CarNetIDs { get; set; }
    public WarehouseTaskType WarehouseTaskType { get; set; }
    public WarehouseMachine? WarehouseMachine { get; set; }
    public CargoType CargoType { get; set; }
    public float CargoAmount { get; set; }
    public bool ReadyForMachine { get; set; }

    public override void Serialize(BinaryWriter writer)
    {
        SerializeCommon(writer);

        writer.WriteUShortArray(CarNetIDs);
        writer.Write((byte)WarehouseTaskType);

        ushort warehouseMachineNetId = 0;
        if (WarehouseMachine != null)
            MultiplayerAPI.Instance.TryGetNetId<WarehouseMachine>(WarehouseMachine, out warehouseMachineNetId);

        writer.Write(warehouseMachineNetId);
        writer.Write((int)CargoType);
        writer.Write(CargoAmount);
        writer.Write(ReadyForMachine);
    }

    public override void Deserialize(BinaryReader reader)
    {
        DeserializeCommon(reader);

        CarNetIDs = reader.ReadUShortArray();
        WarehouseTaskType = (WarehouseTaskType)reader.ReadByte();

        ushort warehouseMachineNetId = reader.ReadUInt16();

        if (!MultiplayerAPI.Instance.TryGetObjectFromNetId<WarehouseMachine>(warehouseMachineNetId, out var warehouseMachine) || warehouseMachine == null)
            throw new Exception($"Failed to deserialise CityLoadingTaskData for warehouseMachineNetId {warehouseMachineNetId}, WarehouseMachine was not found");

        WarehouseMachine = warehouseMachine;

        CargoType = (CargoType)reader.ReadInt32();
        CargoAmount = reader.ReadSingle();
        ReadyForMachine = reader.ReadBoolean();
    }

    public override CityLoadingTaskData FromTask(Task task)
    {
        if (task is not CityLoadingTask cityLoadingTask)
            throw new ArgumentException("Task is not a CityLoadingTask");

        FromTaskCommon(task);

        CarNetIDs = cityLoadingTask.Cars.Select
        (
            car =>
            {
                if (car == null || !MultiplayerAPI.Instance.TryGetNetId(car, out var netId))
                    return (ushort)0;

                return netId;
            }
        ).ToArray();

        WarehouseTaskType = cityLoadingTask.warehouseTaskType;
        WarehouseMachine = cityLoadingTask.warehouseMachine;
        CargoType = cityLoadingTask.cargoType;
        CargoAmount = cityLoadingTask.cargoAmount;
        ReadyForMachine = cityLoadingTask.readyForMachine;

        return this;
    }

    public override Task ToTask(ref Dictionary<ushort, Task> netIdToTask)
    {
        List<Car> cars = CarNetIDs
            .Select(netId => MultiplayerAPI.Instance.TryGetObjectFromNetId(netId, out Car car) ? car : null)
            .OfType<Car>()
            .ToList();

        if (WarehouseMachine == null)
            throw new Exception($"Failed to convert CityLoadingTaskData to Task, WarehouseMachine is null! taskNetId: {TaskNetId}");

        CityLoadingTask newCityLoadingTask = new
        (
           cars,
           WarehouseTaskType,
           WarehouseMachine,
           CargoType,
           CargoAmount
        );
        
        newCityLoadingTask.readyForMachine = ReadyForMachine;

        ToTaskCommon(newCityLoadingTask);

        netIdToTask.Add(TaskNetId, newCityLoadingTask);

        return newCityLoadingTask;
    }

    public override List<ushort> GetCars()
    {
        return CarNetIDs.ToList() ?? new List<ushort>();
    }
}