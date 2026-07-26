using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

/// <summary>
/// Applies low-risk platforming scene optimizations:
/// - marks non-moving blockout/environment renderers and colliders as static;
/// - keeps script-driven/dynamic objects non-static;
/// - disables shadow casting/receiving on text renderers.
/// </summary>
[InitializeOnLoad]
public static class PlatformingOptimizationPass
{
    private const string RequestPath =
        "Assets/Editor/RunPlatformingOptimizationPass.request";
    private const string PlatformingScenesFolder =
        "Assets/Scenes/Platforming Levels";
    private const string PrefabFolder = "Assets/Prefabs";
    private const string ParallaxBackgroundName =
        "Countdown Forest Parallax Background";

    private static readonly StaticEditorFlags EnvironmentStaticFlags =
        StaticEditorFlags.BatchingStatic
        | StaticEditorFlags.OccludeeStatic
        | StaticEditorFlags.ReflectionProbeStatic;

    private static readonly HashSet<string> StaticRootNames =
        new HashSet<string>
        {
            "3D Models",
            "Blockout"
        };

    private static readonly HashSet<string> DynamicBehaviourNames =
        new HashSet<string>
        {
            "AudioManager",
            "CombatEncounter",
            "CountdownSequenceRunner",
            "CubeLeverVisual",
            "DeathZone",
            "EditorSceneReloadShortcut",
            "FadeManager",
            "FireWallVFX",
            "GeyserWindVFX",
            "ParallaxLayer3D",
            "PlatformingCameraZone",
            "PlatformPoofVFX",
            "PlayerController2D",
            "PrototypeCameraFollow",
            "SwitchTarget",
            "TimedGeyser",
            "TimedPlatform",
            "TimedSwitch"
        };

    static PlatformingOptimizationPass()
    {
        if (!File.Exists(RequestPath))
        {
            return;
        }

        EditorApplication.update += RunFromRequest;
    }

    [MenuItem("Countdown Hero/Run Platforming Optimization Pass")]
    public static void RunAll()
    {
        string startingScenePath = EditorSceneManager.GetActiveScene().path;
        EditorSceneManager.SaveOpenScenes();

        OptimizationReport report = new OptimizationReport();
        OptimizePlatformingScenes(report);
        OptimizePrefabs(report);
        ReopenStartingScene(startingScenePath);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log(
            "Platforming optimization pass complete. "
            + $"Static objects marked: {report.MarkedStatic}. "
            + $"Dynamic objects cleared: {report.ClearedStatic}. "
            + $"Text renderers without shadows: {report.TextShadowsDisabled}.");
    }

    private static void RunFromRequest()
    {
        if (EditorApplication.isCompiling
            || EditorApplication.isPlayingOrWillChangePlaymode)
        {
            return;
        }

        EditorApplication.update -= RunFromRequest;
        File.Delete(RequestPath);
        RunAll();
    }

