using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

/// <summary>
/// Replaces a fire-wall blockout mesh with two burning orbs and a continuous
/// flame bridge. The visual derives its world-space span from a BoxCollider2D,
/// so level designers can move, rotate, or non-uniformly scale the hazard.
///
/// This component is presentation-only. It deliberately creates no colliders
/// or rigidbodies; the existing DeathZone remains the sole gameplay authority.
/// </summary>
[DisallowMultipleComponent]
public sealed class FireWallVFX : MonoBehaviour
{
    public enum SpanAxis
    {
        Auto,
        LocalX,
        LocalY
    }

    private const string GeneratedRootSuffix = " - Generated Fire Wall VFX";
    private const string ParticleMaterialResource = "VFX/Fire Wall Particle";
    private const string OrbMaterialResource = "VFX/Fire Wall Core";
    private const string ParticleShaderName =
        "Universal Render Pipeline/Particles/Unlit";
    private const string OrbShaderName = "Universal Render Pipeline/Lit";
    private const int BeamPointCount = 26;

    [Header("Source")]
    [SerializeField] private BoxCollider2D sourceCollider;
    [SerializeField] private SpanAxis spanAxis = SpanAxis.Auto;
    [Tooltip("Hides the purple blockout mesh while the generated effect is visible.")]
    [SerializeField] private bool hidePlaceholderRenderer = true;

    [Header("Style")]
    [SerializeField] private Color outlineColor =
        new Color(0.12f, 0.008f, 0.025f, 0.98f);
    [SerializeField] private Color deepRedColor =
        new Color(0.72f, 0.025f, 0.018f, 0.96f);
    [SerializeField] private Color orangeColor =
        new Color(1f, 0.18f, 0.018f, 0.98f);
    [SerializeField] private Color yellowColor =
        new Color(1f, 0.68f, 0.08f, 1f);
    [SerializeField] private Color hotColor =
        new Color(1f, 0.94f, 0.56f, 1f);

    [Header("Shape")]
    [SerializeField, Min(0.5f)] private float orbDiameterMultiplier = 1.3f;
    [SerializeField, Min(0.25f)] private float beamThicknessMultiplier = 1f;
    [SerializeField, Range(0.25f, 1.5f)] private float density = 0.8f;

    [Header("Rendering")]
    [SerializeField] private Material particleMaterialSource;
    [SerializeField] private Material orbMaterialSource;
    [SerializeField] private float visualDepth = -0.2f;
    [SerializeField] private int sortingOrder = -2;

    private readonly List<ParticleSystem> particleSystems =
        new List<ParticleSystem>();
    private readonly List<Object> runtimeAssets = new List<Object>();

    private readonly Vector3[] outlinePoints = new Vector3[BeamPointCount];
    private readonly Vector3[] bodyPoints = new Vector3[BeamPointCount];
    private readonly Vector3[] corePoints = new Vector3[BeamPointCount];

    private GameObject generatedRoot;
    private Renderer placeholderRenderer;
    private bool placeholderWasEnabled;

    private LineRenderer outlineLine;
    private LineRenderer bodyLine;
    private LineRenderer coreLine;
    private Material outlineLineMaterial;
    private Material bodyLineMaterial;
    private Material coreLineMaterial;

    private ParticleSystem beamFlameOutline;
    private ParticleSystem beamFlameFill;
    private ParticleSystem embers;
    private readonly ParticleSystem[] orbFlameOutlines = new ParticleSystem[2];
    private readonly ParticleSystem[] orbFlameFills = new ParticleSystem[2];

    private readonly Transform[] orbRoots = new Transform[2];
    private readonly Transform[] orbShells = new Transform[2];
    private readonly Transform[] orbBodies = new Transform[2];
    private readonly Transform[] orbHotSpots = new Transform[2];

    private Vector3 beamStart;
    private Vector3 beamEnd;
    private Vector3 beamDirection = Vector3.up;
    private Vector3 beamPerpendicular = Vector3.left;
    private float beamLength = 1f;
    private float hazardThickness = 0.5f;
    private float orbRadius = 0.3f;
    private float animationPhase;
    private uint instanceSeed = 1u;
    private bool isBuilt;

    public bool IsReady => isBuilt;

    private void Awake()
    {
        ResolveReferences();
    }

    private void OnEnable()
    {
        if (!Application.isPlaying)
        {
            return;
        }

        ResolveReferences();
        if (!isBuilt)
        {
            BuildEffect();
        }
        else if (generatedRoot != null)
        {
            generatedRoot.SetActive(true);
            PlayParticleSystems();
        }

        // Never leave an invisible hazard if required VFX resources are missing.
        SetPlaceholderVisible(!isBuilt);
    }

    private void LateUpdate()
    {
        if (!isBuilt)
        {
            return;
        }

        RefreshGeometry(false);
        AnimateOrbs();
        AnimateBeam();
    }

    private void OnDisable()
    {
        if (!Application.isPlaying)
        {
            return;
        }

        if (generatedRoot != null)
        {
            generatedRoot.SetActive(false);
        }

        SetPlaceholderVisible(true);
    }

    private void OnDestroy()
    {
        SetPlaceholderVisible(true);

        if (generatedRoot != null)
        {
            if (Application.isPlaying)
            {
                Destroy(generatedRoot);
            }
            else
            {
                DestroyImmediate(generatedRoot);
            }
        }

        DestroyRuntimeAssets();
    }

    private void OnValidate()
    {
        orbDiameterMultiplier = Mathf.Max(0.5f, orbDiameterMultiplier);
        beamThicknessMultiplier = Mathf.Max(0.25f, beamThicknessMultiplier);
        density = Mathf.Clamp(density, 0.25f, 1.5f);

        if (sourceCollider == null)
        {
            sourceCollider = GetComponent<BoxCollider2D>();
        }
    }

