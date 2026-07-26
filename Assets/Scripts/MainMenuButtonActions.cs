using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

/// <summary>
/// Simple main menu actions and selection handoff for keyboard/controller UI.
/// </summary>
public sealed class MainMenuButtonActions : MonoBehaviour
{
    [SerializeField] private string firstGameSceneName = "Intro";
    [SerializeField] private GameObject creditsPanel;
    [SerializeField] private GameObject defaultSelectedButton;
    [SerializeField] private GameObject creditsBackButton;
    [SerializeField, Min(0f)] private float initialSelectionDelay = 2.75f;

    private Coroutine initialSelectionRoutine;

    private void Start()
    {
        if (creditsPanel != null)
        {
            creditsPanel.SetActive(false);
        }

        initialSelectionRoutine = StartCoroutine(SelectDefaultAfterIntro());
    }

    private void Update()
    {
        if (creditsPanel == null || !creditsPanel.activeSelf)
        {
            return;
        }

        bool pressedCancel = Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame;
        pressedCancel |= Gamepad.current != null && Gamepad.current.buttonEast.wasPressedThisFrame;

        if (pressedCancel)
        {
            HideCredits();
        }
    }

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

    public void ShowCredits()
    {
        if (creditsPanel == null)
        {
            return;
        }

        StopInitialSelectionRoutine();
        creditsPanel.SetActive(true);
        Select(creditsBackButton);
    }

    public void HideCredits()
    {
        if (creditsPanel != null)
        {
            creditsPanel.SetActive(false);
        }

        Select(defaultSelectedButton);
    }

    private static void Select(GameObject target)
    {
        if (target != null && EventSystem.current != null)
        {
            EventSystem.current.SetSelectedGameObject(target);
        }
    }

    private IEnumerator SelectDefaultAfterIntro()
    {
        if (initialSelectionDelay > 0f)
        {
            yield return new WaitForSecondsRealtime(initialSelectionDelay);
        }

        if (creditsPanel == null || !creditsPanel.activeSelf)
        {
            Select(defaultSelectedButton);
        }

        initialSelectionRoutine = null;
    }

    private void StopInitialSelectionRoutine()
    {
        if (initialSelectionRoutine == null)
        {
            return;
        }

        StopCoroutine(initialSelectionRoutine);
        initialSelectionRoutine = null;
    }
}
