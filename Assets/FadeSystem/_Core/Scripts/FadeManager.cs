using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using DG.Tweening;

public class FadeManager : MonoBehaviour
{
    [SerializeField] private CanvasGroup _canvasGroup;

    [Space(10)]
    [SerializeField] private float _fadeDuration = 1f;


    private Tween _fadeTween;

    private static FadeManager _instance;


    private void Awake()
    {
        if (_instance == null)
        {
            _instance = this;
            DontDestroyOnLoad(this.gameObject);

            _canvasGroup.alpha = 1f;
        }
        else
            Destroy(this.gameObject);
    }

    private void Start()
    {
        if (_instance != this)
            return;

        StartCoroutine(DelayStartingHideFade());
    }
    private IEnumerator DelayStartingHideFade()
    {
        yield return new WaitForEndOfFrame();
        HideFade();
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

        _fadeTween?.Kill();
    }

    private void OnSceneLoad(Scene scene, LoadSceneMode loadSceneMode)
    {
        if (loadSceneMode != LoadSceneMode.Single)
            return;

        HideFade();
    }


    private void OnDestroy()
    {
        if (_instance != this)
            return;

        _instance = null;
    }


    public static void ShowFade(Action onComplete = null) => _instance?.SetShowFade(true, onComplete);
    public static void HideFade(Action onComplete = null) => _instance?.SetShowFade(false, onComplete);

    private void SetShowFade(bool showFade, Action onComplete)
    {
        _fadeTween?.Kill();

        float targetAlphaValue = (showFade) ?  1f : 0f;

        float tweenSpeed = 1f / _fadeDuration;

        _fadeTween = DOTween.To(() => _canvasGroup.alpha, x => _canvasGroup.alpha = x, targetAlphaValue, tweenSpeed)
            .SetSpeedBased(true)
            .SetEase(Ease.InOutSine)
            .SetUpdate(isIndependentUpdate: true)
            .OnComplete(() => onComplete?.Invoke());
    }
}
