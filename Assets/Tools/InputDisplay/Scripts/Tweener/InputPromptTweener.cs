using UnityEngine;
using DG.Tweening;

public class InputPromptTweener : MonoBehaviour
{
    [HideInInspector][SerializeField] private InputPromptDisplay _promptDisplay;

    private Color _startingColor;

    private Tween _colorTween;

    private const float _tweenDuration = 0.5f;


    private void OnValidate()
    {
        _promptDisplay = this.GetComponent<InputPromptDisplay>();
    }

    private void Awake()
    {
        _startingColor = _promptDisplay.GetColor();
        _promptDisplay.SetColor(Color.clear);
    }

    private void OnDisable()
    {
        _colorTween?.Kill();
    }


    public void ShowPrompt() => SetShowPrompt(true);
    public void HidePrompt() => SetShowPrompt(false);

    private void SetShowPrompt(bool show)
    {
        _colorTween?.Kill();

        Color targetColor = (show) ? _startingColor : Color.clear;
        float tweenSpeed = 1f / _tweenDuration;
        _colorTween = DOTween.To(() => _promptDisplay.GetColor(), x => _promptDisplay.SetColor(x), targetColor, tweenSpeed).SetSpeedBased(true);
    }
}
