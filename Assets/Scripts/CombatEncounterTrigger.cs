using UnityEngine;

/// <summary>Starts its assigned encounter when a player enters this trigger.</summary>
[RequireComponent(typeof(Collider2D))]
public class CombatEncounterTrigger : MonoBehaviour
{
    [SerializeField] private CombatEncounter encounter;

    public void Initialize(CombatEncounter targetEncounter)
    {
        encounter = targetEncounter;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.TryGetComponent(out PlayerController2D player))
        {
            encounter?.Begin(player);
        }
    }
}
