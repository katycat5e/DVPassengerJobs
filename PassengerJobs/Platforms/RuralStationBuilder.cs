using TMPro;
using UnityEngine;

namespace PassengerJobs.Platforms
{
    public static class RuralStationBuilder
    {
        public static void GenerateDecorations(RuralLoadingMachine platform)
        {
            var signPrefab = Resources.Load<GameObject>("TrackSignSide");
            var railTrack = platform.Track.GetRailTrack();

            var pointSet = railTrack.GetPointSet(0).points;
            var lowPoint = pointSet[platform.LowerBound];
            var highPoint = pointSet[platform.UpperBound];

            // Setup track signs
            var lowSignPosition = (Vector3)lowPoint.position + WorldMover.currentMove;
            var lowSignRotation = Quaternion.LookRotation(lowPoint.forward, Vector3.up);
            var lowSign = UnityEngine.Object.Instantiate(signPrefab, lowSignPosition, lowSignRotation, railTrack.transform);
            lowSign.name = $"[track id] {platform.Id} 0";
            SetTrackSignText(lowSign, platform.Id);

            var highSignPosition = (Vector3)highPoint.position + WorldMover.currentMove;
            var highSignRotation = Quaternion.LookRotation(-highPoint.forward, Vector3.up);
            var highSign = UnityEngine.Object.Instantiate(signPrefab, highSignPosition, highSignRotation, railTrack.transform);
            highSign.name = $"[track id] {platform.Id} 1";
            SetTrackSignText(highSign, platform.Id);

            // Setup platform
            var chord = highPoint.position - lowPoint.position;
            var midPoint = (Vector3)(lowPoint.position + (chord / 2)) + WorldMover.currentMove;


            var platformHolder = new GameObject($"[platform] {platform.Id}");
            platformHolder.transform.SetParent(railTrack.transform);
            platformHolder.transform.position = midPoint;
            platformHolder.transform.rotation = Quaternion.LookRotation((Vector3)chord, Vector3.up);

            var platformObj = UnityEngine.Object.Instantiate(BundleLoader.RuralPlatform, platformHolder.transform);
            platformObj.transform.localPosition = platform.PlatformOffset ?? Vector3.zero;
            platformObj.transform.localRotation = platform.PlatformRotation.HasValue ? Quaternion.Euler(platform.PlatformRotation.Value) : Quaternion.identity;
        }

        private static void SetTrackSignText(GameObject signObject, string stationId)
        {
            foreach (var textArea in signObject.GetComponentsInChildren<TextMeshPro>())
            {
                textArea.text = ((textArea.name == "[SubYardID]") ? "L" : stationId);
            }
        }
    }
}
