using TMPro;
using UnityEngine;
using UnityEngine.UI;


[RequireComponent(typeof(CanvasGroup))]
public class DialogueBubble : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI _textUI;
    [SerializeField] private Image _pointer;

    [Space(10)]
    [SerializeField] private float _padding = 25f;


    [HideInInspector][SerializeField] private Transform _transform;
    [HideInInspector][SerializeField] private RectTransform _rectTransform;
    [HideInInspector][SerializeField] private CanvasGroup _canvasGroup;

    private Camera _mainCamera;

    private static DialogueBubble _instance;


    private void OnValidate()
    {
        _transform = this.transform;
        _rectTransform = this.GetComponent<RectTransform>();
        _canvasGroup = this.GetComponent<CanvasGroup>();

        Setup();
    }

    private void Awake() => Setup();

    private void Setup()
    {
        if (_instance == null)
            _instance = this;

        _mainCamera = Camera.main;
    }

    private void OnDestroy()
    {
        if (_instance == this)
            _instance = null;
    }


    public static void SetDialogue(DialogueSpeaker speaker, string dialogueText, float alpha)
    {
        if (_instance == null)
            return;

        if (_instance._mainCamera == null || speaker == null || alpha <= 0)
        {
            ClearDialogue();
            return;
        }

        _instance._transform.position = _instance._mainCamera.WorldToScreenPoint(speaker.pointerPosition);

        _instance._canvasGroup.alpha = Mathf.Clamp01(alpha);

        _instance.SetText(dialogueText);
    }

    public static void ClearDialogue()
    {
        if (_instance == null)
            return;

        _instance._transform.localPosition = Vector3.zero;

        _instance._canvasGroup.alpha = 0;

        _instance.SetText("");
    }


    private void SetText(string text)
    {
        _textUI.SetText(text);

        if (!string.IsNullOrEmpty(text))
        {
            _textUI.ForceMeshUpdate();

            Vector2 size = _textUI.GetPreferredValues();

            float sizePadding = _padding * 2;
            float pointerHeight = _pointer.rectTransform.sizeDelta.y;

            _rectTransform.sizeDelta = new Vector2(
                size.x + sizePadding,
                size.y + sizePadding + pointerHeight
            );
        }
        else
            _rectTransform.sizeDelta = new Vector2(125, 75);
    }
}
