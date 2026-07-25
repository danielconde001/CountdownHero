using UnityEngine;

/// <summary>
/// Base component for anything that can be activated by a TimedSwitch.
/// </summary>
public abstract class SwitchTarget : MonoBehaviour
{
    /// <summary>True from activation until the target has completed its full sequence.</summary>
    public abstract bool IsActivationRunning { get; }

    public abstract void Activate();
}
