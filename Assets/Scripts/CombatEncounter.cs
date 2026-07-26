using System.Collections;
using MoreMountains.Feedbacks;
using MoreMountains.Tools;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Owns one encounter from trigger entry through battle resolution and return
/// to platforming. Combat tuning lives in the assigned ScriptableObject profiles.
/// </summary>
public class CombatEncounter : MonoBehaviour
{
    private const float IntroPause = 0.5f;
    private const float ActionResultPause = 1.0f;
    private const float HealResultPause = 1.0f;
    private const float BattleCameraHeight = 1.25f;

    [Header("Systems and battle data")]
    [SerializeField] private PrototypeCameraFollow cameraFollow;
    [SerializeField] private CountdownSequenceRunner countdown;
    [SerializeField] private PlayerBattleProfile playerProfile;
    [SerializeField] private EnemyBattleProfile enemyProfile;

    [Header("Scene participants")]
    [SerializeField] private BattleCombatant playerCombatant;
    [SerializeField] private BattleCombatant enemyCombatant;
    [SerializeField] private Animator enemyAnimator;
    [SerializeField] private Transform enemy;

    [Header("Encounter staging")]
    [SerializeField] private Transform playerBattlePoint;
    [SerializeField] private Transform enemyBattlePoint;
    [SerializeField] private Transform attackPoint;
    [SerializeField] private Transform cameraPoint;
    [SerializeField] private TextMesh encounterLabel;
    [SerializeField] private TextMesh healthLabel;
    [Min(0.01f)]
    [SerializeField] private float transitionDuration = 0.55f;
    [SerializeField] private float attackDuration;

    [Header("Feedback")]
    [SerializeField] private MMSoundManager mmSoundManager;
    [SerializeField] private MMF_Player battleStartFeedback;
    [SerializeField] private MMF_Player battleBeforeActionFeedback;
    [SerializeField] private MMF_Player battlePlayerAttackFeedback;
    [SerializeField] private MMF_Player battlePlayerMissFeedback;
    [SerializeField] private MMF_Player battleEnemyAttackFeedback;
    [SerializeField] private MMF_Player battlePlayerDefendFeedback;
    [SerializeField] private MMF_Player battlePlayerHealFeedback;
    [SerializeField] private MMF_Player battleEndFeedback;

    private bool hasStarted;
    private CombatantAttack previousEnemyAttack;
    private PlayerController2D playerController2D;
    private PlayerAnimator playerAnimator;

    private enum PlayerBattleAction
    {
        Attack,
        Heal
    }

    public void Initialize(
        PrototypeCameraFollow followCamera,
        CountdownSequenceRunner countdownRunner,
        PlayerBattleProfile playerBattleProfile,
        EnemyBattleProfile profile,
        BattleCombatant playerStats,
        BattleCombatant enemyStats,
        Transform enemyTransform,
        Transform playerPoint,
        Transform enemyPoint,
        Transform exitPoint,
        TextMesh label,
        TextMesh healthDisplay)
    {
        cameraFollow = followCamera;
        countdown = countdownRunner;
        playerProfile = playerBattleProfile;
        enemyProfile = profile;
        playerCombatant = playerStats;
        enemyCombatant = enemyStats;
        enemy = enemyTransform;
        playerBattlePoint = playerPoint;
        enemyBattlePoint = enemyPoint;
        encounterLabel = label;
        healthLabel = healthDisplay;
    }

    public void Begin(PlayerController2D player)
    {
        if (hasStarted || player == null)
        {
            return;
        }

        playerController2D = player;
        playerAnimator = player.gameObject.GetComponent<PlayerAnimator>();
        hasStarted = true;
        StartCoroutine(RunEncounter());
    }

