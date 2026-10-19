using UnityEngine;

/// <summary>Closes the existing hand mesh without adding bones, materials or gameplay effects.</summary>
[DefaultExecutionOrder(90)]
[DisallowMultipleComponent]
public sealed class ProtagonistHandGrip : MonoBehaviour
{
    [SerializeField] private SkinnedMeshRenderer _skin;
    private HandSocketEquipment _equipment;
    private ProtagonistActionAnimator _actions;
    private PlayerController _player;
    private Animator _animator;
    private Mesh _cachedMesh;
    private int _right = -1, _left = -1;
    private bool _subscribed;
    private bool _throwReleased;
    private bool _preview, _previewRight, _previewLeft;

    public void Configure(SkinnedMeshRenderer skin)
    {
        _skin = skin;
        _cachedMesh = null;
        Cache();
        Write(false, false);
    }

    private void Awake()
    {
        if (transform.parent != null && transform.parent.GetComponentInParent<ProtagonistHandGrip>(true) != null)
        {
            enabled = false;
            return;
        }
        Cache();
    }

    private void OnEnable()
    {
        Cache();
        if (_actions != null && !_subscribed)
        {
            _actions.Started += OnStarted;
            _actions.Committed += OnCommitted;
            _actions.Completed += OnEnded;
            _actions.Cancelled += OnEnded;
            _subscribed = true;
        }
    }

    private void OnDisable()
    {
        if (_actions != null && _subscribed)
        {
            _actions.Started -= OnStarted;
            _actions.Committed -= OnCommitted;
            _actions.Completed -= OnEnded;
            _actions.Cancelled -= OnEnded;
        }
        _subscribed = false;
        _throwReleased = false;
        _preview = false;
        Write(false, false);
    }

    private void Cache()
    {
        if (_skin == null) _skin = GetComponentInChildren<SkinnedMeshRenderer>(true);
        if (_equipment == null) _equipment = GetComponent<HandSocketEquipment>();
        if (_actions == null) _actions = GetComponent<ProtagonistActionAnimator>();
        if (_player == null) _player = GetComponent<PlayerController>();
        if (_animator == null) _animator = GetComponent<Animator>();
        Mesh mesh = _skin != null ? _skin.sharedMesh : null;
        if (mesh == _cachedMesh) return;
        _cachedMesh = mesh;
        _right = mesh != null ? mesh.GetBlendShapeIndex("Grip_R") : -1;
        _left = mesh != null ? mesh.GetBlendShapeIndex("Grip_L") : -1;
    }

    private void LateUpdate()
    {
        Cache();
        if (_preview) { Write(_previewRight, _previewLeft); return; }

        // Mecanim mirrors the side-facing pose, while props use the camera-near hand.
        bool nearLeft = transform.forward.x < 0f;
        bool equipped = _equipment != null && _equipment.Equipped != null;
        bool holding = equipped && _equipment.Equipped.gameObject.activeInHierarchy;
        bool right = holding && !nearLeft;
        bool left = holding && nearLeft;
        ProtagonistExtraAction? active = _actions != null ? _actions.ActiveAction : null;
        if (active == ProtagonistExtraAction.Drink || active == ProtagonistExtraAction.SwordSlash || active == ProtagonistExtraAction.MagicCast)
        {
            right |= !nearLeft;
            left |= nearLeft;
        }
        if (active == ProtagonistExtraAction.Throw)
        {
            right = !nearLeft && !_throwReleased;
            left = nearLeft && !_throwReleased;
        }

        bool attack = _player != null && _player.CurrentStateType == PlayerState.Attack;
        if (_animator != null && _animator.isActiveAndEnabled && _animator.runtimeAnimatorController != null)
        {
            AnimatorStateInfo state = _animator.GetCurrentAnimatorStateInfo(0);
            attack |= state.IsName("Attack") || state.IsName("Punch");
        }
        if (attack && !equipped) right = left = true;
        Write(right, left);
    }

    /// <summary>Explicit pose-capture override. Call ClearPreview to restore automatic action handling.</summary>
    public void ApplyPreview(bool right, bool left)
    {
        Cache();
        _preview = true;
        _previewRight = right;
        _previewLeft = left;
        Write(right, left);
    }

    public void ClearPreview()
    {
        _preview = false;
        Write(false, false);
    }

    private void Write(bool right, bool left)
    {
        if (_skin == null || _skin.sharedMesh == null) return;
        if (_right >= 0 && _right < _skin.sharedMesh.blendShapeCount) _skin.SetBlendShapeWeight(_right, right ? 100f : 0f);
        if (_left >= 0 && _left < _skin.sharedMesh.blendShapeCount) _skin.SetBlendShapeWeight(_left, left ? 100f : 0f);
    }

    private void OnStarted(ProtagonistExtraAction action) { if (action == ProtagonistExtraAction.Throw) _throwReleased = false; }
    private void OnCommitted(ProtagonistExtraAction action) { if (action == ProtagonistExtraAction.Throw) _throwReleased = true; }
    private void OnEnded(ProtagonistExtraAction action) { if (action == ProtagonistExtraAction.Throw) _throwReleased = false; }
}
