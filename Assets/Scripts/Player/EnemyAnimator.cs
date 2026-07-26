using UnityEngine;
using MoreMountains.Feedbacks;

public class EnemyAnimator : MonoBehaviour
{
    [Header("General")]
    [SerializeField] private Animator enemyAnimator;

    [Header("Feedback")]
    [SerializeField] private MMF_Player enemyIdleFeedback;

    private void Update()
    {
        if(enemyAnimator.GetCurrentAnimatorStateInfo(0).IsName("Enemy Idle") == true && enemyIdleFeedback.IsPlaying == false)
        {
            enemyIdleFeedback.PlayFeedbacks();
        }
        else if(enemyAnimator.GetCurrentAnimatorStateInfo(0).IsName("Enemy Idle") == false && enemyIdleFeedback.IsPlaying == true)
        {
            enemyIdleFeedback.StopFeedbacks();
        }
    }
}