    private void OnDrawGizmos()
    {
        if (Application.isPlaying)
        {
            return;
        }

        if (!TryCalculateGeometry(
                out Vector3 rawStart,
                out Vector3 rawEnd,
                out float thickness))
        {
            return;
        }

        Vector3 direction = (rawEnd - rawStart).normalized;
        float length = Vector3.Distance(rawStart, rawEnd);
        float radius = CalculateOrbRadius(length, thickness);
        Vector3 start = rawStart + direction * radius;
        Vector3 end = rawEnd - direction * radius;

        Gizmos.color = new Color(1f, 0.16f, 0.02f, 0.75f);
        Gizmos.DrawSphere(start, radius);
        Gizmos.DrawSphere(end, radius);
        Gizmos.color = new Color(1f, 0.7f, 0.08f, 0.85f);
        Gizmos.DrawLine(start, end);
    }

    /// <summary>
    /// Assigns a different source collider and immediately refits the effect.
    /// This is useful when a hazard prefab owns its trigger on a child object.
    /// </summary>
    public void Configure(BoxCollider2D colliderToFollow)
    {
        if (colliderToFollow == null)
        {
            return;
        }

        sourceCollider = colliderToFollow;
        if (isBuilt)
        {
            RefreshGeometry();
        }
        else if (isActiveAndEnabled)
        {
            BuildEffect();
        }
    }

    /// <summary>
    /// Recalculates the world-space endpoints and particle emission volumes.
    /// Normal movement and scaling are also detected automatically in LateUpdate.
    /// </summary>
    public void RefreshGeometry()
    {
        if (isBuilt)
        {
            RefreshGeometry(true);
            if (generatedRoot != null && generatedRoot.activeSelf)
            {
                AnimateOrbs();
                AnimateBeam();
            }
        }
    }

    private void ResolveReferences()
    {
        if (sourceCollider == null)
        {
            sourceCollider = GetComponent<BoxCollider2D>();
        }

        if (placeholderRenderer == null)
        {
            placeholderRenderer = GetComponent<Renderer>();
            if (placeholderRenderer != null)
            {
                placeholderWasEnabled = placeholderRenderer.enabled;
            }
        }
    }

    private void BuildEffect()
    {
        if (isBuilt || !Application.isPlaying || sourceCollider == null)
        {
            return;
        }

        if (!TryCalculateGeometry(
                out Vector3 initialStart,
                out Vector3 initialEnd,
                out _) ||
            (initialEnd - initialStart).sqrMagnitude <= Mathf.Epsilon)
        {
            Debug.LogWarning(
                $"{nameof(FireWallVFX)} needs a non-zero BoxCollider2D span.",
                this);
            return;
        }

        instanceSeed = CalculateStableSeed();
        animationPhase =
            (instanceSeed & 0xffffu) / 65535f * Mathf.PI * 2f;

        Material particleSource = particleMaterialSource != null
            ? particleMaterialSource
            : Resources.Load<Material>(ParticleMaterialResource);
        Material orbSource = orbMaterialSource != null
            ? orbMaterialSource
            : Resources.Load<Material>(OrbMaterialResource);

        Shader particleShader = particleSource != null
            ? particleSource.shader
            : Shader.Find(ParticleShaderName);
        Shader orbShader = orbSource != null
            ? orbSource.shader
            : Shader.Find(OrbShaderName);

        if (particleShader == null || orbShader == null)
        {
            Debug.LogWarning(
                $"{nameof(FireWallVFX)} could not find its URP VFX shaders.",
                this);
            return;
        }

        generatedRoot = new GameObject(name + GeneratedRootSuffix);
        generatedRoot.layer = gameObject.layer;
        SceneManager.MoveGameObjectToScene(generatedRoot, gameObject.scene);

        Texture2D flameTexture = CreateFlameTexture();
        Texture2D lineTexture = CreateBeamTexture();
        Texture2D emberTexture = CreateDiamondTexture();

        Material flameMaterial = CreateParticleMaterial(
            particleSource,
            particleShader,
            flameTexture,
            false,
            "Fire Wall Flame Material");
        Material emberMaterial = CreateParticleMaterial(
            particleSource,
            particleShader,
            emberTexture,
            true,
            "Fire Wall Ember Material");

        outlineLineMaterial = CreateParticleMaterial(
            particleSource,
            particleShader,
            lineTexture,
            false,
            "Fire Wall Outline Material");
        bodyLineMaterial = CreateParticleMaterial(
            particleSource,
            particleShader,
            lineTexture,
            false,
            "Fire Wall Body Material");
        coreLineMaterial = CreateParticleMaterial(
            particleSource,
            particleShader,
            lineTexture,
            true,
            "Fire Wall Core Material");

        Material orbShellMaterial = CreateOrbMaterial(
            orbSource,
            orbShader,
            outlineColor,
            outlineColor * 0.4f,
            "Fire Orb Shell Material");
        Material orbBodyMaterial = CreateOrbMaterial(
            orbSource,
            orbShader,
            orangeColor,
            orangeColor * 2.2f,
            "Fire Orb Body Material");
        Material orbHotMaterial = CreateOrbMaterial(
            orbSource,
            orbShader,
            hotColor,
            hotColor * 3.2f,
            "Fire Orb Hot Spot Material");

        outlineLine = CreateLineRenderer(
            "Dark Ember Outline",
            outlineLineMaterial,
            outlineColor,
            sortingOrder);
        bodyLine = CreateLineRenderer(
            "Molten Orange Body",
            bodyLineMaterial,
            orangeColor,
            sortingOrder + 1);
        coreLine = CreateLineRenderer(
            "White-Hot Core",
            coreLineMaterial,
            hotColor,
            sortingOrder + 2);

        beamFlameOutline = CreateParticleSystem(
            "Bridge Flame Outline",
            flameMaterial,
            sortingOrder + 3,
            CombineSeed(51031u));
        ConfigureBeamFlames(beamFlameOutline, outlineColor);

        beamFlameFill = CreateParticleSystem(
            "Bridge Flame Fill",
            flameMaterial,
            sortingOrder + 4,
            CombineSeed(51031u));
        ConfigureBeamFlames(beamFlameFill, deepRedColor);

        embers = CreateParticleSystem(
            "Flying Embers",
            emberMaterial,
            sortingOrder + 6,
            CombineSeed(76129u));
        ConfigureEmbers(embers);

        for (int i = 0; i < orbRoots.Length; i++)
        {
            CreateFireOrb(
                i,
                orbShellMaterial,
                orbBodyMaterial,
                orbHotMaterial);

            uint seed = CombineSeed((uint)(93001 + i * 1709));
            orbFlameOutlines[i] = CreateParticleSystem(
                $"Fire Orb {i + 1} Flame Outline",
                flameMaterial,
                sortingOrder + 3,
                seed);
            ConfigureOrbFlames(orbFlameOutlines[i], outlineColor);

            orbFlameFills[i] = CreateParticleSystem(
                $"Fire Orb {i + 1} Flame Fill",
                flameMaterial,
                sortingOrder + 4,
                seed);
            ConfigureOrbFlames(orbFlameFills[i], orangeColor);
        }

        isBuilt = true;
        RefreshGeometry(true);
        AnimateOrbs();
        AnimateBeam();
        SetPlaceholderVisible(false);
        PrefillAndPlayParticles();
    }

