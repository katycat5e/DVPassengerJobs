using DV.Teleporters;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;

namespace PassengerJobs.Platforms
{
    public static class RuralStationBuilder
    {
        public static readonly List<MapMarker> MapMarkers = new();
        private static readonly Color MarkerColor = new Color32(220, 204, 255, 255);

        private static readonly Vector3 LABEL_CENTER_RESET = new(0.0125f, 0.001f, 0);
        private static readonly Vector3 LABEL_RADIUS = new(0.01f, 0, 0);
        private static readonly Vector3 LABEL_OFFSET_SCALE = new(1, 1, 0.6f);

        public static void GenerateDecorations(RuralLoadingMachine platform)
        {
            StationFastTravelDestination travelDestination;
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

            var middle = platformHolder.gameObject.GetComponentsInChildren<Transform>().Where(t => t.gameObject.name == "middle").FirstOrDefault();

            if (middle != null) 
            { 
                travelDestination = middle.gameObject.AddComponent<StationFastTravelDestination>();
                travelDestination.playerTeleportAnchor = middle;

                
            }
               CoroutineManager.Instance.StartCoroutine(InitStationLabelCoro(platformHolder.transform, platform.Id, platform.MarkerAngle));
        }

        public static void DestroyDecorations(RuralLoadingMachine platform)
        {
            var trackTransform = platform.Track.GetRailTrack().transform;

            var sign0 = trackTransform.Find($"[track id] {platform.Id} 0");
            if (sign0) Object.Destroy(sign0.gameObject);

            var sign1 = trackTransform.Find($"[track id] {platform.Id} 1");
            if (sign1) Object.Destroy(sign1.gameObject);

            // B99 has removedstationAndPlayerHouseMarkers
            // MarkerController.stationAndPlayerHouseMarkers.RemoveAll(m => m.name == platform.Id);

            /*var marker = MarkerController.map.transform.Find($"MapMarker_{platform.Id}");
            if (marker) Object.Destroy(marker.gameObject);

            var mapName = MarkerController.map.transform.Find($"MapLabel_{platform.Id}");
            if (mapName) Object.Destroy(mapName.gameObject);*/

            var concrete = trackTransform.Find($"[platform] {platform.Id}");
            if (concrete) Object.Destroy(concrete.gameObject);
        }

        private static IEnumerator InitStationLabelCoro(Transform anchor, string id, float? labelAngle)
        {
            // B99 PJMain.Log($"{id} {(bool)anchor}, {(bool)MarkerController}, {(bool)MarkerController.map}, {MarkerController.stationMapMarkerPrefab}");

            // B99 var prefab = MarkerController.stationAndPlayerHouseMarkers.First().prefab;

            // Setup Map Marker
            /* B99 var marker = new MapMarkersController.MapMarker(MarkerController, MarkerController.map, anchor, anchor,
                id, prefab);
            MarkerController.stationAndPlayerHouseMarkers.Add(marker);
           

            yield return null;
            yield return null;
            yield return null;

            marker.marker.name = $"MapMarker_{id}";
            marker.marker.transform.localScale *= 0.8f;
            var renderer = marker.marker.GetComponentsInChildren<MeshRenderer>(true);
            foreach (var mesh in renderer)
            {
                if (mesh.name == "Visuals")
                {
                    mesh.material = MarkerMaterial;
                }
            }
            MapMarkers.Add(marker);

            var namePrefab = GetNamePrefab(MarkerController);
            var nameObj = Object.Instantiate(namePrefab, marker.marker.transform.parent);
            nameObj.name = $"MapLabel_{id}";

            var labelOffset = Vector3.Scale(Quaternion.AngleAxis(labelAngle ?? 0, Vector3.up) * LABEL_RADIUS, LABEL_OFFSET_SCALE);
            nameObj.transform.localPosition = marker.marker.transform.localPosition + LABEL_CENTER_RESET + labelOffset;
            nameObj.transform.localRotation = Quaternion.Euler(90, 0, 0);
            nameObj.transform.localScale = Vector3.one * 0.8f;

            var nameText = nameObj.GetComponent<TextMeshPro>();
            nameText.text = id;
        */
            //MapMarkersController mapcontroller = GameObject.FindObjectOfType<MapMarkersController>();
            GameObject newmarker = GameObject.Instantiate(MarkerController.stationMarkerPrefab.gameObject, MarkerController.transform);
            newmarker.name = id;

            Vector3 position = MarkerController.GetMapPosition(anchor.position - WorldMover.currentMove, true);
            newmarker.transform.position = position;
            //refs.text.localPosition = position with { y = position.y + 0.025f };


            yield return null; // B99 
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
