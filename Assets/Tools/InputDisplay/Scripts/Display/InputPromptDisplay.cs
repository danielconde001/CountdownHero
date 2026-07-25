using UnityEngine;
using UnityEngine.InputSystem;

#if UNITY_EDITOR
using UnityEditor;
#endif

public abstract class InputPromptDisplay : MonoBehaviour
{
    [SerializeField] protected InputPromptDatabase _database;
    [SerializeField] protected InputActionReference _action;

    protected virtual void OnValidate()
    {
        #if UNITY_EDITOR
        if (_database == null)
        {
            _database = InputPromptDatabase.GetDataBase();

            if (_database != null)
                EditorUtility.SetDirty(this);
        }
        #endif

        ApplySprite();
    }

    protected void OnEnable()
    {
        ApplySprite();
        InputSystem.onDeviceChange += OnDeviceChange;
    }

    protected void OnDisable()
    {
        InputSystem.onDeviceChange -= OnDeviceChange;
    }

    protected void OnDeviceChange(InputDevice device, InputDeviceChange change) => ApplySprite();


    protected abstract void ApplySprite();


    protected Sprite GetSprite()
    {
        if (_database == null)
        {
            Debug.LogError("No InputPromptDisplay available", this.gameObject);
            return null;
        }

        int bindingIndex = GetActiveBindingIndex();
        if (bindingIndex < 0)
            return null;

        string path = _action.action.bindings[bindingIndex].effectivePath;
        return _database.GetSprite(path);
    }

    private int GetActiveBindingIndex()
    {
        if (_action == null)
            return -1;

        for (int i = 0; i < _action.action.bindings.Count; i++)
        {
            InputBinding binding = _action.action.bindings[i];

            if (binding.isComposite)
                continue;

            if (binding.effectivePath == null)
                continue;

            if (Application.isPlaying && Gamepad.current != null && InputControlPath.Matches(binding.effectivePath, Gamepad.current))
                return i;
            else 
            {
                if (InputControlPath.Matches(binding.effectivePath, Mouse.current))
                    return i;

                if (InputControlPath.Matches(binding.effectivePath, Keyboard.current))
                    return i;
            }
        }

        return -1;
    }
}
