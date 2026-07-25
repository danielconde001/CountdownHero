using UnityEngine;

/// <summary>
/// Updates the player's latest respawn position when they enter this trigger.
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class RespawnCheckpoint : MonoBehaviour
{
    [SerializeField] private Transform respawnPoint;

    private void Awake()
    {
        GetComponent<Collider2D>().isTrigger = true;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        PlayerRespawn playerRespawn = other.GetComponentInParent<PlayerRespawn>();
        if (playerRespawn == null)
        {
            return;
        }

        Vector3 checkpointPosition = respawnPoint != null
            ? respawnPoint.position
            : transform.position;
        playerRespawn.SetCheckpoint(checkpointPosition);
    }
}
