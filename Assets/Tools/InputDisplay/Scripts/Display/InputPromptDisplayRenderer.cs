using UnityEngine;

[RequireComponent(typeof(SpriteRenderer))]
public class InputPromptDisplayRenderer : InputPromptDisplay
{
    [HideInInspector][SerializeField] private SpriteRenderer _spriteRenderer;

    protected override void OnValidate()
    {
        _spriteRenderer = this.GetComponent<SpriteRenderer>();

        base.OnValidate();
    }

    protected override void ApplySprite() => _spriteRenderer.sprite = GetSprite();
}
