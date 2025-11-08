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

public class RuralLoadingTaskData : TaskNetworkData<RuralLoadingTaskData>
{
    public ushort[]? CarNetIDs { get; set; }
    public WarehouseTaskType WarehouseTaskType { get; set; }
    public RuralLoadingMachine? WarehouseMachine { get; set; }
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
            throw new Exception($"Failed to deserialise RuralLoadingTaskData for warehouseMachineNetId {warehouseMachineNetId}, WarehouseMachine was not found");

        if (warehouseMachine is not RuralLoadingMachine ruralLoadingMachine)
            throw new Exception($"Failed to deserialise RuralLoadingTaskData for warehouseMachineNetId {warehouseMachineNetId}, WarehouseMachine is not a RuralLoadingMachine");
        
        WarehouseMachine = ruralLoadingMachine;

        CargoType = (CargoType)reader.ReadInt32();
        CargoAmount = reader.ReadSingle();
        ReadyForMachine = reader.ReadBoolean();
    } 

    public override RuralLoadingTaskData FromTask(Task task)
    {
        if (task is not RuralLoadingTask ruralLoadingTask)
            throw new ArgumentException("Task is not a RuralLoadingTask");

        FromTaskCommon(task);

        CarNetIDs = ruralLoadingTask.Cars.Select
        (
            car =>
            {
                if (car == null || !MultiplayerAPI.Instance.TryGetNetId(car, out var netId))
                    return (ushort)0;

                return netId;
            }
        ).ToArray();

        WarehouseTaskType = ruralLoadingTask.warehouseTaskType;
        WarehouseMachine = (RuralLoadingMachine)ruralLoadingTask.warehouseMachine;
        CargoType = ruralLoadingTask.cargoType;
        CargoAmount = ruralLoadingTask.cargoAmount;
        ReadyForMachine = ruralLoadingTask.readyForMachine;

        return this;
    }

    public override Task ToTask(ref Dictionary<ushort, Task> netIdToTask)
    {
        List<Car> cars = CarNetIDs
            .Select(netId => MultiplayerAPI.Instance.TryGetObjectFromNetId(netId, out Car car) ? car : null)
            .OfType<Car>()
            .ToList();


        if (WarehouseMachine == null)
            throw new Exception($"Failed to convert RuralLoadingTaskData to Task, WarehouseMachine is null! taskNetId: {TaskNetId}");

        RuralLoadingTask newRuralLoadingTask = new
        (
            cars,
            WarehouseTaskType,
            WarehouseMachine!,
            CargoType,
            CargoAmount
        );

        ToTaskCommon(newRuralLoadingTask);

        newRuralLoadingTask.readyForMachine = ReadyForMachine;

        netIdToTask.Add(TaskNetId, newRuralLoadingTask);

        return newRuralLoadingTask;
    }

    public override List<ushort> GetCars()
    {
        return CarNetIDs.ToList() ?? new List<ushort>();
    }
}