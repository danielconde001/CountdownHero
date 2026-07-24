using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Changes the platforming camera mode while the player crosses a trigger.
/// The zone's transform is used as the fixed camera point unless one is assigned.
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class PlatformingCameraZone : MonoBehaviour
{
    public enum ExitBehavior
    {
        KeepCurrentMode,
        RestorePreviousMode,
        FollowPlayer
    }

    [Header("Camera")]
    [SerializeField] private PrototypeCameraFollow cameraFollow;
    [SerializeField] private PrototypeCameraFollow.PlatformingMode modeOnEnter =
        PrototypeCameraFollow.PlatformingMode.FixedPosition;
    [Tooltip("Optional fixed camera point. If empty, this zone's transform is used.")]
    [SerializeField] private Transform fixedPoint;
    [SerializeField] private bool snapOnEnter;

    [Header("On Exit")]
    [SerializeField] private ExitBehavior exitBehavior = ExitBehavior.RestorePreviousMode;
    [SerializeField] private bool snapOnExit;

    private readonly HashSet<Collider2D> playerColliders = new HashSet<Collider2D>();
    private void Awake()
    {
        Collider2D zoneCollider = GetComponent<Collider2D>();
        zoneCollider.isTrigger = true;
        ResolveCamera();
    }

    private void OnDisable()
    {
        if (playerColliders.Count > 0)
        {
            ApplyExitBehavior();
        }

        playerColliders.Clear();
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.GetComponentInParent<PlayerController2D>() == null
            || !playerColliders.Add(other)
            || playerColliders.Count > 1
            || !ResolveCamera())
        {
            return;
        }

        cameraFollow.SetZoneOverride(this, modeOnEnter, GetCameraPoint(), snapOnEnter);
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (!playerColliders.Remove(other) || playerColliders.Count > 0 || cameraFollow == null)
        {
            return;
        }

        ApplyExitBehavior();
    }

    private void ApplyExitBehavior()
    {
        if (cameraFollow == null)
        {
            return;
        }

        switch (exitBehavior)
        {
            case ExitBehavior.KeepCurrentMode:
                if (modeOnEnter == PrototypeCameraFollow.PlatformingMode.FixedPosition)
                {
                    cameraFollow.FixAt(GetCameraPoint());
                }
                else
                {
                    cameraFollow.FollowPlayer();
                }

                cameraFollow.RemoveZoneOverride(this, snapOnExit);
                break;
            case ExitBehavior.RestorePreviousMode:
                cameraFollow.RemoveZoneOverride(this, snapOnExit);
                break;
            case ExitBehavior.FollowPlayer:
                cameraFollow.RemoveZoneOverride(this, false);
                cameraFollow.FollowPlayer(snapOnExit);
                break;
        }
    }

    private bool ResolveCamera()
    {
        if (cameraFollow != null)
        {
            return true;
        }

        Camera mainCamera = Camera.main;
        cameraFollow = mainCamera != null
            ? mainCamera.GetComponent<PrototypeCameraFollow>()
            : null;

        if (cameraFollow == null)
        {
            Debug.LogWarning($"{name}: PlatformingCameraZone could not find a PrototypeCameraFollow.");
        }

        return cameraFollow != null;
    }

    private Vector3 GetCameraPoint()
    {
        return fixedPoint != null ? fixedPoint.position : transform.position;
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        if (modeOnEnter != PrototypeCameraFollow.PlatformingMode.FixedPosition)
        {
            return;
        }

        Vector3 point = fixedPoint != null ? fixedPoint.position : transform.position;
        Gizmos.color = new Color(0.3f, 0.8f, 1f, 0.9f);
        Gizmos.DrawWireSphere(point, 0.35f);
        Gizmos.DrawLine(transform.position, point);
    }
#endif
}
