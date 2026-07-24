using UnityEngine;

#if UNITY_EDITOR
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Editor-only debug hotkey that reloads the current scene when F1 is pressed.
/// This is kept out of builds so it can be used freely during level testing.
/// </summary>
public class EditorSceneReloadHotkey : MonoBehaviour
{
#if UNITY_EDITOR
    private void Update()
    {
        if (Keyboard.current == null || !Keyboard.current.f1Key.wasPressedThisFrame)
        {
            return;
        }

        Scene currentScene = SceneManager.GetActiveScene();
        if (!currentScene.IsValid() || string.IsNullOrEmpty(currentScene.path))
        {
            Debug.LogWarning("EditorSceneReloadHotkey could not reload the current scene.");
            return;
        }

        EditorSceneManager.LoadSceneInPlayMode(
            currentScene.path,
            new LoadSceneParameters(LoadSceneMode.Single));
    }
#endif
}
