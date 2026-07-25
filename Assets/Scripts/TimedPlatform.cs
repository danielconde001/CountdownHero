using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Runs a countdown-gated platform behavior such as appearing, moving, rotating,
/// scaling, or firing designer-wired events.
/// </summary>
public class TimedPlatform : SwitchTarget
{
    public enum BehaviorMode
    {
        ToggleVisibility,
        Move,
        Rotate,
        Scale,
        Custom
    }

    [Header("Timing")]
    [SerializeField, Min(0f)] private float countdownDuration = 1f;
    [SerializeField, Min(0f)] private float activeDuration = 3f;
    [Tooltip("The state used when the scene starts. Activation temporarily switches to the opposite state.")]
    [SerializeField] private bool startActive;

    [Header("Behavior")]
    [SerializeField] private BehaviorMode mode = BehaviorMode.ToggleVisibility;

    [Header("Move")]
    [SerializeField] private Vector3 moveTarget;
    [SerializeField, Min(0f)] private float moveDuration = 1f;

    [Header("Rotate")]
    [SerializeField] private Vector3 rotateTarget;
    [SerializeField, Min(0f)] private float rotateDuration = 1f;

    [Header("Scale")]
    [SerializeField] private Vector3 scaleTarget = Vector3.one;
    [SerializeField, Min(0f)] private float scaleDuration = 1f;

    [Header("Feedback")]
    [SerializeField] private TextMesh countdownDisplay;

    [Header("Events")]
    [SerializeField] private UnityEvent onActivated = new UnityEvent();
    [SerializeField] private UnityEvent onDeactivated = new UnityEvent();

    private bool isSequenceRunning;
    private Coroutine sequenceRoutine;
    private Renderer[] platformRenderers;
    private Collider2D[] platformColliders;
    private Renderer countdownRenderer;
    private readonly List<Rigidbody2D> invalidPassengers = new List<Rigidbody2D>();
    private Vector3 originalPosition;
    private Quaternion originalRotation;
    private Vector3 originalScale;
    private readonly HashSet<Rigidbody2D> passengers = new HashSet<Rigidbody2D>();

    public override bool IsActivationRunning => isSequenceRunning;

    private void Awake()
    {
        platformRenderers = GetComponentsInChildren<Renderer>(true);
        platformColliders = GetComponentsInChildren<Collider2D>(true);
        countdownRenderer = countdownDisplay != null
            ? countdownDisplay.GetComponent<Renderer>()
            : null;
        TextMeshFontUtility.ApplyFontMaterial(countdownDisplay);
        originalPosition = transform.position;
        originalRotation = transform.rotation;
        originalScale = transform.localScale;

        if (mode == BehaviorMode.ToggleVisibility
            && platformRenderers.Length == 0
            && platformColliders.Length == 0)
        {
            Debug.LogWarning($"{name}: TimedPlatform ToggleVisibility has no child renderers or Collider2D components.");
        }

        ApplyInstantState(startActive);
        ClearCountdownDisplay();
    }

    private void OnDisable()
    {
        if (sequenceRoutine != null)
        {
            StopCoroutine(sequenceRoutine);
            sequenceRoutine = null;
        }

        isSequenceRunning = false;
        passengers.Clear();
        ClearCountdownDisplay();
    }

    /// <summary>Starts the countdown sequence unless this platform is already busy.</summary>
    public override void Activate()
    {
        if (isSequenceRunning)
        {
            return;
        }

        isSequenceRunning = true;
        sequenceRoutine = StartCoroutine(Sequence());
    }

    public void Initialize(
        float countdown,
        float activeTime,
        bool initiallyActive,
        BehaviorMode behaviorMode,
        TextMesh display = null)
    {
        countdownDuration = countdown;
        activeDuration = activeTime;
        startActive = initiallyActive;
        mode = behaviorMode;
        countdownDisplay = display;
    }

    private IEnumerator Sequence()
    {
        yield return PlayCountdown();

        if (startActive)
        {
            yield return RunBehaviorTransition(false);
            onDeactivated.Invoke();
            yield return WaitForDuration(activeDuration);
            yield return RunBehaviorTransition(true);
            onActivated.Invoke();
        }
        else
        {
            yield return RunBehaviorTransition(true);
            onActivated.Invoke();
            yield return WaitForDuration(activeDuration);
            yield return RunBehaviorTransition(false);
            onDeactivated.Invoke();
        }

        isSequenceRunning = false;
        sequenceRoutine = null;
    }

    private IEnumerator PlayCountdown()
    {
        float duration = Mathf.Max(0f, countdownDuration);
        if (countdownDisplay == null)
        {
            yield return WaitForDuration(duration);
            yield break;
        }

        if (duration <= 0f)
        {
            countdownDisplay.text = "GO!";
            yield return null;
            ClearCountdownDisplay();
            yield break;
        }

        for (float remaining = duration; remaining > 0f; remaining -= Time.deltaTime)
        {
            countdownDisplay.text = Mathf.CeilToInt(remaining).ToString();
            yield return null;
        }

        countdownDisplay.text = "GO!";
        yield return null;
        ClearCountdownDisplay();
    }

