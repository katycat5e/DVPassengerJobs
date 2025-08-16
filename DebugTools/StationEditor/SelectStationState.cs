using CommsRadioAPI;
using System.Linq;

namespace PassengerJobs.DebugTools.StationEditor
{
    public class SelectStationState : BaseTrackLocationState
    {
        public SelectStationState() : this(RaycastResult.NONE)
        {
        }

        protected SelectStationState(RaycastResult raycast) :
            base(GetState(raycast), raycast)
        {
        }

        private static CommsRadioState GetState(RaycastResult raycast)
        {
            string prompt = "Cancel";
            if (raycast.IsPlatform) prompt = $"Edit {raycast.PlatformId}";
            if (raycast.IsTrack) prompt = "New Station";

            return new CommsRadioState(RadioSetup.STATION_PLACER_TITLE, prompt);
        }

        public override AStateBehaviour OnUpdate(CommsRadioUtility utility)
        {
            base.OnUpdate(utility);

            // raycast a track point or existing platform
            var result = GetRaycastResult(utility);

            return new SelectStationState(result);
        }

        public override AStateBehaviour OnAction(CommsRadioUtility utility, InputAction action)
        {
            if (_raycastResult.IsPlatform)
            {
                return new EditStationState(_raycastResult.PlatformId);
            }
            if (_raycastResult.IsTrack)
            {
                var currentStation = StationController.allStations.FirstOrDefault(IsInStationRange);
                if (currentStation)
                {
                    return new EditStationState(currentStation.stationInfo.YardID);
                }
                return new NameStationState();
            }
            return new SelectStationState();
        }

        private static bool IsInStationRange(StationController station)
        {
            float dist = station.stationRange.PlayerSqrDistanceFromStationCenter;
            return station.stationRange.IsPlayerInJobGenerationZone(dist);
        }
    }
}
