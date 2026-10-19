using System;
using UnityEngine;

namespace OZ.UI.Contracts
{
    /// <summary>
    /// [플레이어용] HUD 레벨 · 경험치 · 헌터 랭크 · 스킬 포인트 · 계열. 붙이고 값만 넣어 주면 된다.
    ///
    ///   playerUI.SetExp(exp, expToNext);            // 경험치
    ///   playerUI.SetLevel(level);                   // 레벨업이면 레벨업 연출
    ///   playerUI.SetRank(HunterRank.E);             // 승급 연출
    ///   playerUI.SetSkillPoints(sp); playerUI.SetClass(PlayerClass.Magic);
    ///
    /// v0.8: 체력은 여기서 관리하지 않는다 → PlayerHealth(IHealthSource 구현 + GameUI.Bind)가 유일한 기준.
    ///   HUD 체력바 · 플레이어 옆 게이지 · 피해/회복 숫자 · 사망 화면은 모두 PlayerHealth.HealthChanged를 보고 그린다.
    ///   재도전 때 체력 복구는 PlayerHealth.ResetHealth().
    /// 스킬(Q/E/R)·아이템·인벤토리·스킬 트리는 각각 ISkillSource 등을 구현해 GameUI.Bind(this) (README 참고).
    /// </summary>
    [AddComponentMenu("OZ/UI/Links/Player UI Link")]
    public class PlayerUILink : MonoBehaviour, IProgressionSource
    {
        int _level = 1, _sp;
        float _exp, _toNext = 100f;
        HunterRank _rank = HunterRank.F;
        PlayerClass _class = PlayerClass.Sword;

        public PlayerClass Class => _class;
        public int Level => _level;
        public float Exp => _exp;
        public float ExpToNextLevel => _toNext;
        public int SkillPoints => _sp;
        public HunterRank Rank => _rank;

        public event Action ProgressionChanged;
        public event Action<int> LevelUp;
        public event Action<HunterRank> RankUp;

        void OnEnable() => GameUI.Bind(this);
        void OnDisable() => GameUI.Unbind(this);

        // ── v0.8 이전 호환 (컴파일 유지용) ──
        // 체력은 PlayerHealth가 관리한다. 기존 코드(SceneGameLink 등)가 컴파일되도록만 남겨 둔 것이며 아무 일도 하지 않는다.
        // 재도전 체력 복구는 PlayerHealth.ResetHealth()로 바꿔 주세요. 호출처가 없어지면 이 두 줄은 지운다.
        [Obsolete("체력은 PlayerHealth가 관리합니다 — 아무 일도 하지 않음. 재도전 복구는 PlayerHealth.ResetHealth()를 쓰세요.")]
        public void SetHP(float hp, float maxHP = -1f) { }

        [Obsolete("체력은 PlayerHealth가 관리합니다 — PlayerHealth.MaxHP를 쓰세요.")]
        public float MaxHP => 0f;

        public void SetExp(float exp, float expToNext)
        {
            _exp = Mathf.Max(0f, exp);
            _toNext = Mathf.Max(0f, expToNext);
            ProgressionChanged?.Invoke();
        }

        public void SetLevel(int level)
        {
            int prev = _level;
            _level = Mathf.Max(1, level);
            ProgressionChanged?.Invoke();
            if (_level > prev) LevelUp?.Invoke(_level);
        }

        public void SetSkillPoints(int points) { _sp = Mathf.Max(0, points); ProgressionChanged?.Invoke(); }

        public void SetRank(HunterRank rank)
        {
            var prev = _rank;
            _rank = rank;
            ProgressionChanged?.Invoke();
            if (rank > prev) RankUp?.Invoke(rank);
        }

        public void SetClass(PlayerClass cls) { _class = cls; ProgressionChanged?.Invoke(); }
    }
}
