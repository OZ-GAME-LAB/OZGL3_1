using System;
using UnityEngine;

/// <summary>Aligns a retargeted visual skin with the physics root using a small set of skinned landmarks.</summary>
[DefaultExecutionOrder(110)]
[DisallowMultipleComponent]
public sealed class ProtagonistVisualGrounding : MonoBehaviour
{
    [Serializable]
    public struct SkinMarker
    {
        public int vertexIndex;
        public Vector3 position, rightGripDelta, leftGripDelta;
        public int bone0, bone1, bone2, bone3;
        public Vector4 weights;
    }

    [SerializeField] private Transform _visualRoot;
    [SerializeField] private Vector3 _baseLocalPosition;
    [SerializeField] private Quaternion _baseLocalRotation = Quaternion.identity;
    [SerializeField] private SkinnedMeshRenderer _skin;
    [SerializeField] private Transform[] _bones = Array.Empty<Transform>();
    [SerializeField] private Matrix4x4[] _bindPoses = Array.Empty<Matrix4x4>();
    [SerializeField] private SkinMarker[] _soleMarkers = Array.Empty<SkinMarker>();
    [SerializeField] private SkinMarker[] _bodyMarkers = Array.Empty<SkinMarker>();
    [SerializeField] private float _groundOffset;
    [SerializeField, Min(0)] private float _maximumCorrection = .25f;
    private Matrix4x4[] _skinMatrices;
    private Animator _animator;
    private PlayerController _player;
    private int _rightGrip = -1, _leftGrip = -1;
    private Mesh _cachedMesh;
    private bool _preview, _previewGrounded, _previewDead;

    public float LastAppliedOffset { get; private set; }
    public float LastMeasuredMinimum { get; private set; }
    public float LastAppliedYaw { get; private set; }
    public int LastMarkerCount { get; private set; }
    public int SoleMarkerCount => _soleMarkers.Length;
    public int BodyMarkerCount => _bodyMarkers.Length;

    public void Configure(Transform visualRoot, SkinnedMeshRenderer skin, SkinMarker[] soles, SkinMarker[] body,
        float groundOffset, float maximumCorrection = .25f)
    {
        _visualRoot = visualRoot;
        _baseLocalPosition = visualRoot != null ? visualRoot.localPosition : Vector3.zero;
        _baseLocalRotation = visualRoot != null ? visualRoot.localRotation : Quaternion.identity;
        _skin = skin;
        _bones = skin != null ? skin.bones : Array.Empty<Transform>();
        _bindPoses = skin != null && skin.sharedMesh != null ? skin.sharedMesh.bindposes : Array.Empty<Matrix4x4>();
        _soleMarkers = soles ?? Array.Empty<SkinMarker>();
        _bodyMarkers = body ?? Array.Empty<SkinMarker>();
        _groundOffset = groundOffset;
        _maximumCorrection = Mathf.Max(0, maximumCorrection);
        Cache();
        ResetToBase();
    }

    private void Awake()
    {
        // The Player contains a reusable Visual prefab. Only the outer driver owns the same skin.
        if (transform.parent != null && transform.parent.GetComponentInParent<ProtagonistVisualGrounding>(true) != null)
        {
            enabled = false;
            return;
        }
        Cache();
    }

    private void Cache()
    {
        if (_player == null) _player = GetComponent<PlayerController>();
        if (_animator == null) _animator = GetComponent<Animator>();
        if (_skinMatrices == null || _skinMatrices.Length != _bones.Length) _skinMatrices = new Matrix4x4[_bones.Length];
        Mesh mesh = _skin != null ? _skin.sharedMesh : null;
        if (_cachedMesh == mesh) return;
        _cachedMesh = mesh;
        _rightGrip = mesh != null ? mesh.GetBlendShapeIndex("Grip_R") : -1;
        _leftGrip = mesh != null ? mesh.GetBlendShapeIndex("Grip_L") : -1;
    }

