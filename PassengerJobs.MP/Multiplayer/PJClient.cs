using DV.Logic.Job;
using DVLangHelper.Data;
using MPAPI;
using MPAPI.Interfaces;
using PassengerJobs.Generation;
using PassengerJobs.MP.Multiplayer.Packets;
using PassengerJobs.Platforms;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace PassengerJobs.MP.Multiplayer;

public class PJClient : IDisposable
{
    private static IClient? _client;
    private static readonly Dictionary<StationController, Action> _trackedStations = new();

    public PJClient(IClient client)
    {
        PJMain.Log("Client created");

        // Only register client if client is standalone (not host)
        if (MultiplayerAPI.Instance.IsHost)
            return;

        _client = client;

        // Prevent server from sending game state until we have received station data and created stations
        PJMain.Log("Setting Ready Block");
        _client.RegisterReadyBlock(PJMain.ModEntry.Info);

        // Register for incoming packets
        _client.RegisterPacket<ClientBoundPJSettingsPacket>(OnClientBoundPJSettingsPacket);
        _client.RegisterSerializablePacket<ClientBoundPJStationDataPacket>(OnClientBoundPJStationDataPacket);
        _client.RegisterPacket<ClientBoundPJTransferCompletePacket>(OnClientBoundPJTransferCompletePacket);
        _client.RegisterPacket<ClientBoundPJPlatformStatePacket>(OnClientBoundPJPlatformStatePacket);

        // Block local changes to settings
        PJMain.Settings.MPActive = true;
    }

    public void Dispose()
    {
        PJMain.Log("Client stopped");
        _client = null;

        if (UnloadWatcher.isQuitting)
            return;

        PJMain.Translations.Reload();

        SignManager.TryLoadSignLocations();

        // Unblock local changes to settings
        PJMain.Settings.MPActive = false;

        // Force restore of player's settings
        PJMain.ReloadSettings();
    }

    #region Events
    private static void OnStationsCreated()
    {
        PJMain.Log("Stations created, clearing Ready Block");
        RouteManager.RuralStationsCreated -= OnStationsCreated;
        _client!.CancelReadyBlock(PJMain.ModEntry.Info);
    }
    #endregion

    #region Packet Senders
    #endregion

    #region Packet Receivers
    private static void OnClientBoundPJSettingsPacket(ClientBoundPJSettingsPacket packet)
    {
        // Load settings from packet
        PJMain.Settings.UseCustomWages = packet.UseCustomWages;
        PJMain.Settings.CoachLights = packet.CoachLights;
        PJMain.Settings.UseCustomCoachLightColour = packet.UseCustomCoachLightColour;
        PJMain.Settings.CustomCoachLightColour = packet.CustomCoachLightColour;
        PJMain.Settings.CoachLightsRequirePower = packet.CoachLightsRequirePower;

        // Notify listeners that settings have changed
        PJMain.Settings.OnSettingsSaved?.Invoke(PJMain.Settings);
    }

    private static void OnClientBoundPJStationDataPacket(ClientBoundPJStationDataPacket packet)
    {
        SetStationTranslations(packet.RuralStationTranslations);

        SignManager.SetSigns(packet.SignLocations);

        RouteManager.SetStations(packet.CityStations, packet.RuralStations);
    }

    private static void OnClientBoundPJTransferCompletePacket(ClientBoundPJTransferCompletePacket packet)
    {
        if (!MultiplayerAPI.Instance.TryGetObjectFromNetId<Task>(packet.TaskNetId, out var task) || task == null || task is not WarehouseTask warehouseTask)
        {
            PJMain.Error($"OnClientBoundPJTransferCompletePacket() Failed to get task for TaskNetId: {packet.TaskNetId}, task null: {task == null}, task type: {task?.GetType()}");
            return;
        }

        if (!MultiplayerAPI.Instance.TryGetObjectFromNetId<WarehouseMachine>(packet.WarehouseMachineNetId, out var warehouseMachine) || warehouseMachine == null)
        {
            PJMain.Error($"OnClientBoundPJTransferCompletePacket() Failed to get WarehouseMachine for NetId: {packet.WarehouseMachineNetId}");
            return;
        }

        warehouseMachine.RemoveWarehouseTask(warehouseTask);
    }

    private void OnClientBoundPJPlatformStatePacket(ClientBoundPJPlatformStatePacket packet)
    {
        Job? job = null;

        PJMain.LogDebug($"OnClientBoundPJPlatformStatePacket() called for WarehouseMachine NetId: {packet.WarehouseMachineNetId}, JobNetId: {packet.JobNetId}, State: {packet.State}");

        if (!MultiplayerAPI.Instance.TryGetObjectFromNetId<WarehouseMachine>(packet.WarehouseMachineNetId, out var warehouseMachine) || warehouseMachine == null)
        {
            PJMain.Error($"OnClientBoundPJPlatformStatePacket() Failed to get WarehouseMachine for NetId: {packet.WarehouseMachineNetId}");
            return;
        }

        if (!PlatformController.TryGetControllerForTrack(warehouseMachine.ID, out var platformController) || platformController == null)
        {
            PJMain.Error($"OnClientBoundPJPlatformStatePacket() Failed to get PlatformController for WarehouseMachine {warehouseMachine.ID}");
            return;
        }

        string message;

        switch (packet.State)
        {
            case LocalizationKey.SIGN_EMPTY:
                message = LocalizationKey.SIGN_EMPTY.L();

                break;

            case LocalizationKey.SIGN_INCOMING_TRAIN:
            case LocalizationKey.SIGN_OUTGOING_TRAIN:
                if (!MultiplayerAPI.Instance.TryGetObjectFromNetId<Job>(packet.JobNetId, out job) || job == null)
                {
                    PJMain.Error($"OnClientBoundPJPlatformStatePacket() Failed to get job for NetId {packet.JobNetId}");
                    return;
                }

                message = LocalizationKey.SIGN_BOARDING.L() + '\n';
                message += packet.State == LocalizationKey.SIGN_INCOMING_TRAIN
                    ? LocalizationKey.SIGN_INCOMING_TRAIN.L(job.ID, job.chainData.chainOriginYardId)
                    : LocalizationKey.SIGN_OUTGOING_TRAIN.L(job.ID, job.chainData.chainDestinationYardId);
                break;

            case LocalizationKey.SIGN_DEPARTING:
                message = LocalizationKey.SIGN_DEPARTING.L();

                platformController.PlayBellSound();

                break;

            default:
                PJMain.Error($"OnClientBoundPJPlatformStatePacket() Unknown state {packet.State} for Job [{packet.JobNetId}, {job?.ID}] on WarehouseMachine {warehouseMachine.ID}");
                return;
        }
        
        platformController.OverrideText = message;
    }
    #endregion

    #region Utilities
    private static void SetStationTranslations(Dictionary<string, string[]>? translations)
    {
        var languages = PJMain.Translations.Languages.Select(l => l.Code).ToArray();

        if (translations == null)
        {
            PJMain.Warning("Tried to set station translations, but translations are null");
            return;
        }

        foreach (var kvp in translations)
        {
            var key = $"{LocalizationKeyExtensions.STATION_NAME_KEY}{kvp.Key.ToLower()}";

            for (int i = 0; i < kvp.Value.Length; i++)
            {
                var translation = kvp.Value[i];

                if (!Enum.IsDefined(typeof(DVLanguage), i))
                {
                    PJMain.Error($"Failed to add translation \"{translation}\" for station {kvp.Key}, {i} is not a valid DVLanguage enum value");
                    break;
                }

                var dvLang = (DVLanguage)i;

                PJMain.LogDebug($"SetStationTranslations() i: {i}, dvLang {dvLang}, translation: {translation}");

                PJMain.Translations.AddTranslation(key, dvLang, translation, true);

            }
        }
    }

