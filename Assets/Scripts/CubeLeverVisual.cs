using DG.Tweening;
using UnityEngine;

/// <summary>
/// Builds a readable prototype lever from cube primitives and animates its
/// handle between ready and engaged states. Generated cubes are visual-only.
/// </summary>
[DisallowMultipleComponent]
public sealed class CubeLeverVisual : MonoBehaviour
{
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorId = Shader.PropertyToID("_Color");

    [Header("Shape")]
    [Tooltip("Local offset from the Switch Body. Negative Z places the lever toward the camera.")]
    [SerializeField] private Vector3 pivotPosition = new Vector3(0f, 0.2f, -0.65f);
    [SerializeField] private Vector3 pivotScale = new Vector3(0.49f, 0.32f, 0.5f);
    [SerializeField] private Vector3 handlePosition = new Vector3(0f, 0.48f, 0f);
    [SerializeField] private Vector3 handleScale = new Vector3(0.185f, 0.9f, 0.28f);
    [SerializeField] private Vector3 knobPosition = new Vector3(0f, 0.96f, 0f);
    [SerializeField] private Vector3 knobScale = new Vector3(0.43f, 0.25f, 0.44f);

    [Header("Motion")]
    [SerializeField] private float readyAngle = 30f;
    [SerializeField] private float engagedAngle = -30f;
    [SerializeField, Min(0f)] private float moveDuration = 0.22f;
    [SerializeField] private Ease moveEase = Ease.OutBack;

    [Header("Color Coding")]
    [SerializeField] private Color pivotColor = new Color(1f, 0.78f, 0.12f, 1f);
    [SerializeField] private Color readyHandleColor = new Color(0.9f, 0.18f, 0.18f, 1f);
    [SerializeField] private Color engagedHandleColor = new Color(0.2f, 0.85f, 0.3f, 1f);
    [SerializeField] private Color knobColor = new Color(1f, 0.92f, 0.25f, 1f);

    private Transform leverPivot;
    private Renderer handleRenderer;
    private Renderer pivotRenderer;
    private Renderer knobRenderer;
    private Sequence stateSequence;
    private Color currentHandleColor;
    private bool hasState;
    private bool isEngaged;

    private void Awake()
    {
        BuildLever();
        SetEngaged(false, true);
    }

    private void OnDisable()
    {
        KillStateAnimation();
    }

    /// <summary>Moves and recolors the handle only when the state changes.</summary>
    public void SetEngaged(bool engaged, bool instant = false)
    {
        BuildLever();

        if (hasState && isEngaged == engaged && !instant)
        {
            return;
        }

        hasState = true;
        isEngaged = engaged;
        KillStateAnimation();

        float targetAngle = engaged ? engagedAngle : readyAngle;
        Color targetColor = engaged ? engagedHandleColor : readyHandleColor;
        float duration = instant ? 0f : Mathf.Max(0f, moveDuration);

        if (duration <= 0f)
        {
            leverPivot.localRotation = Quaternion.Euler(0f, 0f, targetAngle);
            currentHandleColor = targetColor;
            ApplyColor(handleRenderer, currentHandleColor);
            return;
        }

        stateSequence = DOTween.Sequence();
        stateSequence.Join(
            leverPivot.DOLocalRotate(new Vector3(0f, 0f, targetAngle), duration)
                .SetEase(moveEase));
        stateSequence.Join(
            DOTween.To(
                    () => currentHandleColor,
                    color =>
                    {
                        currentHandleColor = color;
                        ApplyColor(handleRenderer, color);
                    },
                    targetColor,
                    duration)
                .SetEase(Ease.InOutQuad));
        stateSequence
            .SetTarget(this)
            .OnComplete(() => stateSequence = null);
    }

    private void BuildLever()
    {
        if (leverPivot != null)
        {
            return;
        }

        Transform existingBase = transform.Find("Switch Body");
        if (existingBase == null)
        {
            existingBase = CreateCube(
                "Switch Body",
                transform,
                new Vector3(0f, 0f, 0.2f),
                new Vector3(0.65f, 1f, 0.5f)).transform;
        }

        leverPivot = existingBase.Find("Lever Pivot");
        if (leverPivot == null)
        {
            var pivotObject = new GameObject("Lever Pivot");
            leverPivot = pivotObject.transform;
            // Parenting the whole assembly to the visual body makes designer
            // position, rotation, and scale changes carry the lever automatically.
            leverPivot.SetParent(existingBase, false);
            leverPivot.localPosition = pivotPosition;

            GameObject pivotCube = CreateCube("Pivot Cube", leverPivot, Vector3.zero, pivotScale);
            GameObject handleCube = CreateCube("Handle Cube", leverPivot, handlePosition, handleScale);
            GameObject knobCube = CreateCube("Knob Cube", leverPivot, knobPosition, knobScale);

            pivotRenderer = pivotCube.GetComponent<Renderer>();
            handleRenderer = handleCube.GetComponent<Renderer>();
            knobRenderer = knobCube.GetComponent<Renderer>();
        }
        else
        {
            pivotRenderer = FindRenderer(leverPivot, "Pivot Cube");
            handleRenderer = FindRenderer(leverPivot, "Handle Cube");
            knobRenderer = FindRenderer(leverPivot, "Knob Cube");
        }

        ApplyColor(pivotRenderer, pivotColor);
        ApplyColor(knobRenderer, knobColor);
        currentHandleColor = readyHandleColor;
        ApplyColor(handleRenderer, currentHandleColor);
    }

    private static Renderer FindRenderer(Transform parent, string childName)
    {
        Transform child = parent.Find(childName);
        return child != null ? child.GetComponent<Renderer>() : null;
    }

    private static GameObject CreateCube(
        string objectName,
        Transform parent,
        Vector3 localPosition,
        Vector3 localScale)
    {
        GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
        cube.name = objectName;
        cube.transform.SetParent(parent, false);
        cube.transform.localPosition = localPosition;
        cube.transform.localRotation = Quaternion.identity;
        cube.transform.localScale = localScale;

        // The parent TimedSwitch owns the 2D interaction trigger. Keeping the
        // primitive's generated 3D collider would create misleading physics.
        Collider cubeCollider = cube.GetComponent<Collider>();
        if (cubeCollider != null)
        {
            Destroy(cubeCollider);
        }

        return cube;
    }

    private static void ApplyColor(Renderer targetRenderer, Color color)
    {
        if (targetRenderer == null)
        {
            return;
        }

        var properties = new MaterialPropertyBlock();
        targetRenderer.GetPropertyBlock(properties);
        properties.SetColor(BaseColorId, color);
        properties.SetColor(ColorId, color);
        targetRenderer.SetPropertyBlock(properties);
    }

    private void KillStateAnimation()
    {
        if (stateSequence == null)
        {
            return;
        }

        stateSequence.Kill();
        stateSequence = null;
    }
}
