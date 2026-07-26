using System;
using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Lets the player hold Q to skip a platforming level. After enough deaths,
/// a short-lived prompt advertises the option without changing its prefab text.
/// </summary>
public sealed class HoldQLevelSkip : MonoBehaviour
{
    private const string PlatformingScenePrefix = "Platforming";

    [Header("Skip")]
    [SerializeField] private InputActionReference _skipActionRef;
    [SerializeField, Min(0f)] private float holdDuration = 1.75f;
    [SerializeField, Min(0f)] private float warningDuration = 2f;
    [SerializeField] private string warningText = "Skill Issue";
    [SerializeField] private Color warningColor = Color.red;
    [SerializeField] private Font bigJohnFont;
    [SerializeField] private AudioClip toBeContinuedClip;
    [SerializeField] private SceneLoadTrigger sceneLoadTrigger;

    [Header("Prompt")]
    [SerializeField] private GameObject skipPromptPrefab;
    [SerializeField, Min(0f)] private float promptFadeInDuration = 0.2f;
    [SerializeField, Min(0f)] private float promptFadeOutDuration = 0.2f;
    [SerializeField, Min(0f)] private float promptVisibleDuration = 2f;
    [SerializeField, Min(0)] private int deathsBeforePrompt = 5;

    private bool isSkipping;
    private bool isPromptVisible;
    private float holdTime;
    private GameObject warningObject;
    private GameObject promptObject;
    private CanvasGroup promptCanvasGroup;
    private Coroutine promptFadeRoutine;
    private Coroutine promptHideRoutine;

    private void Awake()
    {
        string sceneName = SceneManager.GetActiveScene().name;
        if (!sceneName.StartsWith(PlatformingScenePrefix, StringComparison.Ordinal))
        {
            enabled = false;
            return;
        }

        if (sceneLoadTrigger == null)
        {
            sceneLoadTrigger = GetComponent<SceneLoadTrigger>();
        }

        if (sceneLoadTrigger == null)
        {
            sceneLoadTrigger = FindFirstObjectByType<SceneLoadTrigger>();
        }

        if (sceneLoadTrigger == null)
        {
            Debug.LogWarning(
                $"{nameof(HoldQLevelSkip)} in '{sceneName}' could not find a {nameof(SceneLoadTrigger)}.",
                this);
            enabled = false;
            return;
        }

        if (LevelDeathCounter.GetDeaths() > deathsBeforePrompt)
        {
            ShowSkipPrompt();
        }
    }

    private void OnEnable()
    {
        LevelDeathCounter.DeathCountChanged += HandleDeathCountChanged;
    }

    private void OnDisable()
    {
        LevelDeathCounter.DeathCountChanged -= HandleDeathCountChanged;
        CleanupRuntimeUi();
    }

    private void Update()
    {
        if (isSkipping)
        {
            return;
        }

        if (_skipActionRef.action.IsPressed() != true)
        {
            holdTime = 0f;
            return;
        }

        holdTime += Time.unscaledDeltaTime;
        if (holdTime < holdDuration)
        {
            return;
        }

        holdTime = 0f;
        StartCoroutine(SkipRoutine());
    }

    private IEnumerator SkipRoutine()
    {
        isSkipping = true;
        HideSkipPrompt();
        ShowWarning();

        if (toBeContinuedClip != null)
        {
            AudioManager.Instance.PlayOneShot(toBeContinuedClip);
        }

        yield return new WaitForSecondsRealtime(warningDuration);

        if (warningObject != null)
        {
            Destroy(warningObject);
            warningObject = null;
        }

        sceneLoadTrigger.LoadScene();
    }

