using System.Collections.Generic;
using OZ.UI.Contracts;
using UnityEditor;
using UnityEngine;

namespace OZ.UI.EditorTools
{
    /// <summary>
    /// Setup 3단계: 기획서 v0.2 검증값으로 샘플 데이터 생성 (UI/Data/Samples).
    /// 팀원은 이 에셋을 복제/수정해서 실제 데이터로 쓰면 된다. 이미 있는 에셋은 덮어쓰지 않는다
    /// (예외: 계열 스킬 트리 SkillTree_Sword / SkillTree_Magic 은 매번 기획 규칙으로 다시 채움).
    /// </summary>
    internal static class OZSampleData
    {
        static string P(string file) => $"{OZPaths.SampleData}/{file}.asset";
        public static bool IsDone => AssetDatabase.LoadAssetAtPath<ClassData>(P("Class_Sword")) != null && AssetDatabase.LoadAssetAtPath<SkillTreeData>(P("SkillTree_Sword")) != null;

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

        /// <summary>
        /// 계열 스킬 트리 (기획서 3장 검증값) — 이미 있어도 매번 이 규칙으로 다시 채운다 (Setup 다시 실행 = 기획 수치로 복구).
        ///   Q: Lv.1부터 1단계 보유 (계열 선택 시 무료) · E: Lv.2부터 1P · R: Lv.4부터 1P
        ///   각 스킬 최대 3단계, 강화도 단계당 1P (습득한 뒤 바로 가능). 패시브·택1 없음.
        ///   최대 Lv.7 → 6P, 전부 찍는 데 8P (시작 Q 제외) → 투자처를 고른다.
        /// </summary>
        static SkillTreeData ClassTree(string file, string title, SkillData q, SkillData e, SkillData r)
        {
            var t = Load<SkillTreeData>(file);
            bool isNew = t == null;
            if (isNew) t = ScriptableObject.CreateInstance<SkillTreeData>();

            t.treeId = file.Replace("SkillTree_", "").ToLowerInvariant();
            t.displayName = title;
            t.nodes = new List<SkillTreeNode>();
            t.labels = new List<SkillTreeLabel>();
            int x = 0;
            foreach (var sk in new[] { q, e, r })
            {
                string b = sk.id; // sword_q …
                int lv = sk.learnLevel;
                t.nodes.Add(new SkillTreeNode { id = b, kind = SkillNodeKind.Skill, slot = sk.slot, skill = sk, grantsRank = 1, gridPos = new Vector2Int(x, 0), cost = sk.costPerRank, requiredLevel = lv });
                for (int rank = 2; rank <= sk.maxRank; rank++)
                    t.nodes.Add(new SkillTreeNode { id = $"{b}_{rank}", kind = SkillNodeKind.Upgrade, slot = sk.slot, skill = sk, grantsRank = rank, gridPos = new Vector2Int(x, rank - 1),
                        requires = new List<string> { rank == 2 ? b : $"{b}_{rank - 1}" }, cost = sk.costPerRank, requiredLevel = lv });
                t.labels.Add(new SkillTreeLabel { text = sk.slot.ToString(), gridPos = new Vector2(x, -0.9f) });
                t.labels.Add(new SkillTreeLabel { text = $"Lv.{lv}", gridPos = new Vector2(x + 1f, 0f), small = true });
                x += 2;
            }

            if (isNew) AssetDatabase.CreateAsset(t, P(file));
            else EditorUtility.SetDirty(t);
            return t;
        }

        static void LinkTree(ClassData c, SkillTreeData tree)
        {
            if (c == null || c.skillTree == tree) return;
            c.skillTree = tree;
            EditorUtility.SetDirty(c);
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

            var swordTree = ClassTree("SkillTree_Sword", "검술 스킬", sq, se, sr);
            var magicTree = ClassTree("SkillTree_Magic", "마법 스킬", mq, me, mr);

            var cs = Make<ClassData>("Class_Sword", c => { c.playerClass = PlayerClass.Sword; c.displayName = "검술"; c.description = "짧고 좁은 범위에 높은 피해를 집중한다.\n적에게 접근할 기회와 빠져나올 위치를 판단."; c.icon = Icon("Skill_SwordQ"); c.skills = new[] { sq, se, sr }; });
            var cm = Make<ClassData>("Class_Magic", c => { c.playerClass = PlayerClass.Magic; c.displayName = "마법"; c.description = "넓은 범위 공격과 지속 피해 장판.\n적이 모이는 위치와 경로를 예상해 공격."; c.icon = Icon("Skill_MagicQ"); c.skills = new[] { mq, me, mr }; });
            LinkTree(cs, swordTree);
            LinkTree(cm, magicTree);

            // v0.9: 기획서 범위 밖이 된 예전 샘플 정리 (검술·마법 섞는 공용 트리, 인벤토리 전용 열쇠 아이템)
            foreach (var old in new[] { "SkillTree_Main", "Item_GateKey" })
                if (AssetDatabase.LoadAssetAtPath<Object>(P(old)) != null) AssetDatabase.DeleteAsset(P(old));

            // ── 아이템 (기획서 5장) ──
            Make<ItemData>("Item_Heal", i => { i.id = "item_heal"; i.displayName = "응급 회복제"; i.description = "최대 체력을 초과하지 않는다."; i.icon = Icon("Item_Heal"); i.slotIndex = 0; i.effectType = ItemEffectType.Heal; i.effectPercent = 30; i.buffDuration = 0; i.fxColor = new Color(1f, 0.4f, 0.45f); });
            Make<ItemData>("Item_Attack", i => { i.id = "item_attack"; i.displayName = "공격 강화제"; i.description = "기본 공격과 스킬 피해 증가. 중첩 없이 시간 갱신."; i.icon = Icon("Item_Attack"); i.slotIndex = 1; i.effectType = ItemEffectType.AttackBuff; i.effectPercent = 20; i.buffDuration = 10; i.fxColor = new Color(1f, 0.6f, 0.25f); });
            Make<ItemData>("Item_Defense", i => { i.id = "item_defense"; i.displayName = "방어 강화제"; i.description = "받는 피해 감소. 중첩 없이 시간 갱신."; i.icon = Icon("Item_Defense"); i.slotIndex = 2; i.effectType = ItemEffectType.DefenseBuff; i.effectPercent = 25; i.buffDuration = 10; i.fxColor = new Color(0.45f, 0.65f, 1f); });
            Make<ItemData>("Item_Speed", i => { i.id = "item_speed"; i.displayName = "신속 강화제"; i.description = "수평 이동 속도 증가. 점프 높이·사다리 속도는 그대로."; i.icon = Icon("Item_Speed"); i.slotIndex = 3; i.effectType = ItemEffectType.SpeedBuff; i.effectPercent = 20; i.buffDuration = 10; i.fxColor = new Color(0.45f, 1f, 0.6f); });

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

            EnsureShop();

            AssetDatabase.SaveAssets();
            Debug.Log("[OZ UI] 샘플 데이터 생성 완료 → " + OZPaths.SampleData);
        }

        /// <summary>
        /// v1.0 임시 상점: 1~4 소모품을 파는 Shop_Potion (+ 상인 화자). 없으면 만들고, 있으면 그대로 돌려준다.
        /// 가격은 임시값 (기획데이터에 소모품 가격 없음 — 기획 확정 시 에셋에서 수정). 보유 상한 5 = 기획데이터 9장.
        /// </summary>
        /// <summary>
        /// v1.1 임시 장비 2종 (칼·방패) — 상점에서 1회 구매(maxOwned 1)·영구 보유. 퀵슬롯 없음(slotIndex -1).
        /// 효과·가격은 기획 확정 전 자리값 — 실제 공격/방어 반영은 플레이어 쪽(PlayerStats)이 ItemData.id로 판단.
        /// </summary>
        static List<ShopEntry> EnsureEquipment()
        {
            var sword = Make<ItemData>("Item_Sword", i => { i.id = "equip_sword"; i.displayName = "칼"; i.description = "[임시] 공격 강화 장비 — 효과 기획 미정"; i.icon = Icon("Item_Sword"); i.slotIndex = -1; i.maxStack = 1; i.effectType = ItemEffectType.Equipment; i.fxColor = new Color(0.85f, 0.87f, 0.92f); });
            var shield = Make<ItemData>("Item_Shield", i => { i.id = "equip_shield"; i.displayName = "방패"; i.description = "[임시] 방어 강화 장비 — 효과 기획 미정"; i.icon = Icon("Item_Shield"); i.slotIndex = -1; i.maxStack = 1; i.effectType = ItemEffectType.Equipment; i.fxColor = new Color(0.79f, 0.64f, 0.36f); });
            return new List<ShopEntry>
            {
                new ShopEntry { item = sword, price = 300, maxOwned = 1 },
                new ShopEntry { item = shield, price = 250, maxOwned = 1 },
            };
        }

        public static ShopData EnsureShop()
        {
            var existing = Load<ShopData>("Shop_Potion");
            if (existing != null)
            {
                bool dirty = false;
                // v1.1: 아이템은 성장 포인트와 다른 '아이템 포인트'로 산다 (예전에 만든 에셋도 이름만 바꿔 줌)
                if (existing.currencyName == "포인트") { existing.currencyName = "아이템 포인트"; dirty = true; }
                // v1.1: 임시 장비 (칼·방패) — 1회 구매
                foreach (var eq in EnsureEquipment())
                    if (existing.entries != null && !existing.entries.Exists(e => e != null && e.item == eq.item)) { existing.entries.Add(eq); dirty = true; }
                if (dirty) { EditorUtility.SetDirty(existing); AssetDatabase.SaveAssets(); }
                return existing;
            }
            OZFontBuilder.EnsureFolder(OZPaths.SampleData);
            var heal = Load<ItemData>("Item_Heal");
            if (heal == null) { Run(); return Load<ShopData>("Shop_Potion"); } // 아이템부터 없으면 전체 샘플 생성 (끝에서 다시 여기로 옴)

            var keeper = Make<SpeakerData>("Speaker_Merchant", s => { s.id = "merchant"; s.displayName = "편의점 주인"; s.nameColor = new Color(0.55f, 1f, 0.7f); s.portrait = Portrait("PlayerLarge"); });
            var shop = Make<ShopData>("Shop_Potion", sh =>
            {
                sh.id = "shop_potion";
                sh.displayName = "편의점";
                sh.currencyName = "아이템 포인트";
                sh.keeper = keeper;
                sh.greeting = "게이트 터진 뒤로 이런 것만 팔려. 하나씩 챙겨 가.";
                sh.entries = new List<ShopEntry>
                {
                    new ShopEntry { item = heal, price = 100, maxOwned = 5 },
                    new ShopEntry { item = Load<ItemData>("Item_Attack"), price = 150, maxOwned = 5 },
                    new ShopEntry { item = Load<ItemData>("Item_Defense"), price = 150, maxOwned = 5 },
                    new ShopEntry { item = Load<ItemData>("Item_Speed"), price = 120, maxOwned = 5 },
                };
                sh.entries.AddRange(EnsureEquipment());
            });
            AssetDatabase.SaveAssets();
            return shop;
        }
    }
}
