using UnityEngine;

[System.Serializable]
public struct CombatantAttackStrike
{
    [Tooltip("The rhythm the player must follow for this individual hit.")]
    [SerializeField] private CountdownPattern countdownPattern;
    [Min(0)]
    [SerializeField] private int damage;
    [Min(0f)]
    [Tooltip("Extra pause after resolving this hit. Useful for multi-hit attack pacing.")]
    [SerializeField] private float delayAfter;

    public CountdownPattern CountdownPattern => countdownPattern;
    public int Damage => Mathf.Max(0, damage);
    public float DelayAfter => Mathf.Max(0f, delayAfter);

    public CombatantAttackStrike(CountdownPattern pattern, int strikeDamage, float delay)
    {
        countdownPattern = pattern;
        damage = strikeDamage;
        delayAfter = delay;
    }
}

/// <summary>
/// A reusable move for either the player or an enemy. Multi-hit moves contain
/// several strikes, each with its own countdown, damage, and pacing delay.
/// </summary>
[CreateAssetMenu(
    fileName = "Combatant Attack",
    menuName = "Countdown Hero/Combatant Attack")]
public class CombatantAttack : ScriptableObject
{
    [SerializeField] private string displayName = "Attack";
    [Min(0f)]
    [Tooltip("Relative chance for enemies to select this move. Ignored for player attacks.")]
    [SerializeField] private float selectionWeight = 1f;
    [SerializeField] private CombatantAttackStrike[] strikes;

    public string DisplayName => displayName;
    public float SelectionWeight => Mathf.Max(0f, selectionWeight);
    public CombatantAttackStrike[] Strikes => strikes;

#if UNITY_EDITOR
    public void Configure(
        string attackName,
        float weight,
        CombatantAttackStrike[] attackStrikes)
    {
        displayName = attackName;
        selectionWeight = weight;
        strikes = attackStrikes;
    }
#endif
}
