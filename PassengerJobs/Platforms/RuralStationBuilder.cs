using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

using EQPoint = DV.PointSet.EquiPointSet.Point;

namespace PassengerJobs.Platforms
{
    public static class RuralStationBuilder
    {
        public static readonly List<MapMarker> MapMarkers = new();
        private static readonly Color MarkerColor = new Color32(220, 204, 255, 255);

        private const float LABEL_RADIUS = 0.012f;
        private const float PLATFORM_LENGTH = 65;
        private const float PLATFORM_HALF_LENGTH = PLATFORM_LENGTH / 2;

        private const string TELEPORT_ANCHOR = "[teleport]";

        public static void GenerateDecorations(RuralLoadingMachine platform, bool swapPlatformSide)
        {
            var railTrack = platform.Track.GetRailTrack();

            EQPoint[] pointSet;
            EQPoint lowPoint, highPoint;

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

            GenerateTrackSigns(lowPoint, highPoint, platform.Id, railTrack);
            GeneratePlatformMeshes(lowPoint, highPoint, platform.Id, railTrack, platform.MarkerAngle ?? 0, swapPlatformSide);
        }

        private static void GenerateTrackSigns(EQPoint lowPoint, EQPoint highPoint, string id, RailTrack railTrack)
        {
            var signPrefab = Resources.Load<GameObject>("TrackSignSide");

            // Setup track signs
            var lowSignPosition = (Vector3)lowPoint.position + WorldMover.currentMove;
            var lowSignRotation = Quaternion.LookRotation(-lowPoint.forward, Vector3.up);
            var lowSign = Object.Instantiate(signPrefab, lowSignPosition, lowSignRotation, railTrack.transform);
            lowSign.name = $"[track id] {id} 0";
            SetTrackSignText(lowSign, id);

            var highSignPosition = (Vector3)highPoint.position + WorldMover.currentMove;
            var highSignRotation = Quaternion.LookRotation(highPoint.forward, Vector3.up);
            var highSign = Object.Instantiate(signPrefab, highSignPosition, highSignRotation, railTrack.transform);
            highSign.name = $"[track id] {id} 1";
            SetTrackSignText(highSign, id);
        }

        private static void GeneratePlatformMeshes(EQPoint lowPoint, EQPoint highPoint, string id, RailTrack railTrack, float markerAngle, bool swapPlatformSide)
        {
            // Setup platform
            var platformHolder = new GameObject($"[platform] {id}");
            platformHolder.transform.SetParent(railTrack.transform);

            // adjust offset to keep track clearance
            var pointSet = railTrack.pointSet;
            EQPoint centerPoint = pointSet.points[(lowPoint.index + highPoint.index) / 2];
            
            double platformLengthOffset = swapPlatformSide ? -PLATFORM_HALF_LENGTH : PLATFORM_HALF_LENGTH;

            int platformHighIdx = pointSet.GetPointIndexForSpan(centerPoint.span + platformLengthOffset);
            EQPoint platformHighPoint = pointSet.points[platformHighIdx];

            int platformLowIdx = pointSet.GetPointIndexForSpan(centerPoint.span - platformLengthOffset);
            EQPoint platformLowPoint = pointSet.points[platformLowIdx];

            var chord = (Vector3)(platformHighPoint.position - platformLowPoint.position);
            var midPoint = (Vector3)platformLowPoint.position + (chord / 2) + WorldMover.currentMove;

            int dIndex = highPoint.index - lowPoint.index;
            Vector3 maxOffsetPosition = (Vector3)platformLowPoint.position;
            Vector3 maxPositiveOffset = Vector3.zero;
            for (int i = 1; i < 4; i++)
            {
                int testIndex = lowPoint.index + Mathf.RoundToInt(dIndex * i / 4f);
                EQPoint testPoint = pointSet.points[testIndex];

                Vector3 testOffset = Vector3.Project((Vector3)testPoint.position - (Vector3)platformLowPoint.position, Vector3.Cross(chord, Vector3.down));
                testOffset.Scale(new Vector3(1, 0, 1));

                // left hand rule, if (chord x centerOffset) points up then the center of the track segment is bulged out
                // and we should add the offset to the platform position
                if ((Vector3.Cross(chord, testOffset).y > 0) && (testOffset.sqrMagnitude > maxPositiveOffset.sqrMagnitude))
                {
                    maxPositiveOffset = testOffset;
                    maxOffsetPosition = (Vector3)testPoint.position;
                }
            }

            var platformHolderPos = midPoint + maxPositiveOffset;

            platformHolder.transform.position = platformHolderPos;
            platformHolder.transform.rotation = Quaternion.LookRotation(chord, Vector3.up);

#if DEBUG
            CreateDebugPoint(platformHolder.transform, (Vector3)platformLowPoint.position + WorldMover.currentMove, Color.red);
            CreateDebugPoint(platformHolder.transform, (Vector3)platformHighPoint.position + WorldMover.currentMove, Color.blue);
            CreateDebugPoint(platformHolder.transform, midPoint, Color.green);
            CreateDebugPoint(platformHolder.transform, maxOffsetPosition + WorldMover.currentMove, Color.yellow);
#endif

            var platformObj = Object.Instantiate(BundleLoader.RuralPlatform, platformHolder.transform);
            platformObj.AddComponent<PlatformLightController>();

            string localName = LocalizationKeyExtensions.StationName(id);
            SetPlatformSignText(platformObj, localName);

            var tpAnchor = platformObj.transform.Find(TELEPORT_ANCHOR);

            CoroutineManager.Instance.StartCoroutine(InitStationLabelCoro(tpAnchor, id, localName, markerAngle));
        }

#if DEBUG
        private static void CreateDebugPoint(Transform parent, Vector3 position, Color color)
        {
            var obj = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            obj.GetComponent<Renderer>().material.color = color;
            Object.Destroy(obj.GetComponent<Collider>());
            obj.transform.parent = parent;
            obj.transform.position = position + Vector3.up * 0.5f;
            obj.transform.localScale = Vector3.one * 0.1f;
        }
#endif

