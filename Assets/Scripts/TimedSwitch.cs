using System.Collections;
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

    [SerializeField] private ActivationMode mode = ActivationMode.OnTriggerEnter;
    [SerializeField] private SwitchTarget[] targets;
    [SerializeField, Min(0f)] private float cooldown = 0.5f;
    [Header("Prompt")]
    [SerializeField] private GameObject interactPrompt;
    [SerializeField] private string interactPromptText = "Press E";
    [SerializeField] private Vector3 interactPromptOffset = new Vector3(0f, 1.25f, 0f);
    [SerializeField, Min(0.1f)] private float interactPromptFontSize = 0.35f;

    private bool isOnCooldown;
    private bool playerInRange;
    private Collider2D switchCollider;
    private Coroutine cooldownRoutine;

    private void Awake()
    {
        switchCollider = GetComponent<Collider2D>();

        if (switchCollider == null && mode != ActivationMode.OnAttacked)
        {
            Debug.LogWarning($"{name}: TimedSwitch expects a Collider2D for {mode}.");
        }

        ConfigureCollider();
        EnsureInteractPrompt();
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
        SetInteractPromptVisible(false);
    }

    private void Update()
    {
        if (mode != ActivationMode.OnInteractPress || !playerInRange || isOnCooldown)
        {
            RefreshInteractPrompt();
            return;
        }

        if (Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame)
        {
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
        if (isOnCooldown)
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
        if (interactPrompt != null)
        {
            interactPrompt.SetActive(visible);
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
        promptObject.transform.localScale = Vector3.one;

        TextMesh promptText = promptObject.AddComponent<TextMesh>();
        promptText.text = interactPromptText;
        promptText.anchor = TextAnchor.MiddleCenter;
        promptText.alignment = TextAlignment.Center;
        promptText.fontSize = 24;
        promptText.characterSize = interactPromptFontSize;
        promptText.color = Color.white;

        interactPrompt = promptObject;
    }

    private void RefreshInteractPrompt()
    {
        bool shouldShow = mode == ActivationMode.OnInteractPress
            && playerInRange
            && !isOnCooldown;
        SetInteractPromptVisible(shouldShow);
    }
}
