using TMPro;
using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

public class HealthBar : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI _titleText;
    [SerializeField] private TextMeshProUGUI _valueText;
    [SerializeField] private Image _barFill;

    [Space(10)]
    [SerializeField] private float _tweenDuration = 0.5f;


    private float _currentHealth;
    private float _maxHealth;

    private Tween _barTween;
    private Tween _textTween;


    private void OnDestroy()
    {
        _barTween?.Kill();
        _textTween?.Kill();
    }

    public void Setup(string title, float curHealth, float maxHealth)
    {
        _titleText.text = title.ToUpper();

        _currentHealth = curHealth;
        _maxHealth = maxHealth;

        _barFill.fillAmount = curHealth / maxHealth;
        _valueText.text = $"{Mathf.RoundToInt(curHealth)} / {Mathf.RoundToInt(maxHealth)}";
    }


    public void SetHealth(float curHealth)
    {
        curHealth = Mathf.Clamp(curHealth, 0, _maxHealth);

        float previousHealth = _currentHealth;
        _currentHealth = curHealth;

        float previousFill = previousHealth / _maxHealth;
        float targetFill = curHealth / _maxHealth;

        // Kill previous tweens if health changes again mid-animation
        _barTween?.Kill();
        _textTween?.Kill();

        // Tween the bar fill amount
        _barTween = DOTween.To(() => previousFill, value => _barFill.fillAmount = value, targetFill, _tweenDuration);

        // Tween the displayed health number
        _textTween = DOTween.To(() => previousHealth, value => 
            { int displayHealth = Mathf.RoundToInt(value); _valueText.text = $"{displayHealth} / {Mathf.RoundToInt(_maxHealth)}"; }
            , curHealth, _tweenDuration);
    }


    public void Clear()
    {
        _titleText.text = string.Empty;

        _currentHealth = 0;
        _maxHealth = 0;

        _barFill.fillAmount = 0;
        _valueText.text = string.Empty;
    }
}
