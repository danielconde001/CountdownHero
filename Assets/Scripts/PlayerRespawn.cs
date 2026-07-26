using UnityEngine;

/// <summary>
/// Tracks the player's current respawn point and moves them back there after hazards.
/// </summary>
public class PlayerRespawn : MonoBehaviour
{
    [SerializeField] private Transform startingCheckpoint;

    private PlayerController2D playerController;
    private Rigidbody2D body;
    private Vector3 currentCheckpointPosition;

    private void Awake()
    {
        playerController = GetComponent<PlayerController2D>();
        body = GetComponent<Rigidbody2D>();
        currentCheckpointPosition = startingCheckpoint != null
            ? startingCheckpoint.position
            : transform.position;
    }

    public void SetCheckpoint(Vector3 checkpointPosition)
    {
        currentCheckpointPosition = checkpointPosition;
    }

    public void Respawn()
    {
        if (playerController != null && playerController.IsControlLocked)
        {
            playerController.SetControlLocked(false);
        }

        if (body != null)
        {
            body.linearVelocity = Vector2.zero;
            body.angularVelocity = 0f;
            body.position = currentCheckpointPosition;
        }
        else
        {
            transform.position = currentCheckpointPosition;
        }

        Physics2D.SyncTransforms();
    }
}
