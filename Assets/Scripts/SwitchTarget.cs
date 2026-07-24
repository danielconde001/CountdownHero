using UnityEngine;

/// <summary>
/// Base component for anything that can be activated by a TimedSwitch.
/// </summary>
public abstract class SwitchTarget : MonoBehaviour
{
    public abstract void Activate();
}
