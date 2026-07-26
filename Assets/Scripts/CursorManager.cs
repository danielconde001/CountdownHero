using UnityEngine;
using UnityEngine.SceneManagement;

public class CursorManager : MonoBehaviour
{
    [SerializeField] private SceneField _mainMenuScene;

    private CursorManager _instance;

    private void Awake()
    {
        if (_instance == null)
        {
            _instance = this;
            DontDestroyOnLoad(this.gameObject);

            SetCustorVisisble();
        }
        else
            Destroy(this.gameObject);
    }

    private void OnEnable()
    {
        if (_instance != this)
            return;

        SceneManager.sceneLoaded += OnSceneLoad;
    }
    private void OnDisable()
    {
        if (_instance != this)
            return;

        SceneManager.sceneLoaded -= OnSceneLoad;
    }

    private void OnSceneLoad(Scene scene, LoadSceneMode loadMode)
    {
        if (loadMode == LoadSceneMode.Single)
            SetCustorVisisble();
    }

    private void SetCustorVisisble()
    {
        Cursor.visible = SceneManager.GetActiveScene().name == _mainMenuScene;
    }
}
