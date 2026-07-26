using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Reveals a UI object with a quick paper-comedy bubble pop:
/// fade in, grow past its resting scale, then settle back into place.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(RectTransform))]
[RequireComponent(typeof(CanvasGroup))]
public sealed class BubblePopText : MonoBehaviour
{
    [Header("Playback")]
    [SerializeField] private bool playOnEnable = true;
    [SerializeField] private bool startHidden = true;
    [SerializeField] private bool useUnscaledTime = true;
    [SerializeField, Min(0f)] private float delay;
    [SerializeField, Min(0f)] private float disableAfterSeconds;
    [SerializeField] private bool loadNextSceneOnComplete;

    [Header("Bubble Pop")]
    [SerializeField, Min(0.01f)] private float popDuration = 0.22f;
    [SerializeField, Min(0f)] private float settleDuration = 0.08f;
    [SerializeField, Range(0f, 1f)] private float hiddenAlpha;
    [SerializeField, Range(0f, 1f)] private float visibleAlpha = 1f;
    [SerializeField, Min(0f)] private float hiddenScale = 0.5f;
    [SerializeField, Min(0f)] private float overshootScale = 1.12f;
    [SerializeField, Min(0f)] private float visibleScale = 1f;
    [SerializeField] private float hiddenRotation = 10f;
    [SerializeField] private Vector2 hiddenOffset;
    [SerializeField] private AudioClip popClip;

    private RectTransform rectTransform;
    private CanvasGroup canvasGroup;
    private Coroutine activeRoutine;
    private Vector3 restingScale;
    private Quaternion restingRotation;
    private Vector2 restingAnchoredPosition;
    private bool capturedRestingState;

    private float DeltaTime => useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;

    private void Awake()
    {
        CacheComponents();
        CaptureRestingState();

        if (startHidden)
        {
            ApplyState(hiddenAlpha, hiddenScale, hiddenRotation, hiddenOffset);
        }
    }

    private void OnEnable()
    {
        if (playOnEnable)
        {
            Show();
        }
    }

    private void OnDisable()
    {
        if (activeRoutine != null)
        {
            StopCoroutine(activeRoutine);
            activeRoutine = null;
        }
    }

    /// <summary>Plays the bubble-pop reveal from the hidden state.</summary>
    public void Show()
    {
        PlayRoutine(ShowRoutine());
    }

    /// <summary>Hides the text with a quick reverse pop.</summary>
    public void Hide()
    {
        PlayRoutine(HideRoutine());
    }

    /// <summary>Stores the current transform as the visible resting pose.</summary>
    public void CaptureCurrentAsRestingState()
    {
        CacheComponents();
        restingScale = rectTransform.localScale;
        restingRotation = rectTransform.localRotation;
        restingAnchoredPosition = rectTransform.anchoredPosition;
        capturedRestingState = true;
    }

    private IEnumerator ShowRoutine()
    {
        CacheComponents();
        CaptureRestingState();
        ApplyState(hiddenAlpha, hiddenScale, hiddenRotation, hiddenOffset);

        if (delay > 0f)
        {
            yield return Wait(delay);
        }

        AudioManager.Instance.PlayOneShot(popClip);

        yield return TweenState(
            hiddenAlpha,
            visibleAlpha,
            hiddenScale,
            overshootScale,
            hiddenRotation,
            0f,
            hiddenOffset,
            Vector2.zero,
            popDuration,
            EaseOutBack);

        if (settleDuration > 0f)
        {
            yield return TweenState(
                visibleAlpha,
                visibleAlpha,
                overshootScale,
                visibleScale,
                0f,
                0f,
                Vector2.zero,
                Vector2.zero,
                settleDuration,
                EaseOutQuad);
        }
        else
        {
            ApplyState(visibleAlpha, visibleScale, 0f, Vector2.zero);
        }

        if (disableAfterSeconds > 0f)
        {
            yield return Wait(disableAfterSeconds);
            yield return HideRoutine();
        }

        if (loadNextSceneOnComplete)
        {
            LoadNextScene();
            yield break;
        }

        activeRoutine = null;
    }

    private IEnumerator HideRoutine()
    {
        CacheComponents();
        CaptureRestingState();

        yield return TweenState(
            canvasGroup.alpha,
            hiddenAlpha,
            visibleScale,
            hiddenScale,
            0f,
            -hiddenRotation,
            Vector2.zero,
            -hiddenOffset,
            Mathf.Max(0.01f, popDuration * 0.55f),
            EaseInQuad);

        activeRoutine = null;
    }

    private IEnumerator TweenState(
        float fromAlpha,
        float toAlpha,
        float fromScale,
        float toScale,
        float fromRotation,
        float toRotation,
        Vector2 fromOffset,
        Vector2 toOffset,
        float duration,
        System.Func<float, float> ease)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += DeltaTime;
            float progress = Mathf.Clamp01(elapsed / duration);
            float easedProgress = ease(progress);

            ApplyState(
                Mathf.Lerp(fromAlpha, toAlpha, Mathf.Clamp01(easedProgress)),
                Mathf.Lerp(fromScale, toScale, easedProgress),
                Mathf.Lerp(fromRotation, toRotation, easedProgress),
                Vector2.Lerp(fromOffset, toOffset, easedProgress));

            yield return null;
        }

        ApplyState(toAlpha, toScale, toRotation, toOffset);
    }

    private IEnumerator Wait(float duration)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += DeltaTime;
            yield return null;
        }
    }

    private void PlayRoutine(IEnumerator routine)
    {
        if (activeRoutine != null)
        {
            StopCoroutine(activeRoutine);
        }

        activeRoutine = StartCoroutine(routine);
    }

    private void ApplyState(float alpha, float scale, float rotationOffset, Vector2 positionOffset)
    {
        canvasGroup.alpha = alpha;
        rectTransform.localScale = restingScale * scale;
        rectTransform.localRotation = restingRotation * Quaternion.Euler(0f, 0f, rotationOffset);
        rectTransform.anchoredPosition = restingAnchoredPosition + positionOffset;
    }

    private void CacheComponents()
    {
        if (rectTransform == null)
        {
            rectTransform = GetComponent<RectTransform>();
        }

        if (canvasGroup == null)
        {
            canvasGroup = GetComponent<CanvasGroup>();
        }
    }

    private void CaptureRestingState()
    {
        if (capturedRestingState)
        {
            return;
        }

        CaptureCurrentAsRestingState();
    }

    private static float EaseOutQuad(float progress)
    {
        return 1f - (1f - progress) * (1f - progress);
    }

    private static float EaseInQuad(float progress)
    {
        return progress * progress;
    }

    private static float EaseOutBack(float progress)
    {
        const float overshoot = 1.70158f;
        float shifted = progress - 1f;
        return 1f + (overshoot + 1f) * shifted * shifted * shifted + overshoot * shifted * shifted;
    }

    private static void LoadNextScene()
    {
        int nextSceneIndex = SceneManager.GetActiveScene().buildIndex + 1;
        if (nextSceneIndex < SceneManager.sceneCountInBuildSettings)
        {
            SceneManager.LoadScene(nextSceneIndex);
        }
    }
}
