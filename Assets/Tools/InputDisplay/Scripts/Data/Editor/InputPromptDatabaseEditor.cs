using UnityEditor;
using UnityEngine;


[CustomEditor(typeof(InputPromptDatabase))]
public class InputPromptDatabaseEditor : Editor
{
    private SerializedProperty _prompts;

    private void OnEnable()
    {
        _prompts = serializedObject.FindProperty("_prompts");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        DrawCategory("Keyboard", "<Keyboard>/");
        DrawCategory("Mouse", "<Mouse>/");
        DrawCategory("Gamepad", "<Gamepad>/");

        serializedObject.ApplyModifiedProperties();
    }

    private void DrawCategory(string title, string prefix)
    {
        EditorGUILayout.Space(10);

        EditorGUILayout.BeginHorizontal();

        EditorGUILayout.LabelField(title, EditorStyles.boldLabel);

        if (GUILayout.Button("+", GUILayout.Width(25)))
            AddPrompt(prefix);

        EditorGUILayout.EndHorizontal();

        EditorGUI.indentLevel++;

        for (int i = 0; i < _prompts.arraySize; i++)
        {
            SerializedProperty entry = _prompts.GetArrayElementAtIndex(i);

            SerializedProperty path = entry.FindPropertyRelative("controlPath");

            if (path.stringValue.StartsWith(prefix))
            {
                float height = EditorGUI.GetPropertyHeight(entry, GUIContent.none, true) + 8f;

                Rect rowRect = EditorGUILayout.GetControlRect(false, height);

                Rect boxRect = new Rect(rowRect.x, rowRect.y, rowRect.width - 30f, height);

                GUI.Box(boxRect, GUIContent.none);

                EditorGUI.PropertyField(new Rect(boxRect.x + 4, boxRect.y + 4, boxRect.width - 8, boxRect.height - 8), entry, GUIContent.none, true);

                Rect removeRect = new Rect(rowRect.x + rowRect.width - 25f, rowRect.y, 25f, height);

                if (GUI.Button(removeRect, "-"))
                {
                    _prompts.DeleteArrayElementAtIndex(i);
                    break;
                }
            }
        }

        EditorGUI.indentLevel--;
    }


    private void AddPrompt(string prefix)
    {
        int index = _prompts.arraySize;

        _prompts.InsertArrayElementAtIndex(index);

        SerializedProperty entry = _prompts.GetArrayElementAtIndex(index);

        entry.FindPropertyRelative("controlPath").stringValue = prefix;

        entry.FindPropertyRelative("generic").objectReferenceValue = null;

        entry.FindPropertyRelative("xbox").objectReferenceValue = null;
        entry.FindPropertyRelative("playStation").objectReferenceValue = null;
        entry.FindPropertyRelative("nintendo").objectReferenceValue = null;

        serializedObject.ApplyModifiedProperties();

        EditorUtility.SetDirty(target);
    }
}