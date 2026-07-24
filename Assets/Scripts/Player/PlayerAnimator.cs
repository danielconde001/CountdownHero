using UnityEngine;
using MoreMountains.Feedbacks;

public class PlayerAnimator : MonoBehaviour
{   
    [Header("General")]
    [SerializeField] private Animator playerAnimator;
    [SerializeField] private PlayerController2D playerController2D;
    [SerializeField] private SpriteRenderer playerSpriteRenderer;

    [Header("Feedback")]
    [SerializeField] private MMF_Player playerIdleFeedback;
    [SerializeField] private MMF_Player playerRunningFeedback;

    private bool isMoving = false;

    private void Update()
    {
        if(playerController2D.IsMoving == true && isMoving == false)
        {
            playerIdleFeedback.StopFeedbacks();
            playerRunningFeedback.PlayFeedbacks();
            playerAnimator.SetBool("Player Moving", playerController2D.IsMoving);
            isMoving = true;
        }
        else if(playerController2D.IsMoving == false && isMoving == true)
        {
            playerIdleFeedback.PlayFeedbacks();
            playerRunningFeedback.StopFeedbacks();
            playerAnimator.SetBool("Player Moving", playerController2D.IsMoving);
            isMoving = false;
        }

        if(playerController2D.IsAirborne == true)
        {
            playerIdleFeedback.StopFeedbacks();
            playerRunningFeedback.StopFeedbacks();
        }

        playerAnimator.SetBool("Player Airborne", playerController2D.IsAirborne);
        playerAnimator.SetBool("Player Ledge Climbing", playerController2D.IsLedgeClimbing);
        playerAnimator.SetBool("Player Wall Sliding", playerController2D.IsWallSliding);

        if(playerController2D.IsFacingLeft == true)
        {
            playerSpriteRenderer.flipX = false;
        }
        else
        {
            playerSpriteRenderer.flipX = true;
        }
    }
}