    private IEnumerator RunEncounter()
    {
        playerController2D.SetControlLocked(true);
        TextMeshFontUtility.ApplyFontMaterial(encounterLabel);
        TextMeshFontUtility.ApplyFontMaterial(healthLabel);

        // Vector3 battleCenter = (playerBattlePoint.position + enemyBattlePoint.position) * 0.5f;
        // battleCenter.y += BattleCameraHeight;
        cameraFollow.EnterBattleView(cameraPoint);
        battleStartFeedback.PlayFeedbacks();

        encounterLabel.gameObject.SetActive(true);
        healthLabel.gameObject.SetActive(true);
        encounterLabel.text = "ENCOUNTER!";

        yield return MoveToBattlePositions(playerController2D.transform);
        yield return new WaitForSeconds(IntroPause);

        playerCombatant.ResetHealth();
        enemyCombatant.Configure(enemyProfile.MaxHealth);
        previousEnemyAttack = null;
        UpdateHealthDisplay();
        yield return RunBattleLoop();

        bool playerWon = enemyCombatant.IsDefeated;
        encounterLabel.text = playerWon ? "VICTORY!" : "DEFEAT";
        if (playerWon)
        {
            playerAnimator.PlayAnimationTrigger("Player Victory");
        }
        else
        {
            //TODO: PUT GAME OVER HERE
            playerAnimator.PlayAnimationTrigger("Player Death");
        }
        yield return new WaitForSeconds(ActionResultPause);

        battleEndFeedback.PlayFeedbacks();
        encounterLabel.gameObject.SetActive(false);
        healthLabel.gameObject.SetActive(false);
        cameraFollow.ExitBattleView();
        playerController2D.SetControlLocked(false);
    }

    private IEnumerator RunBattleLoop()
    {
        while (!playerCombatant.IsDefeated && !enemyCombatant.IsDefeated)
        {
            PlayerBattleAction selectedAction = PlayerBattleAction.Attack;
            yield return ChoosePlayerAction(action => selectedAction = action);

            if (selectedAction == PlayerBattleAction.Attack)
            {
                yield return ResolvePlayerAttack();
            }
            else
            {
                yield return ResolvePlayerHeal();
            }

            if (enemyCombatant.IsDefeated)
            {
                yield break;
            }

            CombatantAttack selectedAttack = enemyProfile.ChooseAttack(previousEnemyAttack);
            if (selectedAttack == null)
            {
                Debug.LogError($"{enemyProfile.DisplayName} has no configured attacks.");
                yield break;
            }

            previousEnemyAttack = selectedAttack;
            encounterLabel.text = $"{selectedAttack.DisplayName}\nDEFEND!";
            yield return new WaitForSeconds(ActionResultPause);

            foreach (CombatantAttackStrike strike in selectedAttack.Strikes)
            {
                battleBeforeActionFeedback.PlayFeedbacks();
                TimingJudgement defenseJudgement = TimingJudgement.Miss;
                yield return PlayCountdown(
                    strike.CountdownPattern,
                    result => defenseJudgement = result);
                
                battleBeforeActionFeedback.StopFeedbacks();
                enemyAnimator.SetTrigger("Enemy Attack");

                switch(defenseJudgement)
                {
                    case TimingJudgement.TooEarly:
                    {
                        battleEnemyAttackFeedback.PlayFeedbacks();
                        playerAnimator.PlayAnimationTrigger("Player Take Hit");
                        break;
                    }
                    case TimingJudgement.Miss:
                    {
                        battleEnemyAttackFeedback.PlayFeedbacks();
                        playerAnimator.PlayAnimationTrigger("Player Take Hit");
                        break;
                    }

                    case TimingJudgement.Good:
                    {
                        battlePlayerDefendFeedback.PlayFeedbacks();
                        playerAnimator.PlayAnimationTrigger("Player Defending");
                        break;
                    }
                    case TimingJudgement.Perfect:
                    {
                        battlePlayerDefendFeedback.PlayFeedbacks();
                        playerAnimator.PlayAnimationTrigger("Player Defending");
                        break;
                    }
                }

                StartCoroutine(MoveToAttackPositionAndGoBack(enemy));
                
                int incomingDamage = GetIncomingDamage(defenseJudgement, strike.Damage);
                playerCombatant.TakeDamage(incomingDamage);
                encounterLabel.text = FormatResult(
                    defenseJudgement,
                    incomingDamage,
                    "DAMAGE TAKEN");
                UpdateHealthDisplay();
                yield return WaitAfterStrike(strike);

                if (playerCombatant.IsDefeated)
                {
                    yield break;
                }
            }
        }
    }

    private IEnumerator ChoosePlayerAction(System.Action<PlayerBattleAction> onSelected)
    {
        PlayerBattleAction selection = PlayerBattleAction.Attack;

        while (IsConfirmHeld())
        {
            yield return null;
        }

        while (true)
        {
            encounterLabel.text = selection == PlayerBattleAction.Attack
                ? "CHOOSE ACTION\n> ATTACK <    HEAL"
                : "CHOOSE ACTION\n  ATTACK    > HEAL <";

            if (WasLeftPressed() || WasRightPressed())
            {
                selection = selection == PlayerBattleAction.Attack
                    ? PlayerBattleAction.Heal
                    : PlayerBattleAction.Attack;
            }

            if (WasConfirmPressed())
            {
                onSelected(selection);
                yield break;
            }

            yield return null;
        }
    }

