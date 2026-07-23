using System;
using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

public enum TimingJudgement
{
    Perfect,
    Good,
    Miss,
    TooEarly
}

/// <summary>
/// Presents a countdown and converts Space/left-click timing into a judgement.
/// The runner owns input timing only; damage and healing stay in CombatEncounter.
/// </summary>
public class CountdownSequenceRunner : MonoBehaviour
{
    public IEnumerator Play(
        TextMesh display,
        CountdownPattern pattern,
        Action<TimingJudgement> onFinished)
    {
        if (display == null || pattern == null || onFinished == null)
        {
            Debug.LogError("A countdown requires a display, pattern, and completion callback.");
            onFinished?.Invoke(TimingJudgement.Miss);
            yield break;
        }

        yield return WaitForActionRelease();

        foreach (CountdownBeat beat in pattern.Beats)
        {
            display.text = beat.Text;
            for (float elapsed = 0f; elapsed < beat.Duration; elapsed += Time.deltaTime)
            {
                if (WasActionPressed())
                {
                    onFinished(TimingJudgement.TooEarly);
                    yield break;
                }

                yield return null;
            }
        }

        display.text = "GO!";
        for (float goElapsed = 0f; goElapsed < pattern.InputWindow; goElapsed += Time.deltaTime)
        {
            if (WasActionPressed())
            {
                TimingJudgement judgement = goElapsed <= pattern.PerfectWindow
                    ? TimingJudgement.Perfect
                    : goElapsed <= pattern.GoodWindow
                        ? TimingJudgement.Good
                        : TimingJudgement.Miss;
                onFinished(judgement);
                yield break;
            }

            yield return null;
        }

        onFinished(TimingJudgement.Miss);
    }

    private static IEnumerator WaitForActionRelease()
    {
        while (IsActionHeld())
        {
            yield return null;
        }
    }

    private static bool WasActionPressed()
    {
        bool spacePressed = Keyboard.current != null
            && Keyboard.current.spaceKey.wasPressedThisFrame;
        bool mousePressed = Mouse.current != null
            && Mouse.current.leftButton.wasPressedThisFrame;
        return spacePressed || mousePressed;
    }

    private static bool IsActionHeld()
    {
        bool spaceHeld = Keyboard.current != null && Keyboard.current.spaceKey.isPressed;
        bool mouseHeld = Mouse.current != null && Mouse.current.leftButton.isPressed;
        return spaceHeld || mouseHeld;
    }
}
