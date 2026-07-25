using System.Collections;
using DG.Tweening;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Activates one or more timed platforms when the player triggers this switch.
/// </summary>
public class TimedSwitch : MonoBehaviour
{
    public enum ActivationMode
    {
        OnTriggerEnter,
        OnInteractPress,
        OnAttacked,
        OnJumpedOn
    }

    public enum PromptVisual
    {
        Text,
        Image
    }

    [SerializeField] private ActivationMode mode = ActivationMode.OnTriggerEnter;
    [SerializeField] private SwitchTarget[] targets;
    [SerializeField, Min(0f)] private float cooldown = 0.5f;

    [Header("Prompt")]
    [Tooltip("An optional custom prompt object. When empty, the selected visual is created at runtime.")]
    [SerializeField] private GameObject interactPrompt;
    [SerializeField] private PromptVisual promptVisual = PromptVisual.Image;
    [SerializeField] private Vector3 interactPromptOffset = new Vector3(0f, 1.25f, 0f);

    [Header("Text Prompt")]
    [SerializeField] private string interactPromptText = "E";
    [SerializeField] private Font interactPromptFont;
    [SerializeField, Min(0.1f)] private float interactPromptFontSize = 0.35f;

    [Header("Image Prompt")]
    [SerializeField] private Sprite interactPromptImage;
    [SerializeField] private Vector3 interactPromptImageScale = Vector3.one;
    [SerializeField] private Color interactPromptImageColor = Color.white;
    [SerializeField] private int interactPromptImageSortingOrder = 10;

    [Header("Prompt Animation")]
    [SerializeField, Min(0f)] private float promptShowDuration = 0.25f;
    [SerializeField] private Ease promptShowEase = Ease.InQuad;
    [SerializeField, Range(0f, 1f)] private float promptStartScale = 0.5f;
    [SerializeField] private float promptStartRotation = 10f;

    [Header("Prompt Press Animation")]
    [SerializeField, Min(0f)] private float promptPopDuration = 0.12f;
    [SerializeField, Min(1f)] private float promptPopScale = 1.25f;
    [SerializeField] private Color promptPressedColor = new Color(1f, 0.85f, 0.1f, 1f);
    [SerializeField, Min(0f)] private float promptDisappearDuration = 0.18f;

    private bool isOnCooldown;
    private bool playerInRange;
    private bool isPromptHideAnimating;
    private bool isPromptPressAnimating;
    private Collider2D switchCollider;
    private CubeLeverVisual cubeLeverVisual;
    private Coroutine cooldownRoutine;
    private SpriteRenderer promptImageRenderer;
    private Sequence promptAnimationSequence;
    private Vector3 promptVisibleScale;
    private Quaternion promptVisibleRotation;
    private Color promptVisibleColor;

    private void Awake()
    {
        switchCollider = GetComponent<Collider2D>();

        if (switchCollider == null && mode != ActivationMode.OnAttacked)
        {
            Debug.LogWarning($"{name}: TimedSwitch expects a Collider2D for {mode}.");
        }

        ConfigureCollider();
        cubeLeverVisual = GetComponent<CubeLeverVisual>();
        EnsureInteractPrompt();
        CachePromptAnimationState();
        RefreshCubeLeverVisual(true);
        RefreshInteractPrompt();
    }

    private void OnDisable()
    {
        if (cooldownRoutine != null)
        {
            StopCoroutine(cooldownRoutine);
            cooldownRoutine = null;
        }

        isOnCooldown = false;
        playerInRange = false;
        KillPromptAnimation();

        if (interactPrompt != null)
        {
            interactPrompt.SetActive(false);
            RestorePromptVisibleState();
        }
    }