    private void LateUpdate()
    {
        Cache();
        if (_preview) { Apply(_previewGrounded, _previewDead); return; }
        bool dead = _player != null && _player.IsDead;
        bool grounded = _player != null && _player.IsGrounded;
        if (_animator != null && _animator.isActiveAndEnabled && _animator.runtimeAnimatorController != null)
        {
            var state = _animator.GetCurrentAnimatorStateInfo(0);
            dead |= state.IsName("Dead");
            // Standalone Visuals have no physics controller, so their Animator selects the preview ground state.
            if (_player == null) grounded = !state.IsName("Jump") && !state.IsName("Air");
        }
        else if (_player == null) grounded = true;
        Apply(grounded, dead);
    }

    // Mecanim must evaluate from the authored hierarchy, before the visual-only late correction.
    // This also prevents the previous frame's offset/yaw from influencing the Humanoid hips solve.
    private void Update() => ResetToBase();

    public void ApplyPreview(bool grounded, bool dead)
    {
        Cache();
        _preview = true;
        _previewGrounded = grounded;
        _previewDead = dead;
        Apply(grounded, dead);
    }

    public void ResetPreview()
    {
        _preview = false;
        ResetToBase();
    }

    private void OnDisable() => ResetToBase();

    private void ResetToBase()
    {
        if (_visualRoot != null)
        {
            _visualRoot.localPosition = _baseLocalPosition;
            _visualRoot.localRotation = _baseLocalRotation;
        }
        LastAppliedOffset = 0;
        LastAppliedYaw = 0;
        LastMarkerCount = 0;
    }

    private void Apply(bool grounded, bool dead)
    {
        ResetToBase();
        if (_visualRoot != null && _animator != null && _animator.isActiveAndEnabled && _animator.runtimeAnimatorController != null)
        {
            var state = _animator.GetCurrentAnimatorStateInfo(0);
            if (state.IsName("Dash") || state.IsName("Hit"))
            {
                LastAppliedYaw = transform.forward.x < 0f ? 40f : -40f;
                // Parent-space Y is vertical even for the source Armature's -90-degree X rotation.
                _visualRoot.localRotation = Quaternion.Euler(0, LastAppliedYaw, 0) * _baseLocalRotation;
            }
        }
        if (!grounded || _visualRoot == null || _bones.Length != _bindPoses.Length || _bones.Length == 0) return;
        SkinMarker[] markers = dead ? _bodyMarkers : _soleMarkers;
        if (markers.Length == 0) return;
        for (int i = 0; i < _bones.Length; i++)
            _skinMatrices[i] = _bones[i] != null ? _bones[i].localToWorldMatrix * _bindPoses[i] : Matrix4x4.identity;
        float rightWeight = _skin != null && _rightGrip >= 0 ? _skin.GetBlendShapeWeight(_rightGrip) * .01f : 0f;
        float leftWeight = _skin != null && _leftGrip >= 0 ? _skin.GetBlendShapeWeight(_leftGrip) * .01f : 0f;
        float minimum = float.PositiveInfinity;
        for (int i = 0; i < markers.Length; i++)
        {
            SkinMarker marker = markers[i];
            Vector3 position = marker.position + marker.rightGripDelta * rightWeight + marker.leftGripDelta * leftWeight;
            float y = 0;
            if (marker.weights.x > 0) y += _skinMatrices[marker.bone0].MultiplyPoint3x4(position).y * marker.weights.x;
            if (marker.weights.y > 0) y += _skinMatrices[marker.bone1].MultiplyPoint3x4(position).y * marker.weights.y;
            if (marker.weights.z > 0) y += _skinMatrices[marker.bone2].MultiplyPoint3x4(position).y * marker.weights.z;
            if (marker.weights.w > 0) y += _skinMatrices[marker.bone3].MultiplyPoint3x4(position).y * marker.weights.w;
            minimum = Mathf.Min(minimum, y);
        }
        LastMeasuredMinimum = minimum;
        LastMarkerCount = markers.Length;
        float worldOffset = Mathf.Clamp(transform.position.y + _groundOffset - minimum, -_maximumCorrection, _maximumCorrection);
        Vector3 localOffset = _visualRoot.parent != null
            ? _visualRoot.parent.InverseTransformVector(Vector3.up * worldOffset) : Vector3.up * worldOffset;
        _visualRoot.localPosition = _baseLocalPosition + localOffset;
        LastAppliedOffset = worldOffset;
    }
}
