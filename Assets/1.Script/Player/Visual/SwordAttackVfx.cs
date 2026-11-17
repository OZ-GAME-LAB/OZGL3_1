using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

/// <summary>
/// Visual-only sword feedback. Sample AFTER posing the character; damage remains
/// owned by SwordComboAttack. Ribbon vertices always come from the actual blade.
/// </summary>
[DisallowMultipleComponent]
[DefaultExecutionOrder(15000)]
public sealed class SwordAttackVfx : MonoBehaviour
{
    [Serializable]
    public sealed class StepSettings
    {
        [Range(0f, 1f)] public float start = 0.3f;
        [Range(0f, 1f)] public float peak = 0.45f;
        [Range(0f, 1f)] public float end = 0.63f;
        [Tooltip("Trail lifetime in seconds at the original clip speed.")]
        [Min(0.01f)] public float tail = 0.1f;
        [Tooltip("Original clip length. Normalized progress scales VFX time with attack speed.")]
        [Min(0.01f)] public float referenceDuration = 0.7f;
        [ColorUsage(true, true)] public Color color = new Color(0.55f, 0.85f, 1f, 1f);
        public GameObject burstPrefab;
        [Range(0f, 1f)] public float burstBladeFraction = 0.7f;
        [Tooltip("Offset in the blade frame: Z follows the blade, Y follows the sweep.")]
        public Vector3 burstOffset;
        public Vector3 burstEuler;
        public Vector3 burstScale = Vector3.one;
        [Tooltip("Follow the current blade during the active swing, then let the short tail fade in place.")]
        public bool followBlade = true;
        [Min(0.01f)] public float burstLifetime = 0.22f;
    }

    private struct BladeSample
    {
        public Vector3 bladeBase;
        public Vector3 bladeTip;
        public Vector3 normal;
        public float born;
        public float strength;
    }

    private sealed class Burst
    {
        public GameObject source;
        public GameObject instance;
        public Vector3 sourceScale;
        public ParticleSystem[] roots;
        public float age;
        public bool active;
    }

    private static readonly int[] AttackHashes =
    {
        Animator.StringToHash("Attack_0"),
        Animator.StringToHash("Attack_1"),
        Animator.StringToHash("Attack_2")
    };

    [Header("Pose sources")]
    [SerializeField] private Animator _animator;
    [SerializeField] private Transform _spaceRoot;
    [SerializeField] private Transform _bladeBase;
    [SerializeField] private Transform _bladeTip;
    [SerializeField] private GameObject _swordProp;
    [SerializeField, Min(0)] private int _animatorLayer = 0;
    [Tooltip("Enable when a Timeline, Playable or capture script calls Sample explicitly.")]
    [SerializeField] private bool _manualSampling;

    [Header("Blade ribbon (material must support vertex color and two-sided rendering)")]
    [SerializeField] private Material _ribbonMaterial;
    [SerializeField, Range(8, 128)] private int _historyCapacity = 48;
    [SerializeField, Range(0f, 0.8f)] private float _baseInset = 0.06f;
    [Tooltip("A thin volume keeps the real sweep visible when seen edge-on.")]
    [SerializeField, Min(0f)] private float _ribbonWidth = 0.018f;
    [SerializeField, Range(0f, 0.2f)] private float _tipOverreach = 0.035f;
    [SerializeField, Range(0f, 1f)] private float _baseOpacity = 0.12f;
    [SerializeField, Min(0.1f)] private float _tailFadePower = 1.5f;
    [SerializeField, Min(0.0001f)] private float _minimumSampleDistance = 0.002f;

    [Header("Break the trail on discontinuities")]
    [SerializeField, Range(5f, 180f)] private float _turnBreakDegrees = 60f;
    [SerializeField, Min(0.05f)] private float _teleportBreakDistance = 1.5f;
    [SerializeField, Min(0.05f)] private float _bladeJumpBreakDistance = 1.2f;

