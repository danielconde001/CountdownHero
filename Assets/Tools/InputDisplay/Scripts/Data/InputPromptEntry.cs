using System;
using UnityEngine;

[Serializable]
public class InputPromptEntry
{
    [InputControlPath]
    public string controlPath;

    public Sprite generic;

    public Sprite xbox;
    public Sprite playStation;
    public Sprite nintendo;
}