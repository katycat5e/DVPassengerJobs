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
using DV.ThingTypes.TransitionHelpers;

namespace PassengerJobs.MP.Multiplayer.Serializers;

public class RuralLoadingTaskData : TaskNetworkData<RuralLoadingTaskData>
{
    public ushort[]? CarNetIDs { get; set; }
    public WarehouseTaskType WarehouseTaskType { get; set; }
    public RuralLoadingMachine? WarehouseMachine { get; set; }
    public CargoType_v2? CargoTypeV2 { get; set; }
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

        // CargoType serialisation using V2 to maintain compatibility with Custom Cargo Mod
        if (CargoTypeV2 == null)
            CargoTypeV2 = CargoType.None.ToV2();

        MultiplayerAPI.Instance.TryGetNetId<CargoType_v2>(CargoTypeV2, out uint cargoTypeNetId);
        writer.Write(cargoTypeNetId);

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

        uint cargoTypeNetId = reader.ReadUInt32();
        if (!MultiplayerAPI.Instance.TryGetObjectFromNetId<CargoType_v2>(cargoTypeNetId, out CargoType_v2 cargoTypeV2))
            cargoTypeV2 = CargoType.None.ToV2();

        CargoTypeV2 = cargoTypeV2;

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
                if (car == null || !MultiplayerAPI.Instance.TryGetNetId(car, out ushort netId))
                    return (ushort)0;

                return netId;
            }
        ).ToArray();

        WarehouseTaskType = ruralLoadingTask.warehouseTaskType;
        WarehouseMachine = (RuralLoadingMachine)ruralLoadingTask.warehouseMachine;
        CargoTypeV2 = ruralLoadingTask.cargoType.ToV2();
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
            CargoTypeV2?.v1 ?? CargoType.None,
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