using Unity.AppUI.UI;
using UnityEditor;
using UnityEngine;


[CustomPropertyDrawer(typeof(InputPromptEntry))]
public class InputPromptEntryDrawer : PropertyDrawer
{
    private const float _breakSpace = 8f;
    private const float _spacing = 2f;

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        SerializedProperty path = property.FindPropertyRelative("controlPath");

        bool isGamepad = path.stringValue.Contains("<Gamepad>");

        float height = EditorGUIUtility.singleLineHeight * 2 + _spacing;

        if (isGamepad)
        {
            height += _breakSpace; // Space before Overrides header
            height += EditorGUIUtility.singleLineHeight; // Overrides header
            height += (EditorGUIUtility.singleLineHeight + _spacing) * 3; // Xbox, PlayStation, Nintendo
        }

        return height;
    }

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        EditorGUI.BeginProperty(position, label, property);

        SerializedProperty controlPath = property.FindPropertyRelative("controlPath");

        SerializedProperty generic = property.FindPropertyRelative("generic");

        SerializedProperty xbox = property.FindPropertyRelative("xbox");
        SerializedProperty playStation = property.FindPropertyRelative("playStation");
        SerializedProperty nintendo = property.FindPropertyRelative("nintendo");

        float y = position.y;

        DrawField(position, ref y, controlPath, "Control Path");

        DrawField(position, ref y, generic, "Sprite");

        if (controlPath.stringValue.Contains("<Gamepad>"))
        {
            // Space between Generic Sprite and Overrides
            y += _breakSpace;

            // Overrides header
            Rect headerRect = new Rect(position.x, y, position.width, EditorGUIUtility.singleLineHeight);
            EditorGUI.LabelField(headerRect, "Overrides", EditorStyles.boldLabel);
            y += EditorGUIUtility.singleLineHeight + _spacing;

            DrawField(position, ref y, xbox, "Xbox");
            DrawField(position, ref y, playStation, "PlayStation");
            DrawField(position, ref y, nintendo, "Nintendo");
        }

        EditorGUI.EndProperty();
    }

    private void DrawField(Rect position, ref float y, SerializedProperty property, string label)
    {
        Rect rect = new Rect(position.x, y, position.width, EditorGUIUtility.singleLineHeight);

        EditorGUI.PropertyField(rect, property, new GUIContent(label));

        y += EditorGUIUtility.singleLineHeight + _spacing;
    }
}