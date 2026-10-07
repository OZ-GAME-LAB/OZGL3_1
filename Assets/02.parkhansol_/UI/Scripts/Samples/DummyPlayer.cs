using System;
using OZ.UI.Contracts;
using UnityEngine;
using UnityEngine.InputSystem;

namespace OZ.UI.Samples
{
    /// <summary>
    /// UI_Sandbox 테스트용 가짜 플레이어 + 스테이지 + 인벤토리.
    /// 팀원이 실제로 구현할 인터페이스의 "참고 구현"이기도 하다 (Contracts만 참조).
    ///
    /// 디버그 키 (Sandbox 전용)
    ///   H 피격 -15 / J 회복 +20 / X 경험치 +40
    ///   Q E R 스킬 사용 / 1 2 3 4 아이템 사용 / U 회복제 +1
    ///   G 게이트 봉쇄 / O 게이트 열기 / = 레벨업
    ///
    /// 스킬 트리(v0.4): 규칙은 SkillTreeState가 계산하고, 여기서는 포인트 차감·이벤트만 한다.
    ///   계열 선택 = 그 계열의 Q 스킬 노드를 무료로 찍어 둠 (초기화해도 남음).
    ///   E·R은 트리에서 검술/마법 중 하나를 골라 찍는다.
    /// </summary>
    [AddComponentMenu("OZ/UI/Samples/Dummy Player")]
    public class DummyPlayer : MonoBehaviour,
        IHealthSource, IProgressionSource, ISkillSource, ISkillTreeSource, IItemSource, IGateSource, IInventorySource
    {
        [Header("Data (선택 — 비우면 이름 없는 슬롯으로 표시)")]
        [SerializeField] internal ClassData classData;
        [Tooltip("노드형 스킬 트리. 비우면 예전 방식(Q/E/R 단계만)으로 동작")]
        [SerializeField] internal SkillTreeData skillTree;
        [Tooltip("1~4 퀵슬롯 아이템 (slotIndex 0~3)")]
        [SerializeField] internal ItemData[] quickItems = new ItemData[4];
        [Tooltip("인벤토리에만 들어가는 아이템 (열쇠 등)")]
        [SerializeField] internal ItemData[] extraItems = new ItemData[0];

        [Header("Start Values")]
        [SerializeField] internal float maxHP = 100f;
        [Tooltip("최대 레벨 (레벨당 스킬 포인트 1)")]
        [SerializeField] internal int maxLevel = 15;
        [SerializeField] internal int[] startItemCounts = { 3, 1, 1, 1 };
        [SerializeField] internal int inventoryCapacity = 24;
        [SerializeField] internal int gateTarget = 3;
        [SerializeField] internal int stageNumber = 1;
        [SerializeField] internal bool enableDebugKeys = true;

        float _hp;
        int _level = 1;
        float _exp;
        int _points;
        HunterRank _rank = HunterRank.F;
        readonly int[] _skillRanks = new int[3]; // skillTree 없을 때만 사용
        SkillTreeState _tree;
        readonly float[] _cooldownEnd = new float[3];
        readonly float[] _cooldownDur = new float[3];
        ItemStack[] _inv;
        int _sealed;
        bool _gateActive = true;

        // ── 이벤트 ──
        public event Action<HealthChange> HealthChanged;
        public event Action ProgressionChanged;
        public event Action<int> LevelUp;
        public event Action<HunterRank> RankUp;
        public event Action<SkillSlot, float> CooldownStarted;
        public event Action<SkillSlot> SkillChanged;
        public event Action<SkillSlot, SkillUseFailReason> SkillUseFailed;
        public event Action TreeChanged;
        public event Action<int, int> CountChanged;
        public event Action<int> ItemUsed;
        public event Action<int> ItemUseFailed;
        public event Action<ItemData, float> BuffApplied;
        public event Action<ItemData> BuffEnded;
        public event Action GateOpened;
        public event Action<int, int> GateSealed;
        public event Action BossAreaUnlocked;
        public event Action GateStateReset;
        public event Action Changed; // IInventorySource

        void Awake() => ResetAll();

        SkillTreeState TreeState
        {
            get
            {
                if (skillTree == null) return null;
                if (_tree == null || _tree.Tree != skillTree)
                {
                    if (_tree != null) _tree.Changed -= OnTreeStateChanged;
                    _tree = new SkillTreeState(skillTree);
                    _tree.Changed += OnTreeStateChanged;
                }
                return _tree;
            }
        }

        void OnTreeStateChanged()
        {
            TreeChanged?.Invoke();
            for (int i = 0; i < 3; i++) SkillChanged?.Invoke((SkillSlot)i);
        }

        // 바인딩은 UISourceBinder 컴포넌트가 해도 되고, 이렇게 직접 해도 된다.
        void OnEnable() => GameUI.Bind(this);
        void OnDisable() => GameUI.Unbind(this);

        /// <summary>스테이지 시작 상태로 복구 (재도전 예시)</summary>
        public void ResetAll()
        {
            _hp = maxHP;
            _level = 1; _exp = 0f; _points = 0;
            var tree = TreeState;
            tree?.Clear();
            for (int i = 0; i < 3; i++)
            {
                var start = classData != null ? classData.GetSkill((SkillSlot)i) : null;
                if (tree != null)
                {
                    // 계열의 시작 스킬(검증값: Q)은 무료로 찍어 둔다
                    var node = start != null && start.startsLearned ? skillTree.FindSkillNode(start) : null;
                    if (node != null) tree.Unlock(node.id, free: true);
                }
                else _skillRanks[i] = start != null ? (start.startsLearned ? 1 : 0) : (i == 0 ? 1 : 0);
                _cooldownEnd[i] = 0f; _cooldownDur[i] = 0f;
            }

            _inv = new ItemStack[Mathf.Max(4, inventoryCapacity)];
            int slot = 0;
            for (int i = 0; i < 4; i++)
            {
                var item = i < quickItems.Length ? quickItems[i] : null;
                int count = i < startItemCounts.Length ? startItemCounts[i] : 0;
                if (item != null && count > 0) _inv[slot++] = new ItemStack(item, count);
            }
            foreach (var extra in extraItems)
                if (extra != null && slot < _inv.Length) _inv[slot++] = new ItemStack(extra, 1);

            _sealed = 0;
            _gateActive = true;

            HealthChanged?.Invoke(new HealthChange(_hp, _hp, maxHP));
            ProgressionChanged?.Invoke();
            for (int i = 0; i < 3; i++) SkillChanged?.Invoke((SkillSlot)i);
            for (int i = 0; i < 4; i++) CountChanged?.Invoke(i, GetCount(i));
            Changed?.Invoke();
            GateStateReset?.Invoke();
        }

        public void SetClass(ClassData data)
        {
            classData = data;
            ResetAll();
        }

        // ── IHealthSource ──
        public float HP => _hp;
        public float MaxHP => maxHP;

        public void ChangeHP(float delta)
        {
            float prev = _hp;
            _hp = Mathf.Clamp(_hp + delta, 0f, maxHP);
            if (!Mathf.Approximately(prev, _hp))
                HealthChanged?.Invoke(new HealthChange(prev, _hp, maxHP));
        }

        // ── IProgressionSource ──
        public PlayerClass Class => classData != null ? classData.playerClass : PlayerClass.Sword;
        public int Level => _level;
        public float Exp => _exp;
        public float ExpToNextLevel => _level >= maxLevel ? 0f : 100f;
        public int SkillPoints => _points;
        public HunterRank Rank => _rank;

        public void AddExp(float amount)
        {
            if (ExpToNextLevel <= 0f) return;
            _exp += amount;
            while (ExpToNextLevel > 0f && _exp >= ExpToNextLevel)
            {
                _exp -= ExpToNextLevel;
                _level++;
                _points++;
                LevelUp?.Invoke(_level);
            }
            if (ExpToNextLevel <= 0f) _exp = 0f;
            ProgressionChanged?.Invoke();
        }

        public void PromoteRank()
        {
            if (_rank >= HunterRank.S) return;
            _rank++;
            RankUp?.Invoke(_rank);
            ProgressionChanged?.Invoke();
        }

        /// <summary>디버그: 다음 레벨까지 바로 올림</summary>
        public void LevelUpNow()
        {
            if (ExpToNextLevel > 0f) AddExp(ExpToNextLevel - _exp);
        }

        // ── ISkillSource (Q/E/R 아이콘·쿨타임·단계 — 트리가 있으면 트리에서 계산) ──
        public SkillData GetSkill(SkillSlot slot)
        {
            var tree = TreeState;
            if (tree != null) return tree.GetSkill(slot);
            return classData != null ? classData.GetSkill(slot) : null;
        }

        public int GetRank(SkillSlot slot)
        {
            var tree = TreeState;
            return tree != null ? tree.GetRank(slot) : _skillRanks[(int)slot];
        }

        public float GetCooldownRemaining(SkillSlot slot) => Mathf.Max(0f, _cooldownEnd[(int)slot] - Time.time);
        public float GetCooldownDuration(SkillSlot slot) => _cooldownDur[(int)slot];

        /// <summary>예전 카드형 스킬 창 호환: 트리에서는 그 버튼의 '다음 노드'를 찍는다 (미배정이면 계열 스킬).</summary>
        public bool CanInvest(SkillSlot slot, out string reason)
        {
            var tree = TreeState;
            if (tree != null)
            {
                var next = tree.NextNodeFor(slot, classData != null ? classData.GetSkill(slot) : null);
                if (next == null) { reason = tree.GetRank(slot) > 0 ? "최대 단계" : "트리에서 선택"; return false; }
                return CanUnlock(next.id, out reason);
            }

            var data = GetSkill(slot);
            int rank = GetRank(slot);
            int maxRank = data != null ? data.maxRank : 3;
            int learnLevel = data != null ? data.learnLevel : 1;
            int cost = data != null ? data.costPerRank : 1;

            if (rank >= maxRank) { reason = "최대 단계"; return false; }
            if (rank == 0 && _level < learnLevel) { reason = $"Lv.{learnLevel} 필요"; return false; }
            if (_points < cost) { reason = "포인트 부족"; return false; }
            reason = null;
            return true;
        }

        public bool TryInvest(SkillSlot slot)
        {
            if (!CanInvest(slot, out _)) return false;
            var tree = TreeState;
            if (tree != null) return TryUnlock(tree.NextNodeFor(slot, classData != null ? classData.GetSkill(slot) : null).id);

            var data = GetSkill(slot);
            _points -= data != null ? data.costPerRank : 1;
            _skillRanks[(int)slot]++;
            SkillChanged?.Invoke(slot);
            ProgressionChanged?.Invoke();
            return true;
        }

        public void UseSkill(SkillSlot slot)
        {
            int i = (int)slot;
            int rank = GetRank(slot);
            if (rank <= 0) { SkillUseFailed?.Invoke(slot, SkillUseFailReason.NotLearned); return; }
            if (GetCooldownRemaining(slot) > 0f) { SkillUseFailed?.Invoke(slot, SkillUseFailReason.Cooldown); return; }

            var data = GetSkill(slot);
            float cd = data != null ? data.GetCooldown(rank) : 5f + 3f * i;
            var tree = TreeState;
            if (tree != null) cd *= Mathf.Max(0.1f, 1f + tree.GetStat("cooldown_pct") / 100f); // 패시브 '집중'
            _cooldownDur[i] = cd;
            _cooldownEnd[i] = Time.time + cd;
            CooldownStarted?.Invoke(slot, cd);
        }

        // ── ISkillTreeSource ──
        public SkillTreeData Tree => skillTree;
        public bool IsUnlocked(string nodeId) => TreeState != null && TreeState.IsUnlocked(nodeId);

        public bool CanUnlock(string nodeId, out string reason)
        {
            var tree = TreeState;
            if (tree == null) { reason = "트리 없음"; return false; }
            return tree.CanUnlock(nodeId, _level, _points, out reason);
        }

        public bool TryUnlock(string nodeId)
        {
            if (!CanUnlock(nodeId, out _)) return false;
            _points -= skillTree.Find(nodeId).cost;
            TreeState.Unlock(nodeId); // → TreeChanged, SkillChanged
            ProgressionChanged?.Invoke();
            return true;
        }

        public bool CanResetTree(out string reason)
        {
            var tree = TreeState;
            if (tree == null) { reason = "트리 없음"; return false; }
            if (tree.SpentPoints <= 0) { reason = "찍은 노드 없음"; return false; }
            reason = null;
            return true;
        }

        public bool TryResetTree()
        {
            if (!CanResetTree(out _)) return false;
            _points += TreeState.Reset();
            for (int i = 0; i < 3; i++) { _cooldownEnd[i] = 0f; _cooldownDur[i] = 0f; } // 스킬이 바뀔 수 있으니 쿨타임 비움
            for (int i = 0; i < 3; i++) SkillChanged?.Invoke((SkillSlot)i);
            ProgressionChanged?.Invoke();
            GameUI.Notify.Toast("스킬 트리 초기화 — 포인트를 돌려받았다", ToastType.Info);
            return true;
        }

        // ── IItemSource (1~4 퀵슬롯 = 인벤토리 안 같은 아이템 합계) ──
        public int SlotCount => 4;
        public ItemData GetItem(int slotIndex) => quickItems != null && slotIndex < quickItems.Length ? quickItems[slotIndex] : null;

        public int GetCount(int slotIndex)
        {
            var item = GetItem(slotIndex);
            if (item == null || _inv == null) return 0;
            int total = 0;
            foreach (var s in _inv) if (s.Item == item) total += s.Count;
            return total;
        }

        public void AddItem(int slotIndex, int amount = 1)
        {
            var item = GetItem(slotIndex);
            if (item == null) return;
            for (int i = 0; i < _inv.Length; i++)
                if (_inv[i].Item == item && _inv[i].Count < item.maxStack) { _inv[i] = new ItemStack(item, _inv[i].Count + amount); Notify(slotIndex); return; }
            for (int i = 0; i < _inv.Length; i++)
                if (_inv[i].IsEmpty) { _inv[i] = new ItemStack(item, amount); Notify(slotIndex); return; }
            GameUI.Notify.Toast("인벤토리가 가득 찼다", ToastType.Warning);
        }

        public void UseItem(int slotIndex)
        {
            var item = GetItem(slotIndex);
            int at = item != null ? Array.FindIndex(_inv, s => s.Item == item && s.Count > 0) : -1;
            if (at < 0) { ItemUseFailed?.Invoke(slotIndex); return; }
            ConsumeAt(at);
            ItemUsed?.Invoke(slotIndex);
            Notify(slotIndex);
            ApplyEffect(item);
        }

        void ConsumeAt(int index)
        {
            var s = _inv[index];
            _inv[index] = s.Count > 1 ? new ItemStack(s.Item, s.Count - 1) : ItemStack.Empty;
        }

        void ApplyEffect(ItemData item)
        {
            if (item == null) return;
            if (item.effectType == ItemEffectType.Heal) ChangeHP(maxHP * item.effectPercent / 100f);
            else if (item.IsBuff)
            {
                BuffApplied?.Invoke(item, item.buffDuration); // 같은 버프는 시간 갱신
                StopCoroutineSafe(item);
                _buffRoutines[item] = StartCoroutine(EndBuffLater(item, item.buffDuration));
            }
        }

        readonly System.Collections.Generic.Dictionary<ItemData, Coroutine> _buffRoutines = new System.Collections.Generic.Dictionary<ItemData, Coroutine>();

        void StopCoroutineSafe(ItemData item)
        {
            if (_buffRoutines.TryGetValue(item, out var co) && co != null) StopCoroutine(co);
        }

        System.Collections.IEnumerator EndBuffLater(ItemData item, float seconds)
        {
            yield return new WaitForSeconds(seconds);
            _buffRoutines.Remove(item);
            BuffEnded?.Invoke(item);
        }

        void Notify(int quickSlot)
        {
            if (quickSlot >= 0) CountChanged?.Invoke(quickSlot, GetCount(quickSlot));
            Changed?.Invoke();
        }

        // ── IInventorySource ──
        public int Capacity => _inv?.Length ?? 0;
        public ItemStack GetSlot(int index) => _inv != null && index >= 0 && index < _inv.Length ? _inv[index] : ItemStack.Empty;

        public bool TryMove(int from, int to)
        {
            if (_inv == null || from == to || from < 0 || to < 0 || from >= _inv.Length || to >= _inv.Length) return false;
            var a = _inv[from]; var b = _inv[to];
            if (!b.IsEmpty && b.Item == a.Item && b.Count < a.Item.maxStack)
            {
                int move = Mathf.Min(a.Count, a.Item.maxStack - b.Count); // 같은 아이템이면 합치기
                _inv[to] = new ItemStack(b.Item, b.Count + move);
                _inv[from] = a.Count - move > 0 ? new ItemStack(a.Item, a.Count - move) : ItemStack.Empty;
            }
            else { _inv[from] = b; _inv[to] = a; } // 교환
            Changed?.Invoke();
            return true;
        }

        public bool TryUse(int index)
        {
            var s = GetSlot(index);
            if (s.IsEmpty || !s.Item.IsUsable) return false;
            ConsumeAt(index);
            int quick = s.Item.slotIndex;
            if (quick >= 0) ItemUsed?.Invoke(quick);
            Notify(quick);
            ApplyEffect(s.Item);
            return true;
        }

        // ── IGateSource ──
        public int StageNumber => stageNumber;
        public int SealedCount => _sealed;
        public int TargetCount => gateTarget;
        public bool IsGateActive => _gateActive;
        public bool IsBossAreaUnlocked => _sealed >= gateTarget;

        public void OpenGate()
        {
            if (_gateActive || IsBossAreaUnlocked) return;
            _gateActive = true;
            GateOpened?.Invoke();
            GameUI.Notify.Toast("게이트가 열렸다", ToastType.Warning);
        }

        public void SealGate()
        {
            if (!_gateActive || IsBossAreaUnlocked) return;
            _gateActive = false;
            _sealed++;
            GateSealed?.Invoke(_sealed, gateTarget);
            GameUI.Notify.Toast($"게이트 봉쇄 {_sealed}/{gateTarget}", ToastType.Success);
            if (IsBossAreaUnlocked)
            {
                BossAreaUnlocked?.Invoke();
                GameUI.HUD.ShowGuide("보스 구역이 열렸다");
            }
        }

        // ── 디버그 키 ──
        void Update()
        {
            if (!enableDebugKeys || UIState.IsGameplayInputBlocked) return;
            var kb = Keyboard.current;
            if (kb == null) return;

            if (kb.hKey.wasPressedThisFrame) ChangeHP(-15f);
            if (kb.jKey.wasPressedThisFrame) ChangeHP(20f);
            if (kb.xKey.wasPressedThisFrame) AddExp(40f);
            if (kb.qKey.wasPressedThisFrame) UseSkill(SkillSlot.Q);
            if (kb.eKey.wasPressedThisFrame) UseSkill(SkillSlot.E);
            if (kb.rKey.wasPressedThisFrame) UseSkill(SkillSlot.R);
            if (kb.digit1Key.wasPressedThisFrame) UseItem(0);
            if (kb.digit2Key.wasPressedThisFrame) UseItem(1);
            if (kb.digit3Key.wasPressedThisFrame) UseItem(2);
            if (kb.digit4Key.wasPressedThisFrame) UseItem(3);
            if (kb.uKey.wasPressedThisFrame) AddItem(0);
            if (kb.gKey.wasPressedThisFrame) SealGate();
            if (kb.oKey.wasPressedThisFrame) OpenGate();
            if (kb.equalsKey.wasPressedThisFrame) LevelUpNow();
        }
    }
}
