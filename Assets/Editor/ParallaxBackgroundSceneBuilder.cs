using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

/// <summary>
/// Builds the reusable 3D parallax background prefab and installs it into the
/// platforming scenes. The generated objects are visual-only: colliders,
/// rigidbodies, and imported gameplay components are stripped from instances.
/// </summary>
[InitializeOnLoad]
public static class ParallaxBackgroundSceneBuilder
{
    private const string RequestPath =
        "Assets/Editor/BuildParallaxBackground.request";
    private const string PrefabFolder = "Assets/Prefabs/Environment";
    private const string PrefabPath =
        PrefabFolder + "/Countdown Forest Parallax Background.prefab";
    private const string MaterialFolder = "Assets/Materials/Parallax";
    private const string PlatformingScenesFolder = "Assets/Scenes/Platforming Levels";
    private const string BackgroundName = "Countdown Forest Parallax Background";
    private const string ModelsRootName = "3D Models";

    private const string KenneyRoot = "Assets/Models/Kenney Assets";
    private const string UltimateRoot =
        "Assets/Models/Ultimate Platformer Pack - Dec 2021";

    private static readonly Dictionary<string, Material> Materials =
        new Dictionary<string, Material>();

    static ParallaxBackgroundSceneBuilder()
    {
        if (!File.Exists(RequestPath))
        {
            return;
        }

        EditorApplication.update += BuildFromRequest;
    }

    [MenuItem("Countdown Hero/Build Parallax Background")]
    public static void BuildAll()
    {
        string startingScenePath = EditorSceneManager.GetActiveScene().path;

        Materials.Clear();
        EnsureProjectFolders();
        GameObject prefab = BuildPrefab();
        InstallPrefabInPlatformingScenes(prefab);
        ReopenStartingScene(startingScenePath);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("Built and installed the Countdown Forest Parallax Background.");
    }

    private static void BuildFromRequest()
    {
        if (EditorApplication.isCompiling || EditorApplication.isPlayingOrWillChangePlaymode)
        {
            return;
        }

        EditorApplication.update -= BuildFromRequest;
        File.Delete(RequestPath);
        BuildAll();
    }

    private static GameObject BuildPrefab()
    {
        GameObject root = new GameObject(BackgroundName);

        Material sky = GetMaterial("Parallax Sky Card", new Color(0.73f, 0.73f, 0.73f));
        Material farHill = GetMaterial("Parallax Far Hill", new Color(0.47f, 0.47f, 0.47f));
        Material countdownRune = GetMaterial("Parallax Countdown Rune", new Color(0.58f, 0.58f, 0.58f));
        Material midTree = GetMaterial("Parallax Mid Tree", new Color(0.32f, 0.32f, 0.32f));
        Material nearFoliage = GetMaterial("Parallax Near Foliage", new Color(0.24f, 0.24f, 0.24f));
        Material nearRock = GetMaterial("Parallax Near Rock", new Color(0.38f, 0.38f, 0.38f));

        Transform skyLayer = CreateLayer(
            root.transform,
            "Layer 0 - Painted Sky",
            0.006f,
            0.002f,
            Vector2.zero,
            0f,
            0f,
            26f);
        CreatePrimitiveVisual(
            skyLayer,
            "Sky Color Card",
            PrimitiveType.Cube,
            new Vector3(48f, 3f, 30f),
            Quaternion.identity,
            new Vector3(220f, 70f, 0.25f),
            sky);

        Transform hillLayer = CreateLayer(
            root.transform,
            "Layer 2 - Distant Hills",
            0.055f,
            0.018f,
            Vector2.zero,
            0f,
            0f,
            18f);
        PopulateHills(hillLayer, farHill);

        Transform countdownLayer = CreateLayer(
            root.transform,
            "Layer 3 - Countdown Ruins",
            0.09f,
            0.03f,
            new Vector2(0.035f, 0.01f),
            0.1f,
            1.7f,
            15f);
        PopulateCountdownRuins(countdownLayer, countdownRune);

        Transform forestLayer = CreateLayer(
            root.transform,
            "Layer 4 - Mid Forest",
            0.14f,
            0.045f,
            new Vector2(0.04f, 0.02f),
            0.16f,
            2.2f,
            11f);
        PopulateForest(forestLayer, midTree, nearRock);

        Transform foregroundLayer = CreateLayer(
            root.transform,
            "Layer 5 - Near Foliage",
            0.22f,
            0.065f,
            new Vector2(0.025f, 0.012f),
            0.22f,
            3.1f,
            8f);
        PopulateForeground(foregroundLayer, nearFoliage, nearRock);

        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        UnityEngine.Object.DestroyImmediate(root);
        return prefab;
    }

