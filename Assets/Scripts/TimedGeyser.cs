using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Serialization;

/// <summary>
/// A switch-activated launcher. Entering its trigger while active replaces the
/// player's velocity once; leaving and re-entering allows another launch.
/// </summary>
[RequireComponent(typeof(BoxCollider2D))]
public class TimedGeyser : SwitchTarget
{
    private const string WindVfxResourcePath = "VFX/Geyser Wind VFX";

    [Header("Timing")]
    [SerializeField, Min(0f)] private float countdownDuration = 1f;
    [SerializeField, Min(0f)] private float activeDuration = 3f;

    [Header("Blast")]
    [FormerlySerializedAs("maximumSpeed")]
    [SerializeField, Min(0f)] private float launchSpeed = 18f;

    [Header("Feedback")]
    [Tooltip("Uses the reusable layered paper-wind effect instead of the legacy block visual.")]
    [SerializeField] private bool useStylizedWindVisual = true;
    [SerializeField] private GameObject activeVisual;
    [SerializeField] private TextMesh countdownDisplay;
    [SerializeField] private UnityEvent onActivated = new UnityEvent();
    [SerializeField] private UnityEvent onDeactivated = new UnityEvent();

    private Coroutine sequenceRoutine;
    private GeyserWindVFX windVfx;
    private bool isActive;

    public override bool IsActivationRunning => sequenceRoutine != null;

    private void Awake()
    {
        BoxCollider2D launchZone = GetComponent<BoxCollider2D>();
        launchZone.isTrigger = true;
        EnsureWindVisual(launchZone);
        TextMeshFontUtility.ApplyFontMaterial(countdownDisplay);
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
            if (windVfx != null && windVfx.IsReady)
            {
                windVfx.BeginCharge(countdownDuration);
            }

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

        bool hasStylizedWind = windVfx != null && windVfx.IsReady;
        if (hasStylizedWind)
        {
            windVfx.SetWindActive(active);
        }

        if (activeVisual != null)
        {
            // Existing scenes still serialize the old cube placeholder. Keep it
            // available as a fallback, but do not draw it over the new effect.
            activeVisual.SetActive(active && !hasStylizedWind);
        }
    }

    private void EnsureWindVisual(BoxCollider2D launchZone)
    {
        if (!useStylizedWindVisual)
        {
            return;
        }

        windVfx = GetComponentInChildren<GeyserWindVFX>(true);
        if (windVfx == null)
        {
            GameObject template =
                Resources.Load<GameObject>(WindVfxResourcePath);
            if (template != null)
            {
                GameObject instance = Instantiate(template, transform, false);
                instance.name = template.name;
                windVfx = instance.GetComponent<GeyserWindVFX>();

                if (windVfx == null)
                {
                    Destroy(instance);
                }
            }

            if (windVfx == null)
            {
                // The fallback keeps geysers functional if the presentation
                // prefab is accidentally moved out of Resources.
                windVfx = gameObject.AddComponent<GeyserWindVFX>();
            }
        }

        if (windVfx != null)
        {
            windVfx.Configure(launchZone);
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
