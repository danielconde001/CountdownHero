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

    private void Update()
    {
        if(playerController2D.IsWallSliding == false)
        {
            if(playerController2D.IsAirborne == false)
            {
                if(playerController2D.IsMoving == true)
                {
                    playerAnimator.SetTrigger("Player Running");
                }
                else
                {
                    playerAnimator.SetTrigger("Player Idle");
                }
            }
            else
            {
                playerAnimator.SetTrigger("Player Mid Air");
                
            }

        }
        else
        {
            playerAnimator.SetTrigger("Player Wall Slide");
        }

        if(playerController2D.IsLedgeClimbing == true)
        {
            playerAnimator.SetTrigger("Player Ledge Grab");
        }

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
