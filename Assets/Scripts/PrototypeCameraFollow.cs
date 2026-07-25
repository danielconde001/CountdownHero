using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

/// <summary>
/// Smoothly follows a target within level bounds. The vertical dead zone keeps
/// ordinary jumps steady while still allowing the camera to follow long falls.
/// </summary>
public class PrototypeCameraFollow : MonoBehaviour
{
    public enum PlatformingMode
    {
        FollowPlayer,
        FixedPosition
    }

    [SerializeField] private Transform target;
    [SerializeField] private Vector3 offset = new Vector3(2f, 3.5f, -15f);
    [SerializeField] private float followSpeed = 5f;
    [FormerlySerializedAs("levelBounds")]
    [SerializeField] private Vector2 horizontalBounds = new Vector2(-6.5f, 25.5f);
    [SerializeField] private Vector2 verticalBounds = new Vector2(-10f, 7f);
    [SerializeField] private float verticalDeadZone = 1.25f;

    [Header("Starting Mode")]
    [SerializeField] private PlatformingMode startingMode = PlatformingMode.FollowPlayer;
    [SerializeField] private Vector2 startingFixedPosition;

    public PlatformingMode CurrentPlatformingMode =>
        zoneOverrides.Count > 0 ? zoneOverrides[^1].Mode : basePlatformingMode;

    public Vector3 CurrentFixedPosition =>
        zoneOverrides.Count > 0 ? zoneOverrides[^1].FixedPosition : baseFixedPosition;

    private bool useBattleFocus;
    private Transform battleFocus;
    private PlatformingMode basePlatformingMode;
    private Vector3 baseFixedPosition;
    private readonly List<ZoneOverride> zoneOverrides = new List<ZoneOverride>();

    private readonly struct ZoneOverride
    {
        public readonly Object Owner;
        public readonly PlatformingMode Mode;
        public readonly Vector3 FixedPosition;

        public ZoneOverride(Object owner, PlatformingMode mode, Vector3 fixedPosition)
        {
            Owner = owner;
            Mode = mode;
            FixedPosition = fixedPosition;
        }
    }

    private void Awake()
    {
        if (target == null)
        {
            target = GameObject.FindGameObjectWithTag("Player").transform;
        }

        basePlatformingMode = startingMode;
        baseFixedPosition = startingFixedPosition;
    }

    public void Initialize(Transform followTarget)
    {
        target = followTarget;
    }

    public void EnterBattleView(Transform focus)
    {
        battleFocus = focus;
        useBattleFocus = true;
    }

    public void ExitBattleView()
    {
        useBattleFocus = false;
    }

    /// <summary>Returns the platforming camera to its normal player-follow behavior.</summary>
    public void FollowPlayer()
    {
        FollowPlayer(false);
    }

    public void FollowPlayer(bool snap = false)
    {
        basePlatformingMode = PlatformingMode.FollowPlayer;
        if (snap && zoneOverrides.Count == 0 && target != null && !useBattleFocus)
        {
            transform.position = GetFollowDestination(ignoreVerticalDeadZone: true);
        }
    }

    /// <summary>Holds the camera at a world-space position until its mode changes.</summary>
    public void FixAt(Vector3 position, bool snap = false)
    {
        baseFixedPosition = position;
        basePlatformingMode = PlatformingMode.FixedPosition;
        if (snap && zoneOverrides.Count == 0 && !useBattleFocus)
        {
            transform.position = position;
        }
    }

    /// <summary>
    /// Adds or updates a temporary zone view. The most recently entered active
    /// zone wins, allowing overlapping camera volumes to restore correctly.
    /// </summary>
    public void SetZoneOverride(
        Object owner,
        PlatformingMode mode,
        Vector3 fixedPosition,
        bool snap = false)
    {
        if (owner == null)
        {
            return;
        }

        RemoveZoneOverride(owner, false);
        zoneOverrides.Add(new ZoneOverride(owner, mode, fixedPosition));

        if (snap && !useBattleFocus)
        {
            SnapToCurrentPlatformingView();
        }
    }

    /// <summary>Releases a zone view and resumes the next active zone or base mode.</summary>
    public void RemoveZoneOverride(Object owner, bool snap = false)
    {
        int index = zoneOverrides.FindLastIndex(entry => entry.Owner == owner);
        if (index < 0)
        {
            return;
        }

        bool wasActiveOverride = index == zoneOverrides.Count - 1;
        zoneOverrides.RemoveAt(index);

        if (snap && wasActiveOverride && !useBattleFocus)
        {
            SnapToCurrentPlatformingView();
        }
    }

    private void LateUpdate()
    {
        // Combat temporarily overrides the platforming mode. When combat ends,
        // the camera naturally resumes whichever platforming mode was active.
        if (useBattleFocus)
        {
            Vector3 battleDestination = new Vector3(battleFocus.position.x, battleFocus.position.y, offset.z);
            transform.position = SmoothTowards(battleDestination);
            return;
        }

        transform.position = SmoothTowards(GetPlatformingDestination(ignoreVerticalDeadZone: false));
    }

    private Vector3 GetPlatformingDestination(bool ignoreVerticalDeadZone)
    {
        if (CurrentPlatformingMode == PlatformingMode.FixedPosition)
        {
            return CurrentFixedPosition;
        }

        if (target == null)
        {
            return transform.position;
        }

        return GetFollowDestination(ignoreVerticalDeadZone);
    }

    private Vector3 GetFollowDestination(bool ignoreVerticalDeadZone)
    {
        float targetX = Mathf.Clamp(
            target.position.x + offset.x,
            horizontalBounds.x,
            horizontalBounds.y);

        float desiredY = Mathf.Clamp(
            target.position.y + offset.y,
            verticalBounds.x,
            verticalBounds.y);

        float targetY = desiredY;
        if (!ignoreVerticalDeadZone)
        {
            // Keep normal jumps steady. Once the player leaves this vertical
            // band, move only enough to bring them back to its edge.
            float verticalDifference = desiredY - transform.position.y;
            targetY = Mathf.Abs(verticalDifference) <= verticalDeadZone
                ? transform.position.y
                : desiredY - Mathf.Sign(verticalDifference) * verticalDeadZone;
        }

        return new Vector3(targetX, targetY, offset.z);
    }

    private void SnapToCurrentPlatformingView()
    {
        transform.position = GetPlatformingDestination(ignoreVerticalDeadZone: true);
    }

    private Vector3 SmoothTowards(Vector3 destination)
    {
        return Vector3.Lerp(
            transform.position,
            destination,
            1f - Mathf.Exp(-followSpeed * Time.deltaTime));
    }
}
