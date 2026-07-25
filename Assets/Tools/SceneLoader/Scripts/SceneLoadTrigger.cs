using UnityEngine;

[RequireComponent(typeof(BoxCollider2D))]
public class SceneLoadTrigger : MonoBehaviour
{
    [SerializeField] private SceneField _sceneToLoad;

    [HideInInspector][SerializeField] private Transform _transfrom;
    [HideInInspector][SerializeField] private BoxCollider2D _boxCollider;


    private const string _playerTag = "Player";


    private void OnValidate()
    {
        _transfrom = this.transform;

        _boxCollider = this.GetComponent<BoxCollider2D>();
        _boxCollider.isTrigger = true;
    }


    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.gameObject.tag != _playerTag)
            return;

        FadeManager.ShowFade(() => _sceneToLoad.Load());
    }


    private void OnDrawGizmos()
    {
        Gizmos.color = Color.black;
        Gizmos.matrix = Matrix4x4.TRS(_transfrom.position, _transfrom.rotation, Vector3.one);
        Gizmos.DrawWireCube(Vector3.zero, new Vector3(_transfrom.lossyScale.x, _transfrom.lossyScale.y, 0f));
    }
}