    private LineRenderer CreateLineRenderer(
        string objectName,
        Material material,
        Color color,
        int lineSortingOrder)
    {
        GameObject lineObject = new GameObject(objectName);
        lineObject.layer = gameObject.layer;
        lineObject.transform.SetParent(generatedRoot.transform, false);

        LineRenderer line = lineObject.AddComponent<LineRenderer>();
        line.sharedMaterial = material;
        line.useWorldSpace = true;
        line.loop = false;
        line.positionCount = BeamPointCount;
        line.textureMode = LineTextureMode.Tile;
        line.alignment = LineAlignment.View;
        line.numCapVertices = 5;
        line.numCornerVertices = 3;
        line.startColor = color;
        line.endColor = color;
        line.sortingLayerID = GetVisualSortingLayerId();
        line.sortingOrder = lineSortingOrder;
        line.shadowCastingMode = ShadowCastingMode.Off;
        line.receiveShadows = false;
        line.lightProbeUsage = LightProbeUsage.Off;
        line.reflectionProbeUsage = ReflectionProbeUsage.Off;
        line.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
        return line;
    }

    private ParticleSystem CreateParticleSystem(
        string objectName,
        Material material,
        int systemSortingOrder,
        uint randomSeed)
    {
        GameObject systemObject = new GameObject(objectName);
        systemObject.layer = gameObject.layer;
        systemObject.transform.SetParent(generatedRoot.transform, false);

        ParticleSystem particles = systemObject.AddComponent<ParticleSystem>();
        particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        particles.useAutoRandomSeed = false;
        particles.randomSeed = randomSeed;

        ParticleSystem.MainModule main = particles.main;
        main.playOnAwake = false;
        main.prewarm = false;
        main.loop = true;
        // The generated root has unit world scale, so local simulation keeps
        // flames attached to moving/rotating hazards without stretching them.
        main.simulationSpace = ParticleSystemSimulationSpace.Local;
        main.scalingMode = ParticleSystemScalingMode.Shape;
        main.gravityModifier = 0f;
        main.stopAction = ParticleSystemStopAction.None;

        ParticleSystemRenderer renderer =
            particles.GetComponent<ParticleSystemRenderer>();
        renderer.sharedMaterial = material;
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        renderer.alignment = ParticleSystemRenderSpace.View;
        renderer.sortMode = ParticleSystemSortMode.YoungestInFront;
        renderer.sortingLayerID = GetVisualSortingLayerId();
        renderer.sortingOrder = systemSortingOrder;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        renderer.lightProbeUsage = LightProbeUsage.Off;
        renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
        renderer.motionVectorGenerationMode =
            MotionVectorGenerationMode.ForceNoMotion;

        particleSystems.Add(particles);
        return particles;
    }

    private void CreateFireOrb(
        int index,
        Material shellMaterial,
        Material bodyMaterial,
        Material hotMaterial)
    {
        GameObject rootObject = new GameObject($"Fire Orb {index + 1}");
        rootObject.layer = gameObject.layer;
        rootObject.transform.SetParent(generatedRoot.transform, false);
        orbRoots[index] = rootObject.transform;

        orbShells[index] = CreateSphereLayer(
            "Dark Shell",
            rootObject.transform,
            shellMaterial,
            Vector3.zero,
            sortingOrder);
        orbBodies[index] = CreateSphereLayer(
            "Molten Body",
            rootObject.transform,
            bodyMaterial,
            Vector3.zero,
            sortingOrder + 1);
        orbHotSpots[index] = CreateSphereLayer(
            "Hot Spot",
            rootObject.transform,
            hotMaterial,
            Vector3.zero,
            sortingOrder + 2);
    }

    private Transform CreateSphereLayer(
        string objectName,
        Transform parent,
        Material material,
        Vector3 localPosition,
        int sphereSortingOrder)
    {
        GameObject sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        sphere.name = objectName;
        sphere.layer = gameObject.layer;
        sphere.transform.SetParent(parent, false);
        sphere.transform.localPosition = localPosition;

        Collider generatedCollider = sphere.GetComponent<Collider>();
        if (generatedCollider != null)
        {
            generatedCollider.enabled = false;
            Destroy(generatedCollider);
        }

        MeshRenderer renderer = sphere.GetComponent<MeshRenderer>();
        renderer.sharedMaterial = material;
        renderer.sortingLayerID = GetVisualSortingLayerId();
        renderer.sortingOrder = sphereSortingOrder;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        renderer.lightProbeUsage = LightProbeUsage.Off;
        renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
        renderer.motionVectorGenerationMode =
            MotionVectorGenerationMode.ForceNoMotion;
        return sphere.transform;
    }

