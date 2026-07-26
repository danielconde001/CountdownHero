using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static UnityEngine.Rendering.DebugUI;

public class HealthBar : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI _titleText;
    [SerializeField] private TextMeshProUGUI _valueText;
    [SerializeField] private Image _barFill;

    [Space(10)]
    [SerializeField] private float _tweenDuration = 0.5f;


    private float _currentHealth;

    private Tween _barTween;
    private Tween _textTween;


    private void OnDestroy()
    {
        _barTween?.Kill();
        _textTween?.Kill();
    }

    public void SetHealth(string title, float curHealth, float maxHealth)
    {
        _titleText.text = title;

        if (!this.gameObject.activeInHierarchy)
        {
            _currentHealth = curHealth;
            _barFill.fillAmount = curHealth / maxHealth;
            _valueText.text = $"{Mathf.RoundToInt(curHealth)} / {Mathf.RoundToInt(maxHealth)}";
            return;
        }

        maxHealth = Mathf.Max(1, maxHealth);
        curHealth = Mathf.Clamp(curHealth, 0, maxHealth);

        float previousHealth = _currentHealth;
        _currentHealth = curHealth;

        float previousFill = previousHealth / maxHealth;
        float targetFill = curHealth / maxHealth;

        // Kill previous tweens if health changes again mid-animation
        _barTween?.Kill();
        _textTween?.Kill();

        // Tween the bar fill amount
        _barTween = DOTween.To(() => previousFill, value => _barFill.fillAmount = value, targetFill, _tweenDuration);

        // Tween the displayed health number
        _textTween = DOTween.To(() => previousHealth, value => 
            { int displayHealth = Mathf.RoundToInt(value); _valueText.text = $"{displayHealth} / {Mathf.RoundToInt(maxHealth)}"; }
            , curHealth, _tweenDuration);
    }
}
