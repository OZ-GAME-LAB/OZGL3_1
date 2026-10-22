using UnityEngine;

/// <summary>Visual equipment hook for a future purchase flow; does not own purchase or skill rules.</summary>
[DisallowMultipleComponent]
public sealed class HandSocketEquipment : MonoBehaviour
{
    [SerializeField] private Transform _rightHandSocket;
    [SerializeField] private Transform _leftHandSocket;
    private Animator _animator;
    private ProtagonistAnimationDriver _driver;
    private ProtagonistActionAnimator _actions;
    private PlayerController _player;
    private bool _listeningToActions;
    private SocketWeapon _hiddenWeapon;
    private bool _hiddenWeaponWasActive;
    private SocketWeapon _mountedWeapon;
    private bool _mountedMirroredGrip;
    [SerializeField] private RuntimeAnimatorController _unarmedController;
    [SerializeField] private AnimationClip _unarmedAttack;
    [SerializeField] private SocketWeapon _equipped;
    public SocketWeapon Equipped => _equipped;
    public bool UsesLeftHand => transform.forward.x < 0f;
    public bool MirrorLeft => UsesLeftHand;
    public Transform NearHandSocket => GetNearHandSocket();

    public Transform GetNearHandSocket()
    {
        Cache();
        return UsesLeftHand ? _leftHandSocket ?? _rightHandSocket : _rightHandSocket ?? _leftHandSocket;
    }

    public void Configure(Transform socket, AnimationClip unarmedAttack)
    {
        _rightHandSocket = socket;
        _unarmedAttack = unarmedAttack;
    }

    private void Awake() { Cache(); }

    private void OnEnable()
    {
        Cache();
        SubscribeActions();
        UpdateEquippedHandSocket();
        RefreshWeaponVisibility();
    }

    private void OnDisable()
    {
        UnsubscribeActions();
        RestoreWeaponVisibility();
    }

    private void LateUpdate()
    {
        if (_equipped != null) UpdateEquippedHandSocket();
        RefreshWeaponVisibility();
    }

    private void Cache()
    {
        if (_animator == null) _animator = GetComponent<Animator>();
        if (_driver == null) _driver = GetComponent<ProtagonistAnimationDriver>();
        if (_player == null) _player = GetComponent<PlayerController>();
        if (_actions == null)
        {
            _actions = GetComponent<ProtagonistActionAnimator>();
            if (isActiveAndEnabled) SubscribeActions();
        }
        if (_unarmedController == null && _animator != null) _unarmedController = _animator.runtimeAnimatorController;
        if (_rightHandSocket == null || _leftHandSocket == null)
            foreach (Transform child in GetComponentsInChildren<Transform>(true))
            {
                if (child.name == "WeaponSocket") _rightHandSocket = child;
                else if (child.name == "WeaponSocket_Left") _leftHandSocket = child;
                if (_rightHandSocket != null && _leftHandSocket != null) break;
            }
    }

    public bool Equip(SocketWeapon prefab)
    {
        Cache();
        if (prefab == null || GetNearHandSocket() == null) return false;
        Unequip();
        _equipped = Instantiate(prefab);
        UpdateEquippedHandSocket();
        if (_animator != null && prefab.AnimationOverride != null) _animator.runtimeAnimatorController = prefab.AnimationOverride;
        if (_driver != null && prefab.BasicAttackClip != null){_driver.Configure(prefab.BasicAttackClip);_driver.SetAttackContactNormalized(prefab.AttackContactNormalized);}
        RefreshWeaponVisibility();
        return true;
    }

    public void Unequip()
    {
        RestoreWeaponVisibility();
        if (_equipped != null)
        {
            // Destroy is deferred during play; hide the old prop before another one is mounted.
            _equipped.gameObject.SetActive(false);
            if (Application.isPlaying) Destroy(_equipped.gameObject); else DestroyImmediate(_equipped.gameObject);
            _equipped = null;
        }
        _mountedWeapon = null;
        if (_animator != null && _unarmedController != null) _animator.runtimeAnimatorController = _unarmedController;
        if (_driver != null && _unarmedAttack != null){_driver.Configure(_unarmedAttack);_driver.SetAttackContactNormalized(ProtagonistAnimationDriver.UnarmedAttackContactNormalized);}
    }

    private void SubscribeActions()
    {
        if (_actions == null || _listeningToActions) return;
        _actions.Started += OnActionChanged;
        _actions.Completed += OnActionChanged;
        _actions.Cancelled += OnActionChanged;
        _listeningToActions = true;
    }

    private void UnsubscribeActions()
    {
        if (_actions != null && _listeningToActions)
        {
            _actions.Started -= OnActionChanged;
            _actions.Completed -= OnActionChanged;
            _actions.Cancelled -= OnActionChanged;
        }
        _listeningToActions = false;
    }

    private void OnActionChanged(ProtagonistExtraAction action) => RefreshWeaponVisibility();

    private void UpdateEquippedHandSocket()
    {
        if (_equipped == null) return;
        Transform socket = UsesLeftHand ? _leftHandSocket ?? _rightHandSocket : _rightHandSocket ?? _leftHandSocket;
        if (socket == null) return;
        bool mirrored = _leftHandSocket != null && socket == _leftHandSocket;
        if (_mountedWeapon == _equipped && _equipped.transform.parent == socket && _mountedMirroredGrip == mirrored) return;
        _equipped.AttachTo(socket, mirrored);
        _mountedWeapon = _equipped;
        _mountedMirroredGrip = mirrored;
    }

    private void RefreshWeaponVisibility()
    {
        ProtagonistExtraAction? active = _actions != null ? _actions.ActiveAction : null;
        bool usesNearHand = (_player!=null&&_player.IsDead) || active == ProtagonistExtraAction.Drink || active == ProtagonistExtraAction.Throw ||
            active == ProtagonistExtraAction.Interact || active == ProtagonistExtraAction.Pickup;
        if (!isActiveAndEnabled || !usesNearHand || _equipped == null)
        {
            RestoreWeaponVisibility();
            return;
        }
        if (_hiddenWeapon == _equipped) return;
        RestoreWeaponVisibility();
        _hiddenWeapon = _equipped;
        _hiddenWeaponWasActive = _hiddenWeapon.gameObject.activeSelf;
        _hiddenWeapon.gameObject.SetActive(false);
    }

    private void RestoreWeaponVisibility()
    {
        if (_hiddenWeapon != null) _hiddenWeapon.gameObject.SetActive(_hiddenWeaponWasActive);
        _hiddenWeapon = null;
    }
}
