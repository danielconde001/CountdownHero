using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Builds and controls the geyser's layered paper-cutout wind effect.
///
/// The effect is generated from ordinary ParticleSystems at runtime so one
/// lightweight prefab can adapt to any geyser trigger. All particles simulate
/// in local space and travel along the source collider's local +Y direction,
/// which is the same direction TimedGeyser uses to launch the player.
/// </summary>
[DisallowMultipleComponent]
public sealed class GeyserWindVFX : MonoBehaviour
{
    private const string GeneratedRootName = "Generated Wind Layers";
    private const string UrpParticleShader = "Universal Render Pipeline/Particles/Unlit";

    [Header("Paper Style")]
    [SerializeField] private Color outlineColor = new Color(0.025f, 0.09f, 0.17f, 0.68f);
    [SerializeField] private Color plumeColor = new Color(0.82f, 0.97f, 1f, 0.7f);
    [SerializeField] private Color highlightColor = new Color(0.94f, 1f, 1f, 0.92f);
    [SerializeField] private Color accentColor = new Color(0.35f, 0.86f, 1f, 0.82f);
    [SerializeField] private Color chargeColor = new Color(1f, 0.82f, 0.2f, 0.9f);

    [Header("Paper Puff Sprites")]
    [Tooltip("Main plume silhouette. A procedural cloud is used if this is unassigned.")]
    [SerializeField] private Sprite primaryPuffSprite;
    [Tooltip("Adds shape variation around the main plume.")]
    [SerializeField] private Sprite secondaryPuffSprite;
    [Tooltip("Used by the activation burst at the vent.")]
    [SerializeField] private Sprite burstPuffSprite;

    [Header("Rendering")]
    [Tooltip("Keeps the URP particle shader and its transparent variant in player builds.")]
    [SerializeField] private Material particleMaterialSource;

    [Header("Presentation")]
    [SerializeField, Min(0.25f)] private float density = 0.72f;
    [SerializeField] private float visualDepth = -0.18f;
    [SerializeField] private int sortingOrder = -4;
    [SerializeField, Range(0f, 0.5f)] private float shutdownFadeDuration = 0.2f;

    private readonly List<ParticleSystem> allSystems = new List<ParticleSystem>();
    private readonly List<ParticleSystem> activeSystems = new List<ParticleSystem>();
    private readonly List<Object> runtimeAssets = new List<Object>();

    private BoxCollider2D sourceCollider;
    private Transform generatedRoot;
    private ParticleSystem chargeRings;
    private ParticleSystem burstOutline;
    private ParticleSystem burstFill;
    private Coroutine shutdownRoutine;
    private float effectWidth = 1.25f;
    private float effectLength = 4f;
    private bool isBuilt;
    private bool isWindActive;

    public bool IsWindActive => isWindActive;
    public bool IsReady => isBuilt;

    private void Awake()
    {
        ResolveSourceCollider();
        EnsureBuilt();
        StopImmediately();
    }

    private void OnDisable()
    {
        StopImmediately();
    }

    private void OnDestroy()
    {
        for (int i = 0; i < runtimeAssets.Count; i++)
        {
            Object asset = runtimeAssets[i];
            if (asset == null)
            {
                continue;
            }

            if (Application.isPlaying)
            {
                Destroy(asset);
            }
            else
            {
                DestroyImmediate(asset);
            }
        }

        runtimeAssets.Clear();
    }

    /// <summary>
    /// Fits the visual to the same trigger used by the gameplay launch.
    /// </summary>
    public void Configure(BoxCollider2D launchZone)
    {
        if (launchZone == null)
        {
            return;
        }

        float newWidth = Mathf.Max(0.25f, launchZone.size.x);
        float newLength = Mathf.Max(0.5f, launchZone.size.y);
        bool dimensionsChanged =
            !Mathf.Approximately(effectWidth, newWidth) ||
            !Mathf.Approximately(effectLength, newLength);

        sourceCollider = launchZone;
        effectWidth = newWidth;
        effectLength = newLength;

        if (!isBuilt)
        {
            EnsureBuilt();
        }
        else if (dimensionsChanged)
        {
            Rebuild();
        }
        else
        {
            PositionGeneratedRoot();
        }
    }