    private IEnumerator ResolvePlayerAttack()
    {
        CombatantAttack attack = playerProfile.Attack;
        if (attack == null)
        {
            Debug.LogError("The Player Battle Profile has no Attack assigned.");
            yield break;
        }

        encounterLabel.text = attack.DisplayName;
        yield return new WaitForSeconds(IntroPause);

        foreach (CombatantAttackStrike strike in attack.Strikes)
        {
            battleBeforeActionFeedback.PlayFeedbacks();
            TimingJudgement judgement = TimingJudgement.Miss;
            yield return PlayCountdown(
                strike.CountdownPattern,
                result => judgement = result);

            battleBeforeActionFeedback.StopFeedbacks();

            switch(judgement)
            {
                case TimingJudgement.Perfect:
                {
                    battlePlayerAttackFeedback.PlayFeedbacks();
                    break;
                }
                case TimingJudgement.Good:
                {
                    battlePlayerAttackFeedback.PlayFeedbacks();
                    break;
                }
                case TimingJudgement.Miss:
                {
                    battlePlayerMissFeedback.PlayFeedbacks();
                    break;
                }
                case TimingJudgement.TooEarly:
                {
                    battlePlayerMissFeedback.PlayFeedbacks();
                    break;
                }
            }
            
            StartCoroutine(MoveToAttackPositionAndGoBack(playerController2D.transform));
            playerAnimator.PlayAnimationTrigger("Player Attacking");
            int damage = GetAttackDamage(judgement, strike.Damage);
            enemyCombatant.TakeDamage(damage);
            encounterLabel.text = FormatResult(judgement, damage, "DAMAGE");
            UpdateHealthDisplay();

            if(judgement == TimingJudgement.Perfect || judgement == TimingJudgement.Good)
            {
                if(enemyCombatant.IsDefeated == true)
                {
                    enemyAnimator.SetTrigger("Enemy Death");
                    
                }
                else
                {
                    enemyAnimator.SetTrigger("Enemy Take Hit");
                }
            }

            if(enemyCombatant.IsDefeated == true)
            {
                StartCoroutine(MoveToAttackPositionAndStay(playerController2D.transform));
            }
            else
            {
                StartCoroutine(MoveToAttackPositionAndGoBack(playerController2D.transform));
            }

            yield return WaitAfterStrike(strike);

            if (enemyCombatant.IsDefeated)
            {
                yield break;
            }
        }
    }

    private IEnumerator ResolvePlayerHeal()
    {
        encounterLabel.text = "HEAL";
        yield return new WaitForSeconds(IntroPause);

        battleBeforeActionFeedback.PlayFeedbacks();

        TimingJudgement judgement = TimingJudgement.Miss;
        yield return PlayCountdown(
            playerProfile.HealPattern,
            result => judgement = result);

        int healAmount = judgement switch
        {
            TimingJudgement.Perfect => playerProfile.PerfectHealAmount,
            TimingJudgement.Good => playerProfile.GoodHealAmount,
            _ => 0
        };

        battleBeforeActionFeedback.StopFeedbacks();
        battlePlayerHealFeedback.PlayFeedbacks();
        playerAnimator.PlayAnimationTrigger("Player Healing");

        int healthBefore = playerCombatant.CurrentHealth;
        playerCombatant.Heal(healAmount);
        int restoredHealth = playerCombatant.CurrentHealth - healthBefore;
        encounterLabel.text = FormatResult(judgement, restoredHealth, "HP RESTORED");
        UpdateHealthDisplay();
        yield return new WaitForSeconds(HealResultPause);
    }

    private IEnumerator PlayCountdown(
        CountdownPattern pattern,
        System.Action<TimingJudgement> captureResult)
    {
        yield return countdown.Play(encounterLabel, pattern, captureResult);
    }

    private static WaitForSeconds WaitAfterStrike(CombatantAttackStrike strike)
    {
        return new WaitForSeconds(ActionResultPause + strike.DelayAfter);
    }

    private static int GetAttackDamage(TimingJudgement judgement, int strikeDamage)
    {
        return judgement switch
        {
            TimingJudgement.Perfect => strikeDamage + 1,
            TimingJudgement.Good => strikeDamage,
            _ => 0
        };
    }

    private static int GetIncomingDamage(TimingJudgement judgement, int strikeDamage)
    {
        return judgement switch
        {
            TimingJudgement.Perfect => 0,
            TimingJudgement.Good => Mathf.Max(0, strikeDamage - 1),
            TimingJudgement.TooEarly => strikeDamage + 1,
            _ => strikeDamage
        };
    }

    private void UpdateHealthDisplay()
    {
        healthLabel.text =
            $"HERO  {playerCombatant.CurrentHealth}/{playerCombatant.MaxHealth}"
            + $"\n{enemyProfile.DisplayName.ToUpper()} "
            + $"{enemyCombatant.CurrentHealth}/{enemyCombatant.MaxHealth}";
    }

    private static string FormatResult(TimingJudgement judgement, int damage, string damageLabel)
    {
        return $"{FormatJudgement(judgement)}\n{damage} {damageLabel}";
    }

    private static string FormatJudgement(TimingJudgement judgement)
    {
        return judgement switch
        {
            TimingJudgement.Perfect => "PERFECT!",
            TimingJudgement.Good => "GOOD!",
            TimingJudgement.TooEarly => "TOO EARLY!",
            _ => "MISS!"
        };
    }

    private static bool WasLeftPressed()
    {
        return Keyboard.current != null
            && (Keyboard.current.aKey.wasPressedThisFrame
                || Keyboard.current.leftArrowKey.wasPressedThisFrame);
    }

    private static bool WasRightPressed()
    {
        return Keyboard.current != null
            && (Keyboard.current.dKey.wasPressedThisFrame
                || Keyboard.current.rightArrowKey.wasPressedThisFrame);
    }

    private static bool WasConfirmPressed()
    {
        bool space = Keyboard.current != null
            && Keyboard.current.spaceKey.wasPressedThisFrame;
        bool mouse = Mouse.current != null
            && Mouse.current.leftButton.wasPressedThisFrame;
        return space || mouse;
    }

    private static bool IsConfirmHeld()
    {
        bool space = Keyboard.current != null && Keyboard.current.spaceKey.isPressed;
        bool mouse = Mouse.current != null && Mouse.current.leftButton.isPressed;
        return space || mouse;
    }

    private IEnumerator MoveToBattlePositions(Transform player)
    {
        Vector3 playerStart = player.position;
        Vector3 enemyStart = enemy.position;
        float elapsed = 0f;

        while (elapsed < transitionDuration)
        {
            elapsed += Time.deltaTime;
            float progress = Mathf.SmoothStep(0f, 1f, elapsed / transitionDuration);
            player.position = Vector3.Lerp(playerStart, playerBattlePoint.position, progress);
            enemy.position = Vector3.Lerp(enemyStart, enemyBattlePoint.position, progress);
            yield return null;
        }

        playerAnimator.ResetAnimatorForBattleMode();
        player.position = playerBattlePoint.position;
        enemy.position = enemyBattlePoint.position;
    }

    private IEnumerator MoveToAttackPositionAndGoBack(Transform target)
    {
        Vector3 targetStart = target.position;
        float elapsed = 0f;

        while (elapsed < attackDuration)
        {
            elapsed += Time.deltaTime;
            float progress = Mathf.SmoothStep(0f, 1f, elapsed / attackDuration);
            target.position = Vector3.Lerp(targetStart, attackPoint.position, progress);
            yield return null;
        }

        target.position = attackPoint.position;

        yield return new WaitForSeconds(0.5f);

        elapsed = 0f;

        while (elapsed < attackDuration)
        {
            elapsed += Time.deltaTime;
            float progress = Mathf.SmoothStep(0f, 1f, elapsed / attackDuration);
            target.position = Vector3.Lerp(attackPoint.position, targetStart, progress);
            yield return null;
        }      

        target.position = targetStart;
    }

    private IEnumerator MoveToAttackPositionAndStay(Transform target)
    {
        Vector3 targetStart = target.position;
        float elapsed = 0f;

        while (elapsed < attackDuration)
        {
            elapsed += Time.deltaTime;
            float progress = Mathf.SmoothStep(0f, 1f, elapsed / attackDuration);
            target.position = Vector3.Lerp(targetStart, attackPoint.position, progress);
            yield return null;
        }

        target.position = attackPoint.position;
    }

    private static IEnumerator MoveTransform(Transform target, Vector3 destination, float duration)
    {
        Vector3 start = target.position;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            target.position = Vector3.Lerp(start, destination, Mathf.SmoothStep(0f, 1f, elapsed / duration));
            yield return null;
        }

        target.position = destination;
    }
}
