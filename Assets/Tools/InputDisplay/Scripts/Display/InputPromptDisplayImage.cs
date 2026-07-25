using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Image))]
public class InputPromptDisplayImage : InputPromptDisplay
{
    [HideInInspector][SerializeField] private Image _image;

    protected override void OnValidate()
    {
        _image = this.GetComponent<Image>();

        base.OnValidate();
    }

    protected override void ApplySprite() => _image.sprite = GetActionSprite();
}
