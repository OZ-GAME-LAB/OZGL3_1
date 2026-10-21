using System.Reflection;
using OZ.UI.Contracts;
using UnityEngine;

/// <summary>
/// [박한솔 UI · v0.9] Monster 하나의 체력 값을 EnemyUILink(체력바)에 맞춰 주는 부품. MonsterHealthBarBridge가 자동으로 붙인다.
/// </summary>
[DisallowMultipleComponent]
[AddComponentMenu("")]
public class MonsterHealthBarSync : MonoBehaviour
{
    static FieldInfo s_hp, s_maxHp;
    static bool s_looked;

    Monster _monster;
    EnemyUILink _link;
    float _lastHp = float.NaN;

    /// <summary>체력바는 몬스터 위치 + (0, 2, 0) 높이에 뜸 (EnemyUILink 기본값)</summary>
    internal void Setup()
    {
        _monster = GetComponent<Monster>();
        _link = GetComponent<EnemyUILink>();
        if (_link == null) _link = gameObject.AddComponent<EnemyUILink>();
        Look();
        InitBar();
    }

    static void Look()
    {
        if (s_looked) return;
        s_looked = true;
        const BindingFlags F = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
        s_hp = typeof(Monster).GetField("hp", F);
        s_maxHp = typeof(Monster).GetField("maxHP", F);
        if (s_hp == null || s_maxHp == null)
            Debug.LogWarning("[OZ UI] Monster의 hp / maxHP 필드를 찾지 못해 몬스터 체력바를 표시하지 않습니다 (이름이 바뀌었는지 확인).");
    }

    bool Valid => _monster != null && _link != null && s_hp != null && s_maxHp != null;

    void OnEnable() { if (Valid) InitBar(); } // 풀에서 다시 꺼낼 때 가득 찬 상태로

    void InitBar()
    {
        if (!Valid) return;
        float max = (float)s_maxHp.GetValue(_monster);
        _link.Init(max);
        _lastHp = max;
    }

    void LateUpdate()
    {
        if (!Valid) return;
        float hp = (float)s_hp.GetValue(_monster);
        if (_monster.IsDead) hp = 0f;
        if (Mathf.Approximately(hp, _lastHp)) return;
        _lastHp = hp;
        _link.SetHP(hp);
    }
}
