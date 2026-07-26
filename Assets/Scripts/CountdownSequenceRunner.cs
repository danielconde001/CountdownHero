using System;
using System.Collections;
using MoreMountains.Tools;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;

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
    [SerializeField] private MMSoundManagerPlayOptions mmSoundManagerPlayOptions;
    [SerializeField] private AudioClip tickAudioClip;
    [SerializeField] private AudioClip tockAudioClip;
    [SerializeField] private AudioClip bellAudioClip;

    private bool isUsingTick = false;

    public IEnumerator Play(
        CountdownPattern pattern,
        Action<TimingJudgement> onFinished)
    {
        if (pattern == null || onFinished == null)
        {
            Debug.LogError("A countdown requires a display, pattern, and completion callback.");
            onFinished?.Invoke(TimingJudgement.Miss);
            yield break;
        }
        
        yield return WaitForActionRelease();

        isUsingTick = false;

        foreach (CountdownBeat beat in pattern.Beats)
        {
            if(isUsingTick == false)
            {
                MMSoundManager.Current.PlaySound(tickAudioClip, mmSoundManagerPlayOptions);
                isUsingTick = true;
            }
            else
            {
                MMSoundManager.Current.PlaySound(tockAudioClip, mmSoundManagerPlayOptions);
                isUsingTick = false;
            }
            
            CombatUI.SetCombatText(beat.Text);
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

        CombatUI.SetCombatText("GO!");
        MMSoundManager.Current.PlaySound(bellAudioClip, mmSoundManagerPlayOptions);

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

/// <summary>
/// Keeps legacy TextMesh renderers visible after assigning a custom font.
/// TextMesh does not always swap its renderer material automatically.
/// </summary>
public static class TextMeshFontUtility
{
    public static void ApplyFontMaterial(TextMesh textMesh)
    {
        if (textMesh == null || textMesh.font == null)
        {
            return;
        }

        Renderer textRenderer = textMesh.GetComponent<Renderer>();
        if (textRenderer != null)
        {
            textRenderer.sharedMaterial = textMesh.font.material;
            textRenderer.shadowCastingMode = ShadowCastingMode.Off;
            textRenderer.receiveShadows = false;
        }
    }
}