    private void Update()
    {
        RefreshCubeLeverVisual();
        RefreshInteractPrompt();

        if (mode != ActivationMode.OnInteractPress
            || !playerInRange
            || !CanActivateTargets())
        {
            return;
        }

        if (Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame)
        {
            PlayPromptPressAnimation();
            ActivateTargets();
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.TryGetComponent(out PlayerController2D _))
        {
            return;
        }

        if (mode == ActivationMode.OnTriggerEnter)
        {
            ActivateTargets();
            return;
        }

        if (mode == ActivationMode.OnInteractPress)
        {
            playerInRange = true;
            RefreshInteractPrompt();
        }
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (mode == ActivationMode.OnInteractPress
            && other.TryGetComponent(out PlayerController2D _))
        {
            playerInRange = false;
            RefreshInteractPrompt();
        }
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (mode != ActivationMode.OnJumpedOn
            || !collision.gameObject.TryGetComponent(out PlayerController2D _))
        {
            return;
        }

        if (WasHitFromAbove(collision))
        {
            ActivateTargets();
        }
    }

    /// <summary>Entry point for future player attack hitboxes or raycasts.</summary>
    public void OnHit()
    {
        if (mode == ActivationMode.OnAttacked)
        {
            ActivateTargets();
        }
    }

    public void Initialize(
        ActivationMode activationMode,
        SwitchTarget[] timedTargets,
        float cooldownDuration,
        GameObject prompt = null)
    {
        mode = activationMode;
        targets = timedTargets;
        cooldown = cooldownDuration;
        interactPrompt = prompt;
        EnsureInteractPrompt();
        CachePromptAnimationState();
        RefreshInteractPrompt();
    }

    private void ConfigureCollider()
    {
        if (switchCollider == null)
        {
            return;
        }

        // Interaction modes own their collider shape, while attack detection is
        // delegated to the future combat/hitbox system through OnHit().
        switch (mode)
        {
            case ActivationMode.OnTriggerEnter:
            case ActivationMode.OnInteractPress:
                switchCollider.isTrigger = true;
                break;
            case ActivationMode.OnJumpedOn:
                switchCollider.isTrigger = false;
                break;
            case ActivationMode.OnAttacked:
                break;
        }
    }

    private void ActivateTargets()
    {
        if (isOnCooldown || IsAnyTargetRunning())
        {
            return;
        }

        if (targets == null || targets.Length == 0)
        {
            Debug.LogWarning($"{name}: TimedSwitch has no targets assigned.");
            return;
        }

        foreach (SwitchTarget target in targets)
        {
            if (target != null)
            {
                target.Activate();
            }
        }

        RefreshCubeLeverVisual();
        RefreshInteractPrompt();

        float duration = Mathf.Max(0f, cooldown);
        if (duration > 0f)
        {
            isOnCooldown = true;
            RefreshInteractPrompt();
            cooldownRoutine = StartCoroutine(CooldownRoutine(duration));
        }
    }

    private IEnumerator CooldownRoutine(float duration)
    {
        yield return new WaitForSeconds(duration);
        isOnCooldown = false;
        cooldownRoutine = null;
        RefreshCubeLeverVisual();
        RefreshInteractPrompt();
    }

    private bool WasHitFromAbove(Collision2D collision)
    {
        bool playerIsAboveSwitch = collision.transform.position.y > transform.position.y;
        if (!playerIsAboveSwitch)
        {
            return false;
        }

        for (int i = 0; i < collision.contactCount; i++)
        {
            ContactPoint2D contact = collision.GetContact(i);
            if (Mathf.Abs(contact.normal.y) > 0.6f)
            {
                return true;
            }
        }

        return false;
    }

    private void SetInteractPromptVisible(bool visible)
    {
        if (interactPrompt == null
            || (!visible && isPromptPressAnimating))
        {
            return;
        }

        if (visible)
        {
            if (!interactPrompt.activeSelf || isPromptHideAnimating)
            {
                PlayPromptShowAnimation(!isPromptHideAnimating);
            }

            return;
        }

        if (interactPrompt.activeSelf && !isPromptHideAnimating)
        {
            PlayPromptHideAnimation();
        }
    }