    private void ShowWarning()
    {
        if (warningObject != null)
        {
            Destroy(warningObject);
        }

        warningObject = new GameObject("Skip Warning");

        Canvas canvas = warningObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = short.MaxValue;

        CanvasScaler scaler = warningObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        GameObject textObject = new GameObject("Text");
        textObject.transform.SetParent(warningObject.transform, false);

        Text text = textObject.AddComponent<Text>();
        text.text = warningText;
        text.color = warningColor;
        text.alignment = TextAnchor.MiddleCenter;
        text.font = bigJohnFont;
        text.fontSize = 72;
        text.horizontalOverflow = HorizontalWrapMode.Overflow;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        text.raycastTarget = false;
        text.rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
        text.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        text.rectTransform.pivot = new Vector2(0.5f, 0.5f);
        text.rectTransform.anchoredPosition = Vector2.zero;
        text.rectTransform.sizeDelta = new Vector2(1000f, 200f);
    }

    private void HandleDeathCountChanged(int deathCount)
    {
        if (deathCount > deathsBeforePrompt)
        {
            ShowSkipPrompt();
        }
    }

    private void ShowSkipPrompt()
    {
        if (isPromptVisible || !EnsurePromptExists())
        {
            return;
        }

        isPromptVisible = true;
        promptObject.SetActive(true);

        if (promptHideRoutine != null)
        {
            StopCoroutine(promptHideRoutine);
        }

        StartPromptFade(1f, promptFadeInDuration, false);
        promptHideRoutine = StartCoroutine(HidePromptAfterDelay());
    }

    private bool EnsurePromptExists()
    {
        if (promptObject != null)
        {
            return true;
        }

        if (skipPromptPrefab == null)
        {
            Debug.LogWarning($"{nameof(HoldQLevelSkip)} is missing its Skip Prompt prefab.", this);
            return false;
        }

        promptObject = Instantiate(skipPromptPrefab);
        promptObject.name = skipPromptPrefab.name;
        promptCanvasGroup = promptObject.GetComponent<CanvasGroup>();

        if (promptCanvasGroup == null)
        {
            promptCanvasGroup = promptObject.AddComponent<CanvasGroup>();
        }

        promptCanvasGroup.alpha = 0f;
        promptCanvasGroup.interactable = false;
        promptCanvasGroup.blocksRaycasts = false;
        return true;
    }

    private void HideSkipPrompt()
    {
        if (!isPromptVisible)
        {
            return;
        }

        isPromptVisible = false;

        if (promptHideRoutine != null)
        {
            StopCoroutine(promptHideRoutine);
            promptHideRoutine = null;
        }

        StartPromptFade(0f, promptFadeOutDuration, true);
    }

    private void StartPromptFade(float targetAlpha, float duration, bool disableAfterFade)
    {
        if (promptCanvasGroup == null)
        {
            return;
        }

        if (promptFadeRoutine != null)
        {
            StopCoroutine(promptFadeRoutine);
        }

        promptFadeRoutine = StartCoroutine(
            FadePrompt(targetAlpha, duration, disableAfterFade));
    }

    private IEnumerator FadePrompt(float targetAlpha, float duration, bool disableAfterFade)
    {
        float startAlpha = promptCanvasGroup.alpha;

        if (duration > 0f)
        {
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                promptCanvasGroup.alpha = Mathf.Lerp(
                    startAlpha,
                    targetAlpha,
                    Mathf.Clamp01(elapsed / duration));
                yield return null;
            }
        }

        promptCanvasGroup.alpha = targetAlpha;

        if (disableAfterFade && promptObject != null)
        {
            promptObject.SetActive(false);
        }

        promptFadeRoutine = null;
    }

    private IEnumerator HidePromptAfterDelay()
    {
        yield return new WaitForSecondsRealtime(promptVisibleDuration);
        promptHideRoutine = null;
        HideSkipPrompt();
    }

    private void CleanupRuntimeUi()
    {
        StopAllCoroutines();

        if (warningObject != null)
        {
            Destroy(warningObject);
        }

        if (promptObject != null)
        {
            Destroy(promptObject);
        }

        warningObject = null;
        promptObject = null;
        promptCanvasGroup = null;
        promptFadeRoutine = null;
        promptHideRoutine = null;
        isPromptVisible = false;
    }
}