    private static void UpdateRouteSigns(StationController controller)
    {
        if (controller == null || controller.logicStation == null)
        {
            PJMain.Error($"UpdateRouteSigns failed, {(controller == null ? "Station Controller is null!": "Logic Station is null!")}");
            return;
        }

        var job = controller.logicStation.availableJobs.Last();
        if (job == null || !PassJobType.IsPJType(job.jobType))
            return;

        var taskData = job.tasks.FirstOrDefault().GetTaskData();
        if (taskData == null)
        {
            PJMain.Error($"UpdateRouteSigns({controller.stationInfo.Name}) failed, first task data is null!");
            return;
        }

        var destinationPlatformIds = GetDestinationPlatformIds(job);
        PlatformController platformController;

        if (destinationPlatformIds.Length == 0)
        {
            PJMain.Error($"UpdateRouteSigns({controller.stationInfo.Name}) failed, couldn't determine start or destination platform IDs");
            return;
        }
        else if (destinationPlatformIds.Length > 1)
        {
            platformController = PlatformController.GetControllerForTrack(destinationPlatformIds.First());
            PJMain.LogDebug($"UpdateRouteSigns({controller.stationInfo.Name}) First: {destinationPlatformIds.First()} pcont:{platformController?.Platform?.DisplayId}, pcont:{platformController?.PlatformData?.Track?.ID}, pcont Null: {platformController == null}");

            platformController?.RegisterOutgoingJob(job);

            for (int i = 1; i < destinationPlatformIds.Length - 1; i++)
            {
                platformController = PlatformController.GetControllerForTrack(destinationPlatformIds[i]);
                PJMain.LogDebug($"UpdateRouteSigns({controller.stationInfo.Name}) Inner: {destinationPlatformIds[i]} pcont:{platformController?.Platform?.DisplayId}, pcont:{platformController?.PlatformData?.Track?.ID}, pcont Null: {platformController == null}");

                job.JobTaken += PlatformController.GetControllerForTrack(destinationPlatformIds[i]).RegisterOutgoingJob;
            }
        }

        platformController = PlatformController.GetControllerForTrack(destinationPlatformIds.Last());
        PJMain.LogDebug($"UpdateRouteSigns({controller.stationInfo.Name}) Last: {destinationPlatformIds.Last()} pcont:{platformController?.Platform?.DisplayId}, pcont:{platformController?.PlatformData?.Track?.ID}, pcont Null: {platformController == null}");
        job.JobTaken += PlatformController.GetControllerForTrack(destinationPlatformIds.Last()).RegisterIncomingJob;
    }

    public void RegisterForJobAddedEvents(StationController controller)
    {
        PJMain.LogDebug($"RegisterForJobAddedEvents({controller?.stationInfo.Name}) called");
        if (controller == null)
        {
            PJMain.Error($"RegisterForJobAddedEvents() Station Controller is null!");
            return;
        }

        CoroutineManager.Instance.StartCoroutine(RegisterForJobAddedEvents_internal(controller));
    }

    private IEnumerator RegisterForJobAddedEvents_internal(StationController controller)
    {
        yield return new WaitUntil(() => controller.logicStation != null);

        if (_trackedStations.ContainsKey(controller))
        {
            PJMain.Warning($"RegisterForJobAddedEvents({controller.stationInfo.Name}) Delegate event already registered!");
            yield break;
        }

        PJMain.LogDebug($"RegisterForJobAddedEvents({controller.stationInfo.Name}) Registering delegate event");

        void JobAddedHandler() => UpdateRouteSigns(controller);
        controller.logicStation.JobAddedToStation += JobAddedHandler;
        _trackedStations[controller] = JobAddedHandler;
    }