    private static void OptimizePlatformingScenes(OptimizationReport report)
    {
        if (!Directory.Exists(PlatformingScenesFolder))
        {
            Debug.LogWarning($"Missing platforming scenes folder: {PlatformingScenesFolder}");
            return;
        }

        string[] scenePaths = Directory.GetFiles(
            PlatformingScenesFolder,
            "*.unity",
            SearchOption.TopDirectoryOnly);

        foreach (string rawScenePath in scenePaths)
        {
            string scenePath = rawScenePath.Replace("\\", "/");
            Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            bool changed = OptimizeScene(scene, report);

            if (changed)
            {
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene, scenePath);
            }
        }
    }

    private static bool OptimizeScene(Scene scene, OptimizationReport report)
    {
        bool changed = false;
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            changed |= OptimizeHierarchy(root.transform, report);
        }

        return changed;
    }

    private static void OptimizePrefabs(OptimizationReport report)
    {
        if (!Directory.Exists(PrefabFolder))
        {
            return;
        }

        string[] prefabGuids = AssetDatabase.FindAssets(
            "t:Prefab",
            new[] { PrefabFolder });

        foreach (string prefabGuid in prefabGuids)
        {
            string prefabPath = AssetDatabase.GUIDToAssetPath(prefabGuid);
            GameObject prefabRoot = PrefabUtility.LoadPrefabContents(prefabPath);
            bool changed = OptimizeHierarchy(prefabRoot.transform, report);

            if (changed)
            {
                PrefabUtility.SaveAsPrefabAsset(prefabRoot, prefabPath);
            }

            PrefabUtility.UnloadPrefabContents(prefabRoot);
        }
    }

    private static bool OptimizeHierarchy(
        Transform root,
        OptimizationReport report)
    {
        bool changed = false;
        Transform[] transforms = root.GetComponentsInChildren<Transform>(true);

        foreach (Transform current in transforms)
        {
            changed |= DisableTextRendererShadows(current.gameObject, report);

            if (ShouldForceNonStatic(current))
            {
                changed |= ClearStaticFlags(current.gameObject, report);
                continue;
            }

            if (ShouldMarkStatic(current))
            {
                changed |= AddStaticFlags(current.gameObject, report);
            }
        }

        return changed;
    }

    private static bool ShouldMarkStatic(Transform transform)
    {
        return IsUnderStaticRoot(transform)
            && HasStaticOptimizableComponent(transform.gameObject)
            && !HasTextComponent(transform.gameObject)
            && !HasDynamicComponentInParents(transform);
    }

    private static bool ShouldForceNonStatic(Transform transform)
    {
        return IsParallaxBackground(transform)
            || HasTextComponent(transform.gameObject)
            || HasDynamicComponentInParents(transform);
    }

    private static bool AddStaticFlags(
        GameObject gameObject,
        OptimizationReport report)
    {
        StaticEditorFlags existingFlags =
            GameObjectUtility.GetStaticEditorFlags(gameObject);
        StaticEditorFlags nextFlags = existingFlags | EnvironmentStaticFlags;

        if (nextFlags == existingFlags)
        {
            return false;
        }

        GameObjectUtility.SetStaticEditorFlags(gameObject, nextFlags);
        report.MarkedStatic++;
        return true;
    }

    private static bool ClearStaticFlags(
        GameObject gameObject,
        OptimizationReport report)
    {
        StaticEditorFlags existingFlags =
            GameObjectUtility.GetStaticEditorFlags(gameObject);
        if (existingFlags == 0)
        {
            return false;
        }

        GameObjectUtility.SetStaticEditorFlags(gameObject, 0);
        report.ClearedStatic++;
        return true;
    }

    private static bool DisableTextRendererShadows(
        GameObject gameObject,
        OptimizationReport report)
    {
        if (!HasTextComponent(gameObject))
        {
            return false;
        }

        bool changed = false;
        foreach (Renderer renderer in gameObject.GetComponentsInChildren<Renderer>(true))
        {
            if (renderer.shadowCastingMode != ShadowCastingMode.Off)
            {
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                changed = true;
            }

            if (renderer.receiveShadows)
            {
                renderer.receiveShadows = false;
                changed = true;
            }
        }

        if (changed)
        {
            report.TextShadowsDisabled++;
        }

        return changed;
    }

    private static bool IsUnderStaticRoot(Transform transform)
    {
        for (Transform current = transform; current != null; current = current.parent)
        {
            if (StaticRootNames.Contains(current.name))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsParallaxBackground(Transform transform)
    {
        for (Transform current = transform; current != null; current = current.parent)
        {
            if (current.name == ParallaxBackgroundName
                || current.GetComponent<ParallaxLayer3D>() != null)
            {
                return true;
            }
        }

        return false;
    }

    private static bool HasStaticOptimizableComponent(GameObject gameObject)
    {
        return gameObject.GetComponent<Renderer>() != null
            || gameObject.GetComponent<Collider>() != null
            || gameObject.GetComponent<Collider2D>() != null;
    }

    private static bool HasTextComponent(GameObject gameObject)
    {
        if (gameObject.GetComponent<TextMesh>() != null)
        {
            return true;
        }

        foreach (Component component in gameObject.GetComponents<Component>())
        {
            if (component != null
                && component.GetType().FullName == "TMPro.TextMeshPro")
            {
                return true;
            }
        }

        return false;
    }

    private static bool HasDynamicComponentInParents(Transform transform)
    {
        for (Transform current = transform; current != null; current = current.parent)
        {
            if (HasDynamicComponent(current.gameObject))
            {
                return true;
            }
        }

        return false;
    }

    private static bool HasDynamicComponent(GameObject gameObject)
    {
        return gameObject.GetComponent<Rigidbody>() != null
            || gameObject.GetComponent<Rigidbody2D>() != null
            || gameObject.GetComponent<Animator>() != null
            || gameObject.GetComponent<ParticleSystem>() != null
            || gameObject.GetComponent<AudioSource>() != null
            || gameObject.GetComponent<Camera>() != null
            || HasDynamicBehaviour(gameObject);
    }

    private static bool HasDynamicBehaviour(GameObject gameObject)
    {
        foreach (MonoBehaviour behaviour in gameObject.GetComponents<MonoBehaviour>())
        {
            if (behaviour == null)
            {
                continue;
            }

            if (DynamicBehaviourNames.Contains(behaviour.GetType().Name))
            {
                return true;
            }
        }

        return false;
    }

    private static void ReopenStartingScene(string startingScenePath)
    {
        if (string.IsNullOrEmpty(startingScenePath)
            || !File.Exists(startingScenePath))
        {
            return;
        }

        EditorSceneManager.OpenScene(startingScenePath, OpenSceneMode.Single);
    }

    private sealed class OptimizationReport
    {
        public int MarkedStatic;
        public int ClearedStatic;
        public int TextShadowsDisabled;
    }
}