    private void EnsureInteractPrompt()
    {
        if (mode != ActivationMode.OnInteractPress || interactPrompt != null)
        {
            return;
        }

        var promptObject = new GameObject("Interact Prompt");
        promptObject.transform.SetParent(transform);
        promptObject.transform.localPosition = interactPromptOffset;
        promptObject.transform.localRotation = Quaternion.identity;

        if (promptVisual == PromptVisual.Image)
        {
            promptObject.transform.localScale = interactPromptImageScale;
            SpriteRenderer promptImage = promptObject.AddComponent<SpriteRenderer>();
            promptImage.sprite = interactPromptImage;
            promptImage.color = interactPromptImageColor;
            promptImage.sortingOrder = interactPromptImageSortingOrder;

            if (interactPromptImage == null)
            {
                Debug.LogWarning($"{name}: Image prompt selected, but no prompt Sprite is assigned.");
            }
        }
        else
        {
            promptObject.transform.localScale = Vector3.one;
            TextMesh promptText = promptObject.AddComponent<TextMesh>();
            promptText.text = interactPromptText;
            promptText.font = interactPromptFont;
            promptText.anchor = TextAnchor.MiddleCenter;
            promptText.alignment = TextAlignment.Center;
            promptText.fontSize = 24;
            promptText.characterSize = interactPromptFontSize;
            promptText.color = Color.white;
            TextMeshFontUtility.ApplyFontMaterial(promptText);
        }

        interactPrompt = promptObject;
    }

    private void RefreshCubeLeverVisual(bool instant = false)
    {
        if (cubeLeverVisual != null)
        {
            cubeLeverVisual.SetEngaged(isOnCooldown || IsAnyTargetRunning(), instant);
        }
    }

    private void CachePromptAnimationState()
    {
        if (interactPrompt == null)
        {
            return;
        }

        promptImageRenderer = interactPrompt.GetComponentInChildren<SpriteRenderer>(true);
        promptVisibleScale = interactPrompt.transform.localScale;
        promptVisibleRotation = interactPrompt.transform.localRotation;
        promptVisibleColor = promptImageRenderer != null
            ? promptImageRenderer.color
            : Color.white;
    }

    private void PlayPromptShowAnimation(bool restartFromHidden = true)
    {
        KillPromptAnimation();

        Transform promptTransform = interactPrompt.transform;
        if (restartFromHidden)
        {
            promptTransform.localScale = promptVisibleScale * promptStartScale;
            promptTransform.localRotation = promptVisibleRotation
                * Quaternion.Euler(0f, 0f, promptStartRotation);

            if (promptImageRenderer != null)
            {
                Color transparentColor = promptVisibleColor;
                transparentColor.a = 0f;
                promptImageRenderer.color = transparentColor;
            }
        }

        interactPrompt.SetActive(true);

        float duration = Mathf.Max(0f, promptShowDuration);
        if (duration <= 0f)
        {
            RestorePromptVisibleState();
            return;
        }

        promptAnimationSequence = DOTween.Sequence();
        promptAnimationSequence.Join(
            promptTransform.DOScale(promptVisibleScale, duration)
                .SetEase(promptShowEase));
        promptAnimationSequence.Join(
            promptTransform.DOLocalRotateQuaternion(promptVisibleRotation, duration)
                .SetEase(promptShowEase));

        if (promptImageRenderer != null)
        {
            promptAnimationSequence.Join(
                promptImageRenderer.DOFade(promptVisibleColor.a, duration)
                    .SetEase(promptShowEase));
        }

        promptAnimationSequence
            .SetTarget(interactPrompt)
            .OnComplete(() => promptAnimationSequence = null);
    }

