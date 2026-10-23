using UnityEngine;

/// <summary>A rigid consumable mount calibrated once against the character's hand socket.</summary>
[DisallowMultipleComponent]
public sealed class ConsumableHandProp : MonoBehaviour
{
    public void AttachForThrow(Transform socket,bool mirrored=false)
    {
        Quaternion rotation=Quaternion.Euler(0,0,90);
        Vector3 position=new Vector3(0,.045f,0)-rotation*(Vector3.up*.083f);
        transform.SetParent(socket,false);
        transform.localPosition=mirrored?ReflectLocalPosition(position):position;
        transform.localRotation=mirrored?ReflectLocalRotation(rotation):rotation;
        transform.localScale=Vector3.one;
    }
    [SerializeField] private Vector3 _localPosition;
    [SerializeField] private Quaternion _localRotation = Quaternion.identity;

    public Vector3 LocalPosition => _localPosition;
    public Quaternion LocalRotation => _localRotation;

    public void Configure(Vector3 localPosition, Quaternion localRotation)
    {
        _localPosition = localPosition;
        _localRotation = localRotation.normalized;
    }

    public void AttachTo(Transform socket, bool mirrored = false)
    {
        if (socket == null) return;
        transform.SetParent(socket, false);
        transform.localPosition = mirrored ? ReflectLocalPosition(_localPosition) : _localPosition;
        transform.localRotation = mirrored ? ReflectLocalRotation(_localRotation) : _localRotation;
        transform.localScale = Vector3.one;
    }

    public static Vector3 ReflectLocalPosition(Vector3 position) => new Vector3(-position.x, position.y, position.z);

    public static Quaternion ReflectLocalRotation(Quaternion rotation)
        => new Quaternion(rotation.x, -rotation.y, -rotation.z, rotation.w);
}