    [Header("Chop / slice / thrust")]
    [SerializeField] private StepSettings[] _steps =
    {
        new StepSettings { start = 0.30f, peak = 0.46f, end = 0.63f, tail = 0.11f, referenceDuration = 0.7f },
        new StepSettings { start = 0.20f, peak = 0.36f, end = 0.53f, tail = 0.10f, referenceDuration = 0.6f },
        new StepSettings { start = 0.12f, peak = 0.25f, end = 0.38f, tail = 0.075f, referenceDuration = 0.7f }
    };

    private PlayerController _player;
    private PlayerWeapon _weapon;
    private BladeSample[] _history;
    private Vector3[] _vertices;
    private Vector3[] _normals;
    private Vector2[] _uv;
    private Color[] _colors;
    private int[] _indices;
    private readonly Burst[] _bursts = new Burst[3];
    private GameObject _generatedRoot;
    private Mesh _mesh;
    private MeshRenderer _renderer;
    private int _head;
    private int _count;
    private int _activeStep = -1;
    private float _lastProgress;
    private float _clock;
    private bool _burstTriggered;
    private bool _usedNextState;
    private bool _hasPreviousPose;
    private Vector3 _previousBase;
    private Vector3 _previousTip;
    private Vector3 _previousNormal = Vector3.forward;
    private Vector3 _previousRootPosition;
    private Quaternion _previousRootRotation;
    private Vector3 _previousRootScale;
    private float _previousFacing;

    public bool ManualSampling { get => _manualSampling; set => _manualSampling = value; }
    public StepSettings[] Steps => _steps;
    public int ActiveStep => _activeStep;
    public int RibbonSampleCount => _count;
    public bool IsEmitting { get; private set; }

    private void OnEnable()
    {
        ResolveSources();
        Clear();
        EnsureResources();
    }

    private void LateUpdate()
    {
        if (!_manualSampling)
            Evaluate(_animator != null && _animator.updateMode == AnimatorUpdateMode.UnscaledTime
                ? Time.unscaledDeltaTime : Time.deltaTime);
    }

    /// <summary>Use for live Animator playback, after the animation/IK pass.</summary>
    public void Evaluate(float deltaTime)
    {
        if (_animator == null) ResolveSources();
        if (_animator == null || !_animator.isActiveAndEnabled ||
            _animator.runtimeAnimatorController == null || _animatorLayer < 0 || _animatorLayer >= _animator.layerCount ||
            (_player != null && (_player.IsDead || _player.CurrentStateType == PlayerState.Hit ||
                                 _player.CurrentStateType == PlayerState.Dash)) ||
            (_weapon != null && _weapon.Current != WeaponType.Sword))
        {
            Clear();
            return;
        }

        AnimatorStateInfo state = _animator.GetCurrentAnimatorStateInfo(_animatorLayer);
        bool usingNext = _animator.IsInTransition(_animatorLayer);
        if (usingNext)
        {
            // A transition out of an attack cancels its visuals immediately. On
            // a transition into the next hit, its own normalized time owns the VFX.
            state = _animator.GetNextAnimatorStateInfo(_animatorLayer);
        }

        int step = FindStep(state.shortNameHash);
        if (usingNext && !_usedNextState && step == _activeStep) Clear();
        _usedNextState = usingNext;
        Sample(step, state.normalizedTime, deltaTime);
    }

