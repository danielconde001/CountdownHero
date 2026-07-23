using UnityEngine;

[System.Serializable]
public struct CountdownBeat
{
    [SerializeField] private string text;
    [Min(0f)]
    [SerializeField] private float duration;

    public string Text => text;
    public float Duration => Mathf.Max(0f, duration);

    public CountdownBeat(string displayText, float beatDuration)
    {
        text = displayText;
        duration = beatDuration;
    }
}

/// <summary>
/// Reusable countdown rhythm and timing windows. Create assets through
/// Assets > Create > Countdown Hero > Countdown Pattern.
/// </summary>
[CreateAssetMenu(
    fileName = "Countdown Pattern",
    menuName = "Countdown Hero/Countdown Pattern")]
public class CountdownPattern : ScriptableObject
{
    [SerializeField] private CountdownBeat[] beats =
    {
        new CountdownBeat("3", 0.6f),
        new CountdownBeat("2", 0.6f),
        new CountdownBeat("1", 0.6f)
    };
    [Min(0f)]
    [SerializeField] private float perfectWindow = 0.16f;
    [Min(0f)]
    [SerializeField] private float goodWindow = 0.36f;
    [Min(0f)]
    [SerializeField] private float inputWindow = 0.6f;

    public CountdownBeat[] Beats => beats;
    public float PerfectWindow => perfectWindow;
    public float GoodWindow => Mathf.Max(perfectWindow, goodWindow);
    public float InputWindow => Mathf.Max(GoodWindow, inputWindow);

#if UNITY_EDITOR
    public void Configure(
        CountdownBeat[] countdownBeats,
        float perfect,
        float good,
        float totalInputWindow)
    {
        beats = countdownBeats;
        perfectWindow = perfect;
        goodWindow = good;
        inputWindow = totalInputWindow;
    }
#endif
}
