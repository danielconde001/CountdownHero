using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

/// <summary>
/// Applies the current art direction: the platforming world is grayscale while
/// UI and countdown-critical gameplay objects stay colored.
/// </summary>
[InitializeOnLoad]
public static class CountdownGrayscaleThemePass
{
    private const string RequestPath =
        "Assets/Editor/ApplyCountdownGrayscaleTheme.request";
    private const string PlatformingScenesFolder =
        "Assets/Scenes/Platforming Levels";
    private const string PrefabFolder = "Assets/Prefabs";
    private const string GrayscaleMaterialFolder =
        "Assets/Materials/Grayscale Theme";

    private static readonly Dictionary<Material, Material> GrayscaleMaterialCache =
        new Dictionary<Material, Material>();

    static CountdownGrayscaleThemePass()
    {
        if (!File.Exists(RequestPath))
        {
            return;
        }

        EditorApplication.update += RunFromRequest;
    }

    [MenuItem("Countdown Hero/Apply Grayscale Theme")]
    public static void RunAll()
    {
        string startingScenePath = EditorSceneManager.GetActiveScene().path;

        GrayscaleMaterialCache.Clear();
        EnsureFolder("Assets/Materials");
        EnsureFolder(GrayscaleMaterialFolder);

        ThemeReport report = new ThemeReport();
        ApplyToPlatformingScenes(report);
        ApplyToPrefabs(report);
        ReopenStartingScene(startingScenePath);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log(
            "Countdown grayscale theme applied. "
            + $"Renderers themed: {report.RenderersThemed}. "
            + $"Renderers preserved in color: {report.RenderersPreserved}. "
            + $"Grayscale materials created or reused: {report.GrayscaleMaterialsUsed}.");
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

    private static void ApplyToPlatformingScenes(ThemeReport report)
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
            bool changed = false;

            foreach (GameObject root in scene.GetRootGameObjects())
            {
                changed |= ApplyToHierarchy(root.transform, report);
            }

            foreach (Camera camera in UnityEngine.Object.FindObjectsByType<Camera>(
                         FindObjectsInactive.Include,
                         FindObjectsSortMode.None))
            {
                changed |= ApplyCameraTheme(camera);
            }

            if (changed)
            {
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene, scenePath);
            }
        }
    }

    private static void ApplyToPrefabs(ThemeReport report)
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
            bool changed = ApplyToHierarchy(prefabRoot.transform, report);

            if (changed)
            {
                PrefabUtility.SaveAsPrefabAsset(prefabRoot, prefabPath);
            }

            PrefabUtility.UnloadPrefabContents(prefabRoot);
        }
    }

    private static bool ApplyToHierarchy(Transform root, ThemeReport report)
    {
        bool changed = false;
        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);

        foreach (Renderer renderer in renderers)
        {
            if (renderer == null)
            {
                continue;
            }

            if (ShouldPreserveColor(renderer.transform))
            {
                report.RenderersPreserved++;
                continue;
            }

            changed |= ApplyGrayscaleMaterials(renderer, report);
            changed |= DisableBackgroundRendererCosts(renderer);
        }

        return changed;
    }

    private static bool ApplyGrayscaleMaterials(Renderer renderer, ThemeReport report)
    {
        Material[] materials = renderer.sharedMaterials;
        bool changed = false;

        for (int i = 0; i < materials.Length; i++)
        {
            Material source = materials[i];
            if (source == null)
            {
                continue;
            }

            Material grayscaleMaterial = GetOrCreateGrayscaleMaterial(source, report);
            if (grayscaleMaterial != null && grayscaleMaterial != source)
            {
                materials[i] = grayscaleMaterial;
                changed = true;
            }
        }

        if (!changed)
        {
            return false;
        }

        renderer.sharedMaterials = materials;
        report.RenderersThemed++;
        return true;
    }

    private static bool DisableBackgroundRendererCosts(Renderer renderer)
    {
        bool changed = false;

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

        if (renderer.lightProbeUsage != LightProbeUsage.Off)
        {
            renderer.lightProbeUsage = LightProbeUsage.Off;
            changed = true;
        }

        if (renderer.reflectionProbeUsage != ReflectionProbeUsage.Off)
        {
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            changed = true;
        }

        if (renderer.motionVectorGenerationMode != MotionVectorGenerationMode.ForceNoMotion)
        {
            renderer.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
            changed = true;
        }

        return changed;
    }

    private static Material GetOrCreateGrayscaleMaterial(
        Material source,
        ThemeReport report)
    {
        if (IsGrayscaleThemeMaterial(source))
        {
            return source;
        }

        if (GrayscaleMaterialCache.TryGetValue(source, out Material cached))
        {
            return cached;
        }

        string materialPath = GetGrayscaleMaterialPath(source);
        Material grayscaleMaterial = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
        if (grayscaleMaterial == null)
        {
            grayscaleMaterial = new Material(source)
            {
                name = $"Gray {source.name}"
            };
            DesaturateMaterialColors(grayscaleMaterial);
            AssetDatabase.CreateAsset(grayscaleMaterial, materialPath);
        }
        else
        {
            grayscaleMaterial.CopyPropertiesFromMaterial(source);
            DesaturateMaterialColors(grayscaleMaterial);
            EditorUtility.SetDirty(grayscaleMaterial);
        }

        GrayscaleMaterialCache[source] = grayscaleMaterial;
        report.GrayscaleMaterialsUsed++;
        return grayscaleMaterial;
    }

    private static void DesaturateMaterialColors(Material material)
    {
        Shader shader = material.shader;
        if (shader == null)
        {
            return;
        }

        int propertyCount = shader.GetPropertyCount();
        for (int i = 0; i < propertyCount; i++)
        {
            if (shader.GetPropertyType(i) != ShaderPropertyType.Color)
            {
                continue;
            }

            string propertyName = shader.GetPropertyName(i);
            if (!material.HasProperty(propertyName))
            {
                continue;
            }

            material.SetColor(propertyName, ToGrayscale(material.GetColor(propertyName)));
        }
    }

    private static bool ShouldPreserveColor(Transform transform)
    {
        return IsUi(transform)
            || HasTextRenderer(transform)
            || transform.GetComponentInParent<TimedSwitch>(true) != null
            || transform.GetComponentInParent<TimedPlatform>(true) != null
            || transform.GetComponentInParent<TimedGeyser>(true) != null
            || transform.GetComponentInParent<FireWallVFX>(true) != null
            || transform.GetComponentInParent<CombatEncounter>(true) != null;
    }

    private static bool IsUi(Transform transform)
    {
        return transform.GetComponentInParent<Canvas>(true) != null;
    }

    private static bool HasTextRenderer(Transform transform)
    {
        if (transform.GetComponent<TextMesh>() != null)
        {
            return true;
        }

        foreach (Component component in transform.GetComponents<Component>())
        {
            if (component != null
                && component.GetType().FullName.StartsWith(
                    "TMPro.",
                    StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private static bool ApplyCameraTheme(Camera camera)
    {
        Color grayscaleSky = new Color(0.73f, 0.73f, 0.73f, camera.backgroundColor.a);
        if (camera.backgroundColor == grayscaleSky)
        {
            return false;
        }

        camera.backgroundColor = grayscaleSky;
        EditorUtility.SetDirty(camera);
        return true;
    }

    private static Color ToGrayscale(Color color)
    {
        float value = color.grayscale;
        return new Color(value, value, value, color.a);
    }

    private static bool IsGrayscaleThemeMaterial(Material material)
    {
        string assetPath = AssetDatabase.GetAssetPath(material);
        return !string.IsNullOrEmpty(assetPath)
            && assetPath.Replace("\\", "/").StartsWith(
                GrayscaleMaterialFolder,
                StringComparison.Ordinal);
    }

    private static string GetGrayscaleMaterialPath(Material source)
    {
        string sourcePath = AssetDatabase.GetAssetPath(source);
        string guidPart = "builtin";
        string localIdPart = "0";

        if (!string.IsNullOrEmpty(sourcePath)
            && AssetDatabase.TryGetGUIDAndLocalFileIdentifier(
                source,
                out string guid,
                out long localId))
        {
            guidPart = guid.Substring(0, Mathf.Min(8, guid.Length));
            localIdPart = localId.ToString();
        }

        string fileName = SanitizeFileName(
            $"Gray {source.name} {guidPart} {localIdPart}.mat");
        return $"{GrayscaleMaterialFolder}/{fileName}";
    }

    private static string SanitizeFileName(string fileName)
    {
        foreach (char invalid in Path.GetInvalidFileNameChars())
        {
            fileName = fileName.Replace(invalid, '_');
        }

        return fileName;
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path))
        {
            return;
        }

        string parent = Path.GetDirectoryName(path)?.Replace("\\", "/");
        string folder = Path.GetFileName(path);

        if (string.IsNullOrEmpty(parent) || string.IsNullOrEmpty(folder))
        {
            throw new InvalidOperationException($"Invalid folder path: {path}");
        }

        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, folder);
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

    private sealed class ThemeReport
    {
        public int RenderersThemed;
        public int RenderersPreserved;
        public int GrayscaleMaterialsUsed;
    }
}
