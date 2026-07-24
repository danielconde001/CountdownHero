using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Rebuilds the timed-platform test section in the movement prototype.
/// Keeping this as a menu command makes the generated scene content reproducible.
/// </summary>
public static class PrototypeTimedPlatformBuilder
{
    private const string ScenePath = "Assets/Scenes/PrototypeMovement.unity";

    [MenuItem("Countdown Hero/Build Prototype Timed Platforms")]
    public static void Build()
    {
        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        GameObject existing = GameObject.Find("Prototype Timed Platform Section");
        if (existing != null)
        {
            Object.DestroyImmediate(existing);
        }

        GameObject root = new GameObject("Prototype Timed Platform Section");
        Material stone = AssetDatabase.LoadAssetAtPath<Material>("Assets/PrototypeMaterials/Prototype Stone.mat");
        Material moon = AssetDatabase.LoadAssetAtPath<Material>("Assets/PrototypeMaterials/Prototype Moon.mat");

        CreateStonePlatform(
            root.transform,
            "Timed Section Start",
            new Vector3(29.8f, -2.5f, 0f),
            new Vector3(2.8f, 0.7f, 1.6f),
            stone);
        TimedPlatform timedPlatform = CreateTimedPlatform(root.transform, stone);
        CreateStonePlatform(
            root.transform,
            "Timed Section Landing",
            new Vector3(37.6f, -2.5f, 0f),
            new Vector3(3.2f, 0.7f, 1.6f),
            stone);
        CreateSwitch(root.transform, timedPlatform, moon);
        CreateLabel(
            root.transform,
            "TIMED PLATFORM\nStep on the switch, wait for GO!",
            new Vector3(33.8f, 0.15f, -0.7f),
            0.08f);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene, ScenePath);
        Selection.activeGameObject = root;
        Debug.Log("Added the timed platform prototype section.");
    }

    private static TimedPlatform CreateTimedPlatform(Transform parent, Material material)
    {
        GameObject platform = CreateStonePlatform(
            parent,
            "Countdown Platform",
            new Vector3(33.7f, -2.35f, 0f),
            new Vector3(2.9f, 0.55f, 1.5f),
            material);

        TextMesh countdown = CreateLabel(
            platform.transform,
            string.Empty,
            new Vector3(33.7f, -0.95f, -0.6f),
            0.18f);
        countdown.color = new Color(1f, 0.9f, 0.25f);

        TimedPlatform timedPlatform = platform.AddComponent<TimedPlatform>();
        timedPlatform.Initialize(
            1.5f,
            4f,
            false,
            TimedPlatform.BehaviorMode.ToggleVisibility,
            countdown);
        return timedPlatform;
    }

    private static void CreateSwitch(Transform parent, TimedPlatform timedPlatform, Material material)
    {
        GameObject switchObject = new GameObject("Countdown Switch");
        switchObject.transform.SetParent(parent);
        switchObject.transform.position = new Vector3(30.8f, -1.75f, 0f);

        BoxCollider2D collider = switchObject.AddComponent<BoxCollider2D>();
        collider.size = new Vector2(0.9f, 1.2f);
        collider.isTrigger = true;

        GameObject visual = GameObject.CreatePrimitive(PrimitiveType.Cube);
        visual.name = "Switch Body";
        visual.transform.SetParent(switchObject.transform, false);
        visual.transform.localScale = new Vector3(0.65f, 0.25f, 1.2f);
        Object.DestroyImmediate(visual.GetComponent<Collider>());
        visual.GetComponent<MeshRenderer>().sharedMaterial = material;

        TimedSwitch timedSwitch = switchObject.AddComponent<TimedSwitch>();
        timedSwitch.Initialize(
            TimedSwitch.ActivationMode.OnTriggerEnter,
            new SwitchTarget[] { timedPlatform },
            0.5f);
    }

    private static GameObject CreateStonePlatform(
        Transform parent,
        string name,
        Vector3 position,
        Vector3 scale,
        Material material)
    {
        GameObject platform = new GameObject(name);
        platform.transform.SetParent(parent);
        platform.transform.position = position;
        platform.transform.localScale = new Vector3(scale.x, scale.y, 1f);
        platform.AddComponent<BoxCollider2D>();

        GameObject visual = GameObject.CreatePrimitive(PrimitiveType.Cube);
        visual.name = "Stone Body";
        visual.transform.SetParent(platform.transform, false);
        visual.transform.localScale = new Vector3(1f, 1f, scale.z);
        Object.DestroyImmediate(visual.GetComponent<Collider>());
        visual.GetComponent<MeshRenderer>().sharedMaterial = material;
        return platform;
    }

    private static TextMesh CreateLabel(Transform parent, string text, Vector3 position, float characterSize)
    {
        string objectName = string.IsNullOrEmpty(text)
            ? "Countdown Display"
            : "Timed Platform Sign";
        GameObject labelObject = new GameObject(objectName);
        labelObject.transform.SetParent(parent);
        labelObject.transform.position = position;

        TextMesh label = labelObject.AddComponent<TextMesh>();
        label.text = text;
        label.anchor = TextAnchor.MiddleCenter;
        label.alignment = TextAlignment.Center;
        label.characterSize = characterSize;
        label.fontSize = 64;
        label.color = new Color(0.88f, 0.94f, 1f);

        MeshRenderer renderer = labelObject.GetComponent<MeshRenderer>();
        renderer.sortingOrder = 30;
        return label;
    }
}
