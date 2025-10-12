using DV.Logic.Job;
using MPAPI;
using MPAPI.Interfaces;
using PassengerJobs.Generation;
using PassengerJobs.MP.Multiplayer.Packets;
using PassengerJobs.Platforms;
using System;
using System.Collections.Generic;
using System.Linq;

namespace PassengerJobs.MP.Multiplayer;

internal class PJServer : IDisposable
{
    private IServer? _server;

    public PJServer(IServer server)
    {
        PJMain.Log("Server created");
        _server = server;
        _server.OnPlayerConnected += OnPlayerConnected;

        // Listen for settings changes
        PJMain.Settings.OnSettingsSaved += OnSettingsChanged;

        // Wait for stations to be created before subscribing to platform events
        RouteManager.RuralStationsCreated += Server_OnStationsCreated;
    }

    public void Dispose()
    {
        PJMain.Log("Server stopped");
        _server = null;

        PJMain.Settings.OnSettingsSaved -= OnSettingsChanged;
    }

    #region Events
    private void OnPlayerConnected(IPlayer player)
    {
        PJMain.Log($"Player {player.Username} connected");

        if (player.IsHost)
            return;

        PJMain.Log($"Sending {player.Username} Settings");
        SendPJSettings(player);

        PJMain.Log($"Sending {player.Username} Stations");
        SendPJStationData(player);
    }

    public void OnSettingsChanged(PJModSettings _)
    {
        if (_server == null)
            return;

        SendPJSettings();
    }

    private void Server_OnStationsCreated()
    {
        RouteManager.RuralStationsCreated -= Server_OnStationsCreated;

        PJMain.LogDebug("Server_OnStationsCreated()");
        foreach ( var platformController in PlatformController.AllPlatformControllers)
        {
            if (platformController.Platform != null && platformController.Platform.Warehouse != null)
            {
                platformController.TaskComplete += OnTaskComplete;
                platformController.PlatformStateChange += OnPlatformStateChanged;
            }
            else
            {
                PJMain.LogDebug($"Server_OnStationsCreated() - Subscribing to platform {platformController?.Platform?.DisplayId} events, platform or warehouse null");
            }
        }
    }

    private void OnTaskComplete(object sender, TaskCompleteArgs args)
    {
        if (sender is PlatformController platformController && platformController.Platform != null && platformController.Platform.Warehouse != null)
        {
            if (!MultiplayerAPI.Instance.TryGetNetId<WarehouseMachine>(platformController.Platform.Warehouse, out var warehouseMachineNetId))
            {
                PJMain.Warning($"OnTaskComplete() Could not get netId for warehouse {platformController.Platform.Warehouse.ID}");
                return;
            }             

            if (!MultiplayerAPI.Instance.TryGetNetId<Task>(args.Task, out var taskNetId))
            {
                PJMain.Warning($"OnTaskComplete() Could not get netId for task belonging to {args?.Task?.Job?.ID}, Warehouse {platformController.Platform.Warehouse.ID}");
                return;
            }

            SendTransferCompletePacket(warehouseMachineNetId, taskNetId);
        }
    }

    private void OnPlatformStateChanged(object sender, PlatformStateChangeArgs args)
    {
        if (sender is PlatformController platformController && platformController.Platform != null && platformController.Platform.Warehouse != null)
        {
            PJMain.LogDebug($"Platform {platformController.Platform.Id} state changed. Job: {args.Job?.ID}, State: {args.NewDisplay}");

            if (!MultiplayerAPI.Instance.TryGetNetId<WarehouseMachine>(platformController.Platform.Warehouse, out var warehouseMachineNetId))
                return;
            
            ushort jobNetId = 0;
            if (args.Job != null)
            {
                if (!MultiplayerAPI.Instance.TryGetNetId<Job>(args.Job, out jobNetId))
                    return;
            }

            SendPlatformStateChangePacket(warehouseMachineNetId, jobNetId, args.NewDisplay);
        }
    }
    #endregion 

    #region Packet Senders
    public void SendPJSettings(IPlayer? player = null)
    {
        if (_server == null)
        {
            PJMain.Error("Tried to send settings to player but server is null");
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
            _server.SendPacketToAll(packet, excludeSelf: true);
        else
            _server.SendPacketToPlayer(packet, player);
    }

    private void SendPJStationData(IPlayer? player = null)
    {
        if (_server == null)
        {
            PJMain.Error("Tried to send station data but server is null");
            return;
        }

        PJMain.LogDebug($"Preparing station data packet. CityStations: {RouteManager.CityStations?.Length ?? 0}, RuralStations: {RouteManager.RuralStations?.Length ?? 0}, SignLocations: {SignManager.SignLocations?.Count ?? 0}");

        var packet = new ClientBoundPJStationDataPacket
        {
            CityStations = RouteManager.CityStations,
            RuralStations = RouteManager.RuralStations,
            RuralStationTranslations = GetStationTranslations(),
            SignLocations = SignManager.SignLocations,
        };

        if (player == null)
            _server.SendSerializablePacketToAll(packet, excludeSelf: true);
        else
            _server.SendSerializablePacketToPlayer(packet, player);
    }

    private void SendTransferCompletePacket(ushort warehouseMachineNetId, ushort taskNetId)
    {
        if (_server == null)
        {
            PJMain.Error("Tried to send transfer completion to player but server is null");
            return;
        }

        ClientBoundPJTransferCompletePacket packet = new()
        {
            WarehouseMachineNetId = warehouseMachineNetId,
            TaskNetId = taskNetId,
        };

        _server.SendPacketToAll(packet, excludeSelf: true);
    }

    private void SendPlatformStateChangePacket(ushort warehouseMachineNetId, ushort jobNetId, LocalizationKey state)
    {
        if (_server == null)
        {
            PJMain.Error("Tried to send platform state change but server is null");
            return;
        }

        ClientBoundPJPlatformStatePacket packet = new()
        {
            WarehouseMachineNetId = warehouseMachineNetId,
            JobNetId = jobNetId,
            State = state
        };

        _server.SendPacketToAll(packet, excludeSelf: true);
    }

    #endregion

    #region Packet Receivers
    #endregion

    #region Utilities
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
    #endregion
}
