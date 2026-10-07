using System.Collections.Generic;
using OZ.UI.Contracts;
using UnityEditor;
using UnityEngine;

namespace OZ.UI.EditorTools
{
    /// <summary>
    /// Setup 3단계: 기획서 v0.2 검증값으로 샘플 데이터 생성 (UI/Data/Samples).
    /// 팀원은 이 에셋을 복제/수정해서 실제 데이터로 쓰면 된다. 이미 있는 에셋은 덮어쓰지 않는다.
    /// </summary>
    internal static class OZSampleData
    {
        static string P(string file) => $"{OZPaths.SampleData}/{file}.asset";
        public static bool IsDone => AssetDatabase.LoadAssetAtPath<ClassData>(P("Class_Sword")) != null;

        public static T Load<T>(string file) where T : Object => AssetDatabase.LoadAssetAtPath<T>(P(file));

        static Sprite Icon(string name) =>
            AssetDatabase.LoadAssetAtPath<Sprite>($"{OZPaths.UI}/Art/Icons/{name}.png");

        static Sprite Portrait(string name) =>
            AssetDatabase.LoadAssetAtPath<Sprite>($"{OZPaths.PixelUI}/Sprites/Portraits/{name}.png");

        static T Make<T>(string file, System.Action<T> fill) where T : ScriptableObject
        {
            var existing = Load<T>(file);
            if (existing != null) return existing;
            var so = ScriptableObject.CreateInstance<T>();
            fill(so);
            AssetDatabase.CreateAsset(so, P(file));
            return so;
        }

        static List<SkillRankInfo> Ranks(float cd, params string[] summaries)
        {
            var list = new List<SkillRankInfo>();
            for (int i = 0; i < summaries.Length; i++)
                list.Add(new SkillRankInfo { cooldown = Mathf.Max(1f, cd - i * cd * 0.1f), summary = summaries[i] });
            return list;
        }

        public static void Run()
        {
            OZFontBuilder.EnsureFolder(OZPaths.SampleData);
            AssetDatabase.Refresh();

            // ── 스킬 (기획서 3장 검토안, 대기시간 = 1단계 검증값, 2·3단계는 10%씩 감소 임시값) ──
            var sq = Make<SkillData>("Skill_Sword_Q", s => { s.id = "sword_q"; s.displayName = "짧은 검기"; s.description = "커서 방향의 짧고 좁은 범위에 높은 피해."; s.icon = Icon("Skill_SwordQ"); s.playerClass = PlayerClass.Sword; s.slot = SkillSlot.Q; s.learnLevel = 1; s.startsLearned = true; s.ranks = Ranks(5f, "피해 100%", "피해 130%", "피해 160%"); });
            var se = Make<SkillData>("Skill_Sword_E", s => { s.id = "sword_e"; s.displayName = "근접 범위 베기"; s.description = "가까이 모인 적에게 범위 피해. 범위는 좁고 피해는 높다."; s.icon = Icon("Skill_SwordE"); s.playerClass = PlayerClass.Sword; s.slot = SkillSlot.E; s.learnLevel = 2; s.ranks = Ranks(8f, "범위 피해 120%", "범위 피해 150%", "범위 피해 180%"); });
            var sr = Make<SkillData>("Skill_Sword_R", s => { s.id = "sword_r"; s.displayName = "집중 참격"; s.description = "좁은 구역에 여러 차례 피해를 집중."; s.icon = Icon("Skill_SwordR"); s.playerClass = PlayerClass.Sword; s.slot = SkillSlot.R; s.learnLevel = 4; s.ranks = Ranks(16f, "5회 타격", "6회 타격", "8회 타격"); });
            var mq = Make<SkillData>("Skill_Magic_Q", s => { s.id = "magic_q"; s.displayName = "범위 폭발"; s.description = "사거리 안의 목표 지점에 즉시 범위 피해."; s.icon = Icon("Skill_MagicQ"); s.playerClass = PlayerClass.Magic; s.slot = SkillSlot.Q; s.learnLevel = 1; s.startsLearned = true; s.ranks = Ranks(6f, "폭발 피해 100%", "폭발 피해 125%", "폭발 피해 150%"); });
            var me = Make<SkillData>("Skill_Magic_E", s => { s.id = "magic_e"; s.displayName = "지속 피해 장판"; s.description = "지면에 공격 영역을 남겨 주기적으로 피해."; s.icon = Icon("Skill_MagicE"); s.playerClass = PlayerClass.Magic; s.slot = SkillSlot.E; s.learnLevel = 2; s.ranks = Ranks(10f, "4초 지속", "5초 지속", "6초 지속"); });
            var mr = Make<SkillData>("Skill_Magic_R", s => { s.id = "magic_r"; s.displayName = "광역 연속 공격"; s.description = "넓은 목표 구역에 범위 공격을 순차적으로 발생."; s.icon = Icon("Skill_MagicR"); s.playerClass = PlayerClass.Magic; s.slot = SkillSlot.R; s.learnLevel = 4; s.ranks = Ranks(20f, "6회 낙하", "8회 낙하", "10회 낙하"); });

            Make<ClassData>("Class_Sword", c => { c.playerClass = PlayerClass.Sword; c.displayName = "검술"; c.description = "짧고 좁은 범위에 높은 피해를 집중한다.\n적에게 접근할 기회와 빠져나올 위치를 판단."; c.icon = Icon("Skill_SwordQ"); c.skills = new[] { sq, se, sr }; });
            Make<ClassData>("Class_Magic", c => { c.playerClass = PlayerClass.Magic; c.displayName = "마법"; c.description = "넓은 범위 공격과 지속 피해 장판.\n적이 모이는 위치와 경로를 예상해 공격."; c.icon = Icon("Skill_MagicQ"); c.skills = new[] { mq, me, mr }; });

            // ── 스킬 트리 (v0.4 노드형) — Q/E/R 버튼마다 검술·마법 택1 → 2·3단계 강화, 아래 줄은 공용 패시브 ──
            // 포인트: 레벨당 1 (최대 Lv.15 → 14P). 시작 스킬(계열 선택한 Q) 무료 + 나머지 전부 = 14P.
            Make<SkillTreeData>("SkillTree_Main", t =>
            {
                t.treeId = "main";
                t.displayName = "스킬 트리";
                t.nodes = new List<SkillTreeNode>();
                void Branch(SkillData sk, int x, string group)
                {
                    string b = sk.id; // sword_q …
                    int lv = sk.learnLevel;
                    t.nodes.Add(new SkillTreeNode { id = b, kind = SkillNodeKind.Skill, slot = sk.slot, skill = sk, grantsRank = 1, gridPos = new Vector2Int(x, 0), exclusiveGroup = group, cost = 1, requiredLevel = lv });
                    t.nodes.Add(new SkillTreeNode { id = b + "_2", kind = SkillNodeKind.Upgrade, slot = sk.slot, skill = sk, grantsRank = 2, gridPos = new Vector2Int(x, 1), requires = new List<string> { b }, cost = 1, requiredLevel = lv + 1 });
                    t.nodes.Add(new SkillTreeNode { id = b + "_3", kind = SkillNodeKind.Upgrade, slot = sk.slot, skill = sk, grantsRank = 3, gridPos = new Vector2Int(x, 2), requires = new List<string> { b + "_2" }, cost = 1, requiredLevel = lv + 4 });
                }
                Branch(sq, 0, "slot_q"); Branch(mq, 2, "slot_q");
                Branch(se, 4, "slot_e"); Branch(me, 6, "slot_e");
                Branch(sr, 8, "slot_r"); Branch(mr, 10, "slot_r");

                SkillTreeNode Passive(string id, string name, string desc, string icon, int x, string req, int cost, int lv, string key, float value) =>
                    new SkillTreeNode { id = id, kind = SkillNodeKind.Passive, displayName = name, description = desc, icon = Icon(icon), gridPos = new Vector2Int(x, 4),
                        requires = req != null ? new List<string> { req } : new List<string>(), cost = cost, requiredLevel = lv, statKey = key, statValue = value };
                t.nodes.Add(Passive("p_hp", "강인함", "최대 체력 +10%", "Item_Heal", 2, null, 1, 3, "max_hp_pct", 10f));
                t.nodes.Add(Passive("p_crit", "예리함", "치명타 확률 +5%", "Item_Attack", 4, "p_hp", 1, 5, "crit_pct", 5f));
                t.nodes.Add(Passive("p_cdr", "집중", "모든 스킬 대기시간 -8%", "Item_Defense", 6, "p_crit", 2, 8, "cooldown_pct", -8f));
                t.nodes.Add(Passive("p_move", "질주", "이동 속도 +8%", "Item_Speed", 8, "p_cdr", 2, 11, "move_pct", 8f));

                t.labels = new List<SkillTreeLabel>
                {
                    new SkillTreeLabel { text = "Q", gridPos = new Vector2(1, -0.9f) },
                    new SkillTreeLabel { text = "E", gridPos = new Vector2(5, -0.9f) },
                    new SkillTreeLabel { text = "R", gridPos = new Vector2(9, -0.9f) },
                    new SkillTreeLabel { text = "택1", gridPos = new Vector2(1, 0), small = true },
                    new SkillTreeLabel { text = "택1", gridPos = new Vector2(5, 0), small = true },
                    new SkillTreeLabel { text = "택1", gridPos = new Vector2(9, 0), small = true },
                    new SkillTreeLabel { text = "패시브", gridPos = new Vector2(0.4f, 4), small = true },
                };
            });

            // ── 아이템 (기획서 5장) ──
            Make<ItemData>("Item_Heal", i => { i.id = "item_heal"; i.displayName = "응급 회복제"; i.description = "최대 체력을 초과하지 않는다."; i.icon = Icon("Item_Heal"); i.slotIndex = 0; i.effectType = ItemEffectType.Heal; i.effectPercent = 30; i.buffDuration = 0; i.fxColor = new Color(1f, 0.4f, 0.45f); });
            Make<ItemData>("Item_Attack", i => { i.id = "item_attack"; i.displayName = "공격 강화제"; i.description = "기본 공격과 스킬 피해 증가. 중첩 없이 시간 갱신."; i.icon = Icon("Item_Attack"); i.slotIndex = 1; i.effectType = ItemEffectType.AttackBuff; i.effectPercent = 20; i.buffDuration = 10; i.fxColor = new Color(1f, 0.6f, 0.25f); });
            Make<ItemData>("Item_Defense", i => { i.id = "item_defense"; i.displayName = "방어 강화제"; i.description = "받는 피해 감소. 중첩 없이 시간 갱신."; i.icon = Icon("Item_Defense"); i.slotIndex = 2; i.effectType = ItemEffectType.DefenseBuff; i.effectPercent = 25; i.buffDuration = 10; i.fxColor = new Color(0.45f, 0.65f, 1f); });
            Make<ItemData>("Item_Speed", i => { i.id = "item_speed"; i.displayName = "신속 강화제"; i.description = "수평 이동 속도 증가. 점프 높이·사다리 속도는 그대로."; i.icon = Icon("Item_Speed"); i.slotIndex = 3; i.effectType = ItemEffectType.SpeedBuff; i.effectPercent = 20; i.buffDuration = 10; i.fxColor = new Color(0.45f, 1f, 0.6f); });
            Make<ItemData>("Item_GateKey", i => { i.id = "item_gatekey"; i.displayName = "게이트 인식표"; i.description = "봉쇄한 게이트에서 회수한 인식표. (인벤토리 표시 예시)"; i.icon = Icon("Item_GateKey"); i.slotIndex = -1; i.maxStack = 1; i.effectType = ItemEffectType.KeyItem; i.fxColor = new Color(1f, 0.85f, 0.35f); });

            // ── 보스 ──
            Make<BossData>("Boss_Stage1", b => { b.id = "boss_stage1"; b.displayName = "거리의 포식자"; b.title = "1스테이지 · 서울 거리"; b.phaseThresholds = new[] { 0.5f }; b.barTheme = 0; });
            Make<BossData>("Boss_Stage2", b => { b.id = "boss_stage2"; b.displayName = "선로의 감시자"; b.title = "2스테이지 · 지하철"; b.phaseThresholds = new[] { 0.6f, 0.3f }; b.barTheme = 1; });

            // ── 지도 (1스테이지 예시: 게이트 3 + 보스 구역) ──
            Make<MapData>("Map_Stage1", m =>
            {
                m.id = "map_stage1"; m.displayName = "1스테이지 · 서울 거리";
                m.rooms = new List<MapRoom>
                {
                    new MapRoom { id = "S1_01", displayName = "광장 입구", position = new Vector2Int(0, 1), icon = MapRoomIcon.Start, revealedAtStart = true },
                    new MapRoom { id = "S1_02", displayName = "대로", position = new Vector2Int(1, 1), size = new Vector2Int(2, 1) },
                    new MapRoom { id = "S1_03", displayName = "골목 (게이트 1)", position = new Vector2Int(1, 2), icon = MapRoomIcon.Gate },
                    new MapRoom { id = "S1_04", displayName = "편의점", position = new Vector2Int(3, 1), icon = MapRoomIcon.Item },
                    new MapRoom { id = "S1_05", displayName = "고가도로 아래 (게이트 2)", position = new Vector2Int(3, 0), icon = MapRoomIcon.Gate },
                    new MapRoom { id = "S1_06", displayName = "옥상 연결로", position = new Vector2Int(4, 1), size = new Vector2Int(1, 2) },
                    new MapRoom { id = "S1_07", displayName = "공사장 (게이트 3)", position = new Vector2Int(5, 2), icon = MapRoomIcon.Gate },
                    new MapRoom { id = "S1_08", displayName = "휴식 지점", position = new Vector2Int(5, 1), icon = MapRoomIcon.Save },
                    new MapRoom { id = "S1_09", displayName = "보스 구역", position = new Vector2Int(6, 0), size = new Vector2Int(2, 2), icon = MapRoomIcon.Boss },
                };
                m.links = new List<MapLink>
                {
                    new MapLink { fromRoom = "S1_01", toRoom = "S1_02" }, new MapLink { fromRoom = "S1_02", toRoom = "S1_03" },
                    new MapLink { fromRoom = "S1_02", toRoom = "S1_04" }, new MapLink { fromRoom = "S1_04", toRoom = "S1_05" },
                    new MapLink { fromRoom = "S1_04", toRoom = "S1_06" }, new MapLink { fromRoom = "S1_06", toRoom = "S1_07" },
                    new MapLink { fromRoom = "S1_06", toRoom = "S1_08" }, new MapLink { fromRoom = "S1_08", toRoom = "S1_09" },
                };
            });

            // ── 대화 ──
            var hero = Make<SpeakerData>("Speaker_Player", s => { s.id = "player"; s.displayName = "헌터"; s.nameColor = new Color(0.6f, 0.85f, 1f); s.portrait = Portrait("PlayerLarge"); });
            var agent = Make<SpeakerData>("Speaker_Agent", s => { s.id = "agent"; s.displayName = "관리국 요원"; s.nameColor = new Color(1f, 0.8f, 0.45f); s.portrait = Portrait("EnemyLarge"); });
            Make<DialogueData>("Dialogue_Intro", d =>
            {
                d.id = "intro";
                d.lines = new List<DialogueLine>
                {
                    new DialogueLine { speaker = agent, side = DialogueSide.Right, text = "F급 각성자, 맞지? 서울 도심에 게이트가 열렸다." },
                    new DialogueLine { speaker = hero, side = DialogueSide.Left, text = "게이트를 몇 개나 닫으면 되죠?" },
                    new DialogueLine { speaker = agent, side = DialogueSide.Right, text = "세 곳. 전부 봉쇄하면 보스 구역 출입을 허가하지." },
                    new DialogueLine { speaker = hero, side = DialogueSide.Left, text = "알겠습니다. 바로 출발할게요." },
                };
            });

            AssetDatabase.SaveAssets();
            Debug.Log("[OZ UI] 샘플 데이터 생성 완료 → " + OZPaths.SampleData);
        }
    }
}