    private static void InstallPrefabInPlatformingScenes(GameObject prefab)
    {
        if (prefab == null)
        {
            Debug.LogError($"Could not load or create {PrefabPath}.");
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
            RemoveExistingBackgrounds();

            GameObject modelsRoot = GameObject.Find(ModelsRootName);
            if (modelsRoot == null)
            {
                modelsRoot = new GameObject(ModelsRootName);
            }

            GameObject background = PrefabUtility.InstantiatePrefab(prefab, scene) as GameObject;
            if (background == null)
            {
                Debug.LogWarning($"Failed to instantiate parallax background in {scenePath}.");
                continue;
            }

            background.transform.SetParent(modelsRoot.transform, false);
            background.transform.localPosition = Vector3.zero;
            background.transform.localRotation = Quaternion.identity;
            background.transform.localScale = Vector3.one;
            background.transform.SetAsFirstSibling();

            PolishCameraForBackground();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, scenePath);
        }
    }

    private static void ReopenStartingScene(string startingScenePath)
    {
        if (string.IsNullOrEmpty(startingScenePath) || !File.Exists(startingScenePath))
        {
            return;
        }

        EditorSceneManager.OpenScene(startingScenePath, OpenSceneMode.Single);
    }

    private static void PopulateHills(Transform parent, Material material)
    {
        string[] hillPaths =
        {
            Ultimate("Nature/FBX/RockPlatforms_Large.fbx"),
            Ultimate("Nature/FBX/RockPlatforms_Medium.fbx"),
            Ultimate("Nature/FBX/RockPlatform_Tall.fbx"),
            Kenney("block-grass-large.fbx"),
            Kenney("block-grass-large-tall.fbx")
        };

        for (int i = 0; i < 22; i++)
        {
            float x = -86f + i * 12.5f;
            float y = -7.4f + Mathf.Sin(i * 0.45f) * 0.32f;
            float scale = 2.6f + (i % 5) * 0.34f;
            PlaceModel(
                parent,
                hillPaths[i % hillPaths.Length],
                $"Soft Hill {i:00}",
                new Vector3(x, y, 0f),
                Quaternion.Euler(0f, 25f * (i % 3 - 1), 0f),
                new Vector3(scale * 1.35f, scale * 0.58f, scale * 0.36f),
                material);
        }
    }

    private static void PopulateCountdownRuins(Transform parent, Material material)
    {
        string[] numberPaths =
        {
            Ultimate("Level and Mechanics/FBX/Numbers_3.fbx"),
            Ultimate("Level and Mechanics/FBX/Numbers_2.fbx"),
            Ultimate("Level and Mechanics/FBX/Numbers_1.fbx")
        };

        for (int i = 0; i < 9; i++)
        {
            float x = -48f + i * 24f;
            float y = -1.1f + Mathf.Sin(i * 0.8f) * 0.32f;
            float scale = 2.35f + (i % 3) * 0.22f;
            PlaceModel(
                parent,
                numberPaths[i % numberPaths.Length],
                $"Distant Countdown Number {i:00}",
                new Vector3(x, y, 0f),
                Quaternion.Euler(0f, -8f + i % 3 * 8f, -7f + i % 4 * 4f),
                Vector3.one * scale,
                material);
        }
    }

    private static void PopulateForest(Transform parent, Material treeMaterial, Material rockMaterial)
    {
        string[] treePaths =
        {
            Ultimate("Nature/FBX/Tree.fbx"),
            Ultimate("Nature/FBX/Tree_Fruit.fbx"),
            Kenney("tree.fbx"),
            Kenney("tree-pine.fbx")
        };

        string[] rockPaths =
        {
            Ultimate("Nature/FBX/Rock_1.fbx"),
            Ultimate("Nature/FBX/Rock_2.fbx"),
            Kenney("rocks.fbx")
        };

        for (int i = 0; i < 32; i++)
        {
            float x = -72f + i * 7.4f;
            float y = -5.75f + Mathf.Sin(i * 0.33f) * 0.24f;
            float scale = 1.45f + (i % 5) * 0.16f;
            PlaceModel(
                parent,
                treePaths[i % treePaths.Length],
                $"Background Tree {i:00}",
                new Vector3(x, y, 0f),
                Quaternion.Euler(0f, 180f * (i % 2), 0f),
                Vector3.one * scale,
                treeMaterial);

            if (i % 3 == 0)
            {
                PlaceModel(
                    parent,
                    rockPaths[i % rockPaths.Length],
                    $"Background Rock {i:00}",
                    new Vector3(x + 2.9f, y - 0.35f, -0.08f),
                    Quaternion.Euler(0f, i * 17f, 0f),
                    Vector3.one * (1.2f + (i % 4) * 0.18f),
                    rockMaterial);
            }
        }
    }

    private static void PopulateForeground(Transform parent, Material foliageMaterial, Material rockMaterial)
    {
        string[] foliagePaths =
        {
            Ultimate("Nature/FBX/Bush.fbx"),
            Ultimate("Nature/FBX/Bush_Fruit.fbx"),
            Ultimate("Nature/FBX/Grass_1.fbx"),
            Ultimate("Nature/FBX/Grass_2.fbx"),
            Ultimate("Nature/FBX/Grass_3.fbx"),
            Kenney("grass.fbx")
        };

        string[] rockPaths =
        {
            Ultimate("Nature/FBX/Rock_1.fbx"),
            Ultimate("Nature/FBX/Rock_2.fbx"),
            Kenney("rocks.fbx")
        };

        for (int i = 0; i < 46; i++)
        {
            float x = -66f + i * 5.4f;
            float y = -6.35f + Mathf.Sin(i * 0.52f) * 0.12f;
            float scale = 0.95f + (i % 4) * 0.12f;
            PlaceModel(
                parent,
                foliagePaths[i % foliagePaths.Length],
                $"Near Foliage {i:00}",
                new Vector3(x, y, -0.05f * (i % 4)),
                Quaternion.Euler(0f, 180f * (i % 2), 0f),
                Vector3.one * scale,
                foliageMaterial);

            if (i % 5 == 0)
            {
                PlaceModel(
                    parent,
                    rockPaths[i % rockPaths.Length],
                    $"Near Rock {i:00}",
                    new Vector3(x + 1.6f, y - 0.15f, -0.18f),
                    Quaternion.Euler(0f, i * 13f, 0f),
                    Vector3.one * (0.9f + (i % 3) * 0.15f),
                    rockMaterial);
            }
        }
    }

    private static Transform CreateLayer(
        Transform root,
        string name,
        float horizontalParallax,
        float verticalParallax,
        Vector2 swayAmplitude,
        float swayFrequency,
        float swayPhase,
        float layerDepth)
    {
        GameObject layer = new GameObject(name);
        layer.transform.SetParent(root, false);
        layer.transform.localPosition = new Vector3(0f, 0f, layerDepth);

        ParallaxLayer3D parallax = layer.AddComponent<ParallaxLayer3D>();
        parallax.Configure(
            horizontalParallax,
            verticalParallax,
            swayAmplitude,
            swayFrequency,
            swayPhase);

        return layer.transform;
    }

    private static GameObject PlaceModel(
        Transform parent,
        string assetPath,
        string name,
        Vector3 localPosition,
        Quaternion localRotation,
        Vector3 localScale,
        Material material)
    {
        GameObject source = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
        if (source == null)
        {
            Debug.LogWarning($"Parallax background could not find model: {assetPath}");
            return null;
        }

        GameObject instance = PrefabUtility.InstantiatePrefab(source) as GameObject;
        if (instance == null)
        {
            return null;
        }

        instance.name = name;
        instance.transform.SetParent(parent, false);
        instance.transform.localPosition = localPosition;
        instance.transform.localRotation = localRotation;
        instance.transform.localScale = localScale;
        PrepareVisualOnlyInstance(instance, material);
        return instance;
    }

    private static void CreatePrimitiveVisual(
        Transform parent,
        string name,
        PrimitiveType primitiveType,
        Vector3 localPosition,
        Quaternion localRotation,
        Vector3 localScale,
        Material material)
    {
        GameObject visual = GameObject.CreatePrimitive(primitiveType);
        visual.name = name;
        visual.transform.SetParent(parent, false);
        visual.transform.localPosition = localPosition;
        visual.transform.localRotation = localRotation;
        visual.transform.localScale = localScale;
        PrepareVisualOnlyInstance(visual, material);
    }

    private static void PrepareVisualOnlyInstance(GameObject instance, Material material)
    {
        foreach (Collider collider in instance.GetComponentsInChildren<Collider>(true))
        {
            UnityEngine.Object.DestroyImmediate(collider);
        }

        foreach (Collider2D collider in instance.GetComponentsInChildren<Collider2D>(true))
        {
            UnityEngine.Object.DestroyImmediate(collider);
        }

        foreach (Rigidbody rigidbody in instance.GetComponentsInChildren<Rigidbody>(true))
        {
            UnityEngine.Object.DestroyImmediate(rigidbody);
        }

        foreach (Rigidbody2D rigidbody in instance.GetComponentsInChildren<Rigidbody2D>(true))
        {
            UnityEngine.Object.DestroyImmediate(rigidbody);
        }

        foreach (Renderer renderer in instance.GetComponentsInChildren<Renderer>(true))
        {
            if (material != null)
            {
                renderer.sharedMaterial = material;
            }

            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            renderer.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
        }
    }

    private static void RemoveExistingBackgrounds()
    {
        GameObject[] allObjects = UnityEngine.Object.FindObjectsByType<GameObject>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);
        List<GameObject> backgroundsToRemove = new List<GameObject>();

        foreach (GameObject sceneObject in allObjects)
        {
            if (sceneObject == null)
            {
                continue;
            }

            try
            {
                if (sceneObject.name == BackgroundName)
                {
                    backgroundsToRemove.Add(sceneObject);
                }
            }
            catch (MissingReferenceException)
            {
                // Scene/prefab operations can leave destroyed wrappers in the
                // editor object list for one frame. Ignore those stale entries.
            }
        }

        foreach (GameObject background in backgroundsToRemove)
        {
            if (background != null)
            {
                UnityEngine.Object.DestroyImmediate(background);
            }
        }
    }

    private static void PolishCameraForBackground()
    {
        Camera mainCamera = Camera.main;
        if (mainCamera == null)
        {
            return;
        }

        mainCamera.farClipPlane = Mathf.Max(mainCamera.farClipPlane, 90f);
        mainCamera.backgroundColor = new Color(0.73f, 0.73f, 0.73f);
    }

    private static Material GetMaterial(string materialName, Color color)
    {
        if (Materials.TryGetValue(materialName, out Material cachedMaterial))
        {
            return cachedMaterial;
        }

        string materialPath = $"{MaterialFolder}/{materialName}.mat";
        Material material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
        if (material == null)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit")
                ?? Shader.Find("Universal Render Pipeline/Lit")
                ?? Shader.Find("Standard");
            material = new Material(shader);
            AssetDatabase.CreateAsset(material, materialPath);
        }

        SetMaterialColor(material, color);

        if (material.HasProperty("_Smoothness"))
        {
            material.SetFloat("_Smoothness", 0.18f);
        }

        EditorUtility.SetDirty(material);
        Materials[materialName] = material;
        return material;
    }

    private static void SetMaterialColor(Material material, Color color)
    {
        material.color = color;

        if (material.HasProperty("_BaseColor"))
        {
            material.SetColor("_BaseColor", color);
        }

        if (material.HasProperty("_Color"))
        {
            material.SetColor("_Color", color);
        }
    }

    private static void EnsureProjectFolders()
    {
        EnsureFolder("Assets/Prefabs");
        EnsureFolder(PrefabFolder);
        EnsureFolder("Assets/Materials");
        EnsureFolder(MaterialFolder);
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

    private static string Kenney(string fileName)
    {
        return $"{KenneyRoot}/{fileName}";
    }

    private static string Ultimate(string relativePath)
    {
        return $"{UltimateRoot}/{relativePath}";
    }
}