    /// <summary>
    /// Deterministic evaluation for both runtime and capture. First pose the
    /// character, then pass the attack's unwrapped normalized time. Pass -1 to cancel.
    /// Gameplay weapon/state guards belong to Evaluate; Sample only needs an active blade.
    /// </summary>
    public void Sample(int step, float normalizedTime, float deltaTime)
    {
        if (step < 0 || step >= 3 || _steps == null || step >= _steps.Length || _steps[step] == null ||
            float.IsNaN(normalizedTime) || float.IsInfinity(normalizedTime) || normalizedTime < 0f ||
            normalizedTime > 1.02f || !EnsureResources() || !BladeIsActive())
        {
            Clear();
            return;
        }

        StepSettings settings = _steps[step];
        float dt = float.IsNaN(deltaTime) || float.IsInfinity(deltaTime) ? 0f : Mathf.Max(0f, deltaTime);
        bool newAttack = step != _activeStep || normalizedTime < _lastProgress - 0.025f;
        float playbackScale;
        if (newAttack)
        {
            Clear();
            _activeStep = step;
            _lastProgress = normalizedTime;
            playbackScale = 1f;
        }
        else
        {
            // Measuring progress avoids relying on AnimatorStateInfo.length's
            // controller/blend-tree speed semantics and also works with Playables.
            playbackScale = dt > 0.000001f
                ? Mathf.Max(0f, normalizedTime - _lastProgress) * Mathf.Max(0.01f, settings.referenceDuration) / dt
                : 0f;
        }

        float effectDelta = dt * playbackScale;
        _clock += effectDelta;
        TickBursts(effectDelta);

        Vector3 baseLocal = _spaceRoot.InverseTransformPoint(_bladeBase.position);
        Vector3 tipLocal = _spaceRoot.InverseTransformPoint(_bladeTip.position);
        Vector3 blade = tipLocal - baseLocal;
        if (blade.sqrMagnitude < 0.000001f) { Clear(); return; }

        float facing = _player != null ? Mathf.Sign(_player.FacingX) : 0f;
        bool discontinuity = _hasPreviousPose &&
            (Quaternion.Angle(_previousRootRotation, _spaceRoot.rotation) > _turnBreakDegrees ||
             (_spaceRoot.position - _previousRootPosition).sqrMagnitude > Square(_teleportBreakDistance) ||
             (tipLocal - _previousTip).sqrMagnitude > Square(_bladeJumpBreakDistance) ||
             (_spaceRoot.lossyScale - _previousRootScale).sqrMagnitude > 0.01f ||
             (facing != 0f && _previousFacing != 0f && facing != _previousFacing));
        if (discontinuity)
        {
            ClearHistory();
            StopBursts();
            _hasPreviousPose = false;
            // Do not fire the same hit's one-shot again after a facing change.
        }

        Vector3 sweep = _hasPreviousPose
            ? ((tipLocal + baseLocal) - (_previousTip + _previousBase)) * 0.5f
            : Vector3.zero;
        Vector3 normal = Vector3.Cross(blade, sweep);
        if (normal.sqrMagnitude < 0.000001f)
        {
            normal = _previousNormal;
            if (Mathf.Abs(Vector3.Dot(normal.normalized, blade.normalized)) > 0.98f)
                normal = Vector3.Cross(blade, Vector3.up);
            if (normal.sqrMagnitude < 0.000001f) normal = Vector3.Cross(blade, Vector3.forward);
        }
        normal.Normalize();
        if (_hasPreviousPose && Vector3.Dot(normal, _previousNormal) < 0f) normal = -normal;

        RemoveExpired(Mathf.Max(0.01f, settings.tail));
        IsEmitting = normalizedTime >= settings.start && normalizedTime <= settings.end;
        if (IsEmitting)
        {
            float strength = Envelope(settings, normalizedTime);
            // Compare against the last EMITTED sample, not the last rendered
            // frame, so sub-threshold movement accumulates at slow attack speeds.
            BladeSample lastEmitted = _count > 0 ? _history[(_head + _count - 1) % _history.Length] : default;
            if (_count == 0 || !_hasPreviousPose ||
                Mathf.Max((baseLocal - lastEmitted.bladeBase).sqrMagnitude, (tipLocal - lastEmitted.bladeTip).sqrMagnitude) >= Square(_minimumSampleDistance))
            {
                Push(baseLocal, tipLocal, normal, strength);
            }
        }

        if (!_burstTriggered && normalizedTime >= settings.peak && normalizedTime <= settings.end)
        {
            _burstTriggered = true;
            PlayBurst(step, settings, normalizedTime, baseLocal, tipLocal, sweep, normal);
        }
        if (settings.followBlade && normalizedTime <= settings.end && _bursts[step] != null && _bursts[step].active)
            PoseBurst(_bursts[step], settings, baseLocal, tipLocal, sweep, normal);

        BuildRibbon(settings);
        _previousBase = baseLocal;
        _previousTip = tipLocal;
        _previousNormal = normal;
        _previousRootPosition = _spaceRoot.position;
        _previousRootRotation = _spaceRoot.rotation;
        _previousRootScale = _spaceRoot.lossyScale;
        _previousFacing = facing;
        _hasPreviousPose = true;
        _lastProgress = normalizedTime;
    }

    /// <summary>Optional explicit binding for prefab builders and review tools.</summary>
    public void Bind(Animator animator, Transform spaceRoot, Transform bladeBase, Transform bladeTip, GameObject swordProp = null)
    {
        Clear();
        if (_generatedRoot != null && _spaceRoot != spaceRoot) ReleaseResources();
        _animator = animator;
        _spaceRoot = spaceRoot != null ? spaceRoot : transform;
        _bladeBase = bladeBase;
        _bladeTip = bladeTip;
        _swordProp = swordProp;
        ResolveSources();
    }

    public void SetRibbonMaterial(Material material)
    {
        _ribbonMaterial = material;
        if (_renderer != null) _renderer.sharedMaterial = material;
    }

    /// <summary>Call after replacing burst prefabs at runtime; normal playback never instantiates.</summary>
    public void RebuildBurstPool()
    {
        StopBursts();
        for (int i = 0; i < _bursts.Length; i++)
        {
            if (_bursts[i] != null && _bursts[i].instance != null) DestroyOwned(_bursts[i].instance);
            _bursts[i] = null;
        }
        if (_generatedRoot != null) CreateBursts();
    }

    public void Clear()
    {
        ClearHistory();
        StopBursts();
        _activeStep = -1;
        _lastProgress = 0f;
        _clock = 0f;
        _burstTriggered = false;
        _hasPreviousPose = false;
        _previousNormal = Vector3.forward;
        IsEmitting = false;
    }

    private bool EnsureResources()
    {
        if (_spaceRoot == null || _bladeBase == null || _bladeTip == null) ResolveSources();
        if (_spaceRoot == null || _bladeBase == null || _bladeTip == null) return false;
        if (_generatedRoot != null) return true;

        int capacity = Mathf.Clamp(_historyCapacity, 8, 128);
        _history = new BladeSample[capacity];
        int renderCapacity = (capacity - 1) * 4 + 1;
        _vertices = new Vector3[renderCapacity * 4];
        _normals = new Vector3[renderCapacity * 4];
        _uv = new Vector2[renderCapacity * 4];
        _colors = new Color[renderCapacity * 4];
        _indices = new int[(renderCapacity - 1) * 24];
        for (int i = 0; i < renderCapacity - 1; i++)
        {
            int v = i * 4;
            int index = i * 24;
            Quad(index, v, v + 1, v + 5, v + 4);
            Quad(index + 6, v + 2, v + 6, v + 7, v + 3);
            Quad(index + 12, v + 1, v + 3, v + 7, v + 5);
            Quad(index + 18, v, v + 4, v + 6, v + 2);
        }

        _generatedRoot = new GameObject("SwordAttackVfx_Runtime") { hideFlags = HideFlags.DontSave, layer = gameObject.layer };
        if (_spaceRoot.gameObject.scene.IsValid() && _generatedRoot.scene != _spaceRoot.gameObject.scene)
            SceneManager.MoveGameObjectToScene(_generatedRoot, _spaceRoot.gameObject.scene);
        _generatedRoot.transform.SetParent(_spaceRoot, false);
        _mesh = new Mesh { name = "Sword blade sweep (runtime)", hideFlags = HideFlags.DontSave };
        _mesh.MarkDynamic();
        _generatedRoot.AddComponent<MeshFilter>().sharedMesh = _mesh;
        _renderer = _generatedRoot.AddComponent<MeshRenderer>();
        _renderer.sharedMaterial = _ribbonMaterial;
        _renderer.shadowCastingMode = ShadowCastingMode.Off;
        _renderer.receiveShadows = false;
        _renderer.lightProbeUsage = LightProbeUsage.Off;
        _renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
        _renderer.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
        _renderer.enabled = false;
        CreateBursts();
        return true;
    }

    private void ResolveSources()
    {
        if (_animator == null) _animator = GetComponentInParent<Animator>();
        if (_spaceRoot == null) _spaceRoot = _animator != null ? _animator.transform : transform;
        if (_player == null) _player = GetComponentInParent<PlayerController>();
        if (_weapon == null) _weapon = GetComponentInParent<PlayerWeapon>();
        Transform search = _swordProp != null ? _swordProp.transform : _spaceRoot;
        if (_bladeBase == null) _bladeBase = FindNamed(search, "BladeBase");
        if (_bladeTip == null) _bladeTip = FindNamed(search, "BladeTip");
        if (_swordProp == null && _bladeBase != null && _bladeBase.parent != null)
            _swordProp = _bladeBase.parent.gameObject;
    }

    private bool BladeIsActive()
    {
        return _bladeBase.gameObject.activeInHierarchy && _bladeTip.gameObject.activeInHierarchy &&
               (_swordProp == null || _swordProp.activeInHierarchy);
    }

    private void Push(Vector3 bladeBase, Vector3 bladeTip, Vector3 normal, float strength)
    {
        if (_count == _history.Length)
        {
            _head = (_head + 1) % _history.Length;
            _count--;
        }
        int index = (_head + _count) % _history.Length;
        _history[index] = new BladeSample
        {
            bladeBase = bladeBase, bladeTip = bladeTip, normal = normal, born = _clock, strength = strength
        };
        _count++;
    }

    private void RemoveExpired(float tail)
    {
        while (_count > 0 && _clock - _history[_head].born > tail)
        {
            _head = (_head + 1) % _history.Length;
            _count--;
        }
    }

    private void BuildRibbon(StepSettings settings)
    {
        if (_count < 2 || _ribbonMaterial == null) { _renderer.enabled = false; return; }
        float tail = Mathf.Max(0.01f, settings.tail);
        Bounds bounds = new Bounds(_history[_head].bladeBase, Vector3.zero);
        int renderCount = (_count - 1) * 4 + 1;
        for (int i = 0; i < renderCount; i++)
        {
            int index = i / 4;
            BladeSample sample = _history[(_head + index) % _history.Length];
            if (index + 1 < _count && i % 4 != 0)
            {
                // Interpolate rotation, not the straight chord between blade tips.
                // This keeps fast swings rounded even at lower render frame rates.
                BladeSample next = _history[(_head + index + 1) % _history.Length];
                float t = (i % 4) * .25f;
                Vector3 direction = Vector3.Slerp(sample.bladeTip - sample.bladeBase, next.bladeTip - next.bladeBase, t);
                sample.bladeBase = Vector3.Lerp(sample.bladeBase, next.bladeBase, t);
                sample.bladeTip = sample.bladeBase + direction;
                sample.normal = Vector3.Slerp(sample.normal, next.normal, t).normalized;
                sample.born = Mathf.Lerp(sample.born, next.born, t);
                sample.strength = Mathf.Lerp(sample.strength, next.strength, t);
            }
            Vector3 blade = sample.bladeTip - sample.bladeBase;
            Vector3 from = sample.bladeBase + blade * _baseInset;
            Vector3 to = sample.bladeTip + blade.normalized * _tipOverreach;
            Vector3 thickness = sample.normal * _ribbonWidth;
            int v = i * 4;
            _vertices[v] = from - thickness * 0.2f;
            _vertices[v + 1] = to - thickness;
            _vertices[v + 2] = from + thickness * 0.2f;
            _vertices[v + 3] = to + thickness;
            float age = Mathf.Clamp01((_clock - sample.born) / tail);
            Color color = settings.color;
            color.a *= sample.strength * Mathf.Pow(1f - age, _tailFadePower);
            Color baseColor = color;
            baseColor.a *= _baseOpacity;
            _colors[v] = _colors[v + 2] = baseColor;
            _colors[v + 1] = _colors[v + 3] = color;
            _uv[v] = _uv[v + 2] = new Vector2(age, 0f);
            _uv[v + 1] = _uv[v + 3] = new Vector2(age, 1f);
            _normals[v] = _normals[v + 1] = -sample.normal;
            _normals[v + 2] = _normals[v + 3] = sample.normal;
            for (int k = 0; k < 4; k++) bounds.Encapsulate(_vertices[v + k]);
        }

        // Vertex arrays keep a fixed size; only the active index range changes.
        // This avoids allocations and invalid old indices when the tail shrinks.
        _mesh.SetVertices(_vertices);
        _mesh.SetNormals(_normals);
        _mesh.SetUVs(0, _uv);
        _mesh.SetColors(_colors);
        _mesh.SetIndices(_indices, 0, (renderCount - 1) * 24, MeshTopology.Triangles, 0, false);
        _mesh.bounds = bounds;
        if (_renderer.sharedMaterial != _ribbonMaterial) _renderer.sharedMaterial = _ribbonMaterial;
        _renderer.enabled = true;
    }

    private void Quad(int index, int a, int b, int c, int d)
    {
        _indices[index] = a; _indices[index + 1] = b; _indices[index + 2] = c;
        _indices[index + 3] = a; _indices[index + 4] = c; _indices[index + 5] = d;
    }

    private void CreateBursts()
    {
        for (int i = 0; i < _bursts.Length; i++)
        {
            if (_steps == null || i >= _steps.Length || _steps[i] == null || _steps[i].burstPrefab == null) continue;
            GameObject prefab = _steps[i].burstPrefab;
            GameObject instance = Instantiate(prefab, _generatedRoot.transform);
            instance.name = prefab.name + " (pooled sword hit)";
            instance.hideFlags = HideFlags.DontSave;
            SetLayer(instance.transform, _generatedRoot.layer);
            ParticleSystem[] all = instance.GetComponentsInChildren<ParticleSystem>(true);
            int rootCount = 0;
            for (int p = 0; p < all.Length; p++)
            {
                ParticleSystem.MainModule main = all[p].main;
                main.playOnAwake = false;
                main.loop = false;
                main.stopAction = ParticleSystemStopAction.None;
                all[p].useAutoRandomSeed = false;
                all[p].randomSeed = (uint)(1234 + i * 101 + p);
                all[p].Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
                if (!HasParticleParent(all[p].transform, instance.transform)) rootCount++;
            }
            ParticleSystem[] roots = new ParticleSystem[rootCount];
            for (int p = 0, r = 0; p < all.Length; p++)
                if (!HasParticleParent(all[p].transform, instance.transform)) roots[r++] = all[p];
            _bursts[i] = new Burst { source = prefab, instance = instance, sourceScale = instance.transform.localScale, roots = roots };
            instance.SetActive(false);
        }
    }

    private void PlayBurst(int step, StepSettings settings, float progress, Vector3 bladeBase, Vector3 bladeTip, Vector3 sweep, Vector3 normal)
    {
        Burst burst = _bursts[step];
        if (burst == null || burst.instance == null || burst.source != settings.burstPrefab) return;
        PoseBurst(burst, settings, bladeBase, bladeTip, sweep, normal);
        burst.instance.SetActive(true);
        // Include the fraction of this frame after the peak, so a skipped exact
        // peak sample does not delay the original effect by another whole frame.
        burst.age = Mathf.Max(0.0001f, (progress - settings.peak) * settings.referenceDuration);
        burst.active = true;
        for (int i = 0; i < burst.roots.Length; i++)
        {
            burst.roots[i].Simulate(burst.age, true, true, false);
            burst.roots[i].Pause(true);
        }
    }

    private static void PoseBurst(Burst burst, StepSettings settings, Vector3 bladeBase, Vector3 bladeTip, Vector3 sweep, Vector3 normal)
    {
        Vector3 forward = (bladeTip - bladeBase).normalized;
        Vector3 up = Vector3.ProjectOnPlane(sweep, forward);
        if (up.sqrMagnitude < 0.000001f) up = Vector3.Cross(normal, forward);
        Quaternion frame = Quaternion.LookRotation(forward, up.normalized);
        Transform pose = burst.instance.transform;
        pose.localPosition = Vector3.Lerp(bladeBase, bladeTip, settings.burstBladeFraction) + frame * settings.burstOffset;
        pose.localRotation = frame * Quaternion.Euler(settings.burstEuler);
        pose.localScale = Vector3.Scale(burst.sourceScale, settings.burstScale);
    }

