using UnityEngine;

/// <summary>Keeps the isolated demonstration player visible through its physics jump.</summary>
public sealed class ProtagonistPlaygroundCamera : MonoBehaviour
{
    [SerializeField] private Transform _target;

    public void Configure(Transform target) { _target = target; }

    private void LateUpdate()
    {
        if (_target == null) return;
        transform.position = _target.position + new Vector3(0f, 3.3f, -11f);
        transform.LookAt(_target.position + new Vector3(0f, 1.3f, 0f));
    }
}
