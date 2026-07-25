using UnityEditor;
using UnityEngine;
using System.Collections.Generic;


[CustomPropertyDrawer(typeof(InputControlPathAttribute))]
public class InputControlPathDrawer : PropertyDrawer
{
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        List<string> paths = new();

        paths.AddRange(InputControlPaths.Keyboard);
        paths.AddRange(InputControlPaths.Mouse);
        paths.AddRange(InputControlPaths.Gamepad);

        // Allow empty category values
        if (!string.IsNullOrEmpty(property.stringValue) && !paths.Contains(property.stringValue))
            paths.Insert(0, property.stringValue);


        int index = paths.IndexOf(property.stringValue);

        if (index < 0)
            index = 0;

        index = EditorGUI.Popup(position, label.text, index, paths.ToArray());

        property.stringValue = paths[index];
    }
}
