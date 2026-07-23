using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Owns one encounter from trigger entry through battle resolution and return
/// to platforming. Combat tuning lives in the assigned ScriptableObject profiles.
/// </summary>
public class CombatEncounter : MonoBehaviour
{
    private const float IntroPause = 0.35f;
    private const float ActionResultPause = 0.55f;
    private const float HealResultPause = 0.75f;
    private const float BattleCameraHeight = 2.2f;

    [Header("Systems and battle data")]
    [SerializeField] private PrototypeCameraFollow cameraFollow;
    [SerializeField] private CountdownSequenceRunner countdown;
    [SerializeField] private PlayerBattleProfile playerProfile;
    [SerializeField] private EnemyBattleProfile enemyProfile;

    [Header("Scene participants")]
    [SerializeField] private BattleCombatant playerCombatant;
    [SerializeField] private BattleCombatant enemyCombatant;
    [SerializeField] private Transform enemy;

    [Header("Encounter staging")]
    [SerializeField] private Transform playerBattlePoint;
    [SerializeField] private Transform enemyBattlePoint;
    [SerializeField] private Transform playerExitPoint;
    [SerializeField] private TextMesh encounterLabel;
    [SerializeField] private TextMesh healthLabel;
    [Min(0.01f)]
    [SerializeField] private float transitionDuration = 0.55f;

    private bool hasStarted;
    private CombatantAttack previousEnemyAttack;

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
        playerExitPoint = exitPoint;
        encounterLabel = label;
        healthLabel = healthDisplay;
    }

    public void Begin(PlayerController2D player)
    {
        if (hasStarted || player == null)
        {
            return;
        }

        hasStarted = true;
        StartCoroutine(RunEncounter(player));
    }

    private IEnumerator RunEncounter(PlayerController2D player)
    {
        player.SetControlLocked(true);

        Vector3 battleCenter = (playerBattlePoint.position + enemyBattlePoint.position) * 0.5f;
        battleCenter.y += BattleCameraHeight;
        cameraFollow.EnterBattleView(battleCenter);

        encounterLabel.gameObject.SetActive(true);
        healthLabel.gameObject.SetActive(true);
        encounterLabel.text = "ENCOUNTER!";

        yield return MoveToBattlePositions(player.transform);
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
            enemy.gameObject.SetActive(false);
        }
        yield return new WaitForSeconds(ActionResultPause);

        yield return MoveTransform(player.transform, playerExitPoint.position, 0.35f);
        encounterLabel.gameObject.SetActive(false);
        healthLabel.gameObject.SetActive(false);
        cameraFollow.ExitBattleView();
        player.SetControlLocked(false);
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
                TimingJudgement defenseJudgement = TimingJudgement.Miss;
                yield return PlayCountdown(
                    strike.CountdownPattern,
                    result => defenseJudgement = result);

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
            TimingJudgement judgement = TimingJudgement.Miss;
            yield return PlayCountdown(
                strike.CountdownPattern,
                result => judgement = result);

            int damage = GetAttackDamage(judgement, strike.Damage);
            enemyCombatant.TakeDamage(damage);
            encounterLabel.text = FormatResult(judgement, damage, "DAMAGE");
            UpdateHealthDisplay();
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

        player.position = playerBattlePoint.position;
        enemy.position = enemyBattlePoint.position;
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