    public void UnregisterForJobAddedEvents(StationController controller)
    {
        if (controller == null || controller.logicStation == null)
        {
            PJMain.Error($"UpdateRouteSigns failed, {(controller == null ? "Station Controller is null!" : "Logic Station is null!")}");
            return;
        }

        if (!_trackedStations.TryGetValue(controller, out Action JobAddedHandler))
        {
            PJMain.Error($"UnregisterForJobAddedEvents({controller.stationInfo.Name}) Delegate event not found!");
            return;
        }

        controller.logicStation.JobAddedToStation -= JobAddedHandler;
        _trackedStations.Remove(controller);
    }

    private static string[] GetDestinationPlatformIds(Job job)
    {
        if (job == null || !job.tasks.Any())
            return Array.Empty<string>();

        List<string> destinationPlatformIds = new();

        foreach (var task in job.tasks)
        {
            if (task.state == TaskState.Done)
                continue;

            var result = GetDestinationPlatformIds(task);

            if (result != null && result.Length > 0)
                destinationPlatformIds.AddRange(result);
        }

        PJMain.LogDebug($"GetDestinationPlatformIds() Job {job.ID}, DestinationPlatformIDs: {string.Join(", ", destinationPlatformIds)}");

        return destinationPlatformIds.ToArray();
    }

    private static string[] GetDestinationPlatformIds(Task task)
    {
        if (task == null)
            return Array.Empty<string>();

        List<string> destinationPlatformIds = new();

        var result = task.GetType().Name switch
        {
            nameof(SequentialTasks) => GetDestinationPlatformIds(((SequentialTasks)task).currentTask),
            nameof(ParallelTasks) => GetDestinationPlatformIds(((ParallelTasks)task).tasks),
            _ => GetDestinationPlatformId(task),
        };

        if (result != null && result.Length > 0)
            destinationPlatformIds.AddRange(result);

        return result.ToArray();
    }

    private static string[] GetDestinationPlatformIds(LinkedListNode<Task> task)
    {
        if (task == null || task.Value == null)
            return Array.Empty<string>();

        List<string> destinationPlatformIds = new();

        string? previousDestination = null;

        while (task != null)
        {
            if (task.Value.state != TaskState.Done)
            {
                var result = GetDestinationPlatformIds(task.Value);

                if (result != null && result.Length > 0 && previousDestination != result.FirstOrDefault())
                {
                    destinationPlatformIds.AddRange(result);
                    previousDestination = result.FirstOrDefault();
                }
            }

            task = task.Next;
        }

        return destinationPlatformIds.ToArray();
    }

    private static string[] GetDestinationPlatformIds(List<Task> tasks)
    {
        if (tasks == null || tasks.Count == 0)
            return Array.Empty<string>();

        List<string> destinationPlatformIds = new();

        foreach (var task in tasks)
        {
            if (task == null)
                continue;

            var destination = GetDestinationPlatformId(task);

            if (destination != null && destination.Length > 0)
                destinationPlatformIds.AddRange(destination);
            else
                PJMain.Warning($"GetDestinationPlatformIds() List<Task> Job {task.Job.ID}, Task {task.GetType().Name}, State: {task.state} has no destination platforms");
        }

        return destinationPlatformIds.ToArray();
    }

    private static string[] GetDestinationPlatformId(Task task)
    {
        if (task == null)
            return Array.Empty<string>();

        var taskData = task.GetTaskData();
        if (taskData == null)
            return Array.Empty<string>();

        var track = taskData.destinationTrack?.ID?.FullDisplayID;
        if (track == null)
            return Array.Empty<string>();

        var destinationTrack = RouteManager.GetRouteTrackById(track);

        if (destinationTrack.HasValue)
        {
            var destination = destinationTrack.Value.PlatformID;
            if (!string.IsNullOrEmpty(destination))
                return new string[] { destination };
        }

        return Array.Empty<string>();
    }
    #endregion
}
