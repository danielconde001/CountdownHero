using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Simple menu actions for Play and Exit buttons.
/// </summary>
public sealed class MainMenuButtonActions : MonoBehaviour
{
    [SerializeField] private string firstGameSceneName = "Intro";

    public void PlayGame()
    {
        if (!string.IsNullOrWhiteSpace(firstGameSceneName))
        {
            SceneManager.LoadScene(firstGameSceneName);
        }
    }

    public void ExitGame()
    {
        Application.Quit();
    }
}