    private void ConfigureBeamFlames(
        ParticleSystem particles,
        Color color)
    {
        ParticleSystem.MainModule main = particles.main;
        main.duration = 2f;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.34f, 0.62f);
        main.startSpeed = 0f;
        main.startRotation = new ParticleSystem.MinMaxCurve(
            -Mathf.PI,
            Mathf.PI);
        main.startColor = color;
        main.maxParticles = 90;

        ParticleSystem.ShapeModule shape = particles.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.randomDirectionAmount = 0f;

        ParticleSystem.VelocityOverLifetimeModule velocity =
            particles.velocityOverLifetime;
        velocity.enabled = true;
        velocity.space = ParticleSystemSimulationSpace.Local;
        SetMatchingVelocityCurves(
            velocity,
            new ParticleSystem.MinMaxCurve(-0.08f, 0.08f),
            new ParticleSystem.MinMaxCurve(-0.5f, 0.5f),
            new ParticleSystem.MinMaxCurve(0f, 0f));

        ParticleSystem.NoiseModule noise = particles.noise;
        noise.enabled = true;
        noise.separateAxes = true;
        noise.strengthX = 0.08f;
        noise.strengthY = 0.24f;
        noise.strengthZ = 0.02f;
        noise.frequency = 1.2f;
        noise.scrollSpeed = 1.25f;
        noise.damping = true;
        noise.octaveCount = 2;
        noise.octaveMultiplier = 0.45f;
        noise.quality = ParticleSystemNoiseQuality.Medium;

        ParticleSystem.ColorOverLifetimeModule colorOverLife =
            particles.colorOverLifetime;
        colorOverLife.enabled = true;
        colorOverLife.color = CreateFireFadeGradient(1f, 0.72f);

        ParticleSystem.SizeOverLifetimeModule sizeOverLife =
            particles.sizeOverLifetime;
        sizeOverLife.enabled = true;
        sizeOverLife.size = new ParticleSystem.MinMaxCurve(
            1f,
            CreateCurve(
                new Keyframe(0f, 0.52f),
                new Keyframe(0.28f, 1f),
                new Keyframe(0.72f, 0.76f),
                new Keyframe(1f, 0.18f)));

