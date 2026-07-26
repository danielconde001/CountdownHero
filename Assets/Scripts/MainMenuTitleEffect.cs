using System.Collections;
using UnityEngine;

/// <summary>
/// Gives the main menu title a dramatic entrance: pop, overshoot, pulse, and a little wobble.
/// </summary>
[DisallowMultipleComponent]
public sealed class MainMenuTitleEffect : MonoBehaviour
{
    [SerializeField] private bool playOnEnable = true;
    [SerializeField, Min(0f)] private float delay;
    [SerializeField] private Vector3 introOffset = new Vector3(0f, 420f, 0f);
    [SerializeField, Min(0.05f)] private float introDuration = 0.85f;
    [SerializeField, Min(0f)] private float pulseAmplitude = 0.08f;
    [SerializeField, Min(0f)] private float pulseSpeed = 2.2f;
    [SerializeField, Min(0f)] private float wobbleDegrees = 4f;
    [SerializeField, Min(0f)] private float wobbleSpeed = 8f;
    [SerializeField, Min(0.1f)] private float startScale = 0.2f;
    [SerializeField, Min(0.1f)] private float overshootScale = 1.18f;

    private Vector3 restingScale;
    private Quaternion restingRotation;
    private Vector3 restingPosition;
    private Coroutine activeRoutine;

    private void Awake()
    {
        restingScale = transform.localScale;
        restingRotation = transform.localRotation;
        restingPosition = transform.localPosition;
        SetPose(introOffset, startScale, 0f, 0f);
    }

    private void OnEnable()
    {
        if (playOnEnable)
        {
            Play();
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

    public void Play()
    {
        if (activeRoutine != null)
        {
            StopCoroutine(activeRoutine);
        }

        activeRoutine = StartCoroutine(PlayRoutine());
    }

    private IEnumerator PlayRoutine()
    {
        if (delay > 0f)
        {
            yield return Wait(delay);
        }

        float elapsed = 0f;
        while (elapsed < introDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / introDuration);
            float pop = EaseOutBack(t);
            float pulse = Mathf.Sin(Time.unscaledTime * pulseSpeed) * pulseAmplitude * (1f - t);
            float wobble = Mathf.Sin(Time.unscaledTime * wobbleSpeed) * wobbleDegrees * (1f - t);

            SetPose(Vector3.Lerp(introOffset, Vector3.zero, pop), Mathf.Lerp(startScale, overshootScale, pop), wobble, pulse);
            yield return null;
        }

        SetPose(Vector3.zero, 1f, 0f, 0f);
        activeRoutine = null;
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

    private void SetPose(Vector3 positionOffset, float scaleMultiplier, float zRotation, float scalePulse)
    {
        transform.localScale = restingScale * scaleMultiplier * (1f + scalePulse);
        transform.localRotation = restingRotation * Quaternion.Euler(0f, 0f, zRotation);
        transform.localPosition = restingPosition + positionOffset;
    }

    private static float EaseOutBack(float progress)
    {
        const float overshoot = 1.70158f;
        float shifted = progress - 1f;
        return 1f + (overshoot + 1f) * shifted * shifted * shifted + overshoot * shifted * shifted;
    }
}
