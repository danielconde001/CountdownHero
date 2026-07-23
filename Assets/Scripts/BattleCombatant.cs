using UnityEngine;

/// <summary>Small reusable health and damage model for prototype battles.</summary>
public class BattleCombatant : MonoBehaviour
{
    [SerializeField] private int maxHealth = 6;

    public int CurrentHealth { get; private set; }
    public int MaxHealth => maxHealth;
    public bool IsDefeated => CurrentHealth <= 0;

    private void Awake()
    {
        ResetHealth();
    }

    public void Configure(int health)
    {
        maxHealth = Mathf.Max(1, health);
        ResetHealth();
    }

    public void ResetHealth()
    {
        CurrentHealth = maxHealth;
    }

    public void TakeDamage(int amount)
    {
        CurrentHealth = Mathf.Max(0, CurrentHealth - Mathf.Max(0, amount));
    }

    public void Heal(int amount)
    {
        CurrentHealth = Mathf.Min(maxHealth, CurrentHealth + Mathf.Max(0, amount));
    }
}