    private void PlayPromptHideAnimation()
    {
        KillPromptAnimation();
        isPromptHideAnimating = true;

        Transform promptTransform = interactPrompt.transform;
        float duration = Mathf.Max(0f, promptShowDuration);
        if (duration <= 0f)
        {
            CompletePromptHideAnimation();
            return;
        }

        promptAnimationSequence = DOTween.Sequence();
        promptAnimationSequence.Join(
            promptTransform.DOScale(promptVisibleScale * promptStartScale, duration)
                .SetEase(promptShowEase));
        promptAnimationSequence.Join(
            promptTransform.DOLocalRotateQuaternion(
                    promptVisibleRotation * Quaternion.Euler(0f, 0f, promptStartRotation),
                    duration)
                .SetEase(promptShowEase));

        if (promptImageRenderer != null)
        {
            promptAnimationSequence.Join(
                promptImageRenderer.DOFade(0f, duration)
                    .SetEase(promptShowEase));
        }

        promptAnimationSequence
            .SetTarget(interactPrompt)
            .OnComplete(CompletePromptHideAnimation);
    }

    private void CompletePromptHideAnimation()
    {
        promptAnimationSequence = null;
        isPromptHideAnimating = false;
        interactPrompt.SetActive(false);
        RestorePromptVisibleState();
    }

    private void PlayPromptPressAnimation()
    {
        if (interactPrompt == null || !interactPrompt.activeSelf)
        {
            return;
        }

        KillPromptAnimation();
        isPromptPressAnimating = true;
        RestorePromptVisibleState();

        Transform promptTransform = interactPrompt.transform;
        float popDuration = Mathf.Max(0f, promptPopDuration);
        float disappearDuration = Mathf.Max(0f, promptDisappearDuration);

        promptAnimationSequence = DOTween.Sequence();
        promptAnimationSequence.Append(
            promptTransform.DOScale(promptVisibleScale * promptPopScale, popDuration)
                .SetEase(Ease.OutBack));

        if (promptImageRenderer != null)
        {
            promptAnimationSequence.Join(
                promptImageRenderer.DOColor(promptPressedColor, popDuration)
                    .SetEase(Ease.OutQuad));
        }

        promptAnimationSequence.Append(
            promptTransform.DOScale(Vector3.zero, disappearDuration)
                .SetEase(Ease.InBack));

        if (promptImageRenderer != null)
        {
            promptAnimationSequence.Join(
                promptImageRenderer.DOFade(0f, disappearDuration)
                    .SetEase(Ease.InQuad));
        }

        promptAnimationSequence
            .SetTarget(interactPrompt)
            .OnComplete(CompletePromptPressAnimation);
    }

    private void CompletePromptPressAnimation()
    {
        promptAnimationSequence = null;
        isPromptPressAnimating = false;
        interactPrompt.SetActive(false);
        RestorePromptVisibleState();
    }

    private void KillPromptAnimation()
    {
        isPromptHideAnimating = false;
        isPromptPressAnimating = false;

        if (promptAnimationSequence == null)
        {
            return;
        }

        promptAnimationSequence.Kill();
        promptAnimationSequence = null;
    }

    private void RestorePromptVisibleState()
    {
        interactPrompt.transform.localScale = promptVisibleScale;
        interactPrompt.transform.localRotation = promptVisibleRotation;

        if (promptImageRenderer != null)
        {
            promptImageRenderer.color = promptVisibleColor;
        }
    }

    private void RefreshInteractPrompt()
    {
        bool shouldShow = mode == ActivationMode.OnInteractPress
            && playerInRange
            && CanActivateTargets();
        SetInteractPromptVisible(shouldShow);
    }

    private bool CanActivateTargets()
    {
        if (isOnCooldown || targets == null || targets.Length == 0)
        {
            return false;
        }

        bool hasTarget = false;
        foreach (SwitchTarget target in targets)
        {
            if (target == null)
            {
                continue;
            }

            hasTarget = true;
            if (target.IsActivationRunning)
            {
                return false;
            }
        }

        return hasTarget;
    }

    private bool IsAnyTargetRunning()
    {
        if (targets == null)
        {
            return false;
        }

        foreach (SwitchTarget target in targets)
        {
            if (target != null && target.IsActivationRunning)
            {
                return true;
            }
        }

        return false;
    }
}
