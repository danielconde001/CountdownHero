using UnityEngine;

[CreateAssetMenu(fileName = "New ScriptableEventNoParam", menuName = "ScriptableEvents/ScriptableEventNoParam")]
public class ScriptableEventNoParam : ScriptableObject
{
    public delegate void ScriptableEvent();
    public event ScriptableEvent onRaised;

    public void Raise() => onRaised?.Invoke();
}
