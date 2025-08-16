using CommsRadioAPI;
using DV;
using PassengerJobs.Generation;
using System;
using UnityEngine;

namespace PassengerJobs.DebugTools.StationEditor
{
    public class EditStationState : BaseTrackLocationState
    {
        [Flags]
        public enum PlatformType
        {
            Standard = 0,
            NoLamps = 1,
            Low = 2,
            LowNoLamps = Low | NoLamps,
        }

        private static readonly string[] TYPE_NAMES =
        {
            "Standard",
            "No Lamps",
            "Low",
            "Low No Lamps",
        };

        private readonly string _stationId;
        private readonly PlatformType _platformType;

        public EditStationState(string stationId) : this(stationId, RaycastResult.NONE)
        { }

        protected EditStationState(string stationId, RaycastResult raycast) :
            this(stationId, raycast, GetTypeIndex(stationId))
        { }

        protected EditStationState(string stationId, RaycastResult raycast, PlatformType type) :
            base(GetState(stationId, type), raycast)
        {
            _stationId = stationId;
            _platformType = type;
        }

        private static CommsRadioState GetState(string stationId, PlatformType type)
        {
            string prompt = $"Edit {stationId}:\n{TYPE_NAMES[(int)type]}";

            return new CommsRadioState(RadioSetup.STATION_PLACER_TITLE, prompt, buttonBehaviour: ButtonBehaviourType.Override);
        }

        public override AStateBehaviour OnUpdate(CommsRadioUtility utility)
        {
            base.OnUpdate(utility);

            var result = GetRaycastResult(utility);

            return new EditStationState(_stationId, result, _platformType);
        }

        public override AStateBehaviour OnAction(CommsRadioUtility utility, InputAction action)
        {
            int newIdx;
            switch (action)
            {
                case InputAction.Up:
                    newIdx = (int)_platformType + 1;
                    if (newIdx >= TYPE_NAMES.Length) newIdx = 0;

                    return new EditStationState(_stationId, _raycastResult, (PlatformType)newIdx);

                case InputAction.Down:
                    newIdx = (int)_platformType - 1;
                    if (newIdx < 0) newIdx = TYPE_NAMES.Length - 1;

                    return new EditStationState(_stationId, _raycastResult, (PlatformType)newIdx);

                default:
                    if (_raycastResult.IsTrack)
                    {
                        PJMain.Log($"save {_stationId} center @ {_raycastResult.TrackLocation.Value.index}");
                        SaveChanges(_stationId, TrackPosition.Value, _platformType, _raycastResult.OppositeSide);
                    }
                    return new SelectStationState();
            }
        }

        public static PlatformType GetTypeIndex(string existingId)
        {
            PlatformType type = PlatformType.Standard;

            if (RouteManager.TryGetRuralStation(existingId, out var station))
            {
                if (station.hideConcrete)
                {
                    type |= PlatformType.Low;
                }
                if (station.hideLamps)
                {
                    type |= PlatformType.NoLamps;
                }
            }

            return type;
        }

        private static void SaveChanges(string stationId, Vector3 location, PlatformType type, bool swapSides)
        {
            bool low = type.HasFlag(PlatformType.Low);
            bool hideLamps = type.HasFlag(PlatformType.NoLamps);

            RouteManager.SaveRuralStation(stationId, location, low, hideLamps, swapSides);
        }
    }
}
