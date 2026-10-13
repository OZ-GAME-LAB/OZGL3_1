using System;
using System.Reflection;
using OZ.UI.Contracts;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// [박한솔 UI · v0.8] 플레이어 대시 상태 → 플레이어 옆 게이지(IDashSource). 팀원 코드(PlayerController) 수정 없이 동작한다.
///   PlayerController의 대시 값(_nextDashTime · _dashCooldown · CanDash)을 읽기만 한다 (값을 바꾸지 않음).
///   PlayerController에 대시가 없으면(현재 develop) 아무것도 등록하지 않음 → 게이지에서 대시 줄만 숨겨짐.
///   씬에 PlayerController가 있으면 Play 시 플레이어 오브젝트에 자동으로 붙는다 (프리팹·씬 수정 없음).
///
/// 나중에 플레이어 담당이 PlayerController에 IDashSource를 직접 구현하면 이 컴포넌트는 알아서 빠진다.
/// </summary>
[DisallowMultipleComponent]
[AddComponentMenu("")]
public class PlayerDashBridge : MonoBehaviour, IDashSource
{
    PlayerController _player;
    FieldInfo _nextDashTime;   // float
    FieldInfo _dashCooldown;   // float
    PropertyInfo _canDash;     // bool (dev_Player_2: public CanDash)
    bool _valid;

    public bool IsValid => _valid;

    // dev_Player_2: 대기 시간이 있는 1회 대시 (공중에서는 착지 전까지 1번) → 동그라미 1개
    public int MaxDashCharges => 1;
    public int DashCharges => CanDash ? 1 : 0;

    public float DashCooldownRemaining => _valid ? Mathf.Max(0f, ReadFloat(_nextDashTime) - Time.time) : 0f;
    public float DashCooldownDuration => _valid && _dashCooldown != null ? Mathf.Max(0f, ReadFloat(_dashCooldown)) : 0f;

    public bool CanDash
    {
        get
        {
            if (!_valid) return false;
            if (_canDash != null)
            {
                try { return (bool)_canDash.GetValue(_player); }
                catch (Exception) { /* 지면 체크 오브젝트가 없을 때 등 → 대기 시간만 봄 */ }
            }
            return DashCooldownRemaining <= 0f;
        }
    }

    void Awake()
    {
        _player = GetComponent<PlayerController>();
        if (_player == null) { enabled = false; return; }

        // 플레이어 쪽이 IDashSource를 직접 구현했으면 이건 필요 없음
        if (_player is IDashSource) { enabled = false; return; }

        const BindingFlags F = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
        var t = _player.GetType();
        _nextDashTime = FloatField(t, "_nextDashTime", F);
        _dashCooldown = FloatField(t, "_dashCooldown", F);
        var p = t.GetProperty("CanDash", BindingFlags.Instance | BindingFlags.Public);
        _canDash = p != null && p.PropertyType == typeof(bool) && p.CanRead ? p : null;
        _valid = _nextDashTime != null;

        Debug.Log(_valid
            ? "[OZ UI] PlayerDashBridge: 대시 게이지 연결 (PlayerController 대시 값 읽기)"
            : "[OZ UI] PlayerDashBridge: PlayerController에 대시가 없어 대시 게이지는 숨깁니다.", this);
    }

    static FieldInfo FloatField(Type t, string name, BindingFlags f)
    {
        var fi = t.GetField(name, f);
        return fi != null && fi.FieldType == typeof(float) ? fi : null;
    }

    float ReadFloat(FieldInfo fi) => fi != null ? (float)fi.GetValue(_player) : 0f;

    void OnEnable() { if (_valid) GameUI.Bind(this); }
    void OnDisable() => GameUI.Unbind(this);

    // ── 자동 설치: PlayerController 오브젝트에 붙임 (Play 중에만 → 프리팹·씬에 저장 안 됨) ──
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void AutoInstall()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
        TryInstall();
    }

    static void OnSceneLoaded(Scene scene, LoadSceneMode mode) => TryInstall();

    static void TryInstall()
    {
        var pc = FindAnyObjectByType<PlayerController>(FindObjectsInactive.Include);
        if (pc == null) return;
        // 플레이어 옆 게이지가 따라갈 대상 (PlayerHealthBridge도 붙이지만 PlayerUILink가 없는 씬 대비)
        if (pc.GetComponent<UIPlayerTag>() == null) pc.gameObject.AddComponent<UIPlayerTag>();
        if (pc.GetComponent<PlayerDashBridge>() != null) return;
        pc.gameObject.AddComponent<PlayerDashBridge>();
    }
}
