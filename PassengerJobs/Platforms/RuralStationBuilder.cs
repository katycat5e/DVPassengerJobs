using PassengerJobs.Generation;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;

using SMath = System.Math;
using EQPoint = DV.PointSet.EquiPointSet.Point;

namespace PassengerJobs.Platforms
{
    public static class RuralStationBuilder
    {
        public static readonly List<MapMarker> MapMarkers = new();
        private static readonly Color MarkerColor = new Color32(220, 204, 255, 255);

        private const float LABEL_RADIUS = 0.008f;
        private const float PLATFORM_LENGTH = 65;
        private const float PLATFORM_HALF_LENGTH = PLATFORM_LENGTH / 2;
        private const float PLATFORM_HEIGHT = 1f;

        private const float COACH_LENGTH = 24.54f;
        private const float LOADING_ZONE_LENGTH = 120;
        private const float LOADING_ZONE_HALF_LENGTH = LOADING_ZONE_LENGTH / 2;

        private const string LAMPS_ANCHOR = "[lamps]";
        private const string TELEPORT_ANCHOR = "[teleport]";

        public static RuralStationData? CreateStation(StationConfig.RuralStation station, RuralStationData? existing = null)
        {
            // find closest track point to station location
            Vector3 searchPosition = station.location + WorldMover.currentMove;

            (RailTrack? railTrack, EQPoint? trackPoint) = RailTrack.GetClosest(searchPosition);
            if (!railTrack || !trackPoint.HasValue)
            {
                PJMain.Error($"Couldn't find closest track point for station {station.id}");
                return null;
            }

            IEnumerable<RuralLoadingTask> tasks;
            
            if (existing is not null)
            {
                tasks = existing.Platform.Tasks;
            }
            else
            {
                tasks = Enumerable.Empty<RuralLoadingTask>();
            }

            var track = railTrack.logicTrack;
            bool isYardTrack = YardTracksOrganizer.Instance.IsTrackManagedByOrganizer(track);

            // get loading zone indices
            int centerIdx = trackPoint.Value.index;

            var pointSet = railTrack.pointSet;
            double lowSpan = Mathd.Max(0, trackPoint.Value.span - LOADING_ZONE_HALF_LENGTH);
            if (lowSpan == 0)
            {
                PJMain.Warning($"Station {station.id} hit low end of track segment");
            }
            int lowIdx = railTrack.pointSet.GetPointIndexForSpan(lowSpan);

            double highSpan = Mathd.Min(pointSet.span, trackPoint.Value.span + LOADING_ZONE_HALF_LENGTH);
            if (highSpan == pointSet.span)
            {
                PJMain.Warning($"Station {station.id} hit high end of track segment");
            }
            int highIdx = railTrack.pointSet.GetPointIndexForSpan(highSpan);

            int maxDelta = SMath.Min(SMath.Abs(centerIdx - lowIdx), SMath.Abs(highIdx - centerIdx));

            lowIdx = centerIdx - maxDelta;
            highIdx = centerIdx + maxDelta + 1;

            // create controllers & meshes
            var loadingMachine = new RuralLoadingMachine(station.id, track, lowIdx, highIdx, station.markerAngle, isYardTrack);

            var platformObj = GenerateDecorations(loadingMachine, station);
            if (!platformObj)
            {
                return null;
            }

            PlatformController controller = platformObj!.AddComponent<PlatformController>();

            var platform = new RuralPlatformWrapper(loadingMachine);
            controller.Platform = platform;

            foreach (var task in tasks)
            {
                loadingMachine.AddTask(task);
            }


            return new RuralStationData(loadingMachine);
        }

        public static GameObject? GenerateDecorations(RuralLoadingMachine platform, StationConfig.RuralStation config)
        {
            var railTrack = platform.Track.GetRailTrack();

            EQPoint[] pointSet = railTrack.GetPointSet(0).points;
            EQPoint lowPoint, highPoint;

            try
            {
                lowPoint = pointSet[platform.LowerBound];
                highPoint = pointSet[platform.UpperBound];
            }
            catch (System.IndexOutOfRangeException)
            {
                PJMain.Error($"Rural station {platform.Id} track points don't exist!");
                return null;
            }

            if (!platform.IsYardTrack)
            {
                GenerateTrackSigns(lowPoint, highPoint, platform.Id, railTrack);
            }

            bool isInsideStation = StationController.GetStationByYardID(platform.Id);

            (var holder, var decorations) = GeneratePlatformMeshes(lowPoint, highPoint, platform.Id, railTrack, config, isInsideStation);

            if (!isInsideStation)
            {
                var tpAnchor = decorations.transform.Find(TELEPORT_ANCHOR);
                CoroutineManager.Instance.StartCoroutine(InitStationLabelCoro(tpAnchor, platform.Id, platform.MarkerAngle ?? 0));
            }

            return holder;
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

        private static (GameObject holder, GameObject decorations) GeneratePlatformMeshes(EQPoint lowPoint, EQPoint highPoint, string id, RailTrack railTrack, StationConfig.RuralStation config, bool isInsideStation)
        {
            // Setup platform
            var platformHolder = new GameObject($"[platform] {id}");
            platformHolder.transform.SetParent(railTrack.transform);

            // adjust offset to keep track clearance
            var pointSet = railTrack.pointSet;
            EQPoint centerPoint = pointSet.points[(lowPoint.index + highPoint.index) / 2];

#if DEBUG
            PJMain.Log($"spawn {id} center @ {railTrack.name} {centerPoint.index}");
#endif
            
            double platformLengthOffset = config.swapSides ? -PLATFORM_HALF_LENGTH : PLATFORM_HALF_LENGTH;

            double highSpan = Mathd.Clamp(centerPoint.span + platformLengthOffset, 0, pointSet.span);
            int platformHighIdx = pointSet.GetPointIndexForSpan(highSpan);
            EQPoint platformHighPoint = pointSet.points[platformHighIdx];

            double lowSpan = Mathd.Clamp(centerPoint.span - platformLengthOffset, 0, pointSet.span);
            int platformLowIdx = pointSet.GetPointIndexForSpan(lowSpan);
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
            CreateDebugPoint(platformHolder.transform, (Vector3)centerPoint.position + WorldMover.currentMove, Color.green);
            CreateDebugPoint(platformHolder.transform, maxOffsetPosition + WorldMover.currentMove, Color.yellow);
#endif

            var prefab = config.hideConcrete ? BundleLoader.RuralPlatformNoBase : BundleLoader.RuralPlatform;

            var platformObj = Object.Instantiate(prefab, platformHolder.transform);
            platformObj.transform.localPosition = new Vector3(0, config.extraHeight, 0);

            if (config.hideLamps)
            {
                Object.Destroy(platformObj.transform.Find(LAMPS_ANCHOR).gameObject);
            }
            else
            {
                platformObj.AddComponent<PlatformLightController>();
            }

            string localName = isInsideStation ?
                LocalizationKeyExtensions.BuiltinStationName(id) :
                LocalizationKeyExtensions.StationName(id);

            SetPlatformSignText(platformObj, localName);

            return (platformHolder, platformObj);
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

        private static IEnumerator InitStationLabelCoro(Transform anchor, string id, float? labelAngle)
        {
            yield return null;

            string localName = LocalizationKeyExtensions.StationName(id);

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
            labelRect.localScale *= 0.8f;
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
