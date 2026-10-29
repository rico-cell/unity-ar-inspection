using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

namespace Inspection
{
    public sealed class MarkerPlacer
    {
        readonly Camera camera;
        readonly ARRaycastManager raycast;
        readonly ARPlaneManager planes;
        readonly Transform markerRoot;
        readonly List<ARRaycastHit> hits = new List<ARRaycastHit>();
        readonly List<RaycastResult> uiHits = new List<RaycastResult>();

        public Func<bool> IsTyping;
        public Func<bool> CanPlace;
        public Action<Vector3, GameObject> OnPlaced;
        public Action<string> OnMiss;

        public MarkerPlacer(Camera camera, ARRaycastManager raycast, ARPlaneManager planes, Transform markerRoot)
        {
            this.camera = camera;
            this.raycast = raycast;
            this.planes = planes;
            this.markerRoot = markerRoot;
        }

        public int PlaneCount
        {
            get
            {
                if (planes == null || planes.subsystem == null || !planes.subsystem.running)
                    return 0;
                return planes.trackables.count;
            }
        }

        public void Tick()
        {
            if (CanPlace != null && !CanPlace())
                return;
            if (!TryGetPress(out var screen))
                return;
            // Buttons and inputs live on the same screen. A UI press must not also drop a marker.
            if (IsBlockedByUI(screen))
                return;
            if (IsTyping != null && IsTyping())
                return;
            if (TouchScreenKeyboard.visible)
                return;

            var tracking = ARSession.state == ARSessionState.SessionTracking;
            if (tracking && raycast != null &&
                raycast.Raycast(screen, hits, TrackableType.PlaneWithinPolygon) &&
                hits.Count > 0)
            {
                var pose = hits[0].pose;
                Spawn(pose.position + pose.up * 0.04f, pose.rotation);
                return;
            }

            // Editor only: there is no camera feed or plane. This is not AR detection.
            if (Application.isEditor && !tracking && camera != null)
            {
                var position = camera.transform.position + camera.transform.forward * 1.5f;
                Spawn(position, Quaternion.identity);
                return;
            }

            if (OnMiss != null)
                OnMiss("没有点到水平面。请移动手机扫描地面或桌面，等出现蓝色平面后再点。");
        }

        void Spawn(Vector3 position, Quaternion rotation)
        {
            var marker = GameObject.CreatePrimitive(PrimitiveType.Cube);
            marker.name = "Issue Marker";
            if (markerRoot != null)
                marker.transform.SetParent(markerRoot, true);
            marker.transform.SetPositionAndRotation(position, rotation);
            marker.transform.localScale = Vector3.one * 0.08f;
            SetColor(marker, new Color(0.95f, 0.95f, 0.86f, 1f));
            var renderer = marker.GetComponent<Renderer>();
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            if (OnPlaced != null)
                OnPlaced(marker.transform.position, marker);
        }

        public static void SetColor(GameObject marker, Color color)
        {
            if (marker == null)
                return;
            var renderer = marker.GetComponent<Renderer>();
            if (renderer == null)
                return;
            var material = renderer.material;
            material.color = color;
            if (material.HasProperty("_BaseColor"))
                material.SetColor("_BaseColor", color);
        }

        public static Color ColorForPriority(string priority)
        {
            switch (priority)
            {
                case "high":
                    return new Color(0.86f, 0.28f, 0.24f, 1f);
                case "low":
                    return new Color(0.30f, 0.70f, 0.40f, 1f);
                default:
                    return new Color(0.93f, 0.72f, 0.18f, 1f);
            }
        }

        static bool TryGetPress(out Vector2 screen)
        {
            var touchscreen = Touchscreen.current;
            if (touchscreen != null && touchscreen.primaryTouch.press.wasPressedThisFrame)
            {
                screen = touchscreen.primaryTouch.position.ReadValue();
                return true;
            }

            // Mouse covers the Editor. On a phone, a new touch is handled above,
            // so the emulated mouse does not place a second marker on the same tap.
            var mouse = Mouse.current;
            if (mouse != null && mouse.leftButton.wasPressedThisFrame)
            {
                screen = mouse.position.ReadValue();
                return true;
            }

            screen = default;
            return false;
        }

        bool IsBlockedByUI(Vector2 screen)
        {
            var eventSystem = EventSystem.current;
            if (eventSystem == null)
                return false;
            uiHits.Clear();
            var pointer = new PointerEventData(eventSystem);
            pointer.position = screen;
            eventSystem.RaycastAll(pointer, uiHits);
            return uiHits.Count > 0;
        }
    }
}
