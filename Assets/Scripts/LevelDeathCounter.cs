using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Tracks deaths per active scene so level-specific UI can react even after reloads.
/// </summary>
public static class LevelDeathCounter
{
    private static readonly Dictionary<string, int> deathsByScene = new Dictionary<string, int>();

    public static event Action<int> DeathCountChanged;

    private static string CurrentSceneKey => SceneManager.GetActiveScene().name;

    // Static state must also reset when entering Play Mode with domain reload disabled.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetState()
    {
        deathsByScene.Clear();
        DeathCountChanged = null;
    }

    public static int GetDeaths()
    {
        return deathsByScene.TryGetValue(CurrentSceneKey, out int deaths) ? deaths : 0;
    }

    public static int IncrementDeaths()
    {
        string sceneKey = CurrentSceneKey;
        int deaths = GetDeaths() + 1;
        deathsByScene[sceneKey] = deaths;
        DeathCountChanged?.Invoke(deaths);
        return deaths;
    }
}
