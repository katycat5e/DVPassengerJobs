using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace PassengerJobs.Platforms
{
    public static class RuralStationBuilder
    {
        public static readonly List<MapMarker> MapMarkers = new();
        private static readonly Color MarkerColor = new Color32(220, 204, 255, 255);

        private static readonly float LABEL_RADIUS = 0.012f;

        public static void GenerateDecorations(RuralLoadingMachine platform)
        {
            var signPrefab = Resources.Load<GameObject>("TrackSignSide");
            var railTrack = platform.Track.GetRailTrack();

            DV.PointSet.EquiPointSet.Point[] pointSet;
            DV.PointSet.EquiPointSet.Point lowPoint, highPoint;

            try
            {
                pointSet = railTrack.GetPointSet(0).points;
                lowPoint = pointSet[platform.LowerBound];
                highPoint = pointSet[platform.UpperBound];
            }
            catch (System.IndexOutOfRangeException)
            {
                PJMain.Error($"Rural station {platform.Id} track points don't exist!");
                return;
            }

            // Setup track signs
            var lowSignPosition = (Vector3)lowPoint.position + WorldMover.currentMove;
            var lowSignRotation = Quaternion.LookRotation(-lowPoint.forward, Vector3.up);
            var lowSign = UnityEngine.Object.Instantiate(signPrefab, lowSignPosition, lowSignRotation, railTrack.transform);
            lowSign.name = $"[track id] {platform.Id} 0";
            SetTrackSignText(lowSign, platform.Id);

            var highSignPosition = (Vector3)highPoint.position + WorldMover.currentMove;
            var highSignRotation = Quaternion.LookRotation(highPoint.forward, Vector3.up);
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
            platformObj.AddComponent<PlatformLightController>();

            string localName = LocalizationKeyExtensions.StationName(platform.Id);
            SetPlatformSignText(platformObj, localName);

            CoroutineManager.Instance.StartCoroutine(InitStationLabelCoro(platformHolder.transform, platform.Id, localName, platform.MarkerAngle ?? 0));
        }

        public static void DestroyDecorations(RuralLoadingMachine platform)
        {
            var trackTransform = platform.Track.GetRailTrack().transform;

            var sign0 = trackTransform.Find($"[track id] {platform.Id} 0");
            if (sign0) Object.Destroy(sign0.gameObject);

            var sign1 = trackTransform.Find($"[track id] {platform.Id} 1");
            if (sign1) Object.Destroy(sign1.gameObject);

            var concrete = trackTransform.Find($"[platform] {platform.Id}");
            if (concrete)
            {
                var labelContainer = MarkerController.transform.Find("MapPaper/Names");
                RectTransform label = labelContainer.transform.Find(platform.Id).GetComponent<RectTransform>();

                if (label)
                    Object.Destroy(concrete.gameObject);

                Object.Destroy(concrete.gameObject);
            }
        }

        private static IEnumerator InitStationLabelCoro(Transform anchor, string id, string localName, float labelAngle)
        {
            yield return null;
            RuralStationFastTravelDestination travelDestination;
            MapMarker? marker = null;

            //Add our custom travel destination to our station
            travelDestination = anchor.gameObject.AddComponent<RuralStationFastTravelDestination>();
            travelDestination.Init(anchor, anchor, localName, id);

            //wait for marker generation
            yield return new WaitUntil(()=> MarkerController.markers.TryGetValue(travelDestination, out marker));

            var renderer = marker!.GetComponentsInChildren<MeshRenderer>(true);
            foreach (var mesh in renderer)
            {
                if (mesh.name == "Visuals")
                {
                    mesh.material = MarkerMaterial;
                }
            }

            //Marker will automaticall be added to the map, but text is not
            var namePrefab = GetNamePrefab(MarkerController);
            var nameObj = Object.Instantiate(namePrefab, namePrefab.transform.parent);
            nameObj.name = id;

            var nameRect = nameObj.GetComponent<RectTransform>();

            var nameText = nameObj.GetComponent<TextMeshPro>();
            nameText.text = id;
            nameText.horizontalAlignment = HorizontalAlignmentOptions.Center;

            PositionLabel(marker.transform, nameRect, labelAngle, LABEL_RADIUS);

            yield return null;
        }

        static void PositionLabel(Transform marker, RectTransform labelRect, float angleInDegrees, float radius)
        {
            var id = labelRect.name;
            //PJMain.Log($"[{id}] Input angle: {angleInDegrees:F6}, radius: {radius:F6}");

            // Set up UI anchoring
            var anchors = new Vector2(0.5f, 0.5f);
            labelRect.anchorMax = anchors;
            labelRect.anchorMin = anchors;
            labelRect.pivot = anchors;

            // Convert angle to radians for trig functions
            float angleRad = angleInDegrees * Mathf.Deg2Rad;
            //PJMain.Log($"[{id}] Angle in radians: {angleRad:F6}");

            // Calculate x and z offsets using trigonometry
            float offsetX = radius * Mathf.Cos(angleRad);
            float offsetZ = radius * Mathf.Sin(angleRad);
            //PJMain.Log($"[{id}] Calculated offset: ({offsetX:F6}, {offsetZ:F6})");

            // Apply offset to marker position
            var finalPosition = new Vector2(
                marker.localPosition.x + offsetX,
                marker.localPosition.z + offsetZ
            );
            //PJMain.Log($"[{id}] Marker local: {marker.localPosition:F6}");
            //PJMain.Log($"[{id}] Final position: {finalPosition:F6}");

            labelRect.anchoredPosition = finalPosition;
        }

        private static MapMarkersController? _markerController;
        private static MapMarkersController MarkerController
        {
            get
            {
                if (!_markerController)
                {
                    _markerController = Object.FindObjectOfType<MapMarkersController>();
                }
                return _markerController!;
            }
        }

        private static GameObject? _namePrefab;
        private static GameObject GetNamePrefab(MapMarkersController markers)
        {
            if (!_namePrefab)
            {
                var names = markers.transform.Find("MapPaper/Names");
                _namePrefab = names.GetChild(0).gameObject;
            }
            return _namePrefab!;
        }

        private static Material? _ruralMaterial;
        private static Material MarkerMaterial
        {
            get
            {
                if (!_ruralMaterial)
                {
                    _ruralMaterial = new Material(Shader.Find("Standard"))
                    {
                        color = MarkerColor,
                    };
                }
                return _ruralMaterial!;
            }
        }

        private static void SetTrackSignText(GameObject signObject, string stationId)
        {
            foreach (var textArea in signObject.GetComponentsInChildren<TextMeshPro>())
            {
                textArea.text = ((textArea.name == "[SubYardID]") ? "L" : stationId);
            }
        }

        private static void SetPlatformSignText(GameObject root, string stationName)
        {
            foreach (var textArea in root.GetComponentsInChildren<TextMeshPro>())
            {
                textArea.text = stationName;
            }
        }
    }
}
