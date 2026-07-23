using UnityEngine;
using UnityEngine.Serialization;

/// <summary>
/// Smoothly follows a target within level bounds. The vertical dead zone keeps
/// ordinary jumps steady while still allowing the camera to follow long falls.
/// </summary>
public class PrototypeCameraFollow : MonoBehaviour
{
    [SerializeField] private Transform target;
    [SerializeField] private Vector3 offset = new Vector3(2f, 3.5f, -15f);
    [SerializeField] private float followSpeed = 5f;
    [FormerlySerializedAs("levelBounds")]
    [SerializeField] private Vector2 horizontalBounds = new Vector2(-6.5f, 15.5f);
    [SerializeField] private Vector2 verticalBounds = new Vector2(-10f, 7f);
    [SerializeField] private float verticalDeadZone = 1.25f;

    public void Initialize(Transform followTarget)
    {
        target = followTarget;
    }

    private void LateUpdate()
    {
        if (target == null)
        {
            return;
        }

        float targetX = Mathf.Clamp(
            target.position.x + offset.x,
            horizontalBounds.x,
            horizontalBounds.y);

        float desiredY = Mathf.Clamp(
            target.position.y + offset.y,
            verticalBounds.x,
            verticalBounds.y);

        // Keep normal jumps steady. Once the player leaves this vertical band,
        // move only enough to bring them back to its edge.
        float verticalDifference = desiredY - transform.position.y;
        float targetY = Mathf.Abs(verticalDifference) <= verticalDeadZone
            ? transform.position.y
            : desiredY - Mathf.Sign(verticalDifference) * verticalDeadZone;

        Vector3 destination = new Vector3(targetX, targetY, offset.z);
        transform.position = Vector3.Lerp(transform.position, destination, 1f - Mathf.Exp(-followSpeed * Time.deltaTime));
    }
}
