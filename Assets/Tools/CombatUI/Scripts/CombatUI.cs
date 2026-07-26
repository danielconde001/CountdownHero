using TMPro;
using UnityEngine;
using DG.Tweening;

public class CombatUI : MonoBehaviour
{
    [SerializeField] private CanvasGroup _canvasGroup;

    [Space(10)]
    [SerializeField] private TextMeshProUGUI _combatText;

    [Space(10)]
    [SerializeField] private HealthBar _leftHealthbar;
    [SerializeField] private HealthBar _rightHealthbar;


    private HealthBar _playerHealthbar;
    private HealthBar _enemyHealthbar;


    [HideInInspector][SerializeField] private RectTransform _rectTransform;
    [HideInInspector][SerializeField] private Canvas _canvas;
    [HideInInspector][SerializeField] private Camera _mainCamera;


    private Transform _playerTransform;

    private bool _inCombat;

    private Tween _showTween;

    private static CombatUI _instance;


    private void OnValidate()
    {
        _rectTransform = this.GetComponent<RectTransform>();

        _mainCamera = Camera.main;

        _canvas = this.GetComponent<Canvas>();
        _canvas.worldCamera = _mainCamera;
    }

    private void Awake()
    {
        if (_instance == null)
        {
            _instance = this;

            PlayerController2D player = FindAnyObjectByType<PlayerController2D>();

            if (player != null)
                _playerTransform = player.transform;
        }
    }

    private void OnDisable()
    {
        if (_instance != this)
            return;

        _showTween?.Kill();
    }

    private void OnDestroy()
    {
        if (_instance != this)
            return;

        _instance = null;
    }


    public static void SetupCombat(CombatEncounter combatEncounter, 
        string playerTitle, float playerCurHealth, float playerMaxHealth,
        string enemyTitle, float enemyCurHealth, float enemyMaxHealth)
    {
        if (_instance == null)
        {
            Debug.LogError("No CombatUI in scene.");
            return;
        }

        if (_instance._inCombat)
        {
            Debug.LogWarning("Already in Combat.");
            return;
        }

        _instance._inCombat = true;

        _instance._rectTransform.parent = combatEncounter.transform;
        _instance._rectTransform.localPosition = new Vector3(0, 1.5f, -1);

        _instance._combatText.text = string.Empty;

        bool playerOnLeft = _instance._mainCamera.transform.position.x < _instance._playerTransform.position.x;

        if (playerOnLeft)
        {
            _instance._playerHealthbar = _instance._leftHealthbar;
            _instance._enemyHealthbar = _instance._rightHealthbar;
        }
        else
        {
            _instance._playerHealthbar = _instance._rightHealthbar;
            _instance._enemyHealthbar = _instance._leftHealthbar;
        }

        _instance._playerHealthbar.Setup(playerTitle, playerCurHealth, playerMaxHealth);
        _instance._enemyHealthbar.Setup(enemyTitle, enemyCurHealth, enemyMaxHealth);

        _instance.ShowTween(true);
    }


    public static void SetCombatText(string text)
    {
        if (!CheckCanSet())
            return;

        _instance._combatText.text = text;
    }

    public static void SetPlayerHealth(float health)
    {
        if (!CheckCanSet())
            return;

        _instance._playerHealthbar.SetHealth(health);
    }

    public static void SetEnemyHealth(float health)
    {
        if (!CheckCanSet())
            return;

        _instance._enemyHealthbar.SetHealth(health);
    }


    public static void EndCombat()
    {
        if (!CheckCanSet())
            return;

        _instance._rectTransform.parent = null;
        _instance._rectTransform.localPosition = Vector3.zero;

        _instance._combatText.text = string.Empty;

        _instance._leftHealthbar.Clear();
        _instance._rightHealthbar.Clear();

        _instance.ShowTween(false);

        _instance._inCombat = false;
    }


    private void ShowTween(bool show)
    {
        _showTween?.Kill();

        float targetAlpha = (show) ? 1f : 0f;

        _showTween = DOTween.To(() => _canvasGroup.alpha, a => _canvasGroup.alpha = a, targetAlpha, 0.5f);
    }


    private static bool CheckCanSet()
    {
        if (_instance == null)
        {
            Debug.LogError("No CombatUI in scene.");
            return false;
        }

        if (!_instance._inCombat)
        {
            Debug.LogWarning("Not in Combat.");
            return false;
        }

        return true;
    }
}