    private IEnumerator RunBehaviorTransition(bool active)
    {
        switch (mode)
        {
            case BehaviorMode.ToggleVisibility:
                SetPlatformVisible(active);
                yield break;
            case BehaviorMode.Move:
                yield return MoveTween(active);
                yield break;
            case BehaviorMode.Rotate:
                yield return RotateTween(active);
                yield break;
            case BehaviorMode.Scale:
                yield return ScaleTween(active);
                yield break;
            case BehaviorMode.Custom:
                yield break;
        }
    }

    private IEnumerator MoveTween(bool active)
    {
        Vector3 start = transform.position;
        Vector3 end = active ? moveTarget : originalPosition;
        float duration = Mathf.Max(0f, moveDuration);
        yield return Lerp(duration, progress => MovePlatform(Vector3.Lerp(start, end, progress)));
        MovePlatform(end);
    }

    private IEnumerator RotateTween(bool active)
    {
        Quaternion start = transform.rotation;
        Quaternion end = active ? Quaternion.Euler(rotateTarget) : originalRotation;
        float duration = Mathf.Max(0f, rotateDuration);
        yield return Lerp(duration, progress => transform.rotation = Quaternion.Slerp(start, end, progress));
        transform.rotation = end;
    }

    private IEnumerator ScaleTween(bool active)
    {
        Vector3 start = transform.localScale;
        Vector3 end = active ? scaleTarget : originalScale;
        float duration = Mathf.Max(0f, scaleDuration);
        yield return Lerp(duration, progress => transform.localScale = Vector3.Lerp(start, end, progress));
        transform.localScale = end;
    }

    private static IEnumerator Lerp(float duration, System.Action<float> applyProgress)
    {
        if (duration <= 0f)
        {
            applyProgress(1f);
            yield break;
        }

        for (float elapsed = 0f; elapsed < duration; elapsed += Time.deltaTime)
        {
            applyProgress(Mathf.Clamp01(elapsed / duration));
            yield return null;
        }

        applyProgress(1f);
    }

    private static IEnumerator WaitForDuration(float duration)
    {
        duration = Mathf.Max(0f, duration);
        if (duration > 0f)
        {
            yield return new WaitForSeconds(duration);
        }
    }

    private void ApplyInstantState(bool active)
    {
        switch (mode)
        {
            case BehaviorMode.ToggleVisibility:
                SetPlatformVisible(active);
                break;
            case BehaviorMode.Move:
                MovePlatform(active ? moveTarget : originalPosition);
                break;
            case BehaviorMode.Rotate:
                transform.rotation = active ? Quaternion.Euler(rotateTarget) : originalRotation;
                break;
            case BehaviorMode.Scale:
                transform.localScale = active ? scaleTarget : originalScale;
                break;
            case BehaviorMode.Custom:
                break;
        }
    }

    private void SetPlatformVisible(bool visible)
    {
        foreach (Renderer platformRenderer in platformRenderers)
        {
            // Countdown feedback must remain visible while an appearing platform is hidden.
            if (platformRenderer != null && platformRenderer != countdownRenderer)
            {
                platformRenderer.enabled = visible;
            }
        }

        foreach (Collider2D platformCollider in platformColliders)
        {
            if (platformCollider != null)
            {
                platformCollider.enabled = visible;
            }
        }
    }

    private void ClearCountdownDisplay()
    {
        if (countdownDisplay != null)
        {
            countdownDisplay.text = string.Empty;
        }
    }

    private void MovePlatform(Vector3 nextPosition)
    {
        Vector3 delta = nextPosition - transform.position;
        transform.position = nextPosition;

        if (delta == Vector3.zero)
        {
            return;
        }

        // Move platforms are animated by changing this transform directly.
        // Rigidbody2D passengers do not inherit that motion automatically, so
        // apply the same delta to keep the player planted on top.
        foreach (Rigidbody2D passenger in passengers)
        {
            if (passenger == null)
            {
                invalidPassengers.Add(passenger);
            }
            else
            {
                passenger.position += (Vector2)delta;
            }
        }

        // Unity can destroy objects between collision callbacks; prune stale
        // entries outside the main loop so the HashSet is not modified mid-iteration.
        if (invalidPassengers.Count == 0)
        {
            return;
        }

        foreach (Rigidbody2D invalidPassenger in invalidPassengers)
        {
            passengers.Remove(invalidPassenger);
        }

        invalidPassengers.Clear();
    }

    private void OnCollisionStay2D(Collision2D collision)
    {
        // Only carry the player while they are standing on the top face.
        // Side contacts must stay free so pushing into the platform wall does
        // not drag the player along with it.
        if (mode != BehaviorMode.Move
            || !collision.gameObject.TryGetComponent(out PlayerController2D _)
            || !collision.gameObject.TryGetComponent(out Rigidbody2D passenger))
        {
            return;
        }

        if (IsStandingOnTop(collision))
        {
            passengers.Add(passenger);
        }
        else
        {
            passengers.Remove(passenger);
        }
    }

    private void OnCollisionExit2D(Collision2D collision)
    {
        if (collision.gameObject.TryGetComponent(out Rigidbody2D passenger))
        {
            passengers.Remove(passenger);
        }
    }

    private bool IsStandingOnTop(Collision2D collision)
    {
        if (collision.transform.position.y <= transform.position.y)
        {
            return false;
        }

        // In this collision callback, a downward-facing contact normal means
        // the player's collider is pressing onto the platform from above.
        for (int i = 0; i < collision.contactCount; i++)
        {
            if (collision.GetContact(i).normal.y < -0.5f)
            {
                return true;
            }
        }

        return false;
    }
}