    /// <summary>
    /// Plays a restrained yellow pulse while TimedGeyser counts down.
    /// </summary>
    public void BeginCharge(float countdownDuration)
    {
        EnsureBuilt();
        CancelShutdownRoutine();
        isWindActive = false;
        StopActiveSystems(ParticleSystemStopBehavior.StopEmittingAndClear);

        if (chargeRings == null || countdownDuration <= 0f)
        {
            return;
        }

        ParticleSystem.EmissionModule emission = chargeRings.emission;
        emission.rateOverTime = Mathf.Lerp(
            3.5f,
            6.5f,
            Mathf.InverseLerp(0.5f, 4f, countdownDuration)) * density;

        chargeRings.Clear(true);
        chargeRings.Play(true);
    }

    /// <summary>
    /// Starts or gracefully stops the full wind column.
    /// </summary>
    public void SetWindActive(bool active)
    {
        EnsureBuilt();
        if (!isBuilt)
        {
            return;
        }

        if (active && isWindActive)
        {
            return;
        }

        bool wasWindActive = isWindActive;
        isWindActive = active;
        if (active)
        {
            CancelShutdownRoutine();
            if (chargeRings != null)
            {
                chargeRings.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            }

            // Match the puff travel time so even tall geysers look full on
            // their first active frame instead of visibly filling from below.
            float fillTime = Mathf.Clamp(effectLength / 6.5f, 0.58f, 1.05f);
            for (int i = 0; i < activeSystems.Count; i++)
            {
                ParticleSystem particles = activeSystems[i];
                particles.Simulate(fillTime, false, true, true);
                particles.Play(false);
            }

            // Matching seeds keep the pale fill nested inside the navy outline.
            EmitBurst(burstOutline, 18);
            EmitBurst(burstFill, 18);
        }
        else
        {
            StopActiveSystems(ParticleSystemStopBehavior.StopEmitting);
            if (chargeRings != null)
            {
                chargeRings.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            }

            CancelShutdownRoutine();
            if (wasWindActive &&
                shutdownFadeDuration > 0f &&
                gameObject.activeInHierarchy)
            {
                shutdownRoutine = StartCoroutine(ClearAfterShutdownFade());
            }
            else
            {
                StopActiveSystems(ParticleSystemStopBehavior.StopEmittingAndClear);
            }
        }
    }

    private void ResolveSourceCollider()
    {
        sourceCollider = GetComponent<BoxCollider2D>();
        if (sourceCollider == null)
        {
            sourceCollider = GetComponentInParent<BoxCollider2D>();
        }

        if (sourceCollider != null)
        {
            effectWidth = Mathf.Max(0.25f, sourceCollider.size.x);
            effectLength = Mathf.Max(0.5f, sourceCollider.size.y);
        }
    }

    private void EnsureBuilt()
    {
        if (isBuilt)
        {
            return;
        }

        if (sourceCollider == null)
        {
            ResolveSourceCollider();
        }

        BuildEffect();
    }

    private void Rebuild()
    {
        StopImmediately();

        if (generatedRoot != null)
        {
            if (Application.isPlaying)
            {
                Destroy(generatedRoot.gameObject);
            }
            else
            {
                DestroyImmediate(generatedRoot.gameObject);
            }
        }

        DestroyRuntimeAssets();
        allSystems.Clear();
        activeSystems.Clear();
        chargeRings = null;
        burstOutline = null;
        burstFill = null;
        generatedRoot = null;
        isBuilt = false;
        BuildEffect();
    }

