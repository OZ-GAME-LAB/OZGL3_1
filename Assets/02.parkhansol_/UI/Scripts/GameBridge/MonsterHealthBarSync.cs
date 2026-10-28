using OZ.UI.Contracts;
using UnityEngine;

/// <summary>
/// [박한솔 UI · v1.0] Monster 하나의 체력 값을 EnemyUILink(체력바)에 맞춰 주는 부품. MonsterHealthBarBridge가 자동으로 붙인다.
///   v1.0: Monster가 공개한 MaxHP / CurrentHP를 읽는다 (예전엔 비공개 필드를 이름으로 찾아 읽음 → 이름이 바뀌면 깨짐).
/// </summary>
[DisallowMultipleComponent]
[AddComponentMenu("")]
public class MonsterHealthBarSync : MonoBehaviour
{
    Monster _monster;
    EnemyUILink _link;
    float _lastHp = float.NaN;

    /// <summary>체력바는 몬스터 위치 + (0, 2, 0) 높이에 뜸 (EnemyUILink 기본값)</summary>
    internal void Setup()
    {
        _monster = GetComponent<Monster>();
        _link = GetComponent<EnemyUILink>();
        if (_link == null) _link = gameObject.AddComponent<EnemyUILink>();
        InitBar();
    }

    bool Valid => _monster != null && _link != null;

    // 풀에서 다시 꺼낼 때 가득 찬 상태로 (스포너가 InitializeStats로 MaxHP를 정한 뒤 켬)
    void OnEnable() { if (Valid) InitBar(); }

    void InitBar()
    {
        if (!Valid) return;
        float max = _monster.MaxHP;
        _link.Init(max);
        _lastHp = max;
    }

    void LateUpdate()
    {
        if (!Valid) return;
        float hp = _monster.IsDead ? 0f : _monster.CurrentHP;
        if (Mathf.Approximately(hp, _lastHp)) return;
        _lastHp = hp;
        _link.SetHP(hp);
    }
}