using DV.Logic.Job;
using DVLangHelper.Data;
using MPAPI.Interfaces;
using MPAPI;
using PassengerJobs.Generation;
using PassengerJobs.MP.Multiplayer.Packets;
using PassengerJobs.MP.Multiplayer.Serializers;
using PassengerJobs.Platforms;
using System.Collections.Generic;
using System.Collections;
using System.Linq;
using System.Text;
using System;
using UnityEngine;

namespace PassengerJobs.MP.Multiplayer;

public static class MultiplayerManager
{
    private static IServer? _server;
    private static IClient? _client;

    private static readonly Dictionary<StationController, Action> _trackedStations = new();
    public static void Init()
    {
        // Register custom task types for Multiplayer serialisation and deserialisation.
        MultiplayerAPI.Instance.RegisterTaskType<RuralLoadingTask>
        (
            RuralLoadingTask.TaskType,
            task => new RuralLoadingTaskData { TaskType = RuralLoadingTask.TaskType }.FromTask(task),
            type => new RuralLoadingTaskData { TaskType = type }
        );

        MultiplayerAPI.Instance.RegisterTaskType<CityLoadingTask>
        (
            CityLoadingTask.TaskType,
            task => new CityLoadingTaskData { TaskType = CityLoadingTask.TaskType }.FromTask(task),
            type => new CityLoadingTaskData { TaskType = type }
        );

        // Listen for server creation
        MultiplayerAPI.ServerStarted += OnServerCreated;
        MultiplayerAPI.ServerStopped += OnServerStopped;

        // Listen for client creation
        MultiplayerAPI.ClientStarted += OnClientCreated;
        MultiplayerAPI.ClientStopped += OnClientStopped;
    }

    #region Server
    private static void OnServerCreated(IServer server)
    {
        PJMain.Log("Server created");
        _server = server;
        _server.OnPlayerConnected += OnPlayerConnected;

        // Listen for settings changes
        PJMain.Settings.OnSettingsSaved += OnSettingsChanged;
    }

    private static void OnServerStopped()
    {
        PJMain.Log("Server stopped");
        _server = null;

        PJMain.Settings.OnSettingsSaved -= OnSettingsChanged;
    }

    private static void OnPlayerConnected(IPlayer player)
    {
        PJMain.Log($"Player {player.Username} connected");

        if (player.IsHost)
            return;

        PJMain.Log($"Sending {player.Username} Settings");
        SendPJSettings(player);

        PJMain.Log($"Sending {player.Username} Stations");
        SendPJStationData(player);
    }

    public static void OnSettingsChanged(PJModSettings _)
    {
        if (_server == null)
            return;

        SendPJSettings();
    }

    public static void SendPJSettings(IPlayer? player = null)
    {
        if (_server == null)
        {
            PJMain.Warning("Tried to send PJ settings to player but server is null");
            return;
        }

        var packet = new ClientBoundPJSettingsPacket
        {
            UseCustomWages = PJMain.Settings.UseCustomWages,
            CoachLights = PJMain.Settings.CoachLights,
            UseCustomCoachLightColour = PJMain.Settings.UseCustomCoachLightColour,
            CustomCoachLightColour = PJMain.Settings.CustomCoachLightColour,
            CoachLightsRequirePower = PJMain.Settings.CoachLightsRequirePower
        };

        if (player == null)
            _server.SendPacketToAll(packet);
        else
            _server.SendPacketToPlayer(packet, player);
    }

    public static void SendPJStationData(IPlayer? player = null)
    {
        if (_server == null)
        {
            PJMain.Warning("Tried to send PJ station data but server is null");
            return;
        }

#if DEBUG
        PJMain.Log($"Preparing station data packet. CityStations: {RouteManager.CityStations?.Length ?? 0}, RuralStations: {RouteManager.RuralStations?.Length ?? 0}, SignLocations: {SignManager.SignLocations?.Count ?? 0}");
#endif

        var packet = new ClientBoundPJStationDataPacket
        {
            CityStations = RouteManager.CityStations,
            RuralStations = RouteManager.RuralStations,
            RuralStationTranslations = GetStationTranslations(),
            SignLocations = SignManager.SignLocations,
        };

        if (player == null)
            _server.SendSerializablePacketToAll(packet);
        else
            _server.SendSerializablePacketToPlayer(packet, player);
    }
    #endregion

    #region Client
    private static void OnClientCreated(IClient client)
    {
        PJMain.Log("Client created");

        // Only register client if client is standalone (not host)
        if (MultiplayerAPI.Instance.IsHost)
            return;

        _client = client;

        // Register for incoming packets
        _client.RegisterPacket<ClientBoundPJSettingsPacket>(OnClientBoundPJSettingsPacket);
        _client.RegisterSerializablePacket<ClientBoundPJStationDataPacket>(OnClientBoundPJStationDataPacket);

        // Block local changes to settings
        PJMain.Settings.MPActive = true;
    }

    private static void OnClientStopped()
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

    public static void RegisterForJobAddedEvents(StationController controller)
    {
        if (controller == null)
        {
            PJMain.Error($"RegisterForJobAddedEvents() Station Controller is null!");
            return;
        }

        CoroutineManager.Instance.StartCoroutine(RegisterForJobAddedEvents_internal(controller));
    }

    private static IEnumerator RegisterForJobAddedEvents_internal(StationController controller)
    {
        yield return new WaitUntil(() => controller.logicStation != null);

        if (_trackedStations.ContainsKey(controller))
        {
            PJMain.Warning($"RegisterForJobAddedEvents({controller.stationInfo.Name}) Delegate event already registered!");
            yield break;
        }

#if DEBUG
        PJMain.Log($"RegisterForJobAddedEvents({controller.stationInfo.Name}) Registering delegate event");
#endif

        void JobAddedHandler() => UpdateRouteSigns(controller);
        controller.logicStation.JobAddedToStation += JobAddedHandler;
        _trackedStations[controller] = JobAddedHandler;
    }

    public static void UnregisterForJobAddedEvents(StationController controller)
    {
        if (controller == null)
        {
            PJMain.Error($"UnregisterForJobAddedEvents() Station Controller is null!");
            return;
        }

        if (controller.logicStation == null)
        {
            PJMain.Error($"UnregisterForJobAddedEvents({controller.stationInfo.Name}) Logic Station is null!");
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

    private static void OnClientBoundPJSettingsPacket(ClientBoundPJSettingsPacket packet)
    {
        //PJMain.Log($"OnClientBoundPJSettingsPacket() UseCustomWages: {packet.UseCustomWages}, CoachLightMode: {packet.CoachLights}, UseCustomCoachLightColour: {packet.UseCustomCoachLightColour}, CustomCoachLightColour: {packet.CustomCoachLightColour}, CoachLightsRequirePower: {packet.CoachLightsRequirePower}");

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
#if DEBUG
        StringBuilder sb = new("OnClientBoundPJStationDataPacket()\r\n");

        if (packet.CityStations != null)
            foreach (var city in packet.CityStations)
                sb.AppendLine($"CityStation: {city.yardId}, Platforms: {city.platforms?.Length ?? 0}, TerminusTracks: {city.terminusTracks?.Length ?? 0}, Storage: {city.storage?.Length ?? 0}");

        if (packet.RuralStations != null)
            foreach (var city in packet.RuralStations)
                sb.AppendLine($"RuralStation: {city.id}, Location: {city.location}, SwapSides: {city.swapSides}, HideConcrete: {city.hideConcrete}, HideLamps: {city.hideLamps}, ExtraHeight: {city.extraHeight}, MarkerAngle: {city.markerAngle}");

        if (packet.RuralStationTranslations != null)
            foreach (var kvp in packet.RuralStationTranslations)
                sb.AppendLine($"RuralStationTranslation: {kvp.Key}, Translations: {string.Join("\r\n\t", kvp.Value)}");

        if (packet.SignLocations != null)
            foreach (var kvp in packet.SignLocations)
                sb.AppendLine($"SignLocation: {kvp.Key}, Signs: {kvp.Value.Count}");

        PJMain.Log(sb.ToString());
#endif
        SetStationTranslations(packet.RuralStationTranslations);

        SignManager.SetSigns(packet.SignLocations);

        RouteManager.SetStations(packet.CityStations, packet.RuralStations);
    }

    private static void UpdateRouteSigns(StationController controller)
    {
        if (controller == null)
        {
            PJMain.Error($"UpdateRouteSigns failed, Station Controller is null!");
            return;
        }

        if (controller.logicStation == null)
        {
            PJMain.Error($"UpdateRouteSigns({controller.stationInfo.Name}) failed, Logic Station is null!");
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

        if (destinationPlatformIds.Length == 0)
        {
            PJMain.Error($"UpdateRouteSigns({controller.stationInfo.Name}) failed, couldn't determine start or destination platform IDs");
            return;
        }
        else if (destinationPlatformIds.Length > 1)
        {
            PlatformController.GetControllerForTrack(destinationPlatformIds.First()).RegisterOutgoingJob(job);

            for (int i = 1; i < destinationPlatformIds.Length - 1; i++)
            {
                job.JobTaken += PlatformController.GetControllerForTrack(destinationPlatformIds[i]).RegisterOutgoingJob;
            }
        }

        job.JobTaken += PlatformController.GetControllerForTrack(destinationPlatformIds.Last()).RegisterIncomingJob;
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

#if DEBUG
        PJMain.Log($"GetDestinationPlatformIds() Job {job.ID}, DestinationPlatformIDs: {string.Join(", ", destinationPlatformIds)}");
#endif

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


    #region helpers
    public static TrainCar GetTrainCarFromID(string carId)
    {
        return TrainCarRegistry.Instance.logicCarToTrainCar.FirstOrDefault(kvp => kvp.Value.ID == carId).Value;
    }

    private static Dictionary<string, string[]>? GetStationTranslations()
    {
        if (RouteManager.RuralStations == null || RouteManager.RuralStations.Length == 0)
            return null;

        var ret = new Dictionary<string, string[]>(RouteManager.RuralStations.Length);

        foreach (var station in RouteManager.RuralStations)
        {
            var key = $"{LocalizationKeyExtensions.STATION_NAME_KEY}{station.id.ToLower()}";
            var translations = PJMain.Translations.Terms.Where(t => t.Term.Equals(key.ToLower())).FirstOrDefault();

            if (translations == null || translations.Languages == null || translations.Languages.Length == 0)
                continue;

            ret[station.id] = translations.Languages;
        }

        return ret;
    }

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

#if DEBUG
                PJMain.Log($"SetStationTranslations() i: {i}, dvLang {dvLang}, translation: {translation}");
#endif
                PJMain.Translations.AddTranslation(key, dvLang, translation, true);

            }
        }
    }
    #endregion
}
