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
            _server.SendPacketToAll(packet);
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
            _server.SendSerializablePacketToAll(packet);
        else
            _server.SendSerializablePacketToPlayer(packet, player);
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