        ParticleSystem.RotationOverLifetimeModule rotation =
            particles.rotationOverLifetime;
        rotation.enabled = true;
        rotation.z = new ParticleSystem.MinMaxCurve(-2.2f, 2.2f);
    }

    private void ConfigureEmbers(ParticleSystem particles)
    {
        ParticleSystem.MainModule main = particles.main;
        main.duration = 2f;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.4f, 0.95f);
        main.startSpeed = 0f;
        main.startSize = new ParticleSystem.MinMaxCurve(0.035f, 0.095f);
        main.startRotation = new ParticleSystem.MinMaxCurve(
            -Mathf.PI,
            Mathf.PI);
        main.startColor = yellowColor;
        main.maxParticles = 70;

        ParticleSystem.ShapeModule shape = particles.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.randomDirectionAmount = 0f;

        ParticleSystem.VelocityOverLifetimeModule velocity =
            particles.velocityOverLifetime;
        velocity.enabled = true;
        velocity.space = ParticleSystemSimulationSpace.Local;
        SetMatchingVelocityCurves(
            velocity,
            new ParticleSystem.MinMaxCurve(-0.24f, 0.24f),
            new ParticleSystem.MinMaxCurve(-1.1f, 1.1f),
            new ParticleSystem.MinMaxCurve(0f, 0f));

        ParticleSystem.NoiseModule noise = particles.noise;
        noise.enabled = true;
        noise.separateAxes = true;
        noise.strengthX = 0.2f;
        noise.strengthY = 0.34f;
        noise.strengthZ = 0.02f;
        noise.frequency = 1.45f;
        noise.scrollSpeed = 1.4f;
        noise.damping = true;

        ParticleSystem.ColorOverLifetimeModule colorOverLife =
            particles.colorOverLifetime;
        colorOverLife.enabled = true;
        colorOverLife.color = CreateEmberGradient();

        ParticleSystem.SizeOverLifetimeModule sizeOverLife =
            particles.sizeOverLifetime;
        sizeOverLife.enabled = true;
        sizeOverLife.size = new ParticleSystem.MinMaxCurve(
            1f,
            CreateCurve(
                new Keyframe(0f, 0.35f),
                new Keyframe(0.18f, 1f),
                new Keyframe(1f, 0f)));

        ParticleSystem.RotationOverLifetimeModule rotation =
            particles.rotationOverLifetime;
        rotation.enabled = true;
        rotation.z = new ParticleSystem.MinMaxCurve(-5f, 5f);
    }

    private void ConfigureOrbFlames(
        ParticleSystem particles,
        Color color)
    {
        ParticleSystem.MainModule main = particles.main;
        main.duration = 2f;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.3f, 0.58f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.28f, 0.9f);
        main.startRotation = new ParticleSystem.MinMaxCurve(
            -Mathf.PI,
            Mathf.PI);
        main.startColor = color;
        main.maxParticles = 45;

        ParticleSystem.ShapeModule shape = particles.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radiusThickness = 0.55f;
        shape.randomDirectionAmount = 0.18f;

        ParticleSystem.NoiseModule noise = particles.noise;
        noise.enabled = true;
        noise.separateAxes = true;
        noise.strengthX = 0.18f;
        noise.strengthY = 0.18f;
        noise.strengthZ = 0.02f;
        noise.frequency = 1.1f;
        noise.scrollSpeed = 1.1f;
        noise.damping = true;

        ParticleSystem.ColorOverLifetimeModule colorOverLife =
            particles.colorOverLifetime;
        colorOverLife.enabled = true;
        colorOverLife.color = CreateFireFadeGradient(1f, 0.62f);

        ParticleSystem.SizeOverLifetimeModule sizeOverLife =
            particles.sizeOverLifetime;
        sizeOverLife.enabled = true;
        sizeOverLife.size = new ParticleSystem.MinMaxCurve(
            1f,
            CreateCurve(
                new Keyframe(0f, 0.42f),
                new Keyframe(0.3f, 1f),
                new Keyframe(1f, 0.12f)));

        ParticleSystem.RotationOverLifetimeModule rotation =
            particles.rotationOverLifetime;
        rotation.enabled = true;
        rotation.z = new ParticleSystem.MinMaxCurve(-2.8f, 2.8f);
    }

    private void RefreshGeometry(bool force)
    {
        if (!TryCalculateGeometry(
                out Vector3 rawStart,
                out Vector3 rawEnd,
                out float newThickness))
        {
            if (generatedRoot != null)
            {
                generatedRoot.SetActive(false);
            }

            SetPlaceholderVisible(true);
            return;
        }

        Vector3 direction = rawEnd - rawStart;
        float fullLength = direction.magnitude;
        if (fullLength <= Mathf.Epsilon)
        {
            if (generatedRoot != null)
            {
                generatedRoot.SetActive(false);
            }

            SetPlaceholderVisible(true);
            return;
        }

        if (generatedRoot != null && !generatedRoot.activeSelf)
        {
            generatedRoot.SetActive(true);
            PlayParticleSystems();
            force = true;
        }

        SetPlaceholderVisible(false);
        direction /= fullLength;
        float newOrbRadius = CalculateOrbRadius(fullLength, newThickness);
        Vector3 newStart = rawStart + direction * newOrbRadius;
        Vector3 newEnd = rawEnd - direction * newOrbRadius;
        newStart.z += visualDepth;
        newEnd.z += visualDepth;

        bool dimensionsChanged =
            force ||
            !Mathf.Approximately(hazardThickness, newThickness) ||
            !Mathf.Approximately(orbRadius, newOrbRadius) ||
            !Mathf.Approximately(beamLength, Vector3.Distance(newStart, newEnd));
        bool poseChanged =
            force ||
            (beamStart - newStart).sqrMagnitude > 0.000001f ||
            (beamEnd - newEnd).sqrMagnitude > 0.000001f;

        if (!dimensionsChanged && !poseChanged)
        {
            return;
        }

        beamStart = newStart;
        beamEnd = newEnd;
        beamDirection = (beamEnd - beamStart).normalized;
        beamPerpendicular =
            new Vector3(-beamDirection.y, beamDirection.x, 0f);
        beamLength = Vector3.Distance(beamStart, beamEnd);
        hazardThickness = newThickness;
        orbRadius = newOrbRadius;

        Vector3 midpoint = (beamStart + beamEnd) * 0.5f;
        float angle = Mathf.Atan2(beamDirection.y, beamDirection.x) *
            Mathf.Rad2Deg;
        Quaternion beamRotation = Quaternion.Euler(0f, 0f, angle);

        PositionBeamSystem(beamFlameOutline, midpoint, beamRotation);
        PositionBeamSystem(beamFlameFill, midpoint, beamRotation);
        PositionBeamSystem(embers, midpoint, beamRotation);

        orbRoots[0].position = beamStart;
        orbRoots[1].position = beamEnd;
        for (int i = 0; i < orbRoots.Length; i++)
        {
            orbRoots[i].rotation = Quaternion.identity;
            orbFlameOutlines[i].transform.position = orbRoots[i].position;
            orbFlameFills[i].transform.position = orbRoots[i].position;
        }

        if (dimensionsChanged)
        {
            ApplyBeamDimensions(beamFlameOutline, 1f);
            ApplyBeamDimensions(beamFlameFill, 0.78f);
            ApplyEmberDimensions();

            for (int i = 0; i < orbRoots.Length; i++)
            {
                ApplyOrbFlameDimensions(orbFlameOutlines[i], 1f);
                ApplyOrbFlameDimensions(orbFlameFills[i], 0.76f);
            }
        }

        float tiles = Mathf.Max(1f, beamLength / Mathf.Max(0.15f, hazardThickness));
        outlineLine.textureScale = new Vector2(tiles * 0.65f, 1f);
        bodyLine.textureScale = new Vector2(tiles * 0.9f, 1f);
        coreLine.textureScale = new Vector2(tiles * 1.15f, 1f);
    }

    private bool TryCalculateGeometry(
        out Vector3 rawStart,
        out Vector3 rawEnd,
        out float thickness)
    {
        rawStart = Vector3.zero;
        rawEnd = Vector3.zero;
        thickness = 0f;

        BoxCollider2D colliderToUse = sourceCollider != null
            ? sourceCollider
            : GetComponent<BoxCollider2D>();
        if (colliderToUse == null)
        {
            return false;
        }

        Transform colliderTransform = colliderToUse.transform;
        Vector3 xSpan = colliderTransform.TransformVector(
            Vector3.right * colliderToUse.size.x);
        Vector3 ySpan = colliderTransform.TransformVector(
            Vector3.up * colliderToUse.size.y);

        bool useLocalY = spanAxis == SpanAxis.LocalY ||
            (spanAxis == SpanAxis.Auto && ySpan.magnitude >= xSpan.magnitude);
        Vector3 span = useLocalY ? ySpan : xSpan;
        Vector3 crossSpan = useLocalY ? xSpan : ySpan;
        if (span.sqrMagnitude <= Mathf.Epsilon)
        {
            return false;
        }

        Vector3 center = colliderTransform.TransformPoint(colliderToUse.offset);
        Vector3 halfSpan = span * 0.5f;
        rawStart = center - halfSpan;
        rawEnd = center + halfSpan;
        thickness = Mathf.Max(0.08f, crossSpan.magnitude);
        return true;
    }

    private float CalculateOrbRadius(float fullLength, float thickness)
    {
        float desiredRadius =
            thickness * orbDiameterMultiplier * 0.5f;
        float maximumRadius = Mathf.Max(0.002f, fullLength * 0.22f);
        float minimumRadius = Mathf.Min(0.18f, maximumRadius);
        return Mathf.Clamp(desiredRadius, minimumRadius, maximumRadius);
    }

    private static void PositionBeamSystem(
        ParticleSystem particles,
        Vector3 position,
        Quaternion rotation)
    {
        if (particles != null)
        {
            particles.transform.SetPositionAndRotation(position, rotation);
        }
    }

    private void ApplyBeamDimensions(
        ParticleSystem particles,
        float sizeScale)
    {
        float visualThickness =
            hazardThickness * beamThicknessMultiplier;
        ParticleSystem.MainModule main = particles.main;
        main.startSize = new ParticleSystem.MinMaxCurve(
            visualThickness * 0.48f * sizeScale,
            visualThickness * 0.92f * sizeScale);
        main.maxParticles = Mathf.CeilToInt(90f * density);

        ParticleSystem.EmissionModule emission = particles.emission;
        emission.rateOverTime =
            Mathf.Clamp(beamLength * 7f, 9f, 26f) *
            density;

        ParticleSystem.ShapeModule shape = particles.shape;
        shape.scale = new Vector3(
            Mathf.Max(0.08f, beamLength * 0.94f),
            visualThickness * 0.28f,
            0.04f);

        ParticleSystem.VelocityOverLifetimeModule velocity =
            particles.velocityOverLifetime;
        SetMatchingVelocityCurves(
            velocity,
            new ParticleSystem.MinMaxCurve(
                -visualThickness * 0.22f,
                visualThickness * 0.22f),
            new ParticleSystem.MinMaxCurve(
                -visualThickness * 1.8f,
                visualThickness * 1.8f),
            new ParticleSystem.MinMaxCurve(0f, 0f));

        ParticleSystem.NoiseModule noise = particles.noise;
        noise.strengthX = visualThickness * 0.14f;
        noise.strengthY = visualThickness * 0.48f;
    }

    private void ApplyEmberDimensions()
    {
        float visualThickness =
            hazardThickness * beamThicknessMultiplier;
        ParticleSystem.MainModule main = embers.main;
        main.startSize = new ParticleSystem.MinMaxCurve(
            Mathf.Max(0.025f, visualThickness * 0.07f),
            Mathf.Max(0.065f, visualThickness * 0.18f));
        main.maxParticles = Mathf.CeilToInt(70f * density);

        ParticleSystem.EmissionModule emission = embers.emission;
        emission.rateOverTime =
            Mathf.Clamp(beamLength * 3.2f, 5f, 15f) * density;

        ParticleSystem.ShapeModule shape = embers.shape;
        shape.scale = new Vector3(
            Mathf.Max(0.08f, beamLength),
            visualThickness * 0.42f,
            0.04f);

        ParticleSystem.VelocityOverLifetimeModule velocity =
            embers.velocityOverLifetime;
        SetMatchingVelocityCurves(
            velocity,
            new ParticleSystem.MinMaxCurve(
                -visualThickness * 0.45f,
                visualThickness * 0.45f),
            new ParticleSystem.MinMaxCurve(
                -visualThickness * 2.6f,
                visualThickness * 2.6f),
            new ParticleSystem.MinMaxCurve(0f, 0f));
    }

    private void ApplyOrbFlameDimensions(
        ParticleSystem particles,
        float sizeScale)
    {
        ParticleSystem.MainModule main = particles.main;
        main.startSpeed = new ParticleSystem.MinMaxCurve(
            orbRadius * 0.8f,
            orbRadius * 2.4f);
        main.startSize = new ParticleSystem.MinMaxCurve(
            orbRadius * 0.62f * sizeScale,
            orbRadius * 1.15f * sizeScale);
        main.maxParticles = Mathf.CeilToInt(45f * density);

        ParticleSystem.EmissionModule emission = particles.emission;
        emission.rateOverTime = 10f * density;

        ParticleSystem.ShapeModule shape = particles.shape;
        shape.radius = orbRadius * 0.72f;
    }

    private void AnimateOrbs()
    {
        float diameter = orbRadius * 2f;
        for (int i = 0; i < orbRoots.Length; i++)
        {
            float phase = animationPhase + i * 1.73f;
            float pulse =
                1f +
                Mathf.Sin(Time.time * 5.1f + phase) * 0.045f +
                Mathf.Sin(Time.time * 8.7f + phase * 0.7f) * 0.025f;

            orbShells[i].localScale =
                Vector3.one * diameter * pulse;
            orbBodies[i].localScale =
                Vector3.one * diameter * 0.82f * (2f - pulse);
            orbHotSpots[i].localScale =
                Vector3.one * diameter * 0.38f *
                (1f + Mathf.Sin(Time.time * 7.4f + phase) * 0.08f);

            // Pull the smaller layers toward the camera far enough to emerge
            // from the opaque shell while preserving the 3D sphere silhouette.
            orbBodies[i].localPosition = new Vector3(
                0f,
                0f,
                -orbRadius * 0.34f);
            orbHotSpots[i].localPosition = new Vector3(
                -orbRadius * 0.24f,
                orbRadius * 0.28f,
                -orbRadius * 0.72f);
        }
    }

    private void AnimateBeam()
    {
        float time = Time.time + animationPhase;
        float outlineWidth = hazardThickness *
            beamThicknessMultiplier *
            (1.06f + Mathf.Sin(time * 5.2f) * 0.035f);
        float bodyWidth = hazardThickness *
            beamThicknessMultiplier *
            (0.71f + Mathf.Sin(time * 6.6f + 1.2f) * 0.045f);
        float coreWidth = hazardThickness *
            beamThicknessMultiplier *
            (0.25f + Mathf.Sin(time * 8.3f + 2.4f) * 0.025f);

        outlineLine.widthMultiplier = outlineWidth;
        bodyLine.widthMultiplier = bodyWidth;
        coreLine.widthMultiplier = coreWidth;

        for (int i = 0; i < BeamPointCount; i++)
        {
            float t = i / (BeamPointCount - 1f);
            float endpointEnvelope = Mathf.Sin(t * Mathf.PI);
            Vector3 basePoint = Vector3.Lerp(beamStart, beamEnd, t);
            float largeWave =
                Mathf.Sin(t * 13.4f + time * 4.7f) * 0.55f +
                Mathf.Sin(t * 27.1f - time * 6.1f) * 0.23f;
            float smallWave =
                Mathf.Sin(t * 35.7f + time * 8.2f) * 0.18f;

            float outlineOffset =
                largeWave *
                endpointEnvelope *
                hazardThickness *
                0.1f;
            float bodyOffset =
                (largeWave + smallWave) *
                endpointEnvelope *
                hazardThickness *
                0.075f;
            float coreOffset =
                (largeWave * 0.75f - smallWave) *
                endpointEnvelope *
                hazardThickness *
                0.045f;

            outlinePoints[i] =
                basePoint + beamPerpendicular * outlineOffset;
            bodyPoints[i] =
                basePoint + beamPerpendicular * bodyOffset;
            corePoints[i] =
                basePoint + beamPerpendicular * coreOffset;
        }

        outlineLine.SetPositions(outlinePoints);
        bodyLine.SetPositions(bodyPoints);
        coreLine.SetPositions(corePoints);

        AnimateTextureOffset(outlineLineMaterial, time * -0.34f);
        AnimateTextureOffset(bodyLineMaterial, time * -0.52f);
        AnimateTextureOffset(coreLineMaterial, time * -0.8f);
    }

    private static void AnimateTextureOffset(Material material, float xOffset)
    {
        Vector2 offset = new Vector2(xOffset, 0f);
        if (material.HasProperty("_BaseMap"))
        {
            material.SetTextureOffset("_BaseMap", offset);
        }

        if (material.HasProperty("_MainTex"))
        {
            material.SetTextureOffset("_MainTex", offset);
        }
    }

    private void PrefillAndPlayParticles()
    {
        for (int i = 0; i < particleSystems.Count; i++)
        {
            ParticleSystem particles = particleSystems[i];
            particles.Simulate(0.45f, false, true, true);
            particles.Play(false);
        }
    }

    private void PlayParticleSystems()
    {
        for (int i = 0; i < particleSystems.Count; i++)
        {
            if (particleSystems[i] != null &&
                !particleSystems[i].isPlaying)
            {
                particleSystems[i].Play(false);
            }
        }
    }

    private int GetVisualSortingLayerId()
    {
        return placeholderRenderer != null
            ? placeholderRenderer.sortingLayerID
            : 0;
    }

    private void SetPlaceholderVisible(bool visible)
    {
        if (placeholderRenderer == null)
        {
            return;
        }

        if (!visible && !hidePlaceholderRenderer)
        {
            return;
        }

        placeholderRenderer.enabled = visible && placeholderWasEnabled;
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

        SetTextureIfPresent(material, "_BaseMap", texture);
        SetTextureIfPresent(material, "_MainTex", texture);
        SetColorIfPresent(material, "_BaseColor", Color.white);
        SetColorIfPresent(material, "_Color", Color.white);
        SetFloatIfPresent(material, "_Surface", 1f);
        SetFloatIfPresent(material, "_Blend", additive ? 2f : 0f);
        SetFloatIfPresent(
            material,
            "_SrcBlend",
            (float)BlendMode.SrcAlpha);
        SetFloatIfPresent(
            material,
            "_DstBlend",
            (float)(additive ? BlendMode.One : BlendMode.OneMinusSrcAlpha));
        SetFloatIfPresent(material, "_SrcBlendAlpha", (float)BlendMode.One);
        SetFloatIfPresent(
            material,
            "_DstBlendAlpha",
            (float)(additive ? BlendMode.One : BlendMode.OneMinusSrcAlpha));
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

    private Material CreateOrbMaterial(
        Material source,
        Shader shader,
        Color baseColor,
        Color emissionColor,
        string materialName)
    {
        Material material = source != null
            ? new Material(source)
            : new Material(shader);
        material.name = materialName;
        material.hideFlags = HideFlags.HideAndDontSave;
        material.renderQueue = (int)RenderQueue.Geometry;

        SetColorIfPresent(material, "_BaseColor", baseColor);
        SetColorIfPresent(material, "_Color", baseColor);
        SetColorIfPresent(material, "_EmissionColor", emissionColor);
        SetFloatIfPresent(material, "_Surface", 0f);
        SetFloatIfPresent(material, "_ZWrite", 1f);
        SetFloatIfPresent(material, "_Metallic", 0.08f);
        SetFloatIfPresent(material, "_Smoothness", 0.52f);
        SetFloatIfPresent(material, "_ReceiveShadows", 0f);
        material.EnableKeyword("_EMISSION");
        material.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");
        material.SetOverrideTag("RenderType", "Opaque");

        runtimeAssets.Add(material);
        return material;
    }

    private Texture2D CreateFlameTexture()
    {
        const int size = 128;
        Texture2D texture = CreateRuntimeTexture(
            "Procedural Paper Flame",
            size,
            size);
        Color[] pixels = new Color[size * size];
        Vector3[] lobes =
        {
            new Vector3(-0.38f, -0.36f, 0.42f),
            new Vector3(0f, -0.3f, 0.53f),
            new Vector3(0.4f, -0.34f, 0.39f),
            new Vector3(-0.28f, 0.05f, 0.35f),
            new Vector3(0.05f, 0.16f, 0.41f),
            new Vector3(0.31f, 0.03f, 0.3f),
            new Vector3(-0.09f, 0.48f, 0.31f),
            new Vector3(0.12f, 0.7f, 0.2f)
        };

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                Vector2 point = new Vector2(
                    (x + 0.5f) / size * 2f - 1f,
                    (y + 0.5f) / size * 2f - 1f);
                float distance = float.MaxValue;

                for (int i = 0; i < lobes.Length; i++)
                {
                    Vector2 center = new Vector2(lobes[i].x, lobes[i].y);
                    distance = Mathf.Min(
                        distance,
                        Vector2.Distance(point, center) - lobes[i].z);
                }

                float alpha = 1f -
                    Mathf.SmoothStep(-0.018f, 0.055f, distance);
                pixels[y * size + x] =
                    new Color(1f, 1f, 1f, alpha);
            }
        }

        FinishRuntimeTexture(texture, pixels);
        return texture;
    }

    private Texture2D CreateBeamTexture()
    {
        const int width = 128;
        const int height = 32;
        Texture2D texture = CreateRuntimeTexture(
            "Animated Fire Beam",
            width,
            height);
        texture.wrapMode = TextureWrapMode.Repeat;
        Color[] pixels = new Color[width * height];

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                float px = (x + 0.5f) / width;
                float py = (y + 0.5f) / height * 2f - 1f;
                float centerFade =
                    1f - Mathf.SmoothStep(0.38f, 1f, Mathf.Abs(py));
                float ripple =
                    0.78f +
                    Mathf.Sin(px * Mathf.PI * 8f) * 0.12f +
                    Mathf.Sin(px * Mathf.PI * 18f + 1.3f) * 0.1f;
                float alpha = Mathf.Clamp01(centerFade * ripple);
                pixels[y * width + x] =
                    new Color(1f, 1f, 1f, alpha);
            }
        }

        FinishRuntimeTexture(texture, pixels);
        return texture;
    }

    private Texture2D CreateDiamondTexture()
    {
        const int size = 48;
        Texture2D texture = CreateRuntimeTexture(
            "Fire Ember Diamond",
            size,
            size);
        Color[] pixels = new Color[size * size];

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float px = (x + 0.5f) / size * 2f - 1f;
                float py = (y + 0.5f) / size * 2f - 1f;
                float diamond = Mathf.Abs(px) + Mathf.Abs(py);
                float alpha = 1f -
                    Mathf.SmoothStep(0.62f, 0.98f, diamond);
                pixels[y * size + x] =
                    new Color(1f, 1f, 1f, alpha);
            }
        }

        FinishRuntimeTexture(texture, pixels);
        return texture;
    }

    private Texture2D CreateRuntimeTexture(
        string textureName,
        int width,
        int height)
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

    private static void FinishRuntimeTexture(
        Texture2D texture,
        Color[] pixels)
    {
        texture.SetPixels(pixels);
        texture.Apply(false, true);
    }

    private static ParticleSystem.MinMaxGradient CreateFireFadeGradient(
        float peakAlpha,
        float middleAlpha)
    {
        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new[]
            {
                new GradientColorKey(Color.white, 0f),
                new GradientColorKey(new Color(1f, 0.66f, 0.3f), 0.58f),
                new GradientColorKey(new Color(0.62f, 0.08f, 0.025f), 1f)
            },
            new[]
            {
                new GradientAlphaKey(0f, 0f),
                new GradientAlphaKey(peakAlpha, 0.1f),
                new GradientAlphaKey(middleAlpha, 0.68f),
                new GradientAlphaKey(0f, 1f)
            });
        return new ParticleSystem.MinMaxGradient(gradient);
    }

    private ParticleSystem.MinMaxGradient CreateEmberGradient()
    {
        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new[]
            {
                new GradientColorKey(hotColor, 0f),
                new GradientColorKey(yellowColor, 0.38f),
                new GradientColorKey(orangeColor, 0.72f),
                new GradientColorKey(deepRedColor, 1f)
            },
            new[]
            {
                new GradientAlphaKey(0f, 0f),
                new GradientAlphaKey(1f, 0.08f),
                new GradientAlphaKey(0.86f, 0.62f),
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

    private uint CombineSeed(uint baseSeed)
    {
        uint combined = baseSeed ^ instanceSeed;
        return combined == 0u ? 0x6d2b79f5u : combined;
    }

    private uint CalculateStableSeed()
    {
        unchecked
        {
            const uint offsetBasis = 2166136261u;
            const uint prime = 16777619u;
            uint hash = offsetBasis;
            string identity = gameObject.scene.path + "/" + name;

            for (int i = 0; i < identity.Length; i++)
            {
                hash = (hash ^ identity[i]) * prime;
            }

            Vector3 position = transform.position;
            Vector3 scale = transform.lossyScale;
            hash = HashInt(hash, transform.GetSiblingIndex(), prime);
            hash = HashInt(
                hash,
                Mathf.RoundToInt(position.x * 1000f),
                prime);
            hash = HashInt(
                hash,
                Mathf.RoundToInt(position.y * 1000f),
                prime);
            hash = HashInt(
                hash,
                Mathf.RoundToInt(transform.eulerAngles.z * 100f),
                prime);
            hash = HashInt(
                hash,
                Mathf.RoundToInt(scale.x * 1000f),
                prime);
            hash = HashInt(
                hash,
                Mathf.RoundToInt(scale.y * 1000f),
                prime);
            return hash == 0u ? 1u : hash;
        }
    }

    private static uint HashInt(uint hash, int value, uint prime)
    {
        unchecked
        {
            return (hash ^ (uint)value) * prime;
        }
    }

    private static void SetMatchingVelocityCurves(
        ParticleSystem.VelocityOverLifetimeModule velocity,
        ParticleSystem.MinMaxCurve x,
        ParticleSystem.MinMaxCurve y,
        ParticleSystem.MinMaxCurve z)
    {
        // Unity requires all three axes to use the same MinMaxCurve mode.
        // Every caller supplies two-constant curves, including fixed zero axes.
        velocity.x = x;
        velocity.y = y;
        velocity.z = z;
    }

    private static void SetTextureIfPresent(
        Material material,
        string property,
        Texture texture)
    {
        if (material.HasProperty(property))
        {
            material.SetTexture(property, texture);
        }
    }

    private static void SetColorIfPresent(
        Material material,
        string property,
        Color color)
    {
        if (material.HasProperty(property))
        {
            material.SetColor(property, color);
        }
    }

    private static void SetFloatIfPresent(
        Material material,
        string property,
        float value)
    {
        if (material.HasProperty(property))
        {
            material.SetFloat(property, value);
        }
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
