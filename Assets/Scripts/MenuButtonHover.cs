using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Adds a punchy hover animation and hover sound to UI buttons.
/// </summary>
[DisallowMultipleComponent]
public sealed class MenuButtonHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, ISelectHandler, IDeselectHandler
{
    [SerializeField] private bool animateOnEnable = true;
    [SerializeField, Min(0f)] private float introDelay = 2f;
    [SerializeField] private Vector2 introOffset = new Vector2(0f, -260f);
    [SerializeField, Min(0.05f)] private float introDuration = 0.7f;
    [SerializeField, Min(0.05f)] private float hoverDuration = 0.14f;
    [SerializeField, Min(0f)] private float hoverScale = 1.12f;
    [SerializeField, Min(0f)] private float hoverRotation = 3f;
    [SerializeField, Min(0f)] private float hoverYOffset = 10f;
    [SerializeField] private AudioClip hoverClip;
    [SerializeField, Range(0f, 2f)] private float hoverPitch = 1f;

    private RectTransform rectTransform;
    private Vector3 restingScale;
    private Quaternion restingRotation;
    private Vector2 restingAnchoredPosition;
    private Vector2 hiddenAnchoredPosition;
    private Coroutine activeRoutine;
    private bool highlighted;

    private void Awake()
    {
        rectTransform = GetComponent<RectTransform>();
        restingScale = rectTransform.localScale;
        restingRotation = rectTransform.localRotation;
        restingAnchoredPosition = rectTransform.anchoredPosition;
        hiddenAnchoredPosition = restingAnchoredPosition + introOffset;
    }

    private void OnEnable()
    {
        if (animateOnEnable)
        {
            SetHiddenPose();
            PlayRoutine(IntroRoutine());
        }
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        SetHighlighted(true);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        SetHighlighted(false);
    }

    public void OnSelect(BaseEventData eventData)
    {
        SetHighlighted(true);
    }

    public void OnDeselect(BaseEventData eventData)
    {
        SetHighlighted(false);
    }

    private void SetHighlighted(bool value)
    {
        if (highlighted == value)
        {
            return;
        }

        highlighted = value;

        if (highlighted && hoverClip != null)
        {
            AudioManager.Instance.PlayOneShot(hoverClip, hoverPitch, 1f);
        }

        PlayRoutine(HoverRoutine(highlighted));
    }

    private IEnumerator HoverRoutine(bool hovered)
    {
        Vector3 startScale = rectTransform.localScale;
        Quaternion startRotation = rectTransform.localRotation;
        Vector2 startPosition = rectTransform.anchoredPosition;

        Vector3 targetScale = hovered ? restingScale * hoverScale : restingScale;
        Quaternion targetRotation = hovered
            ? restingRotation * Quaternion.Euler(0f, 0f, -hoverRotation)
            : restingRotation;
        Vector2 targetPosition = hovered
            ? restingAnchoredPosition + new Vector2(0f, hoverYOffset)
            : restingAnchoredPosition;

        float elapsed = 0f;
        while (elapsed < hoverDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / hoverDuration);
            float eased = EaseOutBack(t);

            rectTransform.localScale = Vector3.Lerp(startScale, targetScale, eased);
            rectTransform.localRotation = Quaternion.Slerp(startRotation, targetRotation, eased);
            rectTransform.anchoredPosition = Vector2.Lerp(startPosition, targetPosition, eased);
            yield return null;
        }

        rectTransform.localScale = targetScale;
        rectTransform.localRotation = targetRotation;
        rectTransform.anchoredPosition = targetPosition;
        activeRoutine = null;
    }

    private IEnumerator IntroRoutine()
    {
        if (introDelay > 0f)
        {
            yield return Wait(introDelay);
        }

        Vector2 startPosition = rectTransform.anchoredPosition;
        Vector3 startScale = rectTransform.localScale;
        Quaternion startRotation = rectTransform.localRotation;

        float elapsed = 0f;
        while (elapsed < introDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / introDuration);
            float eased = EaseOutBack(t);

            rectTransform.anchoredPosition = Vector2.Lerp(startPosition, restingAnchoredPosition, eased);
            rectTransform.localScale = Vector3.Lerp(startScale, restingScale, eased);
            rectTransform.localRotation = Quaternion.Slerp(
                startRotation,
                restingRotation,
                eased);

            yield return null;
        }

        rectTransform.anchoredPosition = restingAnchoredPosition;
        rectTransform.localScale = restingScale;
        rectTransform.localRotation = restingRotation;
        activeRoutine = null;
    }

    private void SetHiddenPose()
    {
        rectTransform.localScale = restingScale * 0.65f;
        rectTransform.localRotation = restingRotation * Quaternion.Euler(0f, 0f, 4f);
        rectTransform.anchoredPosition = hiddenAnchoredPosition;
    }

    private IEnumerator Wait(float duration)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
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

    private static float EaseOutBack(float progress)
    {
        const float overshoot = 1.70158f;
        float shifted = progress - 1f;
        return 1f + (overshoot + 1f) * shifted * shifted * shifted + overshoot * shifted * shifted;
    }
}
