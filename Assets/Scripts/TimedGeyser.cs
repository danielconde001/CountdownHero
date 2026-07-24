using System.Collections;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// A switch-activated directional force zone. Its trigger collider defines the
/// geyser's trajectory, while transform.up defines the direction of the blast.
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class TimedGeyser : SwitchTarget
{
    [Header("Timing")]
    [SerializeField, Min(0f)] private float countdownDuration = 1f;
    [SerializeField, Min(0f)] private float activeDuration = 3f;

    [Header("Blast")]
    [SerializeField, Min(0f)] private float acceleration = 55f;
    [SerializeField, Min(0f)] private float maximumSpeed = 18f;

    [Header("Feedback")]
    [SerializeField] private GameObject activeVisual;
    [SerializeField] private TextMesh countdownDisplay;
    [SerializeField] private UnityEvent onActivated = new UnityEvent();
    [SerializeField] private UnityEvent onDeactivated = new UnityEvent();

    private Collider2D forceZone;
    private Coroutine sequenceRoutine;
    private bool isActive;

    private void Awake()
    {
        forceZone = GetComponent<Collider2D>();
        forceZone.isTrigger = true;
        SetActive(false);
        ClearCountdown();
    }

    private void OnDisable()
    {
        if (sequenceRoutine != null)
        {
            StopCoroutine(sequenceRoutine);
            sequenceRoutine = null;
        }

        SetActive(false);
        ClearCountdown();
    }

    /// <summary>Starts the geyser sequence unless one is already running.</summary>
    public override void Activate()
    {
        if (sequenceRoutine == null)
        {
            sequenceRoutine = StartCoroutine(RunSequence());
        }
    }

    public void Initialize(
        float countdown,
        float activeTime,
        float blastAcceleration,
        float speedLimit,
        GameObject visual = null,
        TextMesh display = null)
    {
        countdownDuration = countdown;
        activeDuration = activeTime;
        acceleration = blastAcceleration;
        maximumSpeed = speedLimit;
        activeVisual = visual;
        countdownDisplay = display;
    }

    private IEnumerator RunSequence()
    {
        yield return PlayCountdown();
        SetActive(true);
        onActivated.Invoke();

        if (activeDuration > 0f)
        {
            yield return new WaitForSeconds(activeDuration);
        }

        SetActive(false);
        onDeactivated.Invoke();
        sequenceRoutine = null;
    }

    private void OnTriggerStay2D(Collider2D other)
    {
        if (!isActive || !other.TryGetComponent(out PlayerController2D player))
        {
            return;
        }

        Rigidbody2D playerBody = other.attachedRigidbody;
        Vector2 direction = transform.up.normalized;
        if (playerBody != null && Vector2.Dot(playerBody.linearVelocity, direction) >= maximumSpeed)
        {
            return;
        }

        player.AddExternalAcceleration(direction * acceleration);
    }

    private IEnumerator PlayCountdown()
    {
        float duration = Mathf.Max(0f, countdownDuration);
        if (countdownDisplay == null)
        {
            if (duration > 0f)
            {
                yield return new WaitForSeconds(duration);
            }

            yield break;
        }

        for (float remaining = duration; remaining > 0f; remaining -= Time.deltaTime)
        {
            countdownDisplay.text = Mathf.CeilToInt(remaining).ToString();
            yield return null;
        }

        countdownDisplay.text = "GO!";
        yield return null;
        ClearCountdown();
    }

    private void SetActive(bool active)
    {
        isActive = active;
        if (activeVisual != null)
        {
            activeVisual.SetActive(active);
        }
    }

    private void ClearCountdown()
    {
        if (countdownDisplay != null)
        {
            countdownDisplay.text = string.Empty;
        }
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.2f, 0.8f, 1f, 0.8f);
        Gizmos.DrawRay(transform.position, transform.up * 2f);
    }
#endif
}