        public static void DestroyDecorations(RuralLoadingMachine platform)
        {
            var trackTransform = platform.Track.GetRailTrack().transform;

            var sign0 = trackTransform.Find($"[track id] {platform.Id} 0");
            if (sign0) Object.Destroy(sign0.gameObject);

            var sign1 = trackTransform.Find($"[track id] {platform.Id} 1");
            if (sign1) Object.Destroy(sign1.gameObject);

            var mapLabel = MarkerController.transform.Find($"MapPaper/Names/PJ_MapLabel_{platform.Id}");
            if (mapLabel) Object.Destroy(mapLabel.gameObject);

            var concrete = trackTransform.Find($"[platform] {platform.Id}");
            if (concrete) Object.Destroy(concrete.gameObject);
        }

        private static IEnumerator InitStationLabelCoro(Transform anchor, string id, string localName, float? labelAngle)
        {
            yield return null;

            var travelDestination = anchor.gameObject.AddComponent<RuralStationFastTravelDestination>();
            travelDestination.Init(anchor, localName, id);

            MapMarker? marker = null;

            yield return new WaitUntil(() => MarkerController.markers.TryGetValue(travelDestination, out marker));

            var renderer = marker!.GetComponentsInChildren<MeshRenderer>(true);
            foreach (var mesh in renderer)
            {
                if (mesh.name == "Visuals")
                {
                    mesh.material = MarkerMaterial;
                }
            }
            MapMarkers.Add(marker);

            // Marker is automatically added, but text is not
            var namePrefab = GetNamePrefab(MarkerController);
            var nameObj = Object.Instantiate(namePrefab, namePrefab.transform.parent);
            nameObj.name = $"PJ_MapLabel_{id}";

            var nameRect = nameObj.GetComponent<RectTransform>();

            var nameText = nameObj.GetComponent<TextMeshPro>();
            nameText.text = id;
            nameText.horizontalAlignment = HorizontalAlignmentOptions.Center;

            PositionMapLabel(marker.transform, nameRect, labelAngle ?? 0f, LABEL_RADIUS);

            yield return null;
        }

        private static void PositionMapLabel(Transform marker, RectTransform labelRect, float angleInDegrees, float radius)
        {
            // Set up UI anchoring
            var anchors = new Vector2(0.5f, 0.5f);
            labelRect.anchorMax = anchors;
            labelRect.anchorMin = anchors;
            labelRect.pivot = anchors;

            // Convert angle to radians for trig functions
            float angleRad = angleInDegrees * Mathf.Deg2Rad;

            // Calculate x and z offsets using trigonometry
            float offsetX = radius * Mathf.Cos(angleRad);
            float offsetZ = radius * Mathf.Sin(angleRad);

            // Apply offset to marker position
            var finalPosition = new Vector2(
                marker.localPosition.x + offsetX,
                marker.localPosition.z + offsetZ
            );

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
