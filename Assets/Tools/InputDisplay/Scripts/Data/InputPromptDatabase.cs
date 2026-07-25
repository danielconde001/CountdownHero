using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;

[CreateAssetMenu(fileName = "InputPromptDatabase", menuName = "Input/Input Prompt Database")]
public class InputPromptDatabase : ScriptableObject
{
    [SerializeField] private List<InputPromptEntry> _prompts = new();

    private Dictionary<string, InputPromptEntry> _lookup;


    private void OnEnable() => BuildLookup();

    private void BuildLookup()
    {
        _lookup = new Dictionary<string, InputPromptEntry>();

        foreach (var prompt in _prompts)
        {
            if (string.IsNullOrEmpty(prompt.controlPath))
                continue;

            if (!_lookup.ContainsKey(prompt.controlPath))
                _lookup.Add(prompt.controlPath, prompt);
        }
    }


    public Sprite GetSprite(string controlPath)
    {
        if (_lookup == null)
            BuildLookup();

        if (!_lookup.TryGetValue(controlPath, out InputPromptEntry prompt))
            return null;

        // Gamepad needs device-specific handling
        if (controlPath.Contains("<Gamepad>"))
            return GetGamepadSprite(prompt);

        return prompt.generic;
    }


    private Sprite GetGamepadSprite(InputPromptEntry prompt)
    {
        Gamepad device = Gamepad.current;

        if (device == null)
            return prompt.generic;

        string product = device.description.product.ToLower();

        // PlayStation
        if (product.Contains("dualshock") || product.Contains("dualsense"))
            return prompt.playStation != null ? prompt.playStation : prompt.generic;

        // Nintendo
        if (product.Contains("switch") || product.Contains("nintendo"))
            return prompt.nintendo != null ? prompt.nintendo : prompt.generic;

        // Xbox / Generic Gamepad
        return prompt.xbox != null ? prompt.xbox : prompt.generic;
    }


    public void Refresh() => BuildLookup();


    public static InputPromptDatabase GetDataBase()
    {
        string[] guids = AssetDatabase.FindAssets("t:InputPromptDatabase");

        if (guids.Length > 0)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[0]);
            return AssetDatabase.LoadAssetAtPath<InputPromptDatabase>(path);
        }

        return null;
    }
}