using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
public static class PrototypeVisualPolisher
{
    private const string RequestPath = "Assets/Editor/PolishPrototype.request";
    private const string ScenePath = "Assets/Scenes/PrototypeMovement.unity";
    private static bool waitingForEditMode;

    static PrototypeVisualPolisher()
    {
        if (!File.Exists(RequestPath))
        {
            return;
        }

        waitingForEditMode = true;
        EditorApplication.update += WaitAndPolish;
        if (EditorApplication.isPlaying)
        {
            EditorApplication.isPlaying = false;
        }
    }

    private static void WaitAndPolish()
    {
        if (!waitingForEditMode || EditorApplication.isPlayingOrWillChangePlaymode)
        {
            return;
        }

        waitingForEditMode = false;
        EditorApplication.update -= WaitAndPolish;
        File.Delete(RequestPath);
        Polish();
    }

    [MenuItem("Countdown Hero/Polish Movement Prototype")]
    public static void Polish()
    {
        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        Material stone = GetMaterial("Prototype Stone", new Color(0.18f, 0.28f, 0.38f), 0.15f);
        Material backdrop = GetMaterial("Prototype Backdrop", new Color(0.035f, 0.055f, 0.12f), 0f);
        Material silhouette = GetMaterial("Prototype Silhouette", new Color(0.075f, 0.12f, 0.20f), 0f);
        Material moon = GetMaterial("Prototype Moon", new Color(0.72f, 0.82f, 1f), 0.2f);

        GameObject environment = new GameObject("3D Environment");
        CreateCube(environment.transform, "Backdrop", new Vector3(3f, 1f, 10f), new Vector3(38f, 18f, 0.5f), backdrop);
        CreateSphere(environment.transform, "Moon", new Vector3(-4f, 4.8f, 7f), new Vector3(3f, 3f, 0.5f), moon);

        for (int i = 0; i < 9; i++)
        {
            float x = -12f + i * 4.2f;
            float height = 2.2f + (i % 3) * 0.8f;
            CreateCube(environment.transform, $"Tree Trunk {i}", new Vector3(x, -2.8f, 5f + i % 2), new Vector3(0.45f, height, 0.7f), silhouette);
            CreateSphere(environment.transform, $"Tree Crown {i}", new Vector3(x, -1.2f + height * 0.5f, 5f + i % 2), new Vector3(2.2f, 3.2f, 0.8f), silhouette);
        }

        string[] platformNames =
        {
            "Safety Floor", "Start Platform", "Coyote Gap Landing", "Short Hop Step",
            "Full Jump Ledge", "Air Control Platform", "Final Runway"
        };

        foreach (string platformName in platformNames)
        {
            GameObject platform = GameObject.Find(platformName);
            if (platform == null)
            {
                continue;
            }

            SpriteRenderer oldRenderer = platform.GetComponent<SpriteRenderer>();
            if (oldRenderer != null)
            {
                Object.DestroyImmediate(oldRenderer);
            }

            CreateCube(platform.transform, "Stone Body", Vector3.zero, new Vector3(1f, 1f, 1.8f), stone);
        }

        PolishPlayer();
        PolishCamera();
        PolishSigns();
        CreateLighting();

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene, ScenePath);
        Selection.activeGameObject = GameObject.Find("Player");
        Debug.Log("Applied the 2.5D visual polish pass to the movement prototype.");
    }

    private static void PolishPlayer()
    {
        GameObject player = GameObject.Find("Player");
        if (player == null)
        {
            return;
        }

        SpriteRenderer oldRenderer = player.GetComponent<SpriteRenderer>();
        if (oldRenderer != null)
        {
            Object.DestroyImmediate(oldRenderer);
        }

        GameObject paper = new GameObject("Paper Character");
        paper.transform.SetParent(player.transform, false);
        paper.transform.localPosition = new Vector3(0f, 0.12f, -0.15f);
        paper.transform.localScale = new Vector3(1.05f, 1.45f, 1f);
        SpriteRenderer renderer = paper.AddComponent<SpriteRenderer>();
        renderer.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
        renderer.color = new Color(1f, 0.78f, 0.18f);
        renderer.sortingOrder = 20;

    }

    private static void PolishCamera()
    {
        GameObject cameraObject = GameObject.Find("Main Camera");
        GameObject player = GameObject.Find("Player");
        if (cameraObject == null || player == null)
        {
            return;
        }

        Camera camera = cameraObject.GetComponent<Camera>();
        camera.orthographic = false;
        camera.fieldOfView = 43f;
        camera.nearClipPlane = 0.1f;
        camera.farClipPlane = 50f;
        camera.backgroundColor = new Color(0.02f, 0.03f, 0.07f);
        cameraObject.transform.position = new Vector3(-6.5f, 2.3f, -15f);
        cameraObject.transform.rotation = Quaternion.Euler(2.5f, 0f, 0f);

        PrototypeCameraFollow follow = cameraObject.GetComponent<PrototypeCameraFollow>();
        if (follow == null)
        {
            follow = cameraObject.AddComponent<PrototypeCameraFollow>();
        }
        follow.Initialize(player.transform);
    }

    private static void PolishSigns()
    {
        foreach (TextMesh label in Object.FindObjectsByType<TextMesh>(FindObjectsSortMode.None))
        {
            label.characterSize = 0.075f;
            label.fontSize = 64;
            label.color = new Color(0.88f, 0.94f, 1f);
            label.transform.position += new Vector3(0f, 0.35f, -0.35f);
        }
    }

    private static void CreateLighting()
    {
        GameObject lightObject = new GameObject("Moonlight");
        Light light = lightObject.AddComponent<Light>();
        light.type = LightType.Directional;
        light.color = new Color(0.62f, 0.72f, 1f);
        light.intensity = 1.35f;
        light.shadows = LightShadows.Soft;
        lightObject.transform.rotation = Quaternion.Euler(35f, -35f, 0f);

        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = new Color(0.12f, 0.16f, 0.3f);
        RenderSettings.ambientEquatorColor = new Color(0.07f, 0.1f, 0.18f);
        RenderSettings.ambientGroundColor = new Color(0.025f, 0.035f, 0.06f);
    }

    private static Material GetMaterial(string name, Color color, float metallic)
    {
        const string folder = "Assets/PrototypeMaterials";
        if (!AssetDatabase.IsValidFolder(folder))
        {
            AssetDatabase.CreateFolder("Assets", "PrototypeMaterials");
        }

        string path = $"{folder}/{name}.mat";
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            material = new Material(shader);
            AssetDatabase.CreateAsset(material, path);
        }

        material.color = color;
        material.SetFloat("_Metallic", metallic);
        material.SetFloat("_Smoothness", 0.25f);
        EditorUtility.SetDirty(material);
        return material;
    }

    private static void CreateCube(Transform parent, string name, Vector3 position, Vector3 scale, Material material)
    {
        GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
        cube.name = name;
        cube.transform.SetParent(parent, false);
        cube.transform.localPosition = position;
        cube.transform.localScale = scale;
        Object.DestroyImmediate(cube.GetComponent<Collider>());
        cube.GetComponent<MeshRenderer>().sharedMaterial = material;
    }

    private static void CreateSphere(Transform parent, string name, Vector3 position, Vector3 scale, Material material)
    {
        GameObject sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        sphere.name = name;
        sphere.transform.SetParent(parent, false);
        sphere.transform.localPosition = position;
        sphere.transform.localScale = scale;
        Object.DestroyImmediate(sphere.GetComponent<Collider>());
        sphere.GetComponent<MeshRenderer>().sharedMaterial = material;
    }
}
