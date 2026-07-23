using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
public static class PrototypeEncounterBuilder
{
    private const string RequestPath = "Assets/Editor/BuildPrototypeEncounter.request";
    private const string ScenePath = "Assets/Scenes/PrototypeMovement.unity";
    private const string BattleDataRoot = "Assets/BattleData";
    private const string ProfilesPath = BattleDataRoot + "/Profiles";
    private const string AttacksPath = BattleDataRoot + "/Combatant Attacks";
    private const string PatternsPath = BattleDataRoot + "/Countdown Patterns";

    static PrototypeEncounterBuilder()
    {
        if (File.Exists(RequestPath))
        {
            EditorApplication.delayCall += Build;
        }
    }

    [MenuItem("Countdown Hero/Build Prototype Encounter")]
    public static void Build()
    {
        File.Delete(RequestPath);
        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        GameObject existing = GameObject.Find("Prototype Combat Encounter");
        if (existing != null)
        {
            Object.DestroyImmediate(existing);
        }

        GameObject root = new GameObject("Prototype Combat Encounter");
        Material stone = AssetDatabase.LoadAssetAtPath<Material>(
            "Assets/PrototypeMaterials/Prototype Stone.mat");

        CreateEncounterGround(root.transform, stone);
        Transform playerPoint = CreateMarker(root.transform, "Player Battle Position", new Vector3(19f, -1.3f, 0f));
        Transform enemyPoint = CreateMarker(root.transform, "Enemy Battle Position", new Vector3(23f, -1.2f, 0f));
        Transform exitPoint = CreateMarker(root.transform, "Player Exit Position", new Vector3(24.8f, -1.3f, 0f));
        Transform enemy = CreateEnemy(root.transform, new Vector3(26f, -1.2f, 0f));
        TextMesh label = CreateLabel(root.transform);
        TextMesh healthLabel = CreateHealthLabel(root.transform);

        GameObject encounterObject = new GameObject("Encounter Controller");
        encounterObject.transform.SetParent(root.transform);
        CombatEncounter encounter = encounterObject.AddComponent<CombatEncounter>();
        CountdownSequenceRunner countdown = encounterObject.AddComponent<CountdownSequenceRunner>();
        PlayerBattleProfile playerProfile = GetOrCreatePlayerProfile();
        EnemyBattleProfile enemyProfile = GetOrCreateEnemyProfile();

        PrototypeCameraFollow cameraFollow = Object.FindFirstObjectByType<PrototypeCameraFollow>();
        PlayerController2D player = Object.FindFirstObjectByType<PlayerController2D>();
        BattleCombatant playerStats = player.GetComponent<BattleCombatant>();
        if (playerStats == null)
        {
            playerStats = player.gameObject.AddComponent<BattleCombatant>();
        }
        playerStats.Configure(6);

        BattleCombatant enemyStats = enemy.gameObject.AddComponent<BattleCombatant>();
        enemyStats.Configure(6);

        encounter.Initialize(
            cameraFollow,
            countdown,
            playerProfile,
            enemyProfile,
            playerStats,
            enemyStats,
            enemy,
            playerPoint,
            enemyPoint,
            exitPoint,
            label,
            healthLabel);

        GameObject triggerObject = new GameObject("Encounter Trigger");
        triggerObject.transform.SetParent(root.transform);
        triggerObject.transform.position = new Vector3(17.25f, 0f, 0f);
        BoxCollider2D triggerCollider = triggerObject.AddComponent<BoxCollider2D>();
        triggerCollider.size = new Vector2(1.5f, 5f);
        triggerCollider.isTrigger = true;
        triggerObject.AddComponent<CombatEncounterTrigger>().Initialize(encounter);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene, ScenePath);
        Selection.activeGameObject = root;
        Debug.Log("Added the platforming-to-combat encounter prototype.");
    }

    private static void CreateEncounterGround(Transform parent, Material material)
    {
        GameObject ground = new GameObject("Encounter Runway");
        ground.transform.SetParent(parent);
        ground.transform.position = new Vector3(21f, -2.5f, 0f);
        ground.transform.localScale = new Vector3(10f, 1f, 1f);
        ground.AddComponent<BoxCollider2D>();

        GameObject visual = GameObject.CreatePrimitive(PrimitiveType.Cube);
        visual.name = "Stone Body";
        visual.transform.SetParent(ground.transform, false);
        visual.transform.localScale = new Vector3(1f, 1f, 1.8f);
        Object.DestroyImmediate(visual.GetComponent<Collider>());
        visual.GetComponent<MeshRenderer>().sharedMaterial = material;
    }

    private static Transform CreateMarker(Transform parent, string name, Vector3 position)
    {
        GameObject marker = new GameObject(name);
        marker.transform.SetParent(parent);
        marker.transform.position = position;
        return marker.transform;
    }

    private static Transform CreateEnemy(Transform parent, Vector3 position)
    {
        GameObject enemy = GameObject.CreatePrimitive(PrimitiveType.Quad);
        enemy.name = "Placeholder Enemy";
        enemy.transform.SetParent(parent);
        enemy.transform.position = position;
        enemy.transform.localScale = new Vector3(1.25f, 1.55f, 1f);
        Object.DestroyImmediate(enemy.GetComponent<Collider>());

        Material material = GetEnemyMaterial();
        enemy.GetComponent<MeshRenderer>().sharedMaterial = material;

        CreateEye(enemy.transform, "Left Eye", new Vector3(-0.18f, 0.15f, -0.02f));
        CreateEye(enemy.transform, "Right Eye", new Vector3(0.18f, 0.15f, -0.02f));
        return enemy.transform;
    }

    private static void CreateEye(Transform parent, string name, Vector3 position)
    {
        GameObject eye = GameObject.CreatePrimitive(PrimitiveType.Quad);
        eye.name = name;
        eye.transform.SetParent(parent, false);
        eye.transform.localPosition = position;
        eye.transform.localScale = new Vector3(0.11f, 0.18f, 1f);
        Object.DestroyImmediate(eye.GetComponent<Collider>());
        eye.GetComponent<MeshRenderer>().sharedMaterial =
            AssetDatabase.LoadAssetAtPath<Material>("Assets/PrototypeMaterials/Paper Eyes.mat");
    }

    private static TextMesh CreateLabel(Transform parent)
    {
        GameObject labelObject = new GameObject("Encounter Label");
        labelObject.transform.SetParent(parent);
        labelObject.transform.position = new Vector3(21f, 2.2f, -0.5f);

        TextMesh label = labelObject.AddComponent<TextMesh>();
        label.anchor = TextAnchor.MiddleCenter;
        label.alignment = TextAlignment.Center;
        label.characterSize = 0.1f;
        label.fontSize = 64;
        label.color = new Color(1f, 0.9f, 0.3f);
        labelObject.SetActive(false);
        return label;
    }

    private static TextMesh CreateHealthLabel(Transform parent)
    {
        GameObject labelObject = new GameObject("Battle Health");
        labelObject.transform.SetParent(parent);
        labelObject.transform.position = new Vector3(17.2f, 2.6f, -0.5f);

        TextMesh label = labelObject.AddComponent<TextMesh>();
        label.anchor = TextAnchor.UpperLeft;
        label.alignment = TextAlignment.Left;
        label.characterSize = 0.065f;
        label.fontSize = 54;
        label.color = new Color(0.85f, 0.95f, 1f);
        labelObject.SetActive(false);
        return label;
    }

    private static Material GetEnemyMaterial()
    {
        const string path = "Assets/PrototypeMaterials/Paper Enemy.mat";
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material != null)
        {
            return material;
        }

        Shader shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color");
        material = new Material(shader);
        Color color = new Color(0.82f, 0.18f, 0.28f);
        material.color = color;
        if (material.HasProperty("_BaseColor"))
        {
            material.SetColor("_BaseColor", color);
        }
        AssetDatabase.CreateAsset(material, path);
        return material;
    }

    private static EnemyBattleProfile GetOrCreateEnemyProfile()
    {
        EnsureBattleDataFolders();

        CountdownPattern enemyAttack = GetOrCreatePattern(
            $"{PatternsPath}/Training Enemy - Enemy Attack.asset",
            new[]
            {
                new CountdownBeat("3", 0.7f),
                new CountdownBeat("2", 0.5f),
                new CountdownBeat("1", 0.65f)
            });

        CountdownPattern quickFollowup = GetOrCreatePattern(
            $"{PatternsPath}/Training Enemy - Quick Followup.asset",
            new[]
            {
                new CountdownBeat("3", 0.4f),
                new CountdownBeat("2", 0.35f),
                new CountdownBeat("1", 0.3f)
            });

        CombatantAttack basicStrike = GetOrCreateAttack(
            $"{AttacksPath}/Training Enemy - Basic Strike.asset",
            "BASIC STRIKE",
            3f,
            new[]
            {
                new CombatantAttackStrike(enemyAttack, 2, 0f)
            });

        CombatantAttack tripleStrike = GetOrCreateAttack(
            $"{AttacksPath}/Training Enemy - Triple Trouble.asset",
            "TRIPLE TROUBLE",
            1f,
            new[]
            {
                new CombatantAttackStrike(enemyAttack, 1, 0.15f),
                new CombatantAttackStrike(quickFollowup, 1, 0.15f),
                new CombatantAttackStrike(enemyAttack, 1, 0f)
            });

        const string profilePath =
            ProfilesPath + "/Training Enemy Battle Profile.asset";
        EnemyBattleProfile profile = AssetDatabase.LoadAssetAtPath<EnemyBattleProfile>(profilePath);
        if (profile == null)
        {
            profile = ScriptableObject.CreateInstance<EnemyBattleProfile>();
            AssetDatabase.CreateAsset(profile, profilePath);
        }

        profile.Configure(
            "Training Enemy",
            6,
            new[] { basicStrike, tripleStrike });
        EditorUtility.SetDirty(profile);
        return profile;
    }

    private static PlayerBattleProfile GetOrCreatePlayerProfile()
    {
        EnsureBattleDataFolders();

        CountdownPattern attackPattern = GetOrCreatePattern(
            $"{PatternsPath}/Player - Attack Countdown.asset",
            new[]
            {
                new CountdownBeat("3", 0.6f),
                new CountdownBeat("2", 0.6f),
                new CountdownBeat("1", 0.6f)
            });

        CountdownPattern quickAttackPattern = GetOrCreatePattern(
            $"{PatternsPath}/Player - Quick Followup.asset",
            new[]
            {
                new CountdownBeat("3", 0.4f),
                new CountdownBeat("2", 0.35f),
                new CountdownBeat("1", 0.3f)
            });

        CombatantAttack playerAttack = GetOrCreateAttack(
            $"{AttacksPath}/Player - Triple Attack.asset",
            "TRIPLE ATTACK",
            1f,
            new[]
            {
                new CombatantAttackStrike(attackPattern, 1, 0.15f),
                new CombatantAttackStrike(quickAttackPattern, 1, 0.15f),
                new CombatantAttackStrike(attackPattern, 1, 0f)
            });

        CountdownPattern healPattern = GetOrCreatePattern(
            $"{PatternsPath}/Player - Heal.asset",
            new[]
            {
                new CountdownBeat("3", 0.65f),
                new CountdownBeat("2", 0.65f),
                new CountdownBeat("1", 0.65f)
            });

        const string profilePath = ProfilesPath + "/Player Battle Profile.asset";
        PlayerBattleProfile profile = AssetDatabase.LoadAssetAtPath<PlayerBattleProfile>(profilePath);
        if (profile == null)
        {
            profile = ScriptableObject.CreateInstance<PlayerBattleProfile>();
            AssetDatabase.CreateAsset(profile, profilePath);
        }

        profile.Configure(playerAttack, healPattern, 3, 2);
        EditorUtility.SetDirty(profile);
        return profile;
    }

    private static void EnsureBattleDataFolders()
    {
        if (!AssetDatabase.IsValidFolder(BattleDataRoot))
        {
            AssetDatabase.CreateFolder("Assets", "BattleData");
        }

        EnsureFolder(BattleDataRoot, "Profiles");
        EnsureFolder(BattleDataRoot, "Combatant Attacks");
        EnsureFolder(BattleDataRoot, "Countdown Patterns");
    }

    private static void EnsureFolder(string parent, string name)
    {
        string path = $"{parent}/{name}";
        if (!AssetDatabase.IsValidFolder(path))
        {
            AssetDatabase.CreateFolder(parent, name);
        }
    }

    private static CountdownPattern GetOrCreatePattern(
        string path,
        CountdownBeat[] beats)
    {
        CountdownPattern pattern = AssetDatabase.LoadAssetAtPath<CountdownPattern>(path);
        if (pattern == null)
        {
            pattern = ScriptableObject.CreateInstance<CountdownPattern>();
            AssetDatabase.CreateAsset(pattern, path);
        }

        pattern.Configure(beats, 0.16f, 0.36f, 0.6f);
        EditorUtility.SetDirty(pattern);
        return pattern;
    }

    private static CombatantAttack GetOrCreateAttack(
        string path,
        string displayName,
        float weight,
        CombatantAttackStrike[] strikes)
    {
        CombatantAttack attack = AssetDatabase.LoadAssetAtPath<CombatantAttack>(path);
        if (attack == null)
        {
            attack = ScriptableObject.CreateInstance<CombatantAttack>();
            AssetDatabase.CreateAsset(attack, path);
        }

        attack.Configure(displayName, weight, strikes);
        EditorUtility.SetDirty(attack);
        return attack;
    }
}
