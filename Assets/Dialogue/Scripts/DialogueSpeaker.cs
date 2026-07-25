using UnityEngine;

public class DialogueSpeaker : MonoBehaviour
{
    [SerializeField] private Vector3 _pointerOffset = Vector3.zero;

    [HideInInspector][SerializeField] private Transform _transform;


    public Vector3 pointerPosition => _transform.position + _pointerOffset;


    private void OnValidate() => _transform = this.transform;
}
