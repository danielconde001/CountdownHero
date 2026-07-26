using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// One-shot paper-smoke poof used when a countdown platform appears or disappears.
///
/// The effect is generated at runtime from ParticleSystems so the prefab stays
/// lightweight and can reuse the same puff sprites/material source as the
/// geyser wind without hand-authoring fragile particle YAML.
/// </summary>
[DisallowMultipleComponent]
public sealed class PlatformPoofVFX : MonoBehaviour
{
    private const string GeneratedRootName = "Generated Poof Layers";
    private const string UrpParticleShader = "Universal Render Pipeline/Particles/Unlit";

    [Header("Paper Smoke")]
    [SerializeField] private Color outlineColor = new Color(0.035f, 0.04f, 0.06f, 0.56f);
    [SerializeField] private Color smokeColor = new Color(0.86f, 0.88f, 0.84f, 0.72f);
    [SerializeField] private Color highlightColor = new Color(1f, 0.98f, 0.84f, 0.65f);

    [Header("Paper Puff Sprites")]
    [Tooltip("Main smoke silhouette. A procedural cloud is used if this is unassigned.")]
    [SerializeField] private Sprite primaryPuffSprite;
    [Tooltip("Adds a little shape variation to the smoke burst.")]
    [SerializeField] private Sprite secondaryPuffSprite;

    [Header("Rendering")]
    [Tooltip("Keeps the URP particle shader and transparent settings in player builds.")]
    [SerializeField] private Material particleMaterialSource;
    [SerializeField] private int sortingOrder = 12;
    [SerializeField] private float visualDepth = -0.16f;

    [Header("Shape")]
    [SerializeField, Min(0.1f)] private float radius = 0.72f;
    [SerializeField, Min(1)] private int puffCount = 20;
    [SerializeField, Min(0.1f)] private float lifetime = 0.72f;
    [SerializeField, Min(0f)] private float destroyPadding = 0.3f;

    private readonly List<Object> runtimeAssets = new List<Object>();
    private Transform generatedRoot;

    private void OnEnable()
    {
        if (Application.isPlaying)
        {
            Play();
        }
    }

    private void OnDestroy()
    {
        DestroyRuntimeAssets();
    }

    /// <summary>Builds and plays the poof once, then destroys this object.</summary>
    public void Play()
    {
        ClearGeneratedRoot();

        Shader particleShader = particleMaterialSource != null
            ? particleMaterialSource.shader
            : null;
        if (particleShader == null)
        {
            particleShader = Shader.Find(UrpParticleShader);
        }

        if (particleShader == null)
        {
            Debug.LogWarning($"{nameof(PlatformPoofVFX)} could not find a compatible particle shader.", this);
            DestroySelf(destroyPadding);
            return;
        }

        CreateGeneratedRoot();

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

        Material primaryMaterial = CreateParticleMaterial(
            particleMaterialSource,
            particleShader,
            primaryTexture,
            "Platform Poof Primary Material");
        Material secondaryMaterial = CreateParticleMaterial(
            particleMaterialSource,
            particleShader,
            secondaryTexture,
            "Platform Poof Secondary Material");

        ParticleSystem outline = CreateParticleSystem(
            "Smoke Outline",
            primaryMaterial,
            sortingOrder,
            24137u);
        ConfigureSmoke(outline, outlineColor, 1.16f, puffCount);

        ParticleSystem fill = CreateParticleSystem(
            "Smoke Fill",
            primaryMaterial,
            sortingOrder + 1,
            24137u);
        ConfigureSmoke(fill, smokeColor, 0.95f, puffCount);

        ParticleSystem highlights = CreateParticleSystem(
            "Smoke Highlights",
            secondaryMaterial,
            sortingOrder + 2,
            81929u);
        ConfigureSmoke(highlights, highlightColor, 0.62f, Mathf.Max(4, puffCount / 3));

        outline.Play(true);
        fill.Play(true);
        highlights.Play(true);

        DestroySelf(lifetime + destroyPadding);
    }

    private void CreateGeneratedRoot()
    {
        GameObject rootObject = new GameObject(GeneratedRootName);
        rootObject.transform.SetParent(transform, false);
        rootObject.transform.localPosition = new Vector3(0f, 0f, visualDepth);
        generatedRoot = rootObject.transform;
    }

    private ParticleSystem CreateParticleSystem(
        string objectName,
        Material material,
        int systemSortingOrder,
        uint randomSeed)
    {
        GameObject systemObject = new GameObject(objectName);
        systemObject.transform.SetParent(generatedRoot, false);

        ParticleSystem particles = systemObject.AddComponent<ParticleSystem>();
        particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        particles.useAutoRandomSeed = false;
        particles.randomSeed = randomSeed;

        ParticleSystem.MainModule main = particles.main;
        main.playOnAwake = false;
        main.prewarm = false;
        main.loop = false;
        main.duration = 0.18f;
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

        return particles;
    }