    private void BuildEffect()
    {
        Shader particleShader = particleMaterialSource != null
            ? particleMaterialSource.shader
            : null;
        if (particleShader == null)
        {
            particleShader = Shader.Find(UrpParticleShader);
        }

        if (particleShader == null)
        {
            Debug.LogWarning(
                $"{nameof(GeyserWindVFX)} could not find a compatible particle shader.",
                this);
            return;
        }

        GameObject rootObject = new GameObject(GeneratedRootName);
        rootObject.transform.SetParent(transform, false);
        generatedRoot = rootObject.transform;
        PositionGeneratedRoot();

        Texture primaryTexture = GetSpriteTexture(primaryPuffSprite);
        if (primaryTexture == null)
        {
            primaryTexture = CreateFallbackPuffTexture();
        }

        Texture secondaryTexture = GetSpriteTexture(secondaryPuffSprite);
        if (secondaryTexture == null)
        {
            secondaryTexture = primaryTexture;
        }

        Texture burstTexture = GetSpriteTexture(burstPuffSprite);
        if (burstTexture == null)
        {
            burstTexture = secondaryTexture;
        }

        Texture2D streakTexture = CreateStreakTexture();
        Texture2D ringTexture = CreateRingTexture();
        Texture2D diamondTexture = CreateDiamondTexture();

        Material primaryMaterial = CreateParticleMaterial(
            particleMaterialSource,
            particleShader,
            primaryTexture,
            false,
            "Geyser Puff Material");
        Material secondaryMaterial = CreateParticleMaterial(
            particleMaterialSource,
            particleShader,
            secondaryTexture,
            false,
            "Geyser Secondary Puff Material");
        Material burstMaterial = CreateParticleMaterial(
            particleMaterialSource,
            particleShader,
            burstTexture,
            false,
            "Geyser Burst Material");
        Material streakMaterial = CreateParticleMaterial(
            particleMaterialSource,
            particleShader,
            streakTexture,
            false,
            "Geyser Streak Material");
        Material additiveRingMaterial = CreateParticleMaterial(
            particleMaterialSource,
            particleShader,
            ringTexture,
            true,
            "Geyser Ring Material");
        Material additiveDiamondMaterial = CreateParticleMaterial(
            particleMaterialSource,
            particleShader,
            diamondTexture,
            true,
            "Geyser Mote Material");

        BuildPuffPair(
            "Main Paper Puffs",
            primaryMaterial,
            18431u,
            1f,
            1f,
            1f,
            sortingOrder);
        BuildPuffPair(
            "Secondary Paper Puffs",
            secondaryMaterial,
            53717u,
            0.68f,
            0.5f,
            1.25f,
            sortingOrder + 2);

        ParticleSystem streaks = CreateParticleSystem(
            "Fast Wind Ribbons",
            streakMaterial,
            sortingOrder + 5,
            77659u);
        ConfigureStreaks(streaks);
        activeSystems.Add(streaks);

        ParticleSystem rings = CreateParticleSystem(
            "Traveling Air Rings",
            additiveRingMaterial,
            sortingOrder + 6,
            92821u);
        ConfigureTravelingRings(rings);
        activeSystems.Add(rings);

        ParticleSystem motes = CreateParticleSystem(
            "Bright Air Motes",
            additiveDiamondMaterial,
            sortingOrder + 7,
            36353u);
        ConfigureMotes(motes);
        activeSystems.Add(motes);

        burstOutline = CreateParticleSystem(
            "Vent Burst Outline",
            burstMaterial,
            sortingOrder + 5,
            61441u);
        ConfigureBurst(burstOutline, outlineColor, 1f);

        burstFill = CreateParticleSystem(
            "Vent Burst Fill",
            burstMaterial,
            sortingOrder + 6,
            61441u);
        ConfigureBurst(burstFill, highlightColor, 0.78f);

        chargeRings = CreateParticleSystem(
            "Countdown Charge Rings",
            additiveRingMaterial,
            sortingOrder + 8,
            45737u);
        ConfigureChargeRings(chargeRings);

        isBuilt = true;
    }

    private void BuildPuffPair(
        string name,
        Material material,
        uint randomSeed,
        float sizeScale,
        float emissionScale,
        float spreadScale,
        int pairSortingOrder)
    {
        ParticleSystem outline = CreateParticleSystem(
            name + " Outline",
            material,
            pairSortingOrder,
            randomSeed);
        ConfigurePuffs(
            outline,
            outlineColor,
            sizeScale,
            emissionScale,
            spreadScale);
        activeSystems.Add(outline);

        ParticleSystem fill = CreateParticleSystem(
            name + " Fill",
            material,
            pairSortingOrder + 1,
            randomSeed);
        ConfigurePuffs(
            fill,
            plumeColor,
            sizeScale * 0.86f,
            emissionScale,
            spreadScale);
        activeSystems.Add(fill);
    }

    private ParticleSystem CreateParticleSystem(
        string objectName,
        Material material,
        int systemSortingOrder,
        uint randomSeed)
    {
        GameObject systemObject = new GameObject(objectName);
        systemObject.transform.SetParent(generatedRoot, false);

        // ParticleSystem cone/box shapes emit along local +Z. Rotating the
        // system maps that axis to the geyser's local +Y launch direction.
        systemObject.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);

        ParticleSystem particles = systemObject.AddComponent<ParticleSystem>();
        particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        particles.useAutoRandomSeed = false;
        particles.randomSeed = randomSeed;

        ParticleSystem.MainModule main = particles.main;
        main.playOnAwake = false;
        main.prewarm = false;
        main.loop = true;
        main.simulationSpace = ParticleSystemSimulationSpace.Local;
        main.scalingMode = ParticleSystemScalingMode.Hierarchy;
        main.gravityModifier = 0f;
        main.stopAction = ParticleSystemStopAction.None;

