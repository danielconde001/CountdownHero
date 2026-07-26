using System.Collections;
using DG.Tweening;
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

    [Header("Countdown Animation")]
    [SerializeField] private Color countdownTextColor = Color.white;
    [SerializeField] private Color activeDurationTextColor = Color.red;
    [SerializeField, Min(0f)] private float numberShowDuration = 0.12f;
    [SerializeField, Min(0f)] private float numberHideDuration = 0.06f;
    [SerializeField] private Ease numberShowEase = Ease.InQuad;
    [SerializeField, Range(0f, 1f)] private float numberStartScale = 0.5f;
    [SerializeField] private float numberStartRotation = 10f;

    [SerializeField] private UnityEvent onActivated = new UnityEvent();
    [SerializeField] private UnityEvent onDeactivated = new UnityEvent();

    private Coroutine sequenceRoutine;
    private GeyserWindVFX windVfx;
    private Tween countdownTween;
    private Vector3 countdownVisibleScale;
    private Quaternion countdownVisibleRotation;
    private Color countdownInitialColor;
    private bool isActive;

    public override bool IsActivationRunning => sequenceRoutine != null;

    private void Awake()
    {
        BoxCollider2D launchZone = GetComponent<BoxCollider2D>();
        launchZone.isTrigger = true;
        EnsureWindVisual(launchZone);
        TextMeshFontUtility.ApplyFontMaterial(countdownDisplay);
        CacheCountdownDisplayState();
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
        TextMeshFontUtility.ApplyFontMaterial(countdownDisplay);
        CacheCountdownDisplayState();
    }

    private IEnumerator RunSequence()
    {
        yield return PlayCountdown();
        SetActive(true);
        onActivated.Invoke();

        yield return PlayActiveDurationCountdown();

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
        yield return PlayNumberCountdown(
            countdownDuration,
            countdownTextColor,
            AudioManager.ClockTickPitch.Higher);
    }

    private IEnumerator PlayActiveDurationCountdown()
    {
        yield return PlayNumberCountdown(
            activeDuration,
            activeDurationTextColor,
            AudioManager.ClockTickPitch.Lower);
    }

    private IEnumerator PlayNumberCountdown(
        float duration,
        Color textColor,
        AudioManager.ClockTickPitch tickPitch)
    {
        duration = Mathf.Max(0f, duration);
        if (countdownDisplay == null)
        {
            yield return WaitForDuration(duration);
            yield break;
        }

        int previousNumber = -1;
        for (float remaining = duration; remaining > 0f; remaining -= Time.deltaTime)
        {
            int currentNumber = Mathf.CeilToInt(remaining);
            if (currentNumber != previousNumber)
            {
                previousNumber = currentNumber;
                ShowCountdownNumber(currentNumber.ToString(), textColor, tickPitch);
            }

            yield return null;
        }

        yield return HideCountdownNumber();
    }

    private void SetActive(bool active)
    {
        bool activeChanged = isActive != active;
        isActive = active;

        if (activeChanged)
        {
            if (active)
            {
                AudioManager.Instance.StartGeyserBlow();
            }
            else
            {
                AudioManager.Instance.StopGeyserBlow();
            }
        }

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
        KillCountdownTween();
        ClearCountdown();
    }

    private void ClearCountdown()
    {
        if (countdownDisplay != null)
        {
            countdownDisplay.text = string.Empty;
            RestoreCountdownDisplayState();
        }
    }

    private void CacheCountdownDisplayState()
    {
        if (countdownDisplay == null)
        {
            return;
        }

        countdownVisibleScale = countdownDisplay.transform.localScale;
        countdownVisibleRotation = countdownDisplay.transform.localRotation;
        countdownInitialColor = countdownDisplay.color;
    }

    private void ShowCountdownNumber(
        string displayText,
        Color textColor,
        AudioManager.ClockTickPitch tickPitch)
    {
        KillCountdownTween();
        countdownDisplay.text = displayText;
        countdownDisplay.color = WithAlpha(textColor, 0f);
        countdownDisplay.transform.localScale = countdownVisibleScale * numberStartScale;
        countdownDisplay.transform.localRotation = countdownVisibleRotation
            * Quaternion.Euler(0f, 0f, numberStartRotation);

        AudioManager.Instance.PlayClockTick(tickPitch);

        float duration = Mathf.Max(0f, numberShowDuration);
        if (duration <= 0f)
        {
            countdownDisplay.color = textColor;
            countdownDisplay.transform.localScale = countdownVisibleScale;
            countdownDisplay.transform.localRotation = countdownVisibleRotation;
            return;
        }

        Sequence sequence = DOTween.Sequence();
        sequence.Join(countdownDisplay.transform.DOScale(countdownVisibleScale, duration)
            .SetEase(numberShowEase));
        sequence.Join(countdownDisplay.transform.DOLocalRotateQuaternion(
                countdownVisibleRotation,
                duration)
            .SetEase(numberShowEase));
        sequence.Join(DOTween.To(
                () => countdownDisplay.color,
                color => countdownDisplay.color = color,
                textColor,
                duration)
            .SetEase(numberShowEase));
        countdownTween = sequence.SetTarget(countdownDisplay);
    }

    private IEnumerator HideCountdownNumber()
    {
        KillCountdownTween();

        float duration = Mathf.Max(0f, numberHideDuration);
        if (duration <= 0f)
        {
            ClearCountdown();
            yield break;
        }

        Color transparentColor = WithAlpha(countdownDisplay.color, 0f);
        Sequence sequence = DOTween.Sequence();
        sequence.Join(countdownDisplay.transform.DOScale(countdownVisibleScale * numberStartScale, duration)
            .SetEase(numberShowEase));
        sequence.Join(countdownDisplay.transform.DOLocalRotateQuaternion(
                countdownVisibleRotation * Quaternion.Euler(0f, 0f, numberStartRotation),
                duration)
            .SetEase(numberShowEase));
        sequence.Join(DOTween.To(
                () => countdownDisplay.color,
                color => countdownDisplay.color = color,
                transparentColor,
                duration)
            .SetEase(numberShowEase));
        countdownTween = sequence.SetTarget(countdownDisplay);

        yield return sequence.WaitForCompletion();
        ClearCountdown();
    }

    private void RestoreCountdownDisplayState()
    {
        countdownDisplay.color = countdownInitialColor;
        countdownDisplay.transform.localScale = countdownVisibleScale;
        countdownDisplay.transform.localRotation = countdownVisibleRotation;
    }

    private void KillCountdownTween()
    {
        if (countdownTween == null)
        {
            return;
        }

        countdownTween.Kill();
        countdownTween = null;
    }

    private static IEnumerator WaitForDuration(float duration)
    {
        duration = Mathf.Max(0f, duration);
        if (duration > 0f)
        {
            yield return new WaitForSeconds(duration);
        }
    }

    private static Color WithAlpha(Color color, float alpha)
    {
        color.a = alpha;
        return color;
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.2f, 0.8f, 1f, 0.8f);
        Gizmos.DrawRay(transform.position, transform.up * 2f);
    }
#endif
}
