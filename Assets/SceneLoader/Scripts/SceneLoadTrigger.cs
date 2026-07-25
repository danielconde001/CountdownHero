using UnityEngine;

[RequireComponent(typeof(BoxCollider))]
public class SceneLoadTrigger : MonoBehaviour
{
    [SerializeField] private SceneField _sceneToLoad;

    [HideInInspector][SerializeField] private Transform _transfrom;
    [HideInInspector][SerializeField] private BoxCollider _boxCollider;


    private const string _playerTag = "Player";


    private void OnValidate()
    {
        _transfrom = this.transform;

        _boxCollider = this.GetComponent<BoxCollider>();
        _boxCollider.isTrigger = true;
    }


    private void OnTriggerEnter(Collider other)
    {
        if (other.gameObject.tag != _playerTag)
            return;

        FadeManager.ShowFade(() => _sceneToLoad.Load());
    }


    private void OnDrawGizmos()
    {
        Gizmos.color = Color.black;
        Gizmos.matrix = Matrix4x4.TRS(_transfrom.position, _transfrom.rotation, _transfrom.lossyScale);
        Gizmos.DrawWireCube(Vector3.zero, Vector3.one);
    }
}
