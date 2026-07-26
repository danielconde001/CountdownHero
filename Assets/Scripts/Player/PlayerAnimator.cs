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
    [SerializeField] private MMF_Player playerJumpingFeedback;
    [SerializeField] private MMF_Player playerLandingFeedback;

    private bool isMoving = false;
    private bool isAirborne = false;

    public void PlayAnimationTrigger(string triggerName)
    {
        playerIdleFeedback.StopFeedbacks();
        playerAnimator.SetTrigger(triggerName);
    }

    public void ResetAnimatorForBattleMode()
    {
        if(isMoving == true)
        {
            playerIdleFeedback.PlayFeedbacks();
            playerRunningFeedback.StopFeedbacks();
            playerAnimator.SetBool("Player Moving", false);
            isMoving = false;
        }

        if(isAirborne == true)
        {
            playerJumpingFeedback.StopFeedbacks();
            playerLandingFeedback.PlayFeedbacks();
            playerAnimator.SetBool("Player Airborne", false);
            isAirborne = false;
        }
    }

    private void Update()
    {
        if(playerController2D.IsControlLocked == false)
        {
            if(playerController2D.IsAirborne == false)
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
            }

            if(playerController2D.IsAirborne == true && isAirborne == false)
            {
                playerIdleFeedback.StopFeedbacks();
                playerRunningFeedback.StopFeedbacks();
                playerLandingFeedback.StopFeedbacks();
                playerAnimator.SetBool("Player Airborne", playerController2D.IsAirborne);
                isAirborne = true;
            }
            else if(playerController2D.IsAirborne == false && isAirborne == true)
            {
                playerJumpingFeedback.StopFeedbacks();
                playerLandingFeedback.PlayFeedbacks();
                isMoving = playerController2D.IsMoving == true ? false : true;
                playerAnimator.SetBool("Player Airborne", playerController2D.IsAirborne);
                isAirborne = false;
            }
            
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
        else
        {
            if(playerAnimator.GetCurrentAnimatorStateInfo(0).IsName("Player Idle") == true && playerIdleFeedback.IsPlaying == false)
            {
                playerIdleFeedback.PlayFeedbacks();
            }
            else if(playerAnimator.GetCurrentAnimatorStateInfo(0).IsName("Player Idle") != true && playerIdleFeedback.IsPlaying == true)
            {
                playerIdleFeedback.StopFeedbacks();
            }
        }
    }
}
