using System;
using UnityEngine;

namespace OZ.UI.Contracts
{
    /// <summary>
    /// [플레이어용] HUD 체력바 · 레벨 · 경험치 · 헌터 랭크. 붙이고 값만 넣어 주면 된다.
    ///
    ///   playerUI.SetHP(hp, maxHP);                  // 피격·회복 (줄면 피격 연출, 늘면 회복 연출 자동)
    ///   playerUI.SetExp(exp, expToNext);            // 경험치
    ///   playerUI.SetLevel(level);                   // 레벨업이면 레벨업 연출
    ///   playerUI.SetRank(HunterRank.E);             // 승급 연출
    ///   playerUI.SetSkillPoints(sp); playerUI.SetClass(PlayerClass.Magic);
    ///
    /// 스킬(Q/E/R)·아이템·인벤토리·스킬 트리는 각각 ISkillSource 등을 구현해 GameUI.Bind(this) (README 참고).
    /// 플레이어가 이미 IHealthSource 등을 구현했다면 이 컴포넌트는 필요 없음.
    /// </summary>
    [AddComponentMenu("OZ/UI/Links/Player UI Link")]
    public class PlayerUILink : MonoBehaviour, IHealthSource, IProgressionSource
    {
        [SerializeField] internal float startHP = 100f;
        [SerializeField] internal float startMaxHP = 100f;

        float _hp, _maxHP;
        int _level = 1, _sp;
        float _exp, _toNext = 100f;
        HunterRank _rank = HunterRank.F;
        PlayerClass _class = PlayerClass.Sword;

        public float HP => _hp;
        public float MaxHP => _maxHP;
        public PlayerClass Class => _class;
        public int Level => _level;
        public float Exp => _exp;
        public float ExpToNextLevel => _toNext;
        public int SkillPoints => _sp;
        public HunterRank Rank => _rank;

        public event Action<HealthChange> HealthChanged;
        public event Action ProgressionChanged;
        public event Action<int> LevelUp;
        public event Action<HunterRank> RankUp;

        void Awake() { _maxHP = Mathf.Max(1f, startMaxHP); _hp = Mathf.Clamp(startHP, 0f, _maxHP); }
        void OnEnable() => GameUI.Bind(this);
        void OnDisable() => GameUI.Unbind(this);

        public void SetHP(float hp, float maxHP = -1f)
        {
            float prev = _hp;
            if (maxHP > 0f) _maxHP = maxHP;
            _hp = Mathf.Clamp(hp, 0f, _maxHP);
            HealthChanged?.Invoke(new HealthChange(prev, _hp, _maxHP));
        }

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
