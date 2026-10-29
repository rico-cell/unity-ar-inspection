#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.XR.Management;
using UnityEditor.XR.Management.Metadata;
using UnityEngine;
using UnityEngine.XR.Management;

namespace Inspection.EditorTools
{
    // Applies the player, cleartext HTTP, and XR loader settings this prototype needs.
    // Menu: Inspection → Apply Project Settings.
    public static class InspectionProjectSetup
    {
        const string ScenePath = "Assets/Scenes/Main.unity";
        const string AndroidLoader = "UnityEngine.XR.ARCore.ARCoreLoader";
        const string IosLoader = "UnityEngine.XR.ARKit.ARKitLoader";
        const int MaxAttempts = 40;

        [InitializeOnLoadMethod]
        static void AutoApply()
        {
            if (Application.isBatchMode)
                return;
            EditorApplication.delayCall += () => RunAttempt(0);
        }

        [MenuItem("Inspection/Apply Project Settings")]
        public static void ApplyFromMenu()
        {
            SessionState.EraseBool("inspection.setup.done");
            var xrOk = ApplyPlayerSettings();
            var loadersOk = TryAssignLoaders(out var xrError);
            EnsureMainScene();
            AssetDatabase.SaveAssets();
            if (xrOk && loadersOk)
                Debug.Log("Inspection: player settings applied. ARCore is enabled for Android and ARKit for iOS. Allow downloads over HTTP is Always Allowed.");
            else
                Debug.LogWarning("Inspection: player settings were written, but XR plug-in assignment still needs a check. " + xrError +
                                 " Open Edit > Project Settings > XR Plug-in Management, enable ARCore on the Android tab and ARKit on the iOS tab, then run this menu again.");
        }

        static void RunAttempt(int attempt)
        {
            if (SessionState.GetBool("inspection.setup.done", false))
                return;
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                return;
            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                if (attempt < MaxAttempts)
                    EditorApplication.delayCall += () => RunAttempt(attempt + 1);
                return;
            }

            ApplyPlayerSettings();
            EnsureMainScene();
            var loadersOk = TryAssignLoaders(out var xrError);
            if (loadersOk)
            {
                SessionState.SetBool("inspection.setup.done", true);
                AssetDatabase.SaveAssets();
                Debug.Log("Inspection: project settings ready. Backend URL can be changed in the on-screen form. Default " + BackendConfig.DefaultBaseUrl);
                return;
            }
            if (attempt < MaxAttempts)
            {
                EditorApplication.delayCall += () => RunAttempt(attempt + 1);
                return;
            }
            Debug.LogWarning("Inspection: could not assign XR loaders automatically. " + xrError);
        }

        static bool ApplyPlayerSettings()
        {
            PlayerSettings.companyName = "Inspection";
            PlayerSettings.productName = "现场巡检标注";
            PlayerSettings.colorSpace = ColorSpace.Linear;
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
            PlayerSettings.allowedAutorotateToPortrait = true;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            PlayerSettings.allowedAutorotateToLandscapeLeft = false;
            PlayerSettings.allowedAutorotateToLandscapeRight = false;

            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, "com.inspection.armarker");
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.iOS, "com.inspection.armarker");
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel29;
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.Android.forceInternetPermission = true;
            if (PlayerSettings.GetScriptingBackend(NamedBuildTarget.Android) != ScriptingImplementation.IL2CPP)
                PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);

            PlayerSettings.iOS.cameraUsageDescription = "需要使用相机检测水平面并标注现场问题。";
            PlayerSettings.iOS.targetOSVersionString = "15.0";

            // HTTP to a LAN Go server. This sets Android usesCleartextTraffic and iOS ATS.
            PlayerSettings.insecureHttpOption = InsecureHttpOption.AlwaysAllowed;
            SetActiveInputHandlerBoth();
            return true;
        }

        static void SetActiveInputHandlerBoth()
        {
            var assets = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/ProjectSettings.asset");
            if (assets == null || assets.Length == 0)
                return;
            var serialized = new SerializedObject(assets[0]);
            var prop = serialized.FindProperty("activeInputHandler");
            if (prop != null && prop.intValue != 2)
            {
                prop.intValue = 2; // Both: Input System and the legacy Input Manager.
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        static bool TryAssignLoaders(out string error)
        {
            error = "";
            try
            {
                var perBuild = GetOrCreateSettings();
                if (perBuild == null)
                {
                    error = "XRGeneralSettingsPerBuildTarget.GetOrCreate was not available yet.";
                    return false;
                }
                var android = Assign(perBuild, BuildTargetGroup.Android, AndroidLoader);
                var ios = Assign(perBuild, BuildTargetGroup.iOS, IosLoader);
                if (!android || !ios)
                {
                    error = "AssignLoader returned false. Install the Android and iOS build modules, then reopen XR Plug-in Management.";
                    return false;
                }
                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }

        static bool Assign(XRGeneralSettingsPerBuildTarget perBuild, BuildTargetGroup group, string loaderType)
        {
            if (!perBuild.HasSettingsForBuildTarget(group))
                perBuild.CreateDefaultSettingsForBuildTarget(group);
            if (!perBuild.HasManagerSettingsForBuildTarget(group))
                perBuild.CreateDefaultManagerSettingsForBuildTarget(group);
            var general = perBuild.SettingsForBuildTarget(group);
            if (general == null)
                return false;
            general.InitManagerOnStart = true;
            if (general.AssignedSettings == null)
                return false;
            EditorUtility.SetDirty(general);
            if (XRPackageMetadataStore.IsLoaderAssigned(loaderType, group))
                return true;
            return XRPackageMetadataStore.AssignLoader(general.AssignedSettings, loaderType, group);
        }

        static XRGeneralSettingsPerBuildTarget GetOrCreateSettings()
        {
            var method = typeof(XRGeneralSettingsPerBuildTarget).GetMethod(
                "GetOrCreate",
                BindingFlags.Static | BindingFlags.NonPublic);
            if (method == null)
                return null;
            return method.Invoke(null, null) as XRGeneralSettingsPerBuildTarget;
        }

        static void EnsureMainScene()
        {
            if (!AssetDatabase.IsValidFolder("Assets/Scenes"))
                AssetDatabase.CreateFolder("Assets", "Scenes");
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) == null)
            {
                var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                var go = new GameObject("Inspection");
                go.AddComponent<InspectionApp>();
                EditorSceneManager.SaveScene(scene, ScenePath);
            }

            var scenes = new List<EditorBuildSettingsScene>();
            var found = false;
            foreach (var scene in EditorBuildSettings.scenes)
            {
                if (scene.path == ScenePath)
                {
                    scenes.Add(new EditorBuildSettingsScene(ScenePath, true));
                    found = true;
                }
                else
                {
                    scenes.Add(scene);
                }
            }
            if (!found)
                scenes.Insert(0, new EditorBuildSettingsScene(ScenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();

            var active = EditorSceneManager.GetActiveScene();
            if (string.IsNullOrEmpty(active.path))
                EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        }
    }
}
#endif
