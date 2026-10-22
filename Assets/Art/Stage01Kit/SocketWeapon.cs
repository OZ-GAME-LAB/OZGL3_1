using UnityEngine;

public enum KitWeaponKind { Sword, Staff }

/// <summary>A rigid weapon prop whose root is the grip pivot. No independent rig is needed.</summary>
public sealed class SocketWeapon : MonoBehaviour
{
    [SerializeField] private KitWeaponKind _kind;
    [SerializeField] private Vector3 _gripEuler;
    [SerializeField] private Vector3 _gripPosition = new Vector3(0f, 0.045f, 0f);
    [SerializeField] private RuntimeAnimatorController _animationOverride;
    [SerializeField] private AnimationClip _basicAttackClip;
    [SerializeField,Range(0,1)] private float _attackContactNormalized=.48f;

    public KitWeaponKind Kind => _kind;
    public RuntimeAnimatorController AnimationOverride => _animationOverride;
    public AnimationClip BasicAttackClip => _basicAttackClip;
    public float AttackContactNormalized => _attackContactNormalized;

    public void Configure(KitWeaponKind kind, Vector3 gripEuler, RuntimeAnimatorController animationOverride, AnimationClip basicAttackClip,float attackContactNormalized=.48f)
    {
        _kind = kind; _gripEuler = gripEuler; _animationOverride = animationOverride; _basicAttackClip = basicAttackClip;
        _attackContactNormalized=Mathf.Clamp01(attackContactNormalized);
    }

    public void AttachTo(Transform socket, bool mirroredGrip = false)
    {
        if (socket == null) return;
        transform.SetParent(socket, false);
        transform.localPosition = mirroredGrip ?
            new Vector3(-_gripPosition.x, _gripPosition.y, _gripPosition.z) : _gripPosition;
        Vector3 euler = mirroredGrip ? new Vector3(_gripEuler.x, -_gripEuler.y, -_gripEuler.z) : _gripEuler;
        transform.localRotation = Quaternion.Euler(euler);
        transform.localScale = Vector3.one;
    }
}
