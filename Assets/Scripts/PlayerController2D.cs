using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Owns responsive platforming movement and the player's traversal states.
/// Physics queries use reusable buffers to avoid per-frame allocations.
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
public class PlayerController2D : MonoBehaviour
{
    private const float InputThreshold = 0.25f;
    private const float SurfaceNormalThreshold = 0.6f;
    private const float DefaultGravityScale = 1f;

    [Header("Movement")]
    [SerializeField] private float moveSpeed = 8.5f;
    [SerializeField] private float acceleration = 80f;
    [SerializeField] private float deceleration = 95f;
    [SerializeField] private float airAccelerationMultiplier = 0.75f;

    [Header("Jump")]
    [SerializeField] private float jumpForce = 15f;
    [SerializeField] private float maxJumpHoldTime = 0.16f;
    [SerializeField] private float coyoteTime = 0.11f;
    [SerializeField] private float jumpBufferTime = 0.11f;
    [SerializeField] private float fallGravityMultiplier = 3.25f;
    [SerializeField] private float lowJumpGravityMultiplier = 2.3f;
    [SerializeField] private float jumpCutMultiplier = 0.55f;
    [SerializeField] private float maxFallSpeed = 18f;

    [Header("Input")]
    [SerializeField] private InputActionAsset inputActions;
    [SerializeField] private string actionMapName = "Player";
    [SerializeField] private string moveActionName = "Move";
    [SerializeField] private string jumpActionName = "Jump";

    [Header("Ground Check")]
    [SerializeField] private Transform groundCheckPoint;
    [SerializeField] private Vector2 groundCheckSize = new Vector2(0.55f, 0.12f);
    [SerializeField] private LayerMask groundLayerMask = ~0;

    [Header("Ledge Climb")]
    [SerializeField] private float ledgeGrabDistance = 0.22f;
    [Range(0f, 0.5f)]
    [SerializeField] private float minimumLedgeHeightRatio = 0.05f;
    [SerializeField] private float ledgeReachAboveHead = 0.3f;
    [SerializeField] private float topProbeInset = 0.08f;
    [SerializeField] private float hangGap = 0.035f;
    [Range(0.1f, 0.8f)]
    [SerializeField] private float hangingBodyDropRatio = 0.32f;
    [SerializeField] private float automaticClimbDelay = 0.12f;
    [SerializeField] private float ledgeClimbDuration = 0.22f;
    [SerializeField] private float ledgeTopInset = 0.2f;
    [SerializeField] private float regrabCooldown = 0.2f;

    [Header("Wall Slide")]
    [SerializeField] private float wallCheckDistance = 0.06f;
    [SerializeField] private float wallSlideMinFallSpeed = 0.75f;
    [SerializeField] private float wallSlideMaxFallSpeed = 3.5f;

    /// <summary>True during both the ledge hang and automatic pull-up.</summary>
    public bool IsLedgeClimbing { get; private set; }

    /// <summary>True while downward speed is being limited against a wall.</summary>
    public bool IsWallSliding { get; private set; }

    /// <summary>True while an external system, such as combat, owns the player.</summary>
    public bool IsControlLocked { get; private set; }

    private Rigidbody2D rb;
    private Collider2D bodyCollider;
    private PhysicsMaterial2D runtimeMovementMaterial;
    private ContactFilter2D groundFilter;
    private InputAction moveAction;
    private InputAction jumpAction;

    private Vector2 moveInputVector;
    private float moveInput;
    private float coyoteCounter;
    private float jumpBufferCounter;
    private float jumpHoldCounter;
    private bool jumpCutRequested;
    private bool isGrounded;
    private LedgeState ledgeState;
    private float ledgeClimbTimer;
    private Vector2 ledgeClimbStart;
    private Vector2 ledgeClimbTarget;
    private Vector2 ledgeHangTarget;
    private float ledgeDirection;
    private float ledgeCooldownTimer;
    private bool dropRequested;
    private readonly RaycastHit2D[] ledgeCastHits = new RaycastHit2D[8];
    private readonly Collider2D[] ledgeOverlapResults = new Collider2D[8];
    private readonly Collider2D[] groundCheckResults = new Collider2D[8];

    private enum LedgeState
    {
        None,
        Hanging,
        Climbing,
        Cooldown
    }

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        bodyCollider = GetComponent<Collider2D>();
        rb.gravityScale = DefaultGravityScale;

        // Zero friction prevents horizontal input from pinning the rigidbody
        // against a vertical surface.
        if (bodyCollider != null && bodyCollider.sharedMaterial == null)
        {
            runtimeMovementMaterial = new PhysicsMaterial2D("Player Frictionless")
            {
                friction = 0f,
                bounciness = 0f
            };
            bodyCollider.sharedMaterial = runtimeMovementMaterial;
        }

        // Reuse one filter and fixed-size buffers to avoid generating garbage
        // during the physics loop.
        groundFilter = new ContactFilter2D();
        groundFilter.SetLayerMask(groundLayerMask);
        groundFilter.useTriggers = false;
    }

    private void OnEnable()
    {
        BindInput();
        moveAction?.Enable();
        jumpAction?.Enable();
    }

    private void OnDisable()
    {
        moveAction?.Disable();
        jumpAction?.Disable();

        if (bodyCollider != null)
        {
            bodyCollider.enabled = true;
        }

        if (rb != null)
        {
            rb.gravityScale = DefaultGravityScale;
        }

        IsLedgeClimbing = false;
        IsWallSliding = false;
    }

    private void OnDestroy()
    {
        if (runtimeMovementMaterial != null)
        {
            Destroy(runtimeMovementMaterial);
        }
    }

    private void Update()
    {
        if (IsControlLocked)
        {
            return;
        }

        moveInputVector = moveAction != null ? moveAction.ReadValue<Vector2>() : Vector2.zero;
        moveInput = moveInputVector.x;
        bool jumpPressed = jumpAction != null && jumpAction.WasPressedThisFrame();
        isGrounded = CheckGrounded();

        if (ledgeState == LedgeState.Hanging)
        {
            dropRequested |= moveInputVector.y < -0.5f
                || moveInput * ledgeDirection < -0.5f;
            return;
        }

        if (ledgeState == LedgeState.Climbing)
        {
            return;
        }

        if (jumpPressed)
        {
            jumpBufferCounter = jumpBufferTime;
        }

        if (jumpAction != null && jumpAction.WasReleasedThisFrame() && rb.linearVelocityY > 0f)
        {
            jumpHoldCounter = 0f;
            jumpCutRequested = true;
        }

        if (jumpHoldCounter > 0f)
        {
            jumpHoldCounter -= Time.deltaTime;
            if (jumpHoldCounter <= 0f && rb.linearVelocityY > 0f)
            {
                jumpCutRequested = true;
            }
        }

        if (isGrounded)
        {
            coyoteCounter = coyoteTime;
        }
        else
        {
            coyoteCounter -= Time.deltaTime;
        }

        if (jumpBufferCounter > 0f)
        {
            jumpBufferCounter -= Time.deltaTime;
        }

        if (jumpBufferCounter > 0f && coyoteCounter > 0f)
        {
            PerformJump();
        }
    }

    private void FixedUpdate()
    {
        if (IsControlLocked)
        {
            return;
        }

        isGrounded = CheckGrounded();

        if (ledgeState == LedgeState.Hanging)
        {
            IsWallSliding = false;
            UpdateLedgeHang();
            return;
        }

        if (ledgeState == LedgeState.Climbing)
        {
            IsWallSliding = false;
            UpdateLedgeClimb();
            return;
        }

        if (ledgeState == LedgeState.Cooldown)
        {
            ledgeCooldownTimer -= Time.fixedDeltaTime;
            if (ledgeCooldownTimer <= 0f)
            {
                ledgeState = LedgeState.None;
            }
        }
        else
        {
            TryStartLedgeGrab();
        }

        if (ledgeState == LedgeState.Hanging)
        {
            return;
        }

        // Ledge grabbing has priority. Wall sliding is only considered when the
        // same wall did not produce a valid ledge grab.
        IsWallSliding = CanWallSlide();

        float targetSpeed = moveInput * moveSpeed;
        float speedDelta = Mathf.Abs(targetSpeed) > 0.01f
            ? acceleration * (isGrounded ? 1f : airAccelerationMultiplier)
            : deceleration;
        float newX = Mathf.MoveTowards(rb.linearVelocityX, targetSpeed, speedDelta * Time.fixedDeltaTime);

        float newY = rb.linearVelocityY;
        if (rb.linearVelocityY < 0f)
        {
            newY += Physics2D.gravity.y * (fallGravityMultiplier - 1f) * Time.fixedDeltaTime;
        }
        else if (rb.linearVelocityY > 0f && (jumpHoldCounter <= 0f || !(jumpAction?.IsPressed() ?? false)))
        {
            newY += Physics2D.gravity.y * (lowJumpGravityMultiplier - 1f) * Time.fixedDeltaTime;
        }

        if (jumpCutRequested && rb.linearVelocityY > 0f)
        {
            newY *= jumpCutMultiplier;
            jumpCutRequested = false;
        }

        newY = Mathf.Max(newY, -maxFallSpeed);
        if (IsWallSliding)
        {
            newY = Mathf.Min(newY, -wallSlideMinFallSpeed);
            newY = Mathf.Max(newY, -wallSlideMaxFallSpeed);
        }

        rb.linearVelocity = new Vector2(newX, newY);
    }

    private bool CanWallSlide()
    {
        if (bodyCollider == null
            || isGrounded
            || rb.linearVelocityY > 0f
            || Mathf.Abs(moveInput) < InputThreshold)
        {
            return false;
        }

        float direction = Mathf.Sign(moveInput);
        return TryFindWall(direction, wallCheckDistance, out _);
    }

    private void TryStartLedgeGrab()
    {
        if (bodyCollider == null
            || isGrounded
            || Mathf.Abs(moveInput) < InputThreshold)
        {
            return;
        }

        Bounds bounds = bodyCollider.bounds;
        float direction = Mathf.Sign(moveInput);

        // Casting the whole collider is more consistent than a single chest ray
        // when the player meets a ledge at different heights.
        if (!TryFindWall(direction, ledgeGrabDistance, out RaycastHit2D wall))
        {
            return;
        }

        // Search downward just inside the platform to find its real top surface.
        Vector2 topProbeOrigin = new Vector2(
            wall.point.x + direction * topProbeInset,
            bounds.max.y + ledgeReachAboveHead);
        float topProbeDistance = bounds.size.y + ledgeReachAboveHead;
        int topHitCount = Physics2D.Raycast(
            topProbeOrigin,
            Vector2.down,
            groundFilter,
            ledgeCastHits,
            topProbeDistance);
        RaycastHit2D top = FirstExternalHit(topHitCount);

        if (top.collider == null || top.normal.y < SurfaceNormalThreshold)
        {
            return;
        }

        float ledgeTop = top.point.y;
        float minimumGrabHeight = bounds.min.y + bounds.size.y * minimumLedgeHeightRatio;
        float maximumGrabHeight = bounds.max.y + ledgeReachAboveHead;
        if (ledgeTop < minimumGrabHeight || ledgeTop > maximumGrabHeight)
        {
            return;
        }

        Vector2 standingTarget = new Vector2(
            top.point.x + direction * (bounds.extents.x + ledgeTopInset),
            ledgeTop + bounds.extents.y + 0.03f);

        // Reject the ledge if the player would overlap another collider after
        // being placed on top.
        int overlapCount = Physics2D.OverlapBox(
            standingTarget,
            bounds.size * 0.88f,
            0f,
            groundFilter,
            ledgeOverlapResults);

        for (int i = 0; i < overlapCount; i++)
        {
            Collider2D overlap = ledgeOverlapResults[i];
            if (overlap != null && overlap != bodyCollider)
            {
                return;
            }
        }

        ledgeDirection = direction;
        ledgeHangTarget = new Vector2(
            wall.point.x - direction * (bounds.extents.x + hangGap),
            ledgeTop - bounds.size.y * hangingBodyDropRatio);
        ledgeClimbTarget = standingTarget;
        ledgeState = LedgeState.Hanging;
        IsLedgeClimbing = true;
        dropRequested = false;
        ledgeClimbTimer = 0f;
        rb.linearVelocity = Vector2.zero;
        rb.gravityScale = 0f;
        rb.position = ledgeHangTarget;
    }

    private void UpdateLedgeHang()
    {
        // Hold the rigidbody at a stable anchor before the automatic pull-up.
        rb.linearVelocity = Vector2.zero;
        rb.MovePosition(ledgeHangTarget);
        ledgeClimbTimer += Time.fixedDeltaTime;

        if (dropRequested)
        {
            ExitLedgeHang();
            return;
        }

        if (ledgeClimbTimer < automaticClimbDelay)
        {
            return;
        }

        ledgeState = LedgeState.Climbing;
        ledgeClimbTimer = 0f;
        ledgeClimbStart = rb.position;
        bodyCollider.enabled = false;
    }

    private void UpdateLedgeClimb()
    {
        // Smoothstep gives the placeholder climb a soft start and finish.
        ledgeClimbTimer += Time.fixedDeltaTime;
        float progress = Mathf.Clamp01(ledgeClimbTimer / ledgeClimbDuration);
        float easedProgress = progress * progress * (3f - 2f * progress);
        rb.MovePosition(Vector2.Lerp(ledgeClimbStart, ledgeClimbTarget, easedProgress));

        if (progress < 1f)
        {
            return;
        }

        rb.position = ledgeClimbTarget;
        bodyCollider.enabled = true;
        rb.gravityScale = DefaultGravityScale;
        rb.linearVelocity = Vector2.zero;
        BeginLedgeCooldown();
    }

    private void ExitLedgeHang()
    {
        dropRequested = false;
        rb.gravityScale = DefaultGravityScale;
        rb.position += Vector2.left * ledgeDirection * 0.06f;
        rb.linearVelocity = new Vector2(-ledgeDirection * 1.5f, -2f);
        BeginLedgeCooldown();
    }

    private void BeginLedgeCooldown()
    {
        ledgeState = LedgeState.Cooldown;
        ledgeCooldownTimer = regrabCooldown;
        IsLedgeClimbing = false;
    }

    private RaycastHit2D FirstExternalHit(int hitCount)
    {
        for (int i = 0; i < hitCount; i++)
        {
            RaycastHit2D hit = ledgeCastHits[i];
            if (hit.collider != null && hit.collider != bodyCollider)
            {
                return hit;
            }
        }

        return default;
    }

    private bool TryFindWall(float direction, float distance, out RaycastHit2D wall)
    {
        int hitCount = bodyCollider.Cast(
            Vector2.right * direction,
            groundFilter,
            ledgeCastHits,
            distance);
        wall = FirstExternalHit(hitCount);

        return wall.collider != null
            && Mathf.Abs(wall.normal.x) >= SurfaceNormalThreshold;
    }

    private void PerformJump()
    {
        jumpBufferCounter = 0f;
        coyoteCounter = 0f;
        jumpHoldCounter = maxJumpHoldTime;
        jumpCutRequested = false;

        rb.linearVelocity = new Vector2(rb.linearVelocity.x, 0f);
        rb.AddForce(Vector2.up * jumpForce, ForceMode2D.Impulse);
    }

    /// <summary>
    /// Hands control of the player to or from an external gameplay system.
    /// Combat uses a kinematic body so transition animation is deterministic.
    /// </summary>
    public void SetControlLocked(bool locked)
    {
        if (IsControlLocked == locked)
        {
            return;
        }

        IsControlLocked = locked;
        moveInput = 0f;
        moveInputVector = Vector2.zero;
        jumpBufferCounter = 0f;
        jumpCutRequested = false;
        IsWallSliding = false;

        if (locked)
        {
            ledgeState = LedgeState.None;
            IsLedgeClimbing = false;
            bodyCollider.enabled = true;
            rb.linearVelocity = Vector2.zero;
            rb.bodyType = RigidbodyType2D.Kinematic;
            return;
        }

        rb.bodyType = RigidbodyType2D.Dynamic;
        rb.gravityScale = DefaultGravityScale;
        rb.linearVelocity = Vector2.zero;
    }

    private bool CheckGrounded()
    {
        Vector2 origin = groundCheckPoint != null
            ? (Vector2)groundCheckPoint.position
            : rb.position + Vector2.down * 0.62f;

        int hitCount = Physics2D.OverlapBox(
            origin,
            groundCheckSize,
            0f,
            groundFilter,
            groundCheckResults);

        for (int i = 0; i < hitCount; i++)
        {
            Collider2D hit = groundCheckResults[i];
            if (hit != null && hit.gameObject != gameObject)
            {
                return true;
            }
        }

        return false;
    }

    private void BindInput()
    {
        // A directly assigned asset is supported for prefabs without PlayerInput.
        // Otherwise, use the PlayerInput component as the single action source.
        if (inputActions == null)
        {
            var playerInput = GetComponent<PlayerInput>();
            if (playerInput != null)
            {
                inputActions = playerInput.actions;
            }
        }

        if (inputActions == null)
        {
            return;
        }

        var map = inputActions.FindActionMap(actionMapName, true);
        moveAction = map.FindAction(moveActionName, true);
        jumpAction = map.FindAction(jumpActionName, true);
    }
}
