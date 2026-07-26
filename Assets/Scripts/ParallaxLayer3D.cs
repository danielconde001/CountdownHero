using UnityEngine;

/// <summary>
/// Moves a decorative 3D background layer at a fraction of the camera movement.
/// Lower factors feel farther away; higher factors feel closer to the play plane.
/// </summary>
public class ParallaxLayer3D : MonoBehaviour
{
    [Header("Camera")]
    [SerializeField] private Transform cameraTarget;
    [SerializeField] private bool findMainCamera = true;

    [Header("Parallax")]
    [SerializeField, Range(0f, 1f)] private float horizontalFactor = 0.25f;
    [SerializeField, Range(0f, 1f)] private float verticalFactor = 0.08f;
    [SerializeField] private bool lockInitialZ = true;

    [Header("Ambient Motion")]
    [Tooltip("Small looping drift that keeps distant clouds/foliage from feeling frozen.")]
    [SerializeField] private Vector2 swayAmplitude;
    [SerializeField, Min(0f)] private float swayFrequency = 0.15f;
    [SerializeField] private float swayPhase;

    private Vector3 initialLayerPosition;
    private Vector3 initialCameraPosition;
    private bool hasInitialState;

    private void OnEnable()
    {
        ResolveCameraTarget();
        CaptureInitialState();
    }

    private void LateUpdate()
    {
        if (cameraTarget == null)
        {
            ResolveCameraTarget();
        }

        if (cameraTarget == null)
        {
            return;
        }

        if (!hasInitialState)
        {
            CaptureInitialState();
        }

        Vector3 cameraDelta = cameraTarget.position - initialCameraPosition;
        Vector3 nextPosition = new Vector3(
            initialLayerPosition.x + cameraDelta.x * horizontalFactor,
            initialLayerPosition.y + cameraDelta.y * verticalFactor,
            lockInitialZ
                ? initialLayerPosition.z
                : initialLayerPosition.z + cameraDelta.z);

        if (swayAmplitude != Vector2.zero && swayFrequency > 0f)
        {
            float time = Time.time * swayFrequency + swayPhase;
            nextPosition.x += Mathf.Sin(time) * swayAmplitude.x;
            nextPosition.y += Mathf.Cos(time) * swayAmplitude.y;
        }

        transform.position = nextPosition;
    }

    /// <summary>
    /// Used by editor setup tools so designers can regenerate a consistent
    /// parallax background without manually filling every serialized field.
    /// </summary>
    public void Configure(
        float horizontal,
        float vertical,
        Vector2 sway,
        float frequency,
        float phase)
    {
        horizontalFactor = horizontal;
        verticalFactor = vertical;
        swayAmplitude = sway;
        swayFrequency = frequency;
        swayPhase = phase;
        hasInitialState = false;
    }

    private void ResolveCameraTarget()
    {
        if (cameraTarget != null || !findMainCamera)
        {
            return;
        }

        Camera mainCamera = Camera.main;
        cameraTarget = mainCamera != null ? mainCamera.transform : null;
    }

    private void CaptureInitialState()
    {
        if (cameraTarget == null)
        {
            hasInitialState = false;
            return;
        }

        initialLayerPosition = transform.position;
        initialCameraPosition = cameraTarget.position;
        hasInitialState = true;
    }
}