    private void ConfigureSmoke(
        ParticleSystem particles,
        Color color,
        float sizeScale,
        int burstCount)
    {
        ParticleSystem.MainModule main = particles.main;
        main.startLifetime = new ParticleSystem.MinMaxCurve(lifetime * 0.68f, lifetime);
        main.startSpeed = new ParticleSystem.MinMaxCurve(radius * 0.8f, radius * 2.15f);
        main.startSize = new ParticleSystem.MinMaxCurve(radius * 0.42f * sizeScale, radius * 0.8f * sizeScale);
        main.startRotation = new ParticleSystem.MinMaxCurve(-Mathf.PI, Mathf.PI);
        main.startColor = color;
        main.maxParticles = burstCount;

        ParticleSystem.EmissionModule emission = particles.emission;
        emission.rateOverTime = 0f;
        emission.SetBursts(new[]
        {
            new ParticleSystem.Burst(0f, (short)burstCount)
        });

        ParticleSystem.ShapeModule shape = particles.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = radius * 0.18f;
        shape.randomDirectionAmount = 0.68f;

        ParticleSystem.NoiseModule noise = particles.noise;
        noise.enabled = true;
        noise.separateAxes = true;
        noise.strengthX = radius * 0.34f;
        noise.strengthY = radius * 0.26f;
        noise.strengthZ = radius * 0.08f;
        noise.frequency = 0.9f;
        noise.scrollSpeed = 0.62f;
        noise.damping = true;
        noise.quality = ParticleSystemNoiseQuality.Medium;

        ParticleSystem.ColorOverLifetimeModule colorOverLife = particles.colorOverLifetime;
        colorOverLife.enabled = true;
        colorOverLife.color = CreateFadeGradient(0.9f, 0.58f);

        ParticleSystem.SizeOverLifetimeModule sizeOverLife = particles.sizeOverLifetime;
        sizeOverLife.enabled = true;
        sizeOverLife.size = new ParticleSystem.MinMaxCurve(
            1f,
            CreateCurve(
                new Keyframe(0f, 0.42f),
                new Keyframe(0.22f, 1.08f),
                new Keyframe(1f, 1.34f)));

        ParticleSystem.RotationOverLifetimeModule rotation = particles.rotationOverLifetime;
        rotation.enabled = true;
        rotation.z = new ParticleSystem.MinMaxCurve(-0.9f, 0.9f);
    }

    private Material CreateParticleMaterial(
        Material source,
        Shader shader,
        Texture texture,
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
        SetFloatIfPresent(material, "_Blend", 0f);
        SetFloatIfPresent(material, "_SrcBlend", (float)BlendMode.SrcAlpha);
        SetFloatIfPresent(material, "_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
        SetFloatIfPresent(material, "_SrcBlendAlpha", (float)BlendMode.One);
        SetFloatIfPresent(material, "_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
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

    private Texture2D CreateFallbackPuffTexture()
    {
        const int size = 128;
        Texture2D texture = CreateRuntimeTexture("Procedural Platform Poof Puff", size, size);
        Color[] pixels = new Color[size * size];
        Vector3[] circles =
        {
            new Vector3(-0.32f, 0.05f, 0.45f),
            new Vector3(0.03f, 0.22f, 0.52f),
            new Vector3(0.38f, -0.02f, 0.42f),
            new Vector3(-0.02f, -0.32f, 0.5f)
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
                    distance = Mathf.Min(distance, Vector2.Distance(point, center) - circles[i].z);
                }

                float alpha = 1f - Mathf.SmoothStep(-0.015f, 0.06f, distance);
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

    private void ClearGeneratedRoot()
    {
        if (generatedRoot == null)
        {
            return;
        }

        DestroyUnityObject(generatedRoot.gameObject);
        generatedRoot = null;
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

            DestroyUnityObject(asset);
        }

        runtimeAssets.Clear();
    }

    private void DestroySelf(float delay)
    {
        if (Application.isPlaying)
        {
            Destroy(gameObject, delay);
        }
        else
        {
            DestroyImmediate(gameObject);
        }
    }

    private static void DestroyUnityObject(Object target)
    {
        if (Application.isPlaying)
        {
            Destroy(target);
        }
        else
        {
            DestroyImmediate(target);
        }
    }

    private static void SetFloatIfPresent(Material material, string property, float value)
    {
        if (material.HasProperty(property))
        {
            material.SetFloat(property, value);
        }
    }

    private static Texture GetSpriteTexture(Sprite sprite)
    {
        return sprite != null ? sprite.texture : null;
    }

    private static void FinishRuntimeTexture(Texture2D texture, Color[] pixels)
    {
        texture.SetPixels(pixels);
        texture.Apply(false, true);
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
                new GradientAlphaKey(peakAlpha, 0.08f),
                new GradientAlphaKey(middleAlpha, 0.58f),
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
}
