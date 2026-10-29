using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.XR;
using UnityEngine.Rendering;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

namespace Inspection
{
    public sealed class ArRig
    {
        public Camera Camera;
        public ARRaycastManager Raycast;
        public ARPlaneManager Planes;
        public Transform MarkerRoot;
    }

    // Builds the AR Foundation rig at runtime so the scene does not need
    // package script GUIDs baked into a prefab.
    public static class ArRigBuilder
    {
        public static ArRig Build()
        {
            var sessionGo = new GameObject("AR Session");
            sessionGo.AddComponent<ARSession>();

            var originGo = new GameObject("XR Origin");
            var origin = originGo.AddComponent<XROrigin>();
            origin.RequestedTrackingOriginMode = XROrigin.TrackingOriginMode.Device;
            origin.CameraYOffset = 0f;

            var offset = new GameObject("Camera Offset");
            offset.transform.SetParent(originGo.transform, false);

            var camGo = new GameObject("Main Camera");
            camGo.tag = "MainCamera";
            camGo.transform.SetParent(offset.transform, false);
            var camera = camGo.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.08f, 0.09f, 0.1f, 1f);
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 40f;
            camGo.AddComponent<AudioListener>();

            var cameraManager = camGo.AddComponent<ARCameraManager>();
            cameraManager.requestedFacingDirection = CameraFacingDirection.World;
            cameraManager.autoFocusRequested = true;
            camGo.AddComponent<ARCameraBackground>();
            AddPoseDriver(camGo);

            origin.CameraFloorOffsetObject = offset;
            origin.Camera = camera;

            var planes = originGo.AddComponent<ARPlaneManager>();
            planes.requestedDetectionMode = PlaneDetectionMode.Horizontal;
            planes.planePrefab = CreatePlaneTemplate();

            var raycast = originGo.AddComponent<ARRaycastManager>();

            var lightGo = new GameObject("Marker Light");
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.1f;
            light.shadows = LightShadows.None;
            lightGo.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.7f, 0.7f, 0.7f);

            var markers = new GameObject("Markers").transform;
            return new ArRig
            {
                Camera = camera,
                Raycast = raycast,
                Planes = planes,
                MarkerRoot = markers
            };
        }

        // TrackedPoseDriver.Reset() does not bind an HMD pose, and Reset() is not
        // called in a player. Bind the AR camera pose explicitly.
        static void AddPoseDriver(GameObject camGo)
        {
            var position = new InputAction(
                name: "CenterEyePosition",
                type: InputActionType.Value,
                binding: "<XRHMD>/centerEyePosition",
                expectedControlType: "Vector3");
            var rotation = new InputAction(
                name: "CenterEyeRotation",
                type: InputActionType.Value,
                binding: "<XRHMD>/centerEyeRotation",
                expectedControlType: "Quaternion");
            var tracking = new InputAction(
                name: "TrackingState",
                type: InputActionType.Value,
                binding: "<XRHMD>/trackingState",
                expectedControlType: "Integer");

            var driver = camGo.AddComponent<TrackedPoseDriver>();
            driver.trackingType = TrackedPoseDriver.TrackingType.RotationAndPosition;
            driver.updateType = TrackedPoseDriver.UpdateType.UpdateAndBeforeRender;
            driver.positionInput = new InputActionProperty(position);
            driver.rotationInput = new InputActionProperty(rotation);
            driver.trackingStateInput = new InputActionProperty(tracking);
        }

        static GameObject CreatePlaneTemplate()
        {
            var donor = GameObject.CreatePrimitive(PrimitiveType.Quad);
            donor.SetActive(false);
            var source = donor.GetComponent<MeshRenderer>().sharedMaterial;
            Object.Destroy(donor);

            var go = new GameObject("AR Plane Template");
            go.SetActive(false);
            go.AddComponent<MeshFilter>();
            var renderer = go.AddComponent<MeshRenderer>();
            var material = new Material(source);
            var tint = new Color(0.25f, 0.62f, 0.82f, 1f);
            material.color = tint;
            if (material.HasProperty("_BaseColor"))
                material.SetColor("_BaseColor", tint);
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            go.AddComponent<ARPlane>();
            go.AddComponent<ARPlaneMeshVisualizer>();
            return go;
        }
    }
}
