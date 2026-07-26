using UnityEngine;

[RequireComponent(typeof(BoxCollider2D))]
public sealed class SceneLoadTrigger : MonoBehaviour
{
    [SerializeField] private SceneField _sceneToLoad;

    private const string PlayerTag = "Player";

    private BoxCollider2D triggerCollider;
    private bool _isLoading;

    private void Awake()
    {
        ConfigureCollider();
    }

    private void OnValidate()
    {
        ConfigureCollider();
    }

    private void ConfigureCollider()
    {
        if (triggerCollider == null)
        {
            triggerCollider = GetComponent<BoxCollider2D>();
        }

        if (triggerCollider != null)
        {
            triggerCollider.isTrigger = true;
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag(PlayerTag))
        {
            return;
        }

        LoadScene();
    }

    // Kept public so other systems, such as the level-skip component, can use
    // the same assigned destination without duplicating scene-loading logic.
    public void LoadScene()
    {
        if (_isLoading)
        {
            return;
        }

        if (_sceneToLoad == null || !_sceneToLoad.IsAssigned)
        {
            Debug.LogWarning($"{nameof(SceneLoadTrigger)} on '{name}' has no destination scene assigned.", this);
            return;
        }

        _isLoading = true;
        _sceneToLoad.Load();
    }

    private void OnDrawGizmosSelected()
    {
        if (triggerCollider == null)
        {
            triggerCollider = GetComponent<BoxCollider2D>();
        }

        if (triggerCollider == null)
        {
            return;
        }

        Gizmos.color = Color.black;
        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.DrawWireCube(triggerCollider.offset, triggerCollider.size);
    }
}
