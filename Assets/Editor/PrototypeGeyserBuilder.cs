using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Rebuilds the switch-activated geyser test at the far right of the movement prototype.
/// </summary>
public static class PrototypeGeyserBuilder
{
    private const string ScenePath = "Assets/Scenes/PrototypeMovement.unity";
    private const string SectionName = "Prototype Geyser Section";

    [MenuItem("Countdown Hero/Build Prototype Geyser")]
    public static void Build()
    {
        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        GameObject existing = GameObject.Find(SectionName);
        if (existing != null)
        {
            Object.DestroyImmediate(existing);
        }

        Material stone = AssetDatabase.LoadAssetAtPath<Material>(
            "Assets/PrototypeMaterials/Prototype Stone.mat");
        Material moon = AssetDatabase.LoadAssetAtPath<Material>(
            "Assets/PrototypeMaterials/Prototype Moon.mat");

        GameObject root = new GameObject(SectionName);
        CreatePlatform(root.transform, "Geyser Launch Platform",
            new Vector3(40.7f, -2.5f, 0f), new Vector3(3.2f, 0.7f, 1.6f), stone);
        CreatePlatform(root.transform, "Geyser Landing Platform",
            new Vector3(47.2f, 1f, 0f), new Vector3(4f, 0.7f, 1.6f), stone);

        TimedGeyser geyser = CreateGeyser(root.transform, moon);
        CreateSwitch(root.transform, geyser, moon);
        CreateLabel(root.transform,
            "GEYSER\nStep on the switch, then ride the blast!",
            new Vector3(44.2f, 4.25f, -0.7f), 0.075f,
            new Color(0.88f, 0.94f, 1f));

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene, ScenePath);
        Selection.activeGameObject = root;
        Debug.Log("Added the switch-activated geyser prototype section.");
    }

    private static TimedGeyser CreateGeyser(Transform parent, Material material)
    {
        GameObject geyserObject = new GameObject("Countdown Geyser");
        geyserObject.transform.SetParent(parent);
        geyserObject.transform.position = new Vector3(41.7f, -1.85f, 0f);
        geyserObject.transform.rotation = Quaternion.Euler(0f, 0f, -42f);

        BoxCollider2D forceZone = geyserObject.AddComponent<BoxCollider2D>();
        forceZone.isTrigger = true;
        forceZone.size = new Vector2(1.25f, 6f);
        forceZone.offset = new Vector2(0f, 2.75f);

        GameObject baseVisual = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        baseVisual.name = "Geyser Vent";
        baseVisual.transform.SetParent(geyserObject.transform, false);
        baseVisual.transform.localScale = new Vector3(0.85f, 0.2f, 0.85f);
        Object.DestroyImmediate(baseVisual.GetComponent<Collider>());
        baseVisual.GetComponent<MeshRenderer>().sharedMaterial = material;

        GameObject blastVisual = GameObject.CreatePrimitive(PrimitiveType.Cube);
        blastVisual.name = "Geyser Blast";
        blastVisual.transform.SetParent(geyserObject.transform, false);
        blastVisual.transform.localPosition = new Vector3(0f, 2.75f, 0.15f);
        blastVisual.transform.localScale = new Vector3(0.8f, 5.5f, 0.45f);
        Object.DestroyImmediate(blastVisual.GetComponent<Collider>());
        blastVisual.GetComponent<MeshRenderer>().sharedMaterial = material;
        blastVisual.SetActive(false);

        TextMesh countdown = CreateLabel(geyserObject.transform, string.Empty,
            new Vector3(41.1f, -0.5f, -0.7f), 0.18f,
            new Color(1f, 0.9f, 0.25f));

        TimedGeyser geyser = geyserObject.AddComponent<TimedGeyser>();
        geyser.Initialize(1.5f, 4f, 55f, 18f, blastVisual, countdown);
        return geyser;
    }

    private static void CreateSwitch(Transform parent, TimedGeyser geyser, Material material)
    {
        GameObject switchObject = new GameObject("Geyser Switch");
        switchObject.transform.SetParent(parent);
        switchObject.transform.position = new Vector3(39.8f, -1.75f, 0f);

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
            new SwitchTarget[] { geyser },
            0.5f);
    }

    private static GameObject CreatePlatform(
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

    private static TextMesh CreateLabel(
        Transform parent,
        string text,
        Vector3 position,
        float characterSize,
        Color color)
    {
        GameObject labelObject = new GameObject(
            string.IsNullOrEmpty(text) ? "Geyser Countdown Display" : "Geyser Sign");
        labelObject.transform.SetParent(parent);
        labelObject.transform.position = position;

        TextMesh label = labelObject.AddComponent<TextMesh>();
        label.text = text;
        label.anchor = TextAnchor.MiddleCenter;
        label.alignment = TextAlignment.Center;
        label.characterSize = characterSize;
        label.fontSize = 64;
        label.color = color;
        labelObject.GetComponent<MeshRenderer>().sortingOrder = 30;
        return label;
    }
}
