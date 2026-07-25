using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Serialization;

/// <summary>
/// A switch-activated launcher. Entering its trigger while active replaces the
/// player's velocity once; leaving and re-entering allows another launch.
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class TimedGeyser : SwitchTarget
{
    [Header("Timing")]
    [SerializeField, Min(0f)] private float countdownDuration = 1f;
    [SerializeField, Min(0f)] private float activeDuration = 3f;

    [Header("Blast")]
    [FormerlySerializedAs("maximumSpeed")]
    [SerializeField, Min(0f)] private float launchSpeed = 18f;

    [Header("Feedback")]
    [SerializeField] private GameObject activeVisual;
    [SerializeField] private TextMesh countdownDisplay;
    [SerializeField] private UnityEvent onActivated = new UnityEvent();
    [SerializeField] private UnityEvent onDeactivated = new UnityEvent();

    private Coroutine sequenceRoutine;
    private bool isActive;

    private void Awake()
    {
        GetComponent<Collider2D>().isTrigger = true;
        ResetState();
    }

    private void OnDisable()
    {
        if (sequenceRoutine != null)
        {
            StopCoroutine(sequenceRoutine);
            sequenceRoutine = null;
        }

        ResetState();
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
        float speed,
        GameObject visual = null,
        TextMesh display = null)
    {
        countdownDuration = countdown;
        activeDuration = activeTime;
        launchSpeed = speed;
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

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!isActive)
        {
            return;
        }

        PlayerController2D player = other.attachedRigidbody != null
            ? other.attachedRigidbody.GetComponent<PlayerController2D>()
            : other.GetComponent<PlayerController2D>();
        if (player == null)
        {
            return;
        }

        // Replacing velocity makes every entry produce the same initial path,
        // regardless of how quickly the player approached the geyser.
        player.ApplyLaunchVelocity((Vector2)transform.up * launchSpeed);
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

    private void ResetState()
    {
        SetActive(false);
        ClearCountdown();
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