    private void TickBursts(float effectDelta)
    {
        for (int i = 0; i < _bursts.Length; i++)
        {
            Burst burst = _bursts[i];
            if (burst == null || !burst.active) continue;
            burst.age += effectDelta;
            if (burst.age > Mathf.Max(0.01f, _steps[i].burstLifetime)) { StopBurst(burst); continue; }
            if (effectDelta <= 0f) continue;
            for (int p = 0; p < burst.roots.Length; p++)
                burst.roots[p].Simulate(effectDelta, true, false, false);
        }
    }

    private void StopBursts()
    {
        for (int i = 0; i < _bursts.Length; i++)
            if (_bursts[i] != null && _bursts[i].active) StopBurst(_bursts[i]);
    }

    private static void StopBurst(Burst burst)
    {
        for (int i = 0; i < burst.roots.Length; i++)
            if (burst.roots[i] != null) burst.roots[i].Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        if (burst.instance != null) burst.instance.SetActive(false);
        burst.active = false;
    }

    private void ClearHistory()
    {
        _head = 0;
        _count = 0;
        if (_renderer != null) _renderer.enabled = false;
    }

    private void OnDisable() { Clear(); _usedNextState = false; }
    private void OnDestroy() { ReleaseResources(); }

    private void ReleaseResources()
    {
        Clear();
        if (_generatedRoot != null) DestroyOwned(_generatedRoot);
        if (_mesh != null) DestroyOwned(_mesh);
        _generatedRoot = null;
        _mesh = null;
        _renderer = null;
        for (int i = 0; i < _bursts.Length; i++) _bursts[i] = null;
    }

    private static void DestroyOwned(UnityEngine.Object item)
    {
        if (Application.isPlaying) Destroy(item);
        else DestroyImmediate(item);
    }

    private static int FindStep(int hash)
    {
        for (int i = 0; i < AttackHashes.Length; i++) if (AttackHashes[i] == hash) return i;
        return -1;
    }

    private static Transform FindNamed(Transform root, string target)
    {
        if (root == null) return null;
        if (root.name == target) return root;
        for (int i = 0; i < root.childCount; i++)
        {
            Transform found = FindNamed(root.GetChild(i), target);
            if (found != null) return found;
        }
        return null;
    }

    private static bool HasParticleParent(Transform node, Transform root)
    {
        if (node == root) return false;
        for (Transform parent = node.parent; parent != null; parent = parent.parent)
        {
            if (parent.GetComponent<ParticleSystem>() != null) return true;
            if (parent == root) break;
        }
        return false;
    }

    private static void SetLayer(Transform root, int layer)
    {
        root.gameObject.layer = layer;
        for (int i = 0; i < root.childCount; i++) SetLayer(root.GetChild(i), layer);
    }

    private static float Envelope(StepSettings settings, float progress)
    {
        float peak = Mathf.Clamp(settings.peak, settings.start + 0.0001f, settings.end - 0.0001f);
        float rise = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(settings.start, peak, progress));
        float fall = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(peak, settings.end, progress));
        return rise * fall;
    }

    private static float Square(float value) { return value * value; }

    private void OnValidate()
    {
        if (_steps == null) return;
        for (int i = 0; i < _steps.Length; i++)
        {
            StepSettings step = _steps[i];
            if (step == null) continue;
            step.start = Mathf.Clamp(step.start, 0f, 0.998f);
            step.end = Mathf.Clamp(step.end, step.start + 0.002f, 1f);
            step.peak = Mathf.Clamp(step.peak, step.start + 0.001f, step.end - 0.001f);
            step.tail = Mathf.Max(0.01f, step.tail);
            step.referenceDuration = Mathf.Max(0.01f, step.referenceDuration);
            step.burstLifetime = Mathf.Max(0.01f, step.burstLifetime);
        }
    }
}
