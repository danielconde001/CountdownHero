using UnityEngine;
using UnityEngine.Events;

[RequireComponent(typeof(BoxCollider2D))]
public class TriggerEventBox : MonoBehaviour
{
    [SerializeField] private UnityEvent _onEnter;
    [SerializeField] private UnityEvent _onExit;

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

        _onEnter?.Invoke();
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (other.gameObject.tag != _playerTag)
            return;

        _onExit?.Invoke();
    }


    private void OnDrawGizmos()
    {
        Gizmos.color = Color.green;
        Gizmos.matrix = Matrix4x4.TRS(_transfrom.position, _transfrom.rotation, new Vector3(_transfrom.lossyScale.x, _transfrom.lossyScale.y, 0f));
        Gizmos.DrawWireCube(_boxCollider.offset, _boxCollider.size);
    }
}
