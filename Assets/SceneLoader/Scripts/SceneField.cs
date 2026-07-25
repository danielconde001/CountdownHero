using UnityEngine;
using UnityEngine.SceneManagement;

#if UNITY_EDITOR
using UnityEditor;
#endif

[System.Serializable]
public class SceneField
{
#if UNITY_EDITOR
    [SerializeField] private SceneAsset _scene;
#endif

    [HideInInspector][SerializeField] private string _sceneName;

    public static implicit operator string(SceneField sceneField) => sceneField._sceneName;


    public void Load() => SceneManager.LoadScene(_sceneName);
}
