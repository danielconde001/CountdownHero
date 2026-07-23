using UnityEngine;

/// <summary>
/// Shared player actions and tuning that remain consistent between encounters.
/// Swap the referenced attack asset to change the player's multi-hit sequence.
/// </summary>
[CreateAssetMenu(
    fileName = "Player Battle Profile",
    menuName = "Countdown Hero/Player Battle Profile")]
public class PlayerBattleProfile : ScriptableObject
{
    [SerializeField] private CombatantAttack attack;
    [SerializeField] private CountdownPattern healPattern;
    [SerializeField] private int perfectHealAmount = 3;
    [SerializeField] private int goodHealAmount = 2;

    public CombatantAttack Attack => attack;
    public CountdownPattern HealPattern => healPattern;
    public int PerfectHealAmount => Mathf.Max(0, perfectHealAmount);
    public int GoodHealAmount => Mathf.Max(0, goodHealAmount);

#if UNITY_EDITOR
    public void Configure(
        CombatantAttack attackAction,
        CountdownPattern pattern,
        int perfectAmount,
        int goodAmount)
    {
        attack = attackAction;
        healPattern = pattern;
        perfectHealAmount = perfectAmount;
        goodHealAmount = goodAmount;
    }
#endif
}