        ParticleSystemRenderer renderer = particles.GetComponent<ParticleSystemRenderer>();
        renderer.sharedMaterial = material;
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        renderer.alignment = ParticleSystemRenderSpace.View;
        renderer.sortMode = ParticleSystemSortMode.YoungestInFront;
        renderer.sortingOrder = systemSortingOrder;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        renderer.lightProbeUsage = LightProbeUsage.Off;
        renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
        renderer.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;

        allSystems.Add(particles);
        return particles;
    }

    private void ConfigurePuffs(
        ParticleSystem particles,
        Color color,
        float sizeScale,
        float emissionScale,
        float spreadScale)
    {
        float travelTime = Mathf.Clamp(effectLength / 6.5f, 0.58f, 1.05f);
        float averageSpeed = effectLength / travelTime;

        ParticleSystem.MainModule main = particles.main;
        main.duration = 2f;
        main.startLifetime = travelTime;
        main.startSpeed = new ParticleSystem.MinMaxCurve(
            averageSpeed * 0.96f,
            averageSpeed * 1.04f);
        main.startSize = new ParticleSystem.MinMaxCurve(
            effectWidth * 0.4f * sizeScale,
            effectWidth * 0.64f * sizeScale);
        main.startRotation = new ParticleSystem.MinMaxCurve(-Mathf.PI, Mathf.PI);
        main.startColor = color;
        main.maxParticles = Mathf.CeilToInt(115f * density * emissionScale);

        ParticleSystem.EmissionModule emission = particles.emission;
        emission.rateOverTime =
            Mathf.Clamp(effectLength * 4.3f, 14f, 30f) * density * emissionScale;

        ParticleSystem.ShapeModule shape = particles.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(
            effectWidth * 0.5f * spreadScale,
            0.02f,
            0.04f);
        // Keep the effect in the 2D gameplay plane. Side-to-side movement is
        // added explicitly below so random emission cannot drift through Z.
        shape.randomDirectionAmount = 0f;

        ParticleSystem.VelocityOverLifetimeModule velocity = particles.velocityOverLifetime;
        velocity.enabled = true;
        velocity.space = ParticleSystemSimulationSpace.Local;
        velocity.x = new ParticleSystem.MinMaxCurve(
            -0.2f * spreadScale,
            0.2f * spreadScale);
        // Unity requires all three velocity axes to use the same MinMaxCurve
        // mode. Degenerate two-constant curves keep Y/Z fixed at zero while
        // matching the randomized X axis.
        velocity.y = new ParticleSystem.MinMaxCurve(0f, 0f);
        velocity.z = new ParticleSystem.MinMaxCurve(0f, 0f);

        ParticleSystem.NoiseModule noise = particles.noise;
        noise.enabled = true;
        noise.separateAxes = true;
        noise.strengthX = effectWidth * 0.2f * spreadScale;
        noise.strengthY = 0.01f;
        noise.strengthZ = effectWidth * 0.08f;
        noise.frequency = 0.72f;
        noise.scrollSpeed = 0.65f;
        noise.damping = true;
        noise.octaveCount = 2;
        noise.octaveMultiplier = 0.45f;
        noise.quality = ParticleSystemNoiseQuality.Medium;

        ParticleSystem.ColorOverLifetimeModule colorOverLife =
            particles.colorOverLifetime;
        colorOverLife.enabled = true;
        colorOverLife.color = CreateFadeGradient(0.92f, 0.72f);

        ParticleSystem.SizeOverLifetimeModule sizeOverLife = particles.sizeOverLifetime;
        sizeOverLife.enabled = true;
        sizeOverLife.size = new ParticleSystem.MinMaxCurve(
            1f,
            CreateCurve(
                new Keyframe(0f, 0.54f),
                new Keyframe(0.22f, 0.92f),
                new Keyframe(0.72f, 1.08f),
                new Keyframe(1f, 0.86f)));

        ParticleSystem.RotationOverLifetimeModule rotation =
            particles.rotationOverLifetime;
        rotation.enabled = true;
        rotation.z = new ParticleSystem.MinMaxCurve(-0.45f, 0.45f);
    }

    private void ConfigureStreaks(ParticleSystem particles)
    {
        float speed = Mathf.Clamp(effectLength * 4.3f, 16f, 20f);
        float life = effectLength / speed;

        ParticleSystem.MainModule main = particles.main;
        main.duration = 2f;
        main.startLifetime = life;
        main.startSpeed = new ParticleSystem.MinMaxCurve(speed * 0.96f, speed * 1.04f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.035f, 0.085f);
        main.startColor = accentColor;
        main.maxParticles = Mathf.CeilToInt(70f * density);

        ParticleSystem.EmissionModule emission = particles.emission;
        emission.rateOverTime = Mathf.Clamp(effectLength * 5f, 18f, 34f) * density;

        ParticleSystem.ShapeModule shape = particles.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(effectWidth * 0.72f, 0.02f, 0.03f);
        shape.randomDirectionAmount = 0f;

        ParticleSystem.NoiseModule noise = particles.noise;
        noise.enabled = true;
        noise.separateAxes = true;
        noise.strengthX = effectWidth * 0.18f;
        noise.strengthY = 0.005f;
        noise.strengthZ = 0.04f;
        noise.frequency = 1.15f;
        noise.scrollSpeed = 1.2f;
        noise.damping = true;
        noise.quality = ParticleSystemNoiseQuality.Medium;

        ParticleSystem.ColorOverLifetimeModule colorOverLife =
            particles.colorOverLifetime;
        colorOverLife.enabled = true;
        colorOverLife.color = CreateFadeGradient(0.72f, 0.48f);

        ParticleSystemRenderer renderer = particles.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Stretch;
        renderer.velocityScale = 0.16f;
        renderer.lengthScale = 3.8f;
        renderer.cameraVelocityScale = 0f;
    }

    private void ConfigureTravelingRings(ParticleSystem particles)
    {
        float speed = Mathf.Clamp(effectLength * 2.6f, 9f, 12f);
        float life = effectLength / speed;

        ParticleSystem.MainModule main = particles.main;
        main.duration = 2f;
        main.startLifetime = life;
        main.startSpeed = new ParticleSystem.MinMaxCurve(speed * 0.97f, speed * 1.03f);
        main.startSize = effectWidth * 0.85f;
        main.startColor = highlightColor;
        main.maxParticles = 18;

        ParticleSystem.EmissionModule emission = particles.emission;
        emission.rateOverTime = 3.2f * density;

        ParticleSystem.ShapeModule shape = particles.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(0.025f, 0.015f, 0.015f);
        shape.randomDirectionAmount = 0f;

        // The ellipse's long axis stays perpendicular to the launch velocity,
        // including when a level designer rotates a geyser sideways.
        ParticleSystemRenderer renderer = particles.GetComponent<ParticleSystemRenderer>();
        renderer.alignment = ParticleSystemRenderSpace.Velocity;

        ParticleSystem.ColorOverLifetimeModule colorOverLife =
            particles.colorOverLifetime;
        colorOverLife.enabled = true;
        colorOverLife.color = CreateFadeGradient(0.6f, 0.25f);

        ParticleSystem.SizeOverLifetimeModule sizeOverLife = particles.sizeOverLifetime;
        sizeOverLife.enabled = true;
        sizeOverLife.size = new ParticleSystem.MinMaxCurve(
            1f,
            CreateCurve(
                new Keyframe(0f, 0.48f),
                new Keyframe(0.35f, 1.05f),
                new Keyframe(1f, 1.55f)));
    }

    private void ConfigureMotes(ParticleSystem particles)
    {
        float speed = Mathf.Clamp(effectLength * 3.1f, 11f, 15f);
        float life = effectLength / speed;

        ParticleSystem.MainModule main = particles.main;
        main.duration = 2f;
        main.startLifetime = new ParticleSystem.MinMaxCurve(life * 0.92f, life * 1.08f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(speed * 0.9f, speed * 1.1f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.055f, 0.14f);
        main.startRotation = new ParticleSystem.MinMaxCurve(-Mathf.PI, Mathf.PI);
        main.startColor = highlightColor;
        main.maxParticles = Mathf.CeilToInt(42f * density);

        ParticleSystem.EmissionModule emission = particles.emission;
        emission.rateOverTime = 11f * density;

        ParticleSystem.ShapeModule shape = particles.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(effectWidth * 0.78f, 0.02f, 0.04f);
        shape.randomDirectionAmount = 0f;

        ParticleSystem.VelocityOverLifetimeModule velocity = particles.velocityOverLifetime;
        velocity.enabled = true;
        velocity.space = ParticleSystemSimulationSpace.Local;
        velocity.x = new ParticleSystem.MinMaxCurve(
            -effectWidth * 0.45f,
            effectWidth * 0.45f);
        velocity.y = new ParticleSystem.MinMaxCurve(0f, 0f);
        velocity.z = new ParticleSystem.MinMaxCurve(0f, 0f);

        ParticleSystem.ColorOverLifetimeModule colorOverLife =
            particles.colorOverLifetime;
        colorOverLife.enabled = true;
        colorOverLife.color = CreateFadeGradient(1f, 0.55f);

        ParticleSystem.RotationOverLifetimeModule rotation =
            particles.rotationOverLifetime;
        rotation.enabled = true;
        rotation.z = new ParticleSystem.MinMaxCurve(-2.4f, 2.4f);
    }

    private void ConfigureBurst(ParticleSystem particles, Color color, float sizeScale)
    {
        ParticleSystem.MainModule main = particles.main;
        main.loop = false;
        main.duration = 0.25f;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.42f, 0.72f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(1.8f, 3.7f);
        main.startSize = new ParticleSystem.MinMaxCurve(
            effectWidth * 0.26f * sizeScale,
            effectWidth * 0.52f * sizeScale);
        main.startRotation = new ParticleSystem.MinMaxCurve(-Mathf.PI, Mathf.PI);
        main.startColor = color;
        main.maxParticles = 40;

        ParticleSystem.EmissionModule emission = particles.emission;
        emission.enabled = false;

        ParticleSystem.ShapeModule shape = particles.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(effectWidth * 0.45f, 0.01f, 0.04f);
        shape.randomDirectionAmount = 0f;

        ParticleSystem.VelocityOverLifetimeModule velocity = particles.velocityOverLifetime;
        velocity.enabled = true;
        velocity.space = ParticleSystemSimulationSpace.Local;
        velocity.x = new ParticleSystem.MinMaxCurve(
            -effectWidth * 1.25f,
            effectWidth * 1.25f);
        velocity.y = new ParticleSystem.MinMaxCurve(0f, 0f);
        velocity.z = new ParticleSystem.MinMaxCurve(0f, 0f);

        ParticleSystem.NoiseModule noise = particles.noise;
        noise.enabled = true;
        noise.separateAxes = true;
        noise.strengthX = effectWidth * 0.22f;
        noise.strengthY = 0.006f;
        noise.strengthZ = 0.04f;
        noise.frequency = 0.8f;
        noise.scrollSpeed = 0.65f;
        noise.damping = true;

        ParticleSystem.ColorOverLifetimeModule colorOverLife =
            particles.colorOverLifetime;
        colorOverLife.enabled = true;
        colorOverLife.color = CreateFadeGradient(1f, 0.68f);

        ParticleSystem.SizeOverLifetimeModule sizeOverLife = particles.sizeOverLifetime;
        sizeOverLife.enabled = true;
        sizeOverLife.size = new ParticleSystem.MinMaxCurve(
            1f,
            CreateCurve(
                new Keyframe(0f, 0.48f),
                new Keyframe(0.35f, 1f),
                new Keyframe(1f, 1.22f)));
    }

    private void ConfigureChargeRings(ParticleSystem particles)
    {
        ParticleSystem.MainModule main = particles.main;
        main.duration = 2f;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.42f, 0.62f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.15f, 0.45f);
        main.startSize = effectWidth * 0.48f;
        main.startColor = chargeColor;
        main.maxParticles = 18;

        ParticleSystem.EmissionModule emission = particles.emission;
        emission.rateOverTime = 4.5f * density;

        ParticleSystem.ShapeModule shape = particles.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(0.02f, 0.012f, 0.012f);
        shape.randomDirectionAmount = 0f;

        ParticleSystemRenderer renderer = particles.GetComponent<ParticleSystemRenderer>();
        renderer.alignment = ParticleSystemRenderSpace.Velocity;

        ParticleSystem.ColorOverLifetimeModule colorOverLife =
            particles.colorOverLifetime;
        colorOverLife.enabled = true;
        colorOverLife.color = CreateFadeGradient(0.88f, 0.42f);

        ParticleSystem.SizeOverLifetimeModule sizeOverLife = particles.sizeOverLifetime;
        sizeOverLife.enabled = true;
        sizeOverLife.size = new ParticleSystem.MinMaxCurve(
            1f,
            CreateCurve(
                new Keyframe(0f, 0.42f),
                new Keyframe(0.4f, 1f),
                new Keyframe(1f, 1.45f)));
    }

    private void PositionGeneratedRoot()
    {
        if (generatedRoot == null || sourceCollider == null)
        {
            return;
        }

        Vector3 sourceLocalBase = new Vector3(
            sourceCollider.offset.x,
            sourceCollider.offset.y - sourceCollider.size.y * 0.5f + 0.06f,
            visualDepth);
        Vector3 worldBase = sourceCollider.transform.TransformPoint(sourceLocalBase);

        generatedRoot.localPosition = transform.InverseTransformPoint(worldBase);
        generatedRoot.localRotation =
            Quaternion.Inverse(transform.rotation) * sourceCollider.transform.rotation;
        generatedRoot.localScale = Vector3.one;
    }

    private void StopImmediately()
    {
        CancelShutdownRoutine();
        isWindActive = false;
        for (int i = 0; i < allSystems.Count; i++)
        {
            if (allSystems[i] != null)
            {
                allSystems[i].Stop(
                    true,
                    ParticleSystemStopBehavior.StopEmittingAndClear);
            }
        }
    }

    private void StopActiveSystems(ParticleSystemStopBehavior behavior)
    {
        for (int i = 0; i < activeSystems.Count; i++)
        {
            if (activeSystems[i] != null)
            {
                activeSystems[i].Stop(true, behavior);
            }
        }
    }

    private static void EmitBurst(ParticleSystem particles, int count)
    {
        if (particles == null)
        {
            return;
        }

        particles.Clear(true);
        particles.Emit(count);
    }

    private Material CreateParticleMaterial(
        Material source,
        Shader shader,
        Texture texture,
        bool additive,
        string materialName)
    {
        Material material = source != null
            ? new Material(source)
            : new Material(shader);
        material.name = materialName;
        material.hideFlags = HideFlags.HideAndDontSave;
        material.renderQueue = (int)RenderQueue.Transparent;

        if (material.HasProperty("_BaseMap"))
        {
            material.SetTexture("_BaseMap", texture);
        }

        if (material.HasProperty("_MainTex"))
        {
            material.SetTexture("_MainTex", texture);
        }

        if (material.HasProperty("_BaseColor"))
        {
            material.SetColor("_BaseColor", Color.white);
        }

        if (material.HasProperty("_Color"))
        {
            material.SetColor("_Color", Color.white);
        }

        SetFloatIfPresent(material, "_Surface", 1f);
        SetFloatIfPresent(material, "_Blend", additive ? 2f : 0f);
        SetFloatIfPresent(material, "_SrcBlend", (float)BlendMode.SrcAlpha);
        SetFloatIfPresent(
            material,
            "_DstBlend",
            (float)(additive ? BlendMode.One : BlendMode.OneMinusSrcAlpha));
        SetFloatIfPresent(material, "_SrcBlendAlpha", (float)BlendMode.One);
        SetFloatIfPresent(
            material,
            "_DstBlendAlpha",
            (float)BlendMode.OneMinusSrcAlpha);
        SetFloatIfPresent(material, "_ZWrite", 0f);
        SetFloatIfPresent(material, "_Cull", (float)CullMode.Off);
        SetFloatIfPresent(material, "_SoftParticlesEnabled", 0f);
        SetFloatIfPresent(material, "_CameraFadingEnabled", 0f);

        material.SetOverrideTag("RenderType", "Transparent");
        material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        material.DisableKeyword("_ALPHATEST_ON");
        material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
        material.DisableKeyword("_ALPHAMODULATE_ON");

        runtimeAssets.Add(material);
        return material;
    }

    private static void SetFloatIfPresent(Material material, string property, float value)
    {
        if (material.HasProperty(property))
        {
            material.SetFloat(property, value);
        }
    }

    private Texture2D CreateFallbackPuffTexture()
    {
        const int size = 128;
        Texture2D texture = CreateRuntimeTexture("Procedural Paper Puff", size, size);
        Color[] pixels = new Color[size * size];
        Vector3[] circles =
        {
            new Vector3(-0.34f, 0.02f, 0.46f),
            new Vector3(0.08f, 0.2f, 0.55f),
            new Vector3(0.42f, -0.02f, 0.42f),
            new Vector3(0.02f, -0.3f, 0.5f)
        };

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                Vector2 point = new Vector2(
                    (x + 0.5f) / size * 2f - 1f,
                    (y + 0.5f) / size * 2f - 1f);
                float distance = float.MaxValue;

                for (int i = 0; i < circles.Length; i++)
                {
                    Vector2 center = new Vector2(circles[i].x, circles[i].y);
                    distance = Mathf.Min(
                        distance,
                        Vector2.Distance(point, center) - circles[i].z);
                }

                float alpha = 1f - Mathf.SmoothStep(-0.015f, 0.055f, distance);
                pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
            }
        }

        FinishRuntimeTexture(texture, pixels);
        return texture;
    }

    private Texture2D CreateStreakTexture()
    {
        // Stretched billboards use the texture's vertical axis as their length.
        const int width = 32;
        const int height = 128;
        Texture2D texture = CreateRuntimeTexture("Wind Ribbon", width, height);
        Color[] pixels = new Color[width * height];

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                float px = (x + 0.5f) / width * 2f - 1f;
                float py = (y + 0.5f) / height * 2f - 1f;
                float endFade = 1f - Mathf.SmoothStep(0.68f, 1f, Mathf.Abs(py));
                float centerFade = 1f - Mathf.SmoothStep(0.18f, 0.92f, Mathf.Abs(px));
                float alpha = endFade * centerFade;
                pixels[y * width + x] = new Color(1f, 1f, 1f, alpha);
            }
        }

        FinishRuntimeTexture(texture, pixels);
        return texture;
    }

    private Texture2D CreateRingTexture()
    {
        const int size = 96;
        Texture2D texture = CreateRuntimeTexture("Air Ring", size, size);
        Color[] pixels = new Color[size * size];

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float px = (x + 0.5f) / size * 2f - 1f;
                float py = (y + 0.5f) / size * 2f - 1f;
                // A flattened ring reads as a cross-section of moving air
                // instead of a bubble or magic projectile.
                float ellipticalRadius = Mathf.Sqrt(px * px + (py / 0.28f) * (py / 0.28f));
                float ringDistance = Mathf.Abs(ellipticalRadius - 0.68f);
                float alpha = 1f - Mathf.SmoothStep(0.055f, 0.13f, ringDistance);
                pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
            }
        }

        FinishRuntimeTexture(texture, pixels);
        return texture;
    }

    private Texture2D CreateDiamondTexture()
    {
        const int size = 64;
        Texture2D texture = CreateRuntimeTexture("Air Diamond", size, size);
        Color[] pixels = new Color[size * size];

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float px = (x + 0.5f) / size * 2f - 1f;
                float py = (y + 0.5f) / size * 2f - 1f;
                float diamondDistance = Mathf.Abs(px) + Mathf.Abs(py);
                float alpha = 1f - Mathf.SmoothStep(0.68f, 0.96f, diamondDistance);
                pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
            }
        }

        FinishRuntimeTexture(texture, pixels);
        return texture;
    }

    private Texture2D CreateRuntimeTexture(string textureName, int width, int height)
    {
        Texture2D texture = new Texture2D(
            width,
            height,
            TextureFormat.RGBA32,
            false,
            false)
        {
            name = textureName,
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear,
            anisoLevel = 0,
            hideFlags = HideFlags.HideAndDontSave
        };

        runtimeAssets.Add(texture);
        return texture;
    }

    private static void FinishRuntimeTexture(Texture2D texture, Color[] pixels)
    {
        texture.SetPixels(pixels);
        texture.Apply(false, true);
    }

    private static Texture GetSpriteTexture(Sprite sprite)
    {
        return sprite != null ? sprite.texture : null;
    }

    private static ParticleSystem.MinMaxGradient CreateFadeGradient(
        float peakAlpha,
        float middleAlpha)
    {
        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new[]
            {
                new GradientColorKey(Color.white, 0f),
                new GradientColorKey(Color.white, 1f)
            },
            new[]
            {
                new GradientAlphaKey(0f, 0f),
                new GradientAlphaKey(peakAlpha, 0.12f),
                new GradientAlphaKey(middleAlpha, 0.72f),
                new GradientAlphaKey(0f, 1f)
            });
        return new ParticleSystem.MinMaxGradient(gradient);
    }

    private static AnimationCurve CreateCurve(params Keyframe[] keys)
    {
        AnimationCurve curve = new AnimationCurve(keys);
        for (int i = 0; i < curve.length; i++)
        {
            curve.SmoothTangents(i, 0f);
        }

        return curve;
    }

    private IEnumerator ClearAfterShutdownFade()
    {
        yield return new WaitForSeconds(shutdownFadeDuration);
        StopActiveSystems(ParticleSystemStopBehavior.StopEmittingAndClear);
        shutdownRoutine = null;
    }

    private void CancelShutdownRoutine()
    {
        if (shutdownRoutine == null)
        {
            return;
        }

        StopCoroutine(shutdownRoutine);
        shutdownRoutine = null;
    }

    private void DestroyRuntimeAssets()
    {
        for (int i = 0; i < runtimeAssets.Count; i++)
        {
            Object asset = runtimeAssets[i];
            if (asset == null)
            {
                continue;
            }

            if (Application.isPlaying)
            {
                Destroy(asset);
            }
            else
            {
                DestroyImmediate(asset);
            }
        }

        runtimeAssets.Clear();
    }
}
