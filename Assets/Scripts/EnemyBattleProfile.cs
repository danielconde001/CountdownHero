using UnityEngine;

/// <summary>
/// Designer-authored enemy stats and move pool, kept separate from visuals.
/// Attack weights are relative; repeating the previous move is discouraged.
/// </summary>
[CreateAssetMenu(
    fileName = "Enemy Battle Profile",
    menuName = "Countdown Hero/Enemy Battle Profile")]
public class EnemyBattleProfile : ScriptableObject
{
    private const float RepeatWeightMultiplier = 0.15f;

    [SerializeField] private string displayName = "Enemy";
    [SerializeField] private int maxHealth = 6;
    [SerializeField] private CombatantAttack[] attacks;

    public string DisplayName => displayName;
    public int MaxHealth => Mathf.Max(1, maxHealth);
    public CombatantAttack[] Attacks => attacks;

    public CombatantAttack ChooseAttack(CombatantAttack previousAttack)
    {
        CombatantAttack fallback = FindFirstValidAttack();
        if (fallback == null)
        {
            return null;
        }

        float totalWeight = 0f;

        foreach (CombatantAttack attack in attacks)
        {
            if (attack != null)
            {
                totalWeight += GetAdjustedWeight(attack, previousAttack);
            }
        }

        if (totalWeight <= 0f)
        {
            return fallback;
        }

        float roll = Random.value * totalWeight;
        foreach (CombatantAttack attack in attacks)
        {
            if (attack == null)
            {
                continue;
            }

            roll -= GetAdjustedWeight(attack, previousAttack);
            if (roll <= 0f)
            {
                return attack;
            }
        }

        // Floating-point rounding can leave a tiny positive roll.
        return fallback;
    }

    private CombatantAttack FindFirstValidAttack()
    {
        if (attacks == null)
        {
            return null;
        }

        foreach (CombatantAttack attack in attacks)
        {
            if (attack != null)
            {
                return attack;
            }
        }

        return null;
    }

    private static float GetAdjustedWeight(
        CombatantAttack attack,
        CombatantAttack previousAttack)
    {
        return attack.SelectionWeight
            * (attack == previousAttack ? RepeatWeightMultiplier : 1f);
    }

#if UNITY_EDITOR
    public void Configure(
        string enemyName,
        int health,
        CombatantAttack[] availableAttacks)
    {
        displayName = enemyName;
        maxHealth = health;
        attacks = availableAttacks;
    }
#endif
}
