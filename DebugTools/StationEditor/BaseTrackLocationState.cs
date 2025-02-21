using CommsRadioAPI;
using PassengerJobs.Platforms;
using System.Collections.Generic;
using UnityEngine;

using EQPoint = DV.PointSet.EquiPointSet.Point;

namespace PassengerJobs.DebugTools.StationEditor
{
    public abstract class BaseTrackLocationState : AStateBehaviour
    {
        protected const string TITLE = "PJ Station";
        protected static LayerMask trackMask = LayerMask.GetMask("Default");

        protected readonly RaycastResult _raycastResult;
        protected Vector3? TrackPosition => (Vector3?)_raycastResult.TrackLocation?.position;
        protected Vector3? TrackForward => _raycastResult.TrackLocation?.forward;


        protected List<RailTrack> _nearbyTracks;
        protected const float MATCH_RADIUS = 1.5f;

        protected GameObject _trackMarker = null;

        protected BaseTrackLocationState(CommsRadioState state, RaycastResult raycast) :
            base(SetAction(state, raycast))
        {
            _raycastResult = raycast;
        }

        protected static CommsRadioState SetAction(CommsRadioState state, RaycastResult raycast)
        {
            string action = raycast.IsTrack ? "OK" : "CANCEL";
            return state.Fork(actionText: action);
        }

        public override void OnEnter(CommsRadioUtility utility, AStateBehaviour previous)
        {
            if (previous is BaseTrackLocationState prevSelector)
            {
                _nearbyTracks = SetupNearbyTracks(prevSelector._nearbyTracks);
                _trackMarker = prevSelector._trackMarker;
            }
            else
            {
                _nearbyTracks = SetupNearbyTracks(null);
            }
        }

        public override void OnLeave(CommsRadioUtility utility, AStateBehaviour next)
        {
            if (next is not BaseTrackLocationState)
            {
                Object.Destroy(_trackMarker);
            }
        }

        private static List<RailTrack> SetupNearbyTracks(List<RailTrack> cached)
        {
            if (cached is not null)
            {
                return cached;
            }

            cached = new();

            if (!PlayerManager.PlayerTransform) return cached;

            var playerPosition = PlayerManager.PlayerTransform.position;

            for (int radiusMultiplier = 1; radiusMultiplier < 10; radiusMultiplier++)
            {
                float searchRadius = 100f * radiusMultiplier;

                foreach (var candidate in RailTrackRegistry.Instance.AllTracks)
                {
                    if (RailTrack.GetPointWithinRangeWithYOffset(candidate, playerPosition, searchRadius).HasValue)
                    {
                        cached.Add(candidate);
                    }
                }

                if (cached.Count > 0) break;
            }

            int CompareRT(RailTrack track1, RailTrack track2)
            {
                return (playerPosition - track1.transform.position).sqrMagnitude.CompareTo(
                    (playerPosition - track2.transform.position).sqrMagnitude);
            }

            cached.Sort(CompareRT);
            return cached;
        }

        public override AStateBehaviour OnUpdate(CommsRadioUtility utility)
        {
            if (_raycastResult.IsTrack)
            {
                //var markerTrackPos = GetClosestTrackPoint(_trackLocation.Value - WorldMover.currentMove);
                //if (markerTrackPos.HasValue)
                //{
                //    ActivateTrackMarker();
                //    _trackMarker.transform.position = markerTrackPos.Value + WorldMover.currentMove + Vector3.up * 0.5f;
                //}

                ActivateTrackMarker();
                _trackMarker.transform.position = TrackPosition.Value + WorldMover.currentMove + Vector3.up * 0.5f;

                Quaternion extraRot = _raycastResult.OppositeSide ? Quaternion.AngleAxis(180, Vector3.up) : Quaternion.identity;
                _trackMarker.transform.rotation = Quaternion.LookRotation(TrackForward.Value, Vector3.up) * extraRot;
            }
            else
            {
                if (_trackMarker && _trackMarker.activeSelf)
                {
                    _trackMarker.SetActive(false);
                }
            }

            return this;
        }

        protected RaycastResult? GetClosestTrackPoint(Vector3 searchLocation)
        {
            foreach (var track in _nearbyTracks)
            {
                var match = RailTrack.GetPointWithinRangeWithYOffset(track, searchLocation, MATCH_RADIUS);
                if (match.HasValue)
                {
                    // if [(match -> searchLocation) x trackForward] points down, then searchLocation is to the right
                    Vector3 trackForward = match.Value.forward;
                    Vector3 relativeSearchPos = Vector3.ProjectOnPlane(searchLocation - WorldMover.currentMove - (Vector3)match.Value.position, Vector3.up);

                    Vector3 cross = Vector3.Cross(relativeSearchPos, trackForward);
                    bool opposite = (cross.y > 0);

                    return new RaycastResult(match.Value, opposite);
                }
            }

            return null;
        }

        protected RaycastResult GetRaycastResult(CommsRadioUtility utility)
        {
            if (!Physics.Raycast(utility.SignalOrigin.position, utility.SignalOrigin.forward, out var hit, 100f, trackMask))
            {
                return RaycastResult.NONE;
            }

            var platform = hit.collider.GetComponentInParent<PlatformController>();
            if (platform) return new RaycastResult(platform.Platform.Id);

            var trackHit = GetClosestTrackPoint(hit.point);
            if (trackHit.HasValue)
            {
                //var location = new Vector3(
                //        Mathf.RoundToInt(trackHit.Value.x),
                //        Mathf.RoundToInt(trackHit.Value.y),
                //        Mathf.RoundToInt(trackHit.Value.z));

                return trackHit.Value;
            }

            return RaycastResult.NONE;
        }

        protected static GameObject CreateMarkerPoint(float size = 0.3f)
        {
            var obj = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            obj.GetComponent<Renderer>().material.color = Color.magenta;
            Object.Destroy(obj.GetComponent<Collider>());
            obj.transform.localScale = Vector3.one * size;

            var dirObj = GameObject.CreatePrimitive(PrimitiveType.Cube);
            dirObj.GetComponent<Renderer>().material.color = Color.magenta;
            Object.Destroy(dirObj.GetComponent<Collider>());
            dirObj.transform.SetParent(obj.transform);
            dirObj.transform.localPosition = Vector3.right * (1 / size);
            dirObj.transform.localScale = Vector3.one * size;

            return obj;
        }

        protected void ActivateTrackMarker()
        {
            if (!_trackMarker)
            {
                _trackMarker = CreateMarkerPoint();
            }

            if (!_trackMarker.activeSelf) _trackMarker.SetActive(true);

            _trackMarker.transform.position = TrackPosition.Value + WorldMover.currentMove + Vector3.up * 0.5f;

            Quaternion extraRot = _raycastResult.OppositeSide ? Quaternion.AngleAxis(180, Vector3.up) : Quaternion.identity;
            _trackMarker.transform.rotation = Quaternion.LookRotation(TrackForward.Value, Vector3.up) * extraRot;
        }

        protected readonly struct RaycastResult
        {
            public readonly string PlatformId;
            public readonly EQPoint? TrackLocation;
            public readonly bool OppositeSide;

            public bool IsPlatform => PlatformId is not null;
            public bool IsTrack => TrackLocation.HasValue;

            public static RaycastResult NONE = new();

            public RaycastResult(string platform)
            {
                PlatformId = platform;
                TrackLocation = null;
                OppositeSide = false;
            }

            public RaycastResult(EQPoint trackLocation, bool oppositeSide)
            {
                PlatformId = null;
                TrackLocation = trackLocation;
                OppositeSide = oppositeSide;
            }
        }
    }
}
