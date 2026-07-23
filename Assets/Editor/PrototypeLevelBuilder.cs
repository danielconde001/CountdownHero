using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
public static class PrototypeLevelBuilder
{
    private const string BuildRequestPath = "Assets/Editor/BuildPrototypeLevel.request";
    private const string ScenePath = "Assets/Scenes/PrototypeMovement.unity";

    static PrototypeLevelBuilder()
    {
        if (System.IO.File.Exists(BuildRequestPath))
        {
            EditorApplication.delayCall += BuildRequestedScene;
        }

    }

    [MenuItem("Countdown Hero/Build Movement Prototype")]
    public static void BuildPrototypeScene()
    {
        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        scene.name = "PrototypeMovement";

        CreateCamera();
        CreatePlayer();

        CreatePlatform("Safety Floor", new Vector2(7f, -5.5f), new Vector2(32f, 1f), new Color(0.12f, 0.15f, 0.22f));
        CreatePlatform("Start Platform", new Vector2(-7f, -2.5f), new Vector2(7f, 1f), new Color(0.25f, 0.65f, 0.78f));
        CreatePlatform("Coyote Gap Landing", new Vector2(-1.5f, -2.5f), new Vector2(2.5f, 1f), new Color(0.34f, 0.76f, 0.55f));
        CreatePlatform("Short Hop Step", new Vector2(1.5f, -1.4f), new Vector2(2f, 0.6f), new Color(0.95f, 0.68f, 0.25f));
        CreatePlatform("Full Jump Ledge", new Vector2(4.6f, 0.1f), new Vector2(3f, 0.7f), new Color(0.86f, 0.39f, 0.48f));
        CreatePlatform("Air Control Platform", new Vector2(8.7f, -1.2f), new Vector2(2.2f, 0.7f), new Color(0.55f, 0.46f, 0.88f));
        CreatePlatform("Final Runway", new Vector2(13f, -2.5f), new Vector2(6f, 1f), new Color(0.25f, 0.65f, 0.78f));

        CreateSign("MOVE: A/D or Left/Right", new Vector2(-8.5f, -0.7f));
        CreateSign("JUMP: Space\nTap = short / Hold = high", new Vector2(-4.7f, 0.1f));
        CreateSign("COYOTE GAP", new Vector2(-1.5f, -0.8f));
        CreateSign("SHORT HOP", new Vector2(1.5f, 0f));
        CreateSign("FULL JUMP", new Vector2(4.6f, 1.5f));
        CreateSign("AIR CONTROL", new Vector2(8.7f, 0.1f));

        EditorSceneManager.SaveScene(scene, ScenePath);
        AddSceneToBuildSettings();
        Selection.activeGameObject = GameObject.Find("Player");
        Debug.Log($"Movement prototype created at {ScenePath}");
    }

    private static void BuildRequestedScene()
    {
        System.IO.File.Delete(BuildRequestPath);
        AssetDatabase.Refresh();
        BuildPrototypeScene();
    }

    private static void CreateCamera()
    {
        GameObject cameraObject = new GameObject("Main Camera");
        Camera camera = cameraObject.AddComponent<Camera>();
        cameraObject.tag = "MainCamera";
        camera.orthographic = true;
        camera.orthographicSize = 6.2f;
        camera.backgroundColor = new Color(0.035f, 0.045f, 0.075f);
        camera.clearFlags = CameraClearFlags.SolidColor;
        cameraObject.transform.position = new Vector3(2.5f, 0f, -10f);
    }

    private static void CreatePlayer()
    {
        GameObject player = new GameObject("Player");
        player.transform.position = new Vector3(-8f, -1.25f, 0f);

        SpriteRenderer renderer = player.AddComponent<SpriteRenderer>();
        renderer.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
        renderer.color = new Color(1f, 0.92f, 0.35f);
        renderer.sortingOrder = 10;

        Rigidbody2D body = player.AddComponent<Rigidbody2D>();
        body.freezeRotation = true;
        body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        body.interpolation = RigidbodyInterpolation2D.Interpolate;

        BoxCollider2D collider = player.AddComponent<BoxCollider2D>();
        collider.size = new Vector2(0.85f, 1.35f);

        PlayerInput playerInput = player.AddComponent<PlayerInput>();
        playerInput.actions = AssetDatabase.LoadAssetAtPath<InputActionAsset>("Assets/InputSystem_Actions.inputactions");
        playerInput.defaultActionMap = "Player";

        player.AddComponent<PlayerController2D>();
    }

    private static void CreatePlatform(string name, Vector2 position, Vector2 size, Color color)
    {
        GameObject platform = new GameObject(name);
        platform.transform.position = position;
        platform.transform.localScale = size;

        SpriteRenderer renderer = platform.AddComponent<SpriteRenderer>();
        renderer.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
        renderer.color = color;

        BoxCollider2D collider = platform.AddComponent<BoxCollider2D>();
        collider.size = Vector2.one;
    }

    private static void CreateSign(string text, Vector2 position)
    {
        GameObject sign = new GameObject($"Sign - {text.Replace("\n", " ")}");
        sign.transform.position = position;

        TextMesh label = sign.AddComponent<TextMesh>();
        label.text = text;
        label.anchor = TextAnchor.MiddleCenter;
        label.alignment = TextAlignment.Center;
        label.characterSize = 0.16f;
        label.fontSize = 42;
        label.color = new Color(0.9f, 0.93f, 1f);

        MeshRenderer renderer = sign.GetComponent<MeshRenderer>();
        renderer.sortingOrder = 20;
    }

    private static void AddSceneToBuildSettings()
    {
        foreach (EditorBuildSettingsScene entry in EditorBuildSettings.scenes)
        {
            if (entry.path == ScenePath)
            {
                return;
            }
        }

        var currentScenes = new System.Collections.Generic.List<EditorBuildSettingsScene>(EditorBuildSettings.scenes)
        {
            new EditorBuildSettingsScene(ScenePath, true)
        };
        EditorBuildSettings.scenes = currentScenes.ToArray();
    }
}
