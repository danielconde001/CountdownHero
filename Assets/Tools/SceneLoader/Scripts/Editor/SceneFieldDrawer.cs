using UnityEditor;
using UnityEngine;

[CustomPropertyDrawer(typeof(SceneField))]
public class SceneFieldDrawer : PropertyDrawer
{
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        SerializedProperty sceneProp = property.FindPropertyRelative("_scene");
        SerializedProperty sceneNameProp = property.FindPropertyRelative("_sceneName");

        EditorGUI.BeginProperty(position, label, property);

        EditorGUI.BeginChangeCheck();

        SceneAsset scene = (SceneAsset)EditorGUI.ObjectField(position, label, sceneProp.objectReferenceValue, typeof(SceneAsset),false);

        if (EditorGUI.EndChangeCheck())
        {
            sceneProp.objectReferenceValue = scene;
            sceneNameProp.stringValue = scene != null ? scene.name : "";
        }

        EditorGUI.EndProperty();
    }
}