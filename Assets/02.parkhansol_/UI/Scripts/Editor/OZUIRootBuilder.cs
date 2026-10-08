using OZ.UI.Contracts;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace OZ.UI.EditorTools
{
    /// <summary>
    /// Setup 4단계: UIRoot 프리팹 자동 생성 (UI/Prefabs/UIRoot.prefab).
    /// HUD · 보스바 · 토스트 · 미니맵 + 모든 창(타이틀/계열선택/스킬/인벤토리/지도/일시정지/대화/사망/데모종료).
    /// 다시 실행하면 프리팹을 새로 만든다 (수작업 수정은 프리팹 Variant로 하는 것을 권장).
    /// </summary>
    internal static class OZUIRootBuilder
    {
        public const string PrefabPath = OZPaths.UI + "/Prefabs/UIRoot.prefab";
        public static bool IsDone => AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) != null;

        public static GameObject Run()
        {
            UIB.LoadFonts();
            OZFontBuilder.EnsureFolder(OZPaths.UI + "/Prefabs");

            var root = new GameObject("UIRoot");
            try
            {
                var manager = root.AddComponent<UIManager>();
                root.AddComponent<UIInputRouter>();
                var sfx = root.AddComponent<UISoundPlayer>(); // v0.6 UI 효과음
                sfx.soundSet = AssetDatabase.LoadAssetAtPath<UISoundSet>(OZUISoundSetup.SetPath) ?? OZUISoundSetup.Run();
                var mapCtrl = root.AddComponent<MapController>();

                // UIManager.Awake가 레이어를 만들지만, 프리팹 안에서 미리 배치하려고 직접 생성
                Transform hudLayer = Layer(root.transform, UILayer.HUD);
                Transform screenLayer = Layer(root.transform, UILayer.Screen);
                Transform popupLayer = Layer(root.transform, UILayer.Popup);
                Transform cineLayer = Layer(root.transform, UILayer.Cinematic);
                Transform fullLayer = Layer(root.transform, UILayer.Fullscreen);
                Transform modalLayer = Layer(root.transform, UILayer.Modal);
                Transform toastLayer = Layer(root.transform, UILayer.Toast);
                Transform overlayLayer = Layer(root.transform, UILayer.Overlay);

                BuildDamageFx(hudLayer); // HUD보다 먼저 = 뒤에 그려짐 (숫자가 HUD 패널을 가리지 않게)
                var minimap = BuildHud(hudLayer, overlayLayer);
                BuildBoss(cineLayer);
                BuildToast(toastLayer);

                BuildTitle(fullLayer);
                BuildClassSelect(fullLayer);
                BuildSkillTreeWindow(screenLayer); // v0.4: 카드형 SkillWindow → 노드형 스킬 트리
                BuildInventory(screenLayer);
                var fullMap = BuildMapWindow(screenLayer, mapCtrl);
                BuildPause(screenLayer);
                BuildDialogue(popupLayer);
                BuildDeath(fullLayer);
                BuildDemoEnd(fullLayer);
                BuildOptions(modalLayer);
                BuildFlow(overlayLayer); // v0.5: 로딩·페이드·스테이지 시작 띠 (맨 위)

                mapCtrl.renderers = new[] { minimap, fullMap };
                AssignSelectableSounds(root);

                var prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                Debug.Log("[OZ UI] UIRoot 프리팹 생성 → " + PrefabPath, prefab);
                return prefab;
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        static Transform Layer(Transform root, UILayer layer)
        {
            var rt = UIB.Rect("Layer_" + layer, root);
            var canvas = rt.gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = (int)layer;
            canvas.pixelPerfect = true;
            var scaler = rt.gameObject.AddComponent<PixelCanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
            scaler.referencePixelsPerUnit = 100f;
            rt.gameObject.AddComponent<GraphicRaycaster>();
            return rt;
        }

        // 화면 전체를 덮는 창 루트
        static T Window<T>(string name, Transform layerTf, ScreenId id, UILayer layer, bool pause, bool blockInput = true,
            bool closeOnBack = true, WindowTransition transition = WindowTransition.PopIn, Color? dim = null) where T : UIWindow
        {
            var rt = UIB.Rect(name, layerTf).Stretch();
            var cg = rt.gameObject.AddComponent<CanvasGroup>();
            cg.alpha = 0f; cg.interactable = false; cg.blocksRaycasts = false; // 에디터에서도 닫힌 상태로 보이게
            if (dim.HasValue)
            {
                var bg = UIB.Solid("Dim", rt, dim.Value, true);
                bg.rectTransform.Stretch();
            }
            var w = rt.gameObject.AddComponent<T>();
            w.screenId = id;
            w.layer = layer;
            w.pausesGame = pause;
            w.blocksGameplayInput = blockInput;
            w.closeOnBack = closeOnBack;
            w.transition = transition;
            return w;
        }

        static RectTransform Content(UIWindow w, float width, float height, string panelSprite = "Panels/Blue/Panel")
        {
            var panel = UIB.Panel("Panel", w.transform, panelSprite);
            panel.rectTransform.Place(0.5f, 0.5f, 0, 0, width, height);
            w.content = panel.rectTransform;
            return panel.rectTransform;
        }

        static TweenFillBar Bar(string name, Transform parent, string spriteBase, float w, float h, int inset, Color? fillColor = null, string theme = "Blue")
        {
            var bg = UIB.Img(name, parent, UIB.Px($"ValueBars/{theme}/{spriteBase}Background"), UIB.Px($"ValueBars/{theme}/{spriteBase}Background") != null ? Color.white : new Color(0, 0, 0, 0.6f));
            bg.rectTransform.sizeDelta = new Vector2(w, h);
            var trailSprite = UIB.Px($"ValueBars/{theme}/{spriteBase}FollowFill");
            var trail = UIB.Img("Trail", bg.transform, trailSprite, trailSprite != null ? Color.white : new Color(1f, 1f, 1f, 0.8f)).Filled(Image.FillMethod.Horizontal);
            trail.rectTransform.Stretch(inset, inset, inset, inset);
            var fillSprite = UIB.Px($"ValueBars/{theme}/{spriteBase}Fill");
            var fill = UIB.Img("Fill", bg.transform, fillSprite, fillColor ?? (fillSprite != null ? Color.white : new Color(0.9f, 0.25f, 0.3f))).Filled(Image.FillMethod.Horizontal);
            fill.rectTransform.Stretch(inset, inset, inset, inset);
            var fgSprite = UIB.Px($"ValueBars/{theme}/{spriteBase}Foreground");
            if (fgSprite != null)
            {
                var fg = UIB.Img("Foreground", bg.transform, fgSprite);
                fg.rectTransform.Stretch();
            }
            var bar = bg.gameObject.AddComponent<TweenFillBar>();
            bar.fill = fill;
            bar.trail = trail;
            return bar;
        }

        static CooldownRadial Radial(Transform parent, RectTransform punch, Graphic flash, float size, bool label = true)
        {
            var overlay = UIB.Solid("Cooldown", parent, new Color(0f, 0f, 0f, 0.6f)).Filled(Image.FillMethod.Radial360, (int)Image.Origin360.Top);
            overlay.fillClockwise = false;
            overlay.rectTransform.Place(0.5f, 0.5f, 0, 0, size, size);
            var cd = overlay.gameObject.AddComponent<CooldownRadial>();
            cd.overlay = overlay;
            cd.punchTarget = punch;
            cd.flashTarget = flash;
            if (label)
            {
                var t = UIB.Small10("Seconds", overlay.transform, "", TextAlignmentOptions.Center, Color.white);
                t.rectTransform.Stretch();
                cd.label = t;
            }
            return cd;
        }

        // ───────────────────────────── 피해 숫자 · 타격 이펙트 · 적 체력바 ─────────────────────────────
        static void BuildDamageFx(Transform layer)
        {
            var root = UIB.Rect("DamageFx", layer).Stretch();
            var fx = root.gameObject.AddComponent<DamageFxController>();
            fx.barLayer = UIB.Rect("Bars", root).Stretch();
            fx.sparkLayer = UIB.Rect("Sparks", root).Stretch();
            fx.numberLayer = UIB.Rect("Numbers", root).Stretch();

            // 숫자 템플릿: 그림자(1px 아래 검정) + 숫자 + 위 작은 글자(치명타)
            var num = UIB.Rect("NumberTemplate", fx.numberLayer).Place(0.5f, 0.5f, 0, 0, 90, 18);
            num.pivot = new Vector2(0.5f, 0f);
            var nv = num.gameObject.AddComponent<DamageNumberView>();
            nv.group = UIB.Group(num.gameObject);
            nv.group.blocksRaycasts = false; nv.group.interactable = false;
            var sh = UIB.Text("Shadow", num, "0", UIB.Body, 12, TextAlignmentOptions.Bottom, new Color(0.04f, 0.04f, 0.08f, 0.95f));
            sh.rectTransform.Stretch(); sh.rectTransform.anchoredPosition = new Vector2(1, -1);
            var tx = UIB.Text("Value", num, "0", UIB.Body, 12, TextAlignmentOptions.Bottom, Color.white);
            tx.rectTransform.Stretch();
            var lb = UIB.Small10("Label", num, "치명타", TextAlignmentOptions.Bottom, new Color(1f, 0.5f, 0.18f));
            lb.rectTransform.Place(0.5f, 1, 0, 12, 90, 11);
            foreach (var t in new TMP_Text[] { sh, tx, lb }) { t.raycastTarget = false; t.textWrappingMode = TextWrappingModes.NoWrap; }
            nv.text = tx; nv.shadow = sh; nv.label = lb;
            fx.numberTemplate = nv;

            var styles = DamageNumberStyle.Defaults();
            foreach (var st in styles)
            {
                // 픽셀 폰트는 원본 크기의 정수배만 선명: Galmuri11·Bold=12/24, Galmuri14=15/30, Galmuri9=10
                if (st.kind == DamageKind.Weakness) st.font = UIB.Bold;
                else if (st.fontSize % 15 == 0) st.font = UIB.Title;
                else if (st.fontSize <= 10) st.font = UIB.Small;
                else st.font = UIB.Body;
            }
            fx.styles = styles;

            // 타격 이펙트 템플릿
            var spark = UIB.Img("SparkTemplate", fx.sparkLayer, null);
            spark.rectTransform.Place(0.5f, 0.5f, 0, 0, 16, 16);
            var sv = spark.gameObject.AddComponent<HitSparkView>();
            sv.image = spark;
            fx.sparkTemplate = sv;
            fx.normalSparkFrames = Frames("HitSpark_N_", 4);
            fx.critSparkFrames = Frames("HitSpark_C_", 5);

            // 적 체력바 템플릿 (빨간 MinimalBar, 바닥 중앙 기준 → 머리 위에 얹힘)
            var bar = Bar("EnemyBarTemplate", fx.barLayer, "MinimalBar", 28, 4, 1, null, "Red");
            var brt = bar.GetComponent<RectTransform>();
            brt.Place(0.5f, 0.5f, 0, 0, 28, 4);
            brt.pivot = new Vector2(0.5f, 0f);
            bar.trailDelay = 0.25f;
            var ev = bar.gameObject.AddComponent<EnemyHealthBarView>();
            ev.bar = bar;
            ev.group = UIB.Group(bar.gameObject);
            ev.group.blocksRaycasts = false;
            fx.enemyBarTemplate = ev;

            // 원본은 꺼 둠 (에디터 화면에 템플릿이 보이지 않게)
            num.gameObject.SetActive(false);
            spark.gameObject.SetActive(false);
            bar.gameObject.SetActive(false);
        }

        static Sprite[] Frames(string prefix, int count)
        {
            var list = new System.Collections.Generic.List<Sprite>();
            for (int i = 0; i < count; i++)
            {
                var s = AssetDatabase.LoadAssetAtPath<Sprite>($"{OZPaths.UI}/Art/FX/{prefix}{i}.png");
                if (s != null) list.Add(s);
            }
            return list.ToArray();
        }

        // ───────────────────────────── HUD ─────────────────────────────
        static MapRenderer BuildHud(Transform layer, Transform overlayLayer)
        {
            var hudRt = UIB.Rect("HUD", layer).Stretch();
            var hud = hudRt.gameObject.AddComponent<HudView>();
            hud.group = UIB.Group(hudRt.gameObject);
            hud.group.blocksRaycasts = false;

            // ── 좌상단: 초상화 칸 + HP 바(Sci-Fi Hud_Lifebar B) + 레벨/랭크/경험치 ──
            //  [초상화 34×34] [HP 바 106×26] 62/100
            //  [E급]          Lv.3
            //                 ▬▬▬▬▬▬ EXP
            var tl = UIB.Rect("TopLeft", hudRt).Place(0, 1, 8, -8, 200, 52);
            var health = tl.gameObject.AddComponent<HealthView>();
            // v0.4: 밝은 배경(역 간판·조명)이 뒤로 지나가도 읽히게 반투명 바탕 (오른쪽으로 갈수록 옅게 보이도록 2단)
            var tlBack = UIB.Solid("Backdrop", tl, new Color(0.02f, 0.03f, 0.07f, 0.45f));
            tlBack.rectTransform.Place(0, 1, -4, 4, 196, 58);
            var tlBackFade = UIB.Solid("BackdropFade", tl, new Color(0.02f, 0.03f, 0.07f, 0.2f));
            tlBackFade.rectTransform.Place(0, 1, 192, 4, 16, 58);

            var pFrameSprite = UIB.HudArt("PortraitFrame");
            var pFrame = UIB.Img("PortraitFrame", tl, pFrameSprite ?? UIB.Fallback, pFrameSprite != null ? Color.white : new Color(0f, 0.14f, 0.17f));
            pFrame.rectTransform.Place(0, 1, 0, 0, 34, 34);
            var portraitSprite = UIB.Px("Portraits/PlayerMedium");
            var portrait = UIB.Img("Portrait", pFrame.transform, portraitSprite);
            portrait.rectTransform.Place(0.5f, 0.5f, 0, 0, 20, 25);
            portrait.preserveAspect = true;
            hud.portrait = portrait;
            hud.defaultPortrait = portraitSprite;

            var hpRoot = UIB.Rect("HPBar", tl).Place(0, 1, 38, -4, 106, 26);
            var hpBg = UIB.Img("Background", hpRoot, UIB.Sf("Hud/Hud_Lifebar_B_2") ?? UIB.Fallback, UIB.Sf("Hud/Hud_Lifebar_B_2") != null ? Color.white : new Color(0, 0, 0, 0.7f));
            hpBg.rectTransform.Stretch();
            // 안쪽 영역(가로 102×세로 22, 왼쪽 위 2px 안) 모양 그대로 만든 채움 스프라이트
            var hpTrail = UIB.Img("Trail", hpRoot, UIB.HudArt("HudLifebarB_Trail") ?? UIB.White, new Color(1f, 0.35f, 0.42f)).Filled(Image.FillMethod.Horizontal);
            hpTrail.rectTransform.Place(0, 1, 2, -2, 102, 22);
            var hpFill = UIB.Img("Fill", hpRoot, UIB.HudArt("HudLifebarB_Fill") ?? UIB.White).Filled(Image.FillMethod.Horizontal);
            hpFill.rectTransform.Place(0, 1, 2, -2, 102, 22);
            var hpFrame = UIB.Img("Frame", hpRoot, UIB.Sf("Hud/Hud_Lifebar_B_1"));
            hpFrame.rectTransform.Stretch();
            if (hpFrame.sprite == null) hpFrame.enabled = false;
            var hpBar = hpRoot.gameObject.AddComponent<TweenFillBar>();
            hpBar.fill = hpFill;
            hpBar.trail = hpTrail;
            health.bar = hpBar;
            health.flaskRoot = pFrame.rectTransform; // 피격 흔들림·저체력 맥박은 초상화 칸에
            health.flaskFill = null;
            health.flaskSteps = new Sprite[0];
            health.flashTarget = hpFill;

            var hpText = UIB.Small10("HPText", tl, "100/100", TextAlignmentOptions.Left);
            hpText.rectTransform.Place(0, 1, 148, -12, 50, 12);
            health.valueText = hpText;

            var prog = tl.gameObject.AddComponent<ProgressionView>();
            var lv = UIB.Body12("Level", tl, "Lv.1", TextAlignmentOptions.Left, UIB.Accent);
            lv.rectTransform.Place(0, 1, 38, -31, 60, 13);
            prog.levelText = lv;
            var rankBadge = UIB.Panel("RankBadge", tl, "Panels/Blue/Panel");
            rankBadge.raycastTarget = false;
            rankBadge.rectTransform.Place(0, 1, 2, -37, 30, 15); // 초상화 바로 아래
            var rank = UIB.Body12("Rank", rankBadge.transform, "F급", TextAlignmentOptions.Center);
            rank.rectTransform.Stretch();
            prog.rankText = rank;
            prog.rankBadge = rankBadge.rectTransform;
            var exp = Bar("EXPBar", tl, "MinimalBar", 106, 6, 1, new Color(0.55f, 0.85f, 1f));
            exp.GetComponent<RectTransform>().Place(0, 1, 38, -46, 106, 6);
            exp.trailDelay = 0.05f;
            prog.expBar = exp;
            // SP 표시는 HUD에서 뺌 (스킬 창에서만 표시)

            // ── 좌상단 체력 블록 아래: 퀘스트 목록 (v0.6: 상단 가운데 게이트 패널 대체) ──
            //  ▌퀘스트                Tab 지도
            //  □ 게이트 봉쇄             1 / 2
            //    ▬▬▬▬▬▬▬────────────
            //  □ 보스 처치            게이트 후
            hud.quests = BuildQuestList(hudRt);

            var guide = UIB.Rect("Guide", hudRt).Place(0.5f, 1, 0, -64, 400, 20);
            hud.guideGroup = UIB.Group(guide.gameObject);
            var guideText = UIB.Title15("Text", guide, "", TextAlignmentOptions.Center, Color.white);
            guideText.rectTransform.Stretch();
            hud.guideText = guideText;

            // ── 가운데: 큰 띠 문구 ("게이트 파괴") ──
            var bannerRoot = UIB.Rect("Banner", hudRt).Place(0.5f, 0.5f, 0, 50, 320, 44);
            var bannerView = bannerRoot.gameObject.AddComponent<HudBannerView>();
            bannerView.group = UIB.Group(bannerRoot.gameObject);
            bannerView.group.alpha = 0f;
            var bandSprite = UIB.Px("Banners/Blue/TitleBanner");
            var band = UIB.Img("Band", bannerRoot, bandSprite ?? UIB.Fallback, bandSprite != null ? Color.white : new Color(0.05f, 0.08f, 0.16f, 0.9f));
            band.rectTransform.Place(0.5f, 1, 0, 0, 320, 32);
            bannerView.band = band.rectTransform;
            var bTitle = UIB.Title15("Title", band.transform, "게이트 파괴", TextAlignmentOptions.Center, Color.white);
            bTitle.rectTransform.Stretch(0, 0, 4, 4);
            bannerView.titleText = bTitle;
            var bSub = UIB.Small10("Subtitle", band.transform, "", TextAlignmentOptions.Center, UIB.Accent);
            bSub.rectTransform.Place(0.5f, 0, 0, -13, 300, 12);
            bannerView.subtitleText = bSub;
            hud.banner = bannerView;

            // ── 우상단: 미니맵 ──
            var mmFrame = UIB.Panel("Minimap", hudRt, "Panels/Blue/Panel");
            mmFrame.raycastTarget = false;
            mmFrame.rectTransform.Place(1, 1, -8, -8, 104, 70);
            var minimap = MapView(mmFrame.transform, 6f, true, 4);
            hud.systemAlarm = BuildSystemAlarm(hudRt);
            _systemAlarm = hud.systemAlarm;

            // ── 하단 양손 분리 (v0.6) ──
            //  [버프 20px …]                                          ← 좌하단 y 80
            //   1    2    3    4                          Q    E    R
            //  [36] [36] [36] [36]  (아이템, 좌하단)       [36] [36] [36]  (스킬, 우하단)
            var bottom = UIB.Rect("BottomLeft", hudRt).Place(0, 0, 8, 8, 160, 62);
            var itemsRt = UIB.Rect("Items", bottom).Place(0, 0, 0, 0, 160, 62);
            var itemBar = itemsRt.gameObject.AddComponent<ItemBarView>();
            for (int i = 0; i < 4; i++) itemBar.slots[i] = ItemSlot(itemsRt, i, i * 39f);

            var bottomRight = UIB.Rect("BottomRight", hudRt).Place(1, 0, -8, 8, 116, 62);
            var skillBar = bottomRight.gameObject.AddComponent<SkillBarView>();
            for (int i = 0; i < 3; i++) skillBar.slots[i] = SkillSlot(bottomRight, (SkillSlot)i, i * 40f);

            var buffRt = UIB.Rect("Buffs", hudRt).Place(0, 0, 8, 80, 200, 20);
            var hl = buffRt.gameObject.AddComponent<HorizontalLayoutGroup>();
            hl.spacing = 2; hl.childAlignment = TextAnchor.LowerLeft;
            hl.childControlWidth = hl.childControlHeight = false;
            hl.childForceExpandWidth = hl.childForceExpandHeight = false;
            var buffs = buffRt.gameObject.AddComponent<BuffTrayView>();
            var tplImg = UIB.Panel("BuffTemplate", buffRt, "Panels/Blue/Panel");
            tplImg.raycastTarget = false;
            tplImg.rectTransform.sizeDelta = new Vector2(20, 20);
            var tplIcon = UIB.Img("Icon", tplImg.transform, null);
            tplIcon.rectTransform.Place(0.5f, 0.5f, 0, 0, 16, 16);
            var buffIcon = tplImg.gameObject.AddComponent<BuffIconView>();
            buffIcon.icon = tplIcon;
            buffIcon.timer = Radial(tplImg.transform, null, null, 18, false);
            buffs.template = buffIcon;

            hud.health = health;
            hud.progression = prog;
            hud.gate = null; // v0.6: 게이트는 퀘스트 목록으로
            hud.skills = skillBar;
            hud.items = itemBar;
            hud.buffs = buffs;

            // 화면 플래시 (Overlay 레이어)
            var flash = UIB.Solid("ScreenFlash", overlayLayer, new Color(1, 1, 1, 0));
            flash.rectTransform.Stretch();
            hud.flashOverlay = flash;

            return minimap;
        }

        static SystemAlarmView _systemAlarm;

        // 모든 버튼·토글·슬라이더에 소리. 직접 소리를 내는 칸(스킬 노드·인벤토리 칸)은 클릭 소리 없음
        static void AssignSelectableSounds(GameObject root)
        {
            foreach (var sel in root.GetComponentsInChildren<Selectable>(true))
            {
                var s = sel.gameObject.GetComponent<UISelectableSound>() ?? sel.gameObject.AddComponent<UISelectableSound>();
                string n = sel.name.ToLowerInvariant();
                if (sel is Slider) { s.click = UISound.None; s.valueChange = UISound.Slider; }
                else if (sel is Toggle) { s.click = UISound.None; s.valueChange = UISound.Tab; }
                else if (sel.GetComponent<SkillNodeView>() != null || sel.GetComponent<InventorySlotView>() != null) s.click = UISound.None;
                else if (n.Contains("close") || n.Contains("back") || n.Contains("title") || n.Contains("cancel")) s.click = UISound.Back;
                else if (n.Contains("tab") || n.Contains("arrow")) s.click = UISound.Tab;
                else if (n.Contains("choose") || n.Contains("새 게임") || n.Contains("재도전") || n.Contains("계속하기")) s.click = UISound.Confirm;
                else if (n.Contains("타이틀로") || n.Contains("닫기")) s.click = UISound.Back;
                else s.click = UISound.Click;
            }
            // 창 소리: 타이틀·대화는 무음 (흐름 연출·대화 넘김 소리가 따로 있음)
            foreach (var w in root.GetComponentsInChildren<UIWindow>(true))
            {
                if (w.screenId == ScreenId.Title || w.screenId == ScreenId.Dialogue) { w.openSound = UISound.None; w.closeSound = UISound.None; }
                if (w.screenId == ScreenId.Death) w.closeSound = UISound.None;
            }
        }
        static readonly Color HudBack = new Color(0.02f, 0.03f, 0.07f, 0.45f);

        static QuestListView BuildQuestList(Transform hudRt)
        {
            var root = UIB.Rect("Quests", hudRt).Place(0, 1, 4, -66, 170, 80);
            var view = root.gameObject.AddComponent<QuestListView>();
            view.group = UIB.Group(root.gameObject);
            view.group.blocksRaycasts = false;
            var back = UIB.Solid("Backdrop", root, HudBack);
            back.rectTransform.Place(0, 1, 0, 0, 166, 50);
            view.backdrop = back;
            var accent = UIB.Accent;
            var bar = UIB.Solid("HeaderMark", root, accent);
            bar.rectTransform.Place(0, 1, 4, -3, 2, 9);
            var head = UIB.Small10("Header", root, "퀘스트", TextAlignmentOptions.Left, accent);
            head.rectTransform.Place(0, 1, 9, -1, 60, 12);
            var hint = UIB.Small10("Hint", root, "Tab 지도", TextAlignmentOptions.Right, new Color(0.45f, 0.5f, 0.62f));
            hint.rectTransform.Place(1, 1, -8, -1, 60, 12);
            view.rowsRoot = root;
            view.headerHeight = 14f;
            view.rowHeight = 17f;

            // 줄 템플릿
            var row = UIB.Rect("RowTemplate", root).Place(0, 1, 4, -14, 158, 17);
            var rv = row.gameObject.AddComponent<QuestRowView>();
            rv.group = UIB.Group(row.gameObject);
            var box = UIB.Solid("Box", row, new Color(0.32f, 0.86f, 1f));
            box.rectTransform.Place(0, 1, 1, -4, 7, 7);
            var inner = UIB.Solid("Inner", box.transform, new Color(0.03f, 0.05f, 0.1f));
            inner.rectTransform.Place(0.5f, 0.5f, 0, 0, 5, 5);
            var check = UIB.Solid("Check", box.transform, Color.white);
            check.rectTransform.Place(0.5f, 0.5f, 0, 0, 3, 3);
            rv.box = box;
            rv.check = check;
            var title = UIB.Body12("Title", row, "게이트 봉쇄", TextAlignmentOptions.Left);
            title.rectTransform.Place(0, 1, 12, -1, 104, 13);
            title.textWrappingMode = TextWrappingModes.NoWrap;
            title.overflowMode = TextOverflowModes.Overflow; // Ellipsis는 줄 높이(13px 칸 초과) 때문에 글자를 통째로 숨김
            rv.title = title;
            var right = UIB.Body12("Right", row, "0 / 2", TextAlignmentOptions.Right);
            right.rectTransform.Place(1, 1, 0, -1, 56, 13);
            right.textWrappingMode = TextWrappingModes.NoWrap;
            rv.right = right;
            var barBack = UIB.Solid("Bar", row, new Color(1f, 1f, 1f, 0.1f));
            barBack.rectTransform.Place(0, 1, 12, -14, 146, 2);
            var fill = UIB.Solid("Fill", barBack.transform, new Color(0.32f, 0.86f, 1f)).Filled(Image.FillMethod.Horizontal);
            fill.rectTransform.Stretch();
            fill.fillAmount = 0f;
            rv.barBack = barBack;
            rv.barFill = fill;
            view.rowTemplate = rv;
            return view;
        }

        // 지도(우상단 104×70) 바로 아래. RectMask2D로 위쪽을 잘라 "지도 밑에서 내려오는" 모양
        static SystemAlarmView BuildSystemAlarm(Transform hudRt)
        {
            var root = UIB.Rect("SystemAlarm", hudRt).Place(1, 1, -8, -82, 104, 140);
            root.gameObject.AddComponent<RectMask2D>();
            var view = root.gameObject.AddComponent<SystemAlarmView>();

            var card = UIB.Solid("CardTemplate", root, new Color(0.024f, 0.078f, 0.19f, 0.94f));
            card.rectTransform.Place(0, 1, 0, 0, 104, 40);
            UIB.Group(card.gameObject).blocksRaycasts = false;
            var edge = new Color(0.35f, 0.67f, 1f);
            UIB.Solid("EdgeL", card.transform, edge).rectTransform.Place(0, 1, 0, 0, 1, 40);
            UIB.Solid("EdgeR", card.transform, edge).rectTransform.Place(1, 1, 0, 0, 1, 40);
            UIB.Solid("EdgeB", card.transform, edge).rectTransform.Place(0, 0, 0, 0, 104, 1);
            var header = UIB.Solid("Header", card.transform, new Color(0.12f, 0.31f, 0.63f));
            header.rectTransform.Place(0, 1, 0, 0, 104, 11);
            var sys = UIB.Small10("System", header.transform, "SYSTEM", TextAlignmentOptions.Left, new Color(0.8f, 0.9f, 1f));
            sys.rectTransform.Place(0, 1, 4, 0, 50, 11);
            var kind = UIB.Small10("Kind", header.transform, "레벨 업", TextAlignmentOptions.Right, UIB.Accent);
            kind.rectTransform.Place(1, 1, -4, 0, 60, 11);
            var main = UIB.Body12("Main", card.transform, "Lv.3 → Lv.6", TextAlignmentOptions.Center, Color.white);
            main.rectTransform.Place(0.5f, 1, 0, -13, 100, 13);
            main.textWrappingMode = TextWrappingModes.NoWrap;
            var sub = UIB.Small10("Sub", card.transform, "스킬 포인트 +4", TextAlignmentOptions.Center, new Color(0.6f, 0.86f, 1f));
            sub.rectTransform.Place(0.5f, 1, 0, -27, 100, 11);
            sub.textWrappingMode = TextWrappingModes.NoWrap;
            view.cardTemplate = card.rectTransform;
            return view;
        }

        static SkillSlotView SkillSlot(Transform parent, SkillSlot slot, float x)
        {
            var frame = UIB.Panel("Skill_" + slot, parent, "Panels/Blue/Panel");
            frame.raycastTarget = false;
            frame.rectTransform.Place(0, 0, x, 20, 36, 36);
            var view = frame.gameObject.AddComponent<SkillSlotView>();
            view.slot = slot;
            var icon = UIB.Img("Icon", frame.transform, null);
            icon.rectTransform.Place(0.5f, 0.5f, 0, 0, 32, 32);
            view.icon = icon;
            var lockImg = UIB.Solid("Locked", frame.transform, new Color(0, 0, 0, 0.55f));
            lockImg.rectTransform.Place(0.5f, 0.5f, 0, 0, 32, 32);
            view.lockOverlay = lockImg.gameObject;
            view.cooldown = Radial(frame.transform, icon.rectTransform, frame, 32);
            view.cooldown.label.fontSize = 12; view.cooldown.label.font = UIB.Body;
            view.cooldown.completeSound = UISound.SkillReady;
            var key = UIB.Body12("Key", frame.transform, slot.ToString(), TextAlignmentOptions.Center, UIB.Accent);
            key.rectTransform.Place(0.5f, 1, 0, 13, 36, 12);
            view.keyLabel = key;
            var rank = UIB.Small10("Rank", frame.transform, "", TextAlignmentOptions.Center, UIB.Accent);
            rank.rectTransform.Place(0.5f, 0, 0, -10, 36, 10);
            view.rankText = rank;
            view.shakeTarget = frame.rectTransform;
            view.tintTarget = frame;
            return view;
        }

        static ItemSlotView ItemSlot(Transform parent, int index, float x)
        {
            var frame = UIB.Panel("Item_" + (index + 1), parent, "Panels/Blue/Panel");
            frame.raycastTarget = false;
            frame.rectTransform.Place(0, 0, x, 20, 36, 36);
            var view = frame.gameObject.AddComponent<ItemSlotView>();
            view.slotIndex = index;
            var fx = UIB.Img("UseFx", frame.transform, UIB.Art("Map_Marker"));
            fx.rectTransform.Place(0.5f, 0.5f, 0, 0, 34, 34);
            fx.gameObject.SetActive(false);
            view.useFx = fx;
            var icon = UIB.Img("Icon", frame.transform, null);
            icon.rectTransform.Place(0.5f, 0.5f, 0, 0, 32, 32);
            view.icon = icon;
            var countSh = UIB.Small10("CountShadow", frame.transform, "", TextAlignmentOptions.BottomRight, new Color(0, 0, 0, 0.9f));
            countSh.rectTransform.Place(1, 0, -2, 1, 30, 10);
            var count = UIB.Small10("Count", frame.transform, "0", TextAlignmentOptions.BottomRight, Color.white);
            count.rectTransform.Place(1, 0, -3, 2, 30, 10);
            view.countText = count;
            view.countShadow = countSh;
            var key = UIB.Body12("Key", frame.transform, (index + 1).ToString(), TextAlignmentOptions.Center, UIB.Accent);
            key.rectTransform.Place(0.5f, 1, 0, 13, 36, 12);
            view.keyLabel = key;
            view.punchTarget = frame.rectTransform;
            return view;
        }

        static MapRenderer MapView(Transform frame, float cell, bool follow, float inset)
        {
            var viewport = UIB.Rect("Viewport", frame).Stretch(inset, inset, inset, inset);
            viewport.gameObject.AddComponent<RectMask2D>();
            var content = UIB.Rect("Content", viewport).Place(0.5f, 0.5f, 0, 0, 0, 0);
            var r = frame.gameObject.AddComponent<MapRenderer>();
            r.content = content;
            r.cellSize = cell;
            r.gap = 1f;
            r.followCurrent = follow;

            var room = UIB.Img("RoomTemplate", content, UIB.White);
            room.rectTransform.Place(0.5f, 0.5f, 0, 0, cell, cell);
            r.roomTemplate = room;
            var link = UIB.Img("LinkTemplate", content, UIB.White);
            link.rectTransform.Place(0.5f, 0.5f, 0, 0, cell, 1);
            r.linkTemplate = link;
            var icon = UIB.Img("IconTemplate", content, UIB.Art("Map_Item"));
            icon.rectTransform.Place(0.5f, 0.5f, 0, 0, cell * 0.6f, cell * 0.6f);
            r.iconTemplate = icon;
            var marker = UIB.Img("PlayerMarker", content, UIB.Art("Map_Marker"), new Color(1f, 0.3f, 0.3f));
            marker.rectTransform.Place(0.5f, 0.5f, 0, 0, Mathf.Max(4f, cell * 0.5f), Mathf.Max(4f, cell * 0.5f));
            r.playerMarker = marker.rectTransform;
            r.iconSprites = new[] { null, UIB.Art("Map_Start"), UIB.Art("Map_Gate"), UIB.Art("Map_Boss"), UIB.Art("Map_Save"), UIB.Art("Map_Item") };
            return r;
        }

        // ───────────────────────────── Boss / Toast ─────────────────────────────
        static void BuildBoss(Transform layer)
        {
            var rt = UIB.Rect("BossHUD", layer).Stretch();
            var boss = rt.gameObject.AddComponent<BossHudView>();

            var top = UIB.Solid("LetterboxTop", rt, Color.black);
            top.rectTransform.StretchH(1, 0, 0);
            var bottom = UIB.Solid("LetterboxBottom", rt, Color.black);
            bottom.rectTransform.StretchH(0, 0, 0);
            boss.letterboxTop = top.rectTransform;
            boss.letterboxBottom = bottom.rectTransform;

            var bannerSprite = UIB.Px("Banners/Blue/TitleBanner");
            var banner = UIB.Img("Banner", rt, bannerSprite ?? UIB.Fallback, bannerSprite != null ? Color.white : new Color(0.1f, 0.1f, 0.2f, 0.9f));
            banner.rectTransform.Place(0.5f, 0.5f, 0, 30, 288, 32);
            boss.bannerGroup = UIB.Group(banner.gameObject);
            boss.bannerGroup.alpha = 0f;
            boss.bannerRoot = banner.rectTransform;
            var bn = UIB.Title15("Name", banner.transform, "BOSS", TextAlignmentOptions.Center, Color.white);
            bn.rectTransform.Stretch(0, 0, 4, 4);
            boss.bannerName = bn;
            var bt = UIB.Small10("Title", banner.transform, "", TextAlignmentOptions.Center, UIB.Dim);
            bt.rectTransform.Place(0.5f, 0, 0, -14, 280, 12);
            boss.bannerTitle = bt;

            var barRoot = UIB.Rect("BossBar", rt).Place(0.5f, 1, 0, -10, 260, 22); // v0.6: 상단 가운데가 비어 맨 위로
            boss.barRoot = barRoot;
            boss.barGroup = UIB.Group(barRoot.gameObject);
            boss.barGroup.alpha = 0f;
            var name = UIB.Body12("Name", barRoot, "BOSS", TextAlignmentOptions.Center, new Color(1f, 0.6f, 0.6f));
            name.rectTransform.Place(0.5f, 1, 0, 0, 260, 13);
            boss.barNameText = name;
            var bar = Bar("Bar", barRoot, "BossBarA", 260, 8, 2);
            bar.GetComponent<RectTransform>().Place(0.5f, 0, 0, 0, 260, 8);
            bar.trailDelay = 0.5f;
            bar.trailDuration = 0.5f;
            boss.bar = bar;
            boss.barFlashTarget = bar.fill;
            var tick = UIB.Solid("PhaseTick", bar.transform, new Color(1f, 1f, 1f, 0.9f));
            tick.rectTransform.anchorMin = tick.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            tick.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            tick.rectTransform.sizeDelta = new Vector2(1, 8);
            boss.phaseTickTemplate = tick.rectTransform;
        }

        static void BuildToast(Transform layer)
        {
            var rt = UIB.Rect("Toasts", layer).Place(1, 0, -8, 76, 200, 120); // v0.6: 우하단 스킬 칸 위
            var vl = rt.gameObject.AddComponent<VerticalLayoutGroup>();
            vl.childAlignment = TextAnchor.LowerRight;
            vl.spacing = 2;
            vl.childControlWidth = vl.childControlHeight = true;
            vl.childForceExpandWidth = false;
            vl.childForceExpandHeight = false;
            var toast = rt.gameObject.AddComponent<ToastView>();
            toast.systemAlarm = _systemAlarm;

            var item = UIB.Panel("ToastTemplate", rt, "Panels/Blue/Panel");
            item.raycastTarget = false;
            var hl = item.gameObject.AddComponent<HorizontalLayoutGroup>();
            hl.padding = new RectOffset(6, 6, 3, 3);
            hl.childControlWidth = hl.childControlHeight = true;
            hl.childForceExpandWidth = hl.childForceExpandHeight = false;
            var t = UIB.Body12("Text", item.transform, "알림", TextAlignmentOptions.Right);
            t.textWrappingMode = TextWrappingModes.NoWrap;
            toast.template = item.rectTransform;
        }

        // ───────────────────────────── Screens ─────────────────────────────
        static readonly Color DimColor = new Color(0.02f, 0.03f, 0.08f, 0.65f);
        static readonly Color FullBg = new Color(0.04f, 0.05f, 0.1f, 0.96f);

        static void BuildTitle(Transform layer)
        {
            var w = Window<TitleScreen>("Title", layer, ScreenId.Title, UILayer.Fullscreen, false, true, false, WindowTransition.Fade, FullBg);
            var t = UIB.Text("Logo", w.transform, "PROJECT M", UIB.Title, 30, TextAlignmentOptions.Center, UIB.Accent);
            t.rectTransform.Place(0.5f, 0.5f, 0, 80, 400, 40);
            var sub = UIB.Body12("Sub", w.transform, "게이트 헌터 · UI 샌드박스", TextAlignmentOptions.Center, UIB.Dim);
            sub.rectTransform.Place(0.5f, 0.5f, 0, 50, 400, 14);
            w.newGameButton = MenuButton(w.transform, "새 게임", 0);
            w.continueButton = MenuButton(w.transform, "이어하기", -26);
            w.settingsButton = MenuButton(w.transform, "설정", -52);
            w.quitButton = MenuButton(w.transform, "종료", -78);
            w.firstSelected = w.newGameButton;
        }

        static Button MenuButton(Transform parent, string label, float y, float width = 110)
        {
            var b = UIB.Button(label, parent, label);
            ((RectTransform)b.transform).Place(0.5f, 0.5f, 0, y, width, 22);
            return b;
        }

        static void BuildClassSelect(Transform layer)
        {
            var w = Window<ClassSelectWindow>("ClassSelect", layer, ScreenId.ClassSelect, UILayer.Fullscreen, false, true, false, WindowTransition.Fade, FullBg);
            var title = UIB.Title15("Title", w.transform, "계열 선택", TextAlignmentOptions.Center, UIB.Accent);
            title.rectTransform.Place(0.5f, 1, 0, -40, 300, 18);
            var hint = UIB.Small10("Hint", w.transform, "선택한 계열의 Q/E/R 스킬로 두 스테이지를 진행합니다", TextAlignmentOptions.Center, UIB.Dim);
            hint.rectTransform.Place(0.5f, 1, 0, -60, 400, 12);
            w.cards = new[] { ClassCard(w.transform, -105), ClassCard(w.transform, 105) };
        }

        static ClassCardView ClassCard(Transform parent, float x)
        {
            var bg = UIB.Panel("ClassCard", parent, "Panels/Blue/Panel");
            bg.rectTransform.Place(0.5f, 0.5f, x, -14, 196, 214);
            var view = bg.gameObject.AddComponent<ClassCardView>();
            var icon = UIB.Img("Icon", bg.transform, null);
            icon.rectTransform.Place(0.5f, 1, 0, -10, 32, 32);
            view.icon = icon;
            var name = UIB.Title15("Name", bg.transform, "계열", TextAlignmentOptions.Center, UIB.Accent);
            name.rectTransform.Place(0.5f, 1, 0, -46, 170, 18);
            view.nameText = name;
            var desc = UIB.Small10("Desc", bg.transform, "", TextAlignmentOptions.Center);
            desc.rectTransform.Place(0.5f, 1, 0, -68, 176, 48);
            view.descriptionText = desc;
            var skills = UIB.Small10("Skills", bg.transform, "", TextAlignmentOptions.Left, UIB.Dim);
            skills.rectTransform.Place(0.5f, 1, 0, -122, 170, 40);
            view.skillsText = skills;
            var btn = UIB.Button("Choose", bg.transform, "선택");
            ((RectTransform)btn.transform).Place(0.5f, 0, 0, 10, 90, 22);
            view.button = btn;
            return view;
        }

        static void BuildSkillWindow(Transform layer)
        {
            var w = Window<SkillWindow>("SkillWindow", layer, ScreenId.SkillWindow, UILayer.Screen, true, dim: DimColor);
            var c = Content(w, 420, 240);
            var title = UIB.Title15("Title", c, "스킬", TextAlignmentOptions.Left, UIB.Accent);
            title.rectTransform.Place(0, 1, 12, -8, 100, 18);
            var cls = UIB.Small10("Class", c, "", TextAlignmentOptions.Left, UIB.Dim);
            cls.rectTransform.Place(0, 1, 60, -12, 160, 12);
            w.classText = cls;
            var pts = UIB.Panel("Points", c, "Panels/Blue/Panel");
            pts.raycastTarget = false;
            pts.rectTransform.Place(1, 1, -10, -6, 110, 18);
            var ptsText = UIB.Body12("Text", pts.transform, "스킬 포인트 0", TextAlignmentOptions.Center, UIB.Accent);
            ptsText.rectTransform.Stretch();
            w.pointsText = ptsText;
            w.pointsBadge = pts.rectTransform;
            for (int i = 0; i < 3; i++) w.cards[i] = SkillCard(c, (SkillSlot)i, -134 + i * 134);
            var close = UIB.Small10("CloseHint", c, "[K] / [ESC] 닫기", TextAlignmentOptions.Right, UIB.Dim);
            close.rectTransform.Place(1, 0, -10, 6, 160, 12);
        }

        // ───────────────────────────── 스킬 트리 (K) ─────────────────────────────
        static void BuildSkillTreeWindow(Transform layer)
        {
            var w = Window<SkillTreeWindow>("SkillTreeWindow", layer, ScreenId.SkillWindow, UILayer.Screen, true, dim: DimColor);
            var c = Content(w, 600, 300);

            var title = UIB.Title15("Title", c, "스킬 트리", TextAlignmentOptions.Left, UIB.Accent);
            title.rectTransform.Place(0, 1, 12, -8, 120, 18);
            w.titleText = title;
            var lv = UIB.Small10("Level", c, "Lv.1", TextAlignmentOptions.Left, UIB.Dim);
            lv.rectTransform.Place(0, 1, 100, -12, 120, 12);
            w.levelText = lv;
            var pts = UIB.Panel("Points", c, "Panels/Blue/Panel");
            pts.raycastTarget = false;
            pts.rectTransform.Place(1, 1, -10, -6, 110, 18);
            var ptsText = UIB.Body12("Text", pts.transform, "스킬 포인트  0", TextAlignmentOptions.Center, UIB.Accent);
            ptsText.rectTransform.Stretch();
            w.pointsText = ptsText;
            w.pointsBadge = pts.rectTransform;

            // 트리 영역 (왼쪽)
            var areaBg = UIB.Solid("TreeArea", c, new Color(0.02f, 0.03f, 0.08f, 0.55f));
            areaBg.rectTransform.Place(0, 1, 10, -30, 404, 248);
            var area = areaBg.rectTransform;
            w.treeArea = area;

            var empty = UIB.Small10("Empty", area, "스킬 트리 소스가 연결되지 않았습니다\n(ISkillTreeSource)", TextAlignmentOptions.Center, UIB.Dim);
            empty.rectTransform.Place(0.5f, 0.5f, 0, 0, 300, 30);
            empty.gameObject.SetActive(false);
            w.emptyText = empty;

            var link = UIB.Solid("LinkTemplate", area, Color.white);
            link.rectTransform.Place(0.5f, 0.5f, 0, 0, 10, 2);
            w.linkTemplate = link;

            var label = UIB.Body12("LabelTemplate", area, "Q", TextAlignmentOptions.Center, UIB.Accent);
            label.rectTransform.Place(0.5f, 0.5f, 0, 0, 60, 13);
            label.textWrappingMode = TextWrappingModes.NoWrap;
            w.labelTemplate = label;

            // 노드 템플릿: 배경(45° 사각) → 아이콘 → 테두리 → 선택 표시 → 단계 숫자
            var nodeRt = UIB.Rect("NodeTemplate", area).Place(0.5f, 0.5f, 0, 0, 28, 28);
            var hit = nodeRt.gameObject.AddComponent<Image>();
            hit.sprite = UIB.White; hit.color = new Color(0, 0, 0, 0); hit.raycastTarget = true;
            var node = nodeRt.gameObject.AddComponent<SkillNodeView>();
            var btn = nodeRt.gameObject.AddComponent<Button>();
            btn.targetGraphic = hit;
            btn.transition = Selectable.Transition.None;
            node.button = btn;
            var bg = UIB.Solid("Bg", nodeRt, new Color(0.05f, 0.07f, 0.14f, 0.95f));
            bg.rectTransform.Place(0.5f, 0.5f, 0, 0, 18, 18);
            bg.rectTransform.localRotation = Quaternion.Euler(0, 0, 45);
            node.background = bg;
            var icon = UIB.Img("Icon", nodeRt, null);
            icon.rectTransform.Place(0.5f, 0.5f, 0, 0, 16, 16);
            node.icon = icon;
            var frame = UIB.Img("Frame", nodeRt, UIB.Px("SkillTree/Grey/SkillSlotLarge"));
            frame.rectTransform.Stretch();
            node.frame = frame;
            var sel = UIB.Img("Selector", nodeRt, UIB.Px("SkillTree/White/SelectorLarge"));
            sel.rectTransform.Stretch(-3, -3, -3, -3);
            node.selector = sel;
            var rank = UIB.Body12("Rank", nodeRt, "", TextAlignmentOptions.Center);
            rank.rectTransform.Stretch();
            node.rankText = rank;
            w.nodeTemplate = node;

            string[] colors = { "Grey", "Blue", "Yellow", "Red" }; // SkillNodeState 순서
            w.skillFrames = new Sprite[4]; w.upgradeFrames = new Sprite[4]; w.passiveFrames = new Sprite[4];
            for (int i = 0; i < 4; i++)
            {
                w.skillFrames[i] = UIB.Px($"SkillTree/{colors[i]}/SkillSlotLarge");
                w.upgradeFrames[i] = UIB.Px($"SkillTree/{colors[i]}/SkillSlotSharp");
                w.passiveFrames[i] = UIB.Px($"SkillTree/{colors[i]}/SkillSlotRound");
            }
            w.skillSelector = UIB.Px("SkillTree/White/SelectorLarge");
            w.upgradeSelector = UIB.Px("SkillTree/White/SelectorSharp");
            w.passiveSelector = UIB.Px("SkillTree/White/Selector");

            // 설명 칸 (오른쪽)
            var detail = UIB.Panel("Detail", c, "Panels/Blue/Panel");
            detail.raycastTarget = false;
            detail.rectTransform.Place(1, 1, -10, -30, 166, 248);
            var dIconFrame = UIB.Img("IconFrame", detail.transform, UIB.Px("SkillTree/Blue/SkillSlotLarge") ?? UIB.Fallback);
            dIconFrame.rectTransform.Place(0, 1, 8, -8, 28, 28);
            var dIcon = UIB.Img("Icon", dIconFrame.transform, null);
            dIcon.rectTransform.Place(0.5f, 0.5f, 0, 0, 16, 16);
            w.detailIcon = dIcon;
            var dName = UIB.Body12("Name", detail.transform, "", TextAlignmentOptions.Left, UIB.Accent);
            dName.rectTransform.Place(0, 1, 42, -9, 118, 13);
            w.detailName = dName;
            var dKind = UIB.Small10("Kind", detail.transform, "", TextAlignmentOptions.Left, UIB.Dim);
            dKind.rectTransform.Place(0, 1, 42, -23, 118, 11);
            w.detailKind = dKind;
            var dDesc = UIB.Small10("Desc", detail.transform, "", TextAlignmentOptions.TopLeft);
            dDesc.rectTransform.Place(0, 1, 8, -44, 150, 120);
            w.detailDesc = dDesc;
            var dStatus = UIB.Small10("Status", detail.transform, "", TextAlignmentOptions.TopLeft, UIB.Accent);
            dStatus.rectTransform.Place(0, 0, 8, 34, 150, 24);
            w.detailStatus = dStatus;
            var reset = UIB.Button("Reset", detail.transform, "초기화", out var resetLabel);
            ((RectTransform)reset.transform).Place(0.5f, 0, 0, 8, 120, 20);
            w.resetButton = reset;
            w.resetLabel = resetLabel;

            var hint = UIB.Small10("Hint", c, "클릭: 해금   ·   [K] / [ESC] 닫기", TextAlignmentOptions.Left, UIB.Dim);
            hint.rectTransform.Place(0, 0, 12, 6, 300, 12);
        }

        static SkillCardView SkillCard(Transform parent, SkillSlot slot, float x)
        {
            var bg = UIB.Panel("Card_" + slot, parent, "Panels/Blue/Panel");
            bg.raycastTarget = false;
            bg.rectTransform.Place(0.5f, 0.5f, x, -8, 126, 190);
            var v = bg.gameObject.AddComponent<SkillCardView>();
            v.slot = slot;
            var key = UIB.Title15("Key", bg.transform, slot.ToString(), TextAlignmentOptions.Left, UIB.Accent);
            key.rectTransform.Place(0, 1, 8, -6, 20, 18);
            v.keyText = key;
            var frame = UIB.Img("IconFrame", bg.transform, UIB.Px("SkillTree/Blue/SkillSlotSharp") ?? UIB.Fallback);
            frame.rectTransform.Place(0.5f, 1, 0, -8, 21, 21);
            var icon = UIB.Img("Icon", frame.transform, null);
            icon.rectTransform.Place(0.5f, 0.5f, 0, 0, 16, 16);
            v.icon = icon;
            var name = UIB.Body12("Name", bg.transform, "스킬", TextAlignmentOptions.Center);
            name.rectTransform.Place(0.5f, 1, 0, -33, 118, 13);
            v.nameText = name;
            var rank = UIB.Small10("Rank", bg.transform, "미습득", TextAlignmentOptions.Center, UIB.Dim);
            rank.rectTransform.Place(0.5f, 1, 0, -47, 118, 11);
            v.rankText = rank;
            v.rankPips = new Image[3];
            for (int i = 0; i < 3; i++)
            {
                var pip = UIB.Solid("Pip" + i, bg.transform, Color.white);
                pip.rectTransform.Place(0.5f, 1, -12 + i * 12, -60, 8, 4);
                v.rankPips[i] = pip;
            }
            var cur = UIB.Small10("Current", bg.transform, "", TextAlignmentOptions.TopLeft);
            cur.rectTransform.Place(0.5f, 1, 0, -68, 114, 46);
            v.currentText = cur;
            var next = UIB.Small10("Next", bg.transform, "", TextAlignmentOptions.TopLeft, new Color(0.6f, 1f, 0.7f));
            next.rectTransform.Place(0.5f, 1, 0, -116, 114, 30);
            v.nextText = next;
            var cost = UIB.Small10("Cost", bg.transform, "", TextAlignmentOptions.Center, UIB.Accent);
            cost.rectTransform.Place(0.5f, 0, 0, 31, 118, 11);
            v.costText = cost;
            var btn = UIB.Button("Invest", bg.transform, "습득", out var label);
            ((RectTransform)btn.transform).Place(0.5f, 0, 0, 8, 100, 20);
            v.investButton = btn;
            v.investLabel = label;
            return v;
        }

        static void BuildInventory(Transform layer)
        {
            var w = Window<InventoryWindow>("Inventory", layer, ScreenId.Inventory, UILayer.Screen, true, dim: DimColor);
            var c = Content(w, 500, 252);
            var title = UIB.Title15("Title", c, "인벤토리", TextAlignmentOptions.Left, UIB.Accent);
            title.rectTransform.Place(0, 1, 12, -8, 120, 18);

            // 칸 36px (아이템 아이콘 16px을 정확히 2배 = 32px로 표시)
            const float Cell = 36f, Gap = 3f;
            float gridW = 6 * Cell + 5 * Gap, gridH = 4 * Cell + 3 * Gap;

            var cap = UIB.Small10("Capacity", c, "0 / 24", TextAlignmentOptions.Right, UIB.Dim);
            cap.rectTransform.Place(0, 1, 12 + gridW - 80, -13, 80, 11);
            w.capacityText = cap;
            var line = UIB.Solid("TitleLine", c, new Color(0.25f, 0.75f, 1f, 0.45f));
            line.rectTransform.Place(0, 1, 12, -27, gridW, 1);

            // 그리드 뒤 어두운 판 (칸이 떠 보이지 않게)
            var well = UIB.Solid("GridWell", c, new Color(0.01f, 0.02f, 0.06f, 0.75f));
            well.rectTransform.Place(0, 1, 8, -31, gridW + 8, gridH + 8);
            var gridRt = UIB.Rect("Grid", c).Place(0, 1, 12, -35, gridW, gridH);
            var grid = gridRt.gameObject.AddComponent<GridLayoutGroup>();
            grid.cellSize = new Vector2(Cell, Cell);
            grid.spacing = new Vector2(Gap, Gap);
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = 6;
            w.grid = gridRt;

            var slotBg = UIB.Img("SlotTemplate", gridRt, UIB.Px("Panels/Blue/GridPanelIndent") ?? UIB.Px("Panels/Blue/GridPanel") ?? UIB.Fallback, null, true);
            slotBg.type = Image.Type.Sliced;
            var slot = slotBg.gameObject.AddComponent<InventorySlotView>();
            slot.background = slotBg;
            var sel = slotBg.gameObject.AddComponent<Button>();
            sel.targetGraphic = slotBg;
            sel.transition = Selectable.Transition.None; // 강조는 Highlight 테두리로만
            var icon = UIB.Img("Icon", slotBg.transform, null);
            icon.rectTransform.Place(0.5f, 0.5f, 0, 0, 32, 32);
            icon.preserveAspect = true;
            slot.icon = icon;
            var quick = UIB.Small10("Quick", slotBg.transform, "", TextAlignmentOptions.TopLeft, UIB.Accent);
            quick.rectTransform.Place(0, 1, 3, -1, 12, 10);
            slot.quickText = quick;
            var cntSh = UIB.Small10("CountShadow", slotBg.transform, "", TextAlignmentOptions.BottomRight, new Color(0, 0, 0, 0.9f));
            cntSh.rectTransform.Place(1, 0, -2, 0, 30, 11);
            slot.countShadow = cntSh;
            var cnt = UIB.Small10("Count", slotBg.transform, "", TextAlignmentOptions.BottomRight, Color.white);
            cnt.rectTransform.Place(1, 0, -3, 1, 30, 11);
            slot.countText = cnt;
            var hi = UIB.Img("Highlight", slotBg.transform, UIB.Px("Grid/Blue/SelectorThin_Focus") ?? UIB.Px("Grid/Blue/SelectorA") ?? UIB.White);
            hi.rectTransform.Place(0.5f, 0.5f, 0, 0, Cell + 4, Cell + 4);
            hi.enabled = false;
            slot.highlight = hi;
            var held = UIB.Solid("Held", slotBg.transform, new Color(1f, 0.85f, 0.3f, 0.25f));
            held.rectTransform.Stretch(2, 2, 2, 2);
            held.enabled = false;
            slot.heldMark = held;
            w.slotTemplate = slot;
            w.hoverColor = new Color(0.55f, 0.85f, 1f, 1f);
            w.dropOkColor = new Color(0.5f, 1f, 0.6f, 1f);

            var empty = UIB.Small10("Empty", c, "인벤토리 소스가 연결되지 않았습니다\n(IInventorySource)", TextAlignmentOptions.Center, UIB.Dim);
            empty.rectTransform.Place(0, 1, 12, -90, gridW, 30);
            w.emptyText = empty;

            // 툴팁 (오른쪽 고정 패널): 색 띠 · 아이콘 칸 · 이름/종류 · 구분선 · 효과 · 설명 · 보유/조작
            var tipBg = UIB.Panel("Tooltip", c, "Panels/Blue/Panel");
            tipBg.raycastTarget = false;
            tipBg.rectTransform.Place(1, 1, -10, -31, 226, gridH + 8);
            var tip = tipBg.gameObject.AddComponent<ItemTooltipView>();
            tip.group = UIB.Group(tipBg.gameObject);
            var accent = UIB.Solid("Accent", tipBg.transform, Color.white);
            accent.rectTransform.Place(0, 1, 4, -4, 218, 2);
            tip.accent = accent;
            var iconWell = UIB.Img("IconWell", tipBg.transform, UIB.Px("Panels/Blue/GridPanelIndent") ?? UIB.Fallback);
            iconWell.type = Image.Type.Sliced;
            iconWell.rectTransform.Place(0, 1, 8, -10, 38, 38);
            var tIcon = UIB.Img("Icon", iconWell.transform, null);
            tIcon.rectTransform.Place(0.5f, 0.5f, 0, 0, 32, 32);
            tip.icon = tIcon;
            var tName = UIB.Body12("Name", tipBg.transform, "", TextAlignmentOptions.Left);
            tName.rectTransform.Place(0, 1, 52, -14, 166, 14);
            tip.nameText = tName;
            var tType = UIB.Small10("Type", tipBg.transform, "", TextAlignmentOptions.Left, UIB.Dim);
            tType.rectTransform.Place(0, 1, 52, -31, 166, 11);
            tip.typeText = tType;
            var tLine = UIB.Solid("Divider", tipBg.transform, new Color(0.25f, 0.75f, 1f, 0.35f));
            tLine.rectTransform.Place(0, 1, 8, -54, 210, 1);
            var tEff = UIB.Small10("Effect", tipBg.transform, "", TextAlignmentOptions.TopLeft, new Color(0.6f, 1f, 0.7f));
            tEff.rectTransform.Place(0, 1, 8, -60, 210, 24);
            tip.effectText = tEff;
            var tDesc = UIB.Small10("Desc", tipBg.transform, "", TextAlignmentOptions.TopLeft, new Color(0.82f, 0.86f, 0.95f));
            tDesc.rectTransform.Place(0, 1, 8, -86, 210, 50);
            tip.descriptionText = tDesc;
            var fLine = UIB.Solid("FooterLine", tipBg.transform, new Color(0.25f, 0.75f, 1f, 0.25f));
            fLine.rectTransform.Place(0, 0, 8, 20, 210, 1);
            var tFoot = UIB.Small10("Footer", tipBg.transform, "", TextAlignmentOptions.Left, UIB.Accent);
            tFoot.rectTransform.Place(0, 0, 8, 6, 210, 11);
            tip.footerText = tFoot;
            w.tooltip = tip;

            var hint = UIB.Small10("Hint", c, "[좌클릭] 집기/놓기   [우클릭] 사용   [I] / [ESC] 닫기", TextAlignmentOptions.Left, UIB.Dim);
            hint.rectTransform.Place(0, 0, 12, 7, 320, 12);

            var cursor = UIB.Img("HeldCursor", w.transform, null);
            cursor.rectTransform.Place(0.5f, 0.5f, 0, 0, 32, 32);
            w.cursorIcon = cursor;
        }

        static MapRenderer BuildMapWindow(Transform layer, MapController ctrl)
        {
            var w = Window<MapWindow>("MapWindow", layer, ScreenId.Map, UILayer.Screen, true, dim: DimColor);
            var c = Content(w, 440, 260);
            var title = UIB.Title15("Title", c, "지도", TextAlignmentOptions.Left, UIB.Accent);
            title.rectTransform.Place(0, 1, 12, -8, 300, 18);
            w.titleText = title;
            var room = UIB.Small10("Room", c, "", TextAlignmentOptions.Left, UIB.Dim);
            room.rectTransform.Place(0, 0, 12, 6, 300, 12);
            w.roomText = room;
            var frame = UIB.Rect("MapArea", c).Stretch(12, 12, 30, 22);
            var r = MapView(frame, 22f, false, 0);
            r.gap = 3f;
            w.controller = ctrl;
            w.fullMap = r;
            var legend = UIB.Small10("Legend", c, "■ 방문  ■ 발견  ● 현재 위치   [Tab]/[ESC] 닫기", TextAlignmentOptions.Right, UIB.Dim);
            legend.rectTransform.Place(1, 0, -12, 6, 260, 12);
            return r;
        }

        static void BuildPause(Transform layer)
        {
            var w = Window<PauseWindow>("Pause", layer, ScreenId.Pause, UILayer.Screen, true, dim: DimColor);
            var c = Content(w, 160, 196);
            var title = UIB.Title15("Title", c, "일시정지", TextAlignmentOptions.Center, UIB.Accent);
            title.rectTransform.Place(0.5f, 1, 0, -8, 140, 18);
            w.resumeButton = MenuButton(c, "계속하기", 56);
            w.skillButton = MenuButton(c, "스킬", 30);
            w.inventoryButton = MenuButton(c, "인벤토리", 4);
            w.mapButton = MenuButton(c, "지도", -22);
            w.optionsButton = MenuButton(c, "옵션", -48);
            w.titleButton = MenuButton(c, "타이틀로", -74);
            w.firstSelected = w.resumeButton;
        }

        static void BuildDialogue(Transform layer)
        {
            var w = Window<DialogueView>("Dialogue", layer, ScreenId.Dialogue, UILayer.Popup, false, true, false, WindowTransition.Fade);
            var left = UIB.Img("PortraitLeft", w.transform, null);
            left.rectTransform.Place(0, 0, 40, 92, 66, 105);
            left.enabled = false;
            w.leftPortrait = left;
            var right = UIB.Img("PortraitRight", w.transform, null);
            right.rectTransform.Place(1, 0, -40, 92, 66, 105);
            right.rectTransform.localScale = Vector3.one;
            right.enabled = false;
            w.rightPortrait = right;

            var box = UIB.Panel("Box", w.transform, "Panels/Blue/Panel");
            box.rectTransform.Place(0.5f, 0, 0, 8, 600, 84);
            w.box = box.rectTransform;
            var name = UIB.Body12("Name", box.transform, "", TextAlignmentOptions.Left, UIB.Accent);
            name.rectTransform.StretchH(1, -8, 14, 14, 14);
            w.nameText = name;
            var body = UIB.Body12("Body", box.transform, "", TextAlignmentOptions.TopLeft);
            body.rectTransform.Stretch(14, 14, 26, 10);
            w.bodyText = body;
            var next = UIB.Body12("Next", box.transform, "▼", TextAlignmentOptions.Center, UIB.Accent);
            next.rectTransform.Place(1, 0, -10, 6, 12, 12);
            w.nextIndicator = next;
        }

        static void BuildDeath(Transform layer)
        {
            var w = Window<DeathScreen>("Death", layer, ScreenId.Death, UILayer.Fullscreen, false, true, false, WindowTransition.Fade, new Color(0.18f, 0.02f, 0.04f, 0.85f));
            var titleRoot = UIB.Rect("TitleRoot", w.transform).Place(0.5f, 0.5f, 0, 50, 300, 40);
            var t = UIB.Text("Title", titleRoot, "쓰러졌다", UIB.Title, 30, TextAlignmentOptions.Center, new Color(1f, 0.45f, 0.45f));
            t.rectTransform.Stretch();
            w.titleRoot = titleRoot;
            var msg = UIB.Small10("Message", w.transform, "", TextAlignmentOptions.Center, UIB.Dim);
            msg.rectTransform.Place(0.5f, 0.5f, 0, 14, 400, 26);
            w.messageText = msg;
            w.retryButton = MenuButton(w.transform, "재도전", -24);
            w.titleButton = MenuButton(w.transform, "타이틀로", -50);
            w.firstSelected = w.retryButton;
        }

        // ───────────────────────────── 게임 흐름 (GameUI.Flow) ─────────────────────────────
        //  스테이지 띠:   ────────  STAGE 1  ────────
        //                       지 하 철 역
        //  로딩:  검은 화면 + 아래쪽 진행 막대 + 퍼센트 + 문구 + TIP
        static void BuildFlow(Transform layer)
        {
            var rt = UIB.Rect("Flow", layer).Stretch();
            var flow = rt.gameObject.AddComponent<FlowOverlayView>();
            var accent = new Color(1f, 0.85f, 0.4f);

            // 1) 스테이지 시작 띠 (페이드보다 아래)
            var intro = UIB.Rect("StageIntro", rt).Stretch();
            flow.introGroup = UIB.Group(intro.gameObject);
            flow.introGroup.alpha = 0f;
            flow.introGroup.blocksRaycasts = false;
            var band = UIB.Solid("Band", intro, new Color(0.02f, 0.03f, 0.07f, 0.72f));
            band.rectTransform.StretchH(0.5f, 36, 52);
            flow.introBand = band.rectTransform;
            var edgeTop = UIB.Solid("EdgeTop", band.transform, new Color(accent.r, accent.g, accent.b, 0.35f));
            edgeTop.rectTransform.StretchH(1, 0, 1);
            var edgeBottom = UIB.Solid("EdgeBottom", band.transform, new Color(accent.r, accent.g, accent.b, 0.35f));
            edgeBottom.rectTransform.StretchH(0, 0, 1);

            var stage = UIB.Body12("Stage", band.transform, "STAGE 1", TextAlignmentOptions.Center, accent);
            stage.rectTransform.Place(0.5f, 0.5f, 0, 14, 120, 13);
            flow.introStage = stage;
            var lineL = UIB.Solid("LineLeft", band.transform, accent);
            lineL.rectTransform.Place(0.5f, 0.5f, 0, 14, 110, 1);
            lineL.rectTransform.pivot = new Vector2(1f, 0.5f);
            lineL.rectTransform.anchoredPosition = new Vector2(-34, 14);
            flow.introLineLeft = lineL.rectTransform;
            var lineR = UIB.Solid("LineRight", band.transform, accent);
            lineR.rectTransform.Place(0.5f, 0.5f, 0, 14, 110, 1);
            lineR.rectTransform.pivot = new Vector2(0f, 0.5f);
            lineR.rectTransform.anchoredPosition = new Vector2(34, 14);
            flow.introLineRight = lineR.rectTransform;

            var title = UIB.Text("Title", band.transform, "지하철역", UIB.Title, 28, TextAlignmentOptions.Center, Color.white);
            title.rectTransform.Place(0.5f, 0.5f, 0, -8, 400, 30);
            title.textWrappingMode = TextWrappingModes.NoWrap;
            flow.introTitle = title;
            var sub = UIB.Small10("Subtitle", intro, "", TextAlignmentOptions.Center, UIB.Dim);
            sub.rectTransform.Place(0.5f, 0.5f, 0, 2, 400, 12);
            flow.introSubtitle = sub;

            // 2) 페이드
            var fade = UIB.Solid("Fade", rt, Color.black, true);
            fade.rectTransform.Stretch();
            flow.fadeGroup = UIB.Group(fade.gameObject);
            flow.fadeGroup.alpha = 0f;
            flow.fadeGroup.blocksRaycasts = false;

            // 3) 로딩 (맨 위)
            var loading = UIB.Solid("Loading", rt, new Color(0.02f, 0.025f, 0.05f, 1f), true);
            loading.rectTransform.Stretch();
            flow.loadingGroup = UIB.Group(loading.gameObject);
            flow.loadingGroup.alpha = 0f;
            flow.loadingGroup.blocksRaycasts = false;

            var lt = UIB.Title15("Title", loading.transform, "LOADING", TextAlignmentOptions.Left, Color.white);
            lt.rectTransform.Place(1, 0, -80, 72, 90, 16);
            lt.textWrappingMode = TextWrappingModes.NoWrap;
            flow.loadingTitle = lt;
            var msg = UIB.Small10("Message", loading.transform, "", TextAlignmentOptions.Left, UIB.Dim);
            msg.rectTransform.Place(0.5f, 0, -60, 74, 360, 12);
            flow.loadingMessage = msg;

            var track = UIB.Solid("Track", loading.transform, new Color(1f, 1f, 1f, 0.08f));
            track.rectTransform.Place(0.5f, 0, 0, 60, 480, 4);
            var fill = UIB.Solid("Fill", loading.transform, accent).Filled(Image.FillMethod.Horizontal);
            fill.rectTransform.Place(0.5f, 0, 0, 60, 480, 4);
            fill.rectTransform.pivot = new Vector2(0.5f, 0f);
            fill.fillAmount = 0f;
            flow.loadingFill = fill;
            var runner = UIB.Solid("Runner", loading.transform, Color.white);
            runner.rectTransform.Place(0.5f, 0, -240, 62, 6, 6);
            runner.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            flow.loadingRunner = runner.rectTransform;
            var pct = UIB.Body12("Percent", loading.transform, "0%", TextAlignmentOptions.Right, accent);
            pct.rectTransform.Place(0.5f, 0, 220, 44, 40, 13);
            flow.loadingPercent = pct;

            var tipLine = UIB.Solid("TipLine", loading.transform, new Color(1f, 1f, 1f, 0.06f));
            tipLine.rectTransform.Place(0.5f, 0, 0, 34, 480, 1);
            var tip = UIB.Small10("Tip", loading.transform, "TIP", TextAlignmentOptions.Left, new Color(0.75f, 0.8f, 0.9f));
            tip.rectTransform.Place(0.5f, 0, 0, 18, 480, 12);
            tip.textWrappingMode = TextWrappingModes.NoWrap;
            flow.loadingTip = tip;
        }

        // ───────────────────────────── 옵션 ─────────────────────────────
        // [설정]  [사운드][화면][게임]
        // ┌──────────────────────────────┐
        // │ 마스터 볼륨      ▬▬▬▬▬▬▬▮── 80 │  ← 선택된 줄은 ListItemSelected
        // │ 배경음           ...           │
        // └──────────────────────────────┘
        //  [Q/E] 탭  [ESC] 닫기      [기본값] [닫기]
        static void BuildOptions(Transform layer)
        {
            var w = Window<OptionsWindow>("Options", layer, ScreenId.Options, UILayer.Modal, false, dim: new Color(0.02f, 0.03f, 0.08f, 0.75f));
            var c = Content(w, 360, 196);
            var title = UIB.Title15("Title", c, "설정", TextAlignmentOptions.Left, UIB.Accent);
            title.rectTransform.Place(0, 1, 12, -8, 80, 18);

            string[] tabNames = { "사운드", "화면", "게임" };
            w.tabButtons = new Button[3]; w.tabImages = new Image[3]; w.tabLabels = new TMP_Text[3]; w.pages = new GameObject[3];
            w.tabActive = UIB.Px("Panels/Blue/TabTop");
            w.tabInactive = UIB.Px("Panels/Blue/TabInactiveTop");
            var pageArea = UIB.Rect("PageArea", c).Stretch(10, 10, 46, 34);
            for (int i = 0; i < 3; i++)
            {
                var tab = UIB.Img("Tab_" + tabNames[i], c, w.tabActive ?? UIB.Fallback, null, true);
                tab.rectTransform.Place(0, 1, 12 + i * 70, -30, 66, 16);
                var tb = tab.gameObject.AddComponent<Button>();
                tb.targetGraphic = tab;
                var nav = tb.navigation; nav.mode = Navigation.Mode.None; tb.navigation = nav; // 탭은 Q/E·클릭으로만
                var tl = UIB.Small10("Label", tab.transform, tabNames[i], TextAlignmentOptions.Center);
                tl.rectTransform.Stretch(0, 0, 1, 0);
                w.tabButtons[i] = tb; w.tabImages[i] = tab; w.tabLabels[i] = tl;

                var page = UIB.Rect("Page_" + tabNames[i], pageArea).Stretch(0, 0, 0, 0);
                w.pages[i] = page.gameObject;
            }

            var p0 = w.pages[0].transform; var p1 = w.pages[1].transform; var p2 = w.pages[2].transform;
            w.master = OptSlider(p0, "마스터 볼륨", 0);
            w.bgm = OptSlider(p0, "배경음", 1);
            w.sfx = OptSlider(p0, "효과음", 2);
            w.fullscreen = OptToggle(p1, "전체 화면", 0);
            w.resolution = OptSelector(p1, "해상도", 1);
            w.vsync = OptToggle(p1, "수직 동기화", 2);
            w.screenShake = OptToggle(p2, "화면 흔들림", 0);
            w.damageNumbers = OptToggle(p2, "피해 숫자 표시", 1);
            w.numberSize = OptSelector(p2, "피해 숫자 크기", 2);
            w.compactNumbers = OptToggle(p2, "큰 숫자 줄여 쓰기", 3);

            var hint = UIB.Small10("Hint", c, "[Q/E] 탭   [ESC] 닫기", TextAlignmentOptions.Left, UIB.Dim);
            hint.rectTransform.Place(0, 0, 12, 10, 150, 12);
            w.defaultsButton = UIB.Button("Defaults", c, "기본값");
            ((RectTransform)w.defaultsButton.transform).Place(1, 0, -84, 6, 68, 20);
            w.closeButton = UIB.Button("Close", c, "닫기");
            ((RectTransform)w.closeButton.transform).Place(1, 0, -10, 6, 68, 20);
            w.firstSelected = w.master.Selectable;
        }

        const float RowH = 24f;
        static float RowY(int i) => 4 + i * (RowH + 2);

        /// <summary>옵션 한 줄 바탕 (선택되면 ListItemSelected로 바뀜) + 이름</summary>
        static Image OptRow(Transform page, string label, int index)
        {
            var row = UIB.Img("Row_" + label, page, UIB.Px("Lists/Blue/ListItem") ?? UIB.Fallback, null, true);
            row.rectTransform.anchorMin = new Vector2(0, 1); row.rectTransform.anchorMax = new Vector2(1, 1);
            row.rectTransform.pivot = new Vector2(0.5f, 1);
            row.rectTransform.offsetMin = new Vector2(0, -RowY(index) - RowH);
            row.rectTransform.offsetMax = new Vector2(0, -RowY(index));
            var t = UIB.Body12("Label", row.transform, label, TextAlignmentOptions.Left);
            t.rectTransform.Place(0, 0.5f, 10, 0, 150, 14);
            return row;
        }

        static void RowTransition(Selectable s, Image row)
        {
            s.targetGraphic = row;
            s.transition = Selectable.Transition.SpriteSwap;
            var sel = UIB.Px("Lists/Blue/ListItemSelected");
            var st = s.spriteState;
            st.highlightedSprite = sel; st.selectedSprite = sel; st.pressedSprite = sel;
            s.spriteState = st;
        }

        static OptionSlider OptSlider(Transform page, string label, int index)
        {
            var row = OptRow(page, label, index);
            var opt = row.gameObject.AddComponent<OptionSlider>();
            var ctl = UIB.Rect("Slider", row.transform).Place(0, 0.5f, 170, 0, 120, 9);
            var bg = UIB.Img("Background", ctl, UIB.Px("FormElements/Blue/SliderFullEmpty"), null, true);
            bg.rectTransform.Stretch();
            var fillArea = UIB.Rect("Fill Area", ctl).Stretch();
            var fill = UIB.Img("Fill", fillArea, UIB.Px("FormElements/Blue/SliderFull"));
            fill.rectTransform.anchorMin = Vector2.zero; fill.rectTransform.anchorMax = new Vector2(0, 1);
            fill.rectTransform.offsetMin = fill.rectTransform.offsetMax = Vector2.zero;
            var handleArea = UIB.Rect("Handle Slide Area", ctl).Stretch(2, 2, 0, 0);
            var handle = UIB.Img("Handle", handleArea, UIB.Px("FormElements/Blue/SliderHandleLarge"), null, true);
            handle.rectTransform.anchorMin = Vector2.zero; handle.rectTransform.anchorMax = new Vector2(0, 1);
            handle.rectTransform.sizeDelta = new Vector2(4, 4);
            var slider = ctl.gameObject.AddComponent<Slider>();
            slider.fillRect = fill.rectTransform;
            slider.handleRect = handle.rectTransform;
            slider.direction = Slider.Direction.LeftToRight;
            slider.minValue = 0; slider.maxValue = 1; slider.value = 0.8f;
            RowTransition(slider, row);
            var val = UIB.Small10("Value", row.transform, "80", TextAlignmentOptions.Right);
            val.rectTransform.Place(1, 0.5f, -10, 0, 28, 12);
            opt.slider = slider;
            opt.valueText = val;
            return opt;
        }

        static OptionToggle OptToggle(Transform page, string label, int index)
        {
            var row = OptRow(page, label, index);
            var opt = row.gameObject.AddComponent<OptionToggle>();
            var off = UIB.Img("Switch", row.transform, UIB.Px("FormElements/Blue/SwitchOff"));
            off.rectTransform.Place(0, 0.5f, 170, 0, 15, 12);
            var on = UIB.Img("On", off.transform, UIB.Px("FormElements/Blue/SwitchOn"));
            on.rectTransform.Stretch();
            var toggle = row.gameObject.AddComponent<Toggle>(); // 줄 전체 클릭으로 켜고 끔
            toggle.graphic = on;
            toggle.isOn = true;
            RowTransition(toggle, row);
            var state = UIB.Small10("State", row.transform, "켜짐", TextAlignmentOptions.Left, UIB.Accent);
            state.rectTransform.Place(0, 0.5f, 192, 0, 60, 12);
            opt.toggle = toggle;
            opt.stateText = state;
            return opt;
        }

        static OptionSelector OptSelector(Transform page, string label, int index)
        {
            var row = OptRow(page, label, index);
            var sel = row.gameObject.AddComponent<OptionSelector>();
            RowTransition(sel, row);
            var prev = ArrowButton(row.transform, "Prev", "FormElements/Blue/SelectorPrevious", "FormElements/Blue/SelectorPreviousHighlighted", 164);
            var next = ArrowButton(row.transform, "Next", "FormElements/Blue/SelectorNext", "FormElements/Blue/SelectorNextHighlighted", 290);
            var val = UIB.Body12("Value", row.transform, "1920 × 1080", TextAlignmentOptions.Center);
            val.rectTransform.Place(0, 0.5f, 178, 0, 110, 14);
            sel.prevButton = prev; sel.nextButton = next; sel.valueText = val;
            return sel;
        }

        static Button ArrowButton(Transform parent, string name, string sprite, string hiSprite, float x)
        {
            var hit = UIB.Img(name, parent, UIB.White, new Color(1, 1, 1, 0), true);
            hit.rectTransform.Place(0, 0.5f, x, 0, 12, 16);
            var arrow = UIB.Img("Arrow", hit.transform, UIB.Px(sprite));
            arrow.rectTransform.Place(0.5f, 0.5f, 0, 0, 3, 5);
            var b = hit.gameObject.AddComponent<Button>();
            b.targetGraphic = arrow;
            b.transition = Selectable.Transition.SpriteSwap;
            var st = b.spriteState; var hs = UIB.Px(hiSprite); st.highlightedSprite = hs; st.pressedSprite = hs; b.spriteState = st;
            var nav = b.navigation; nav.mode = Navigation.Mode.None; b.navigation = nav; // 좌우는 줄(Selector)이 처리
            return b;
        }

        static void BuildDemoEnd(Transform layer)
        {
            var w = Window<DemoEndScreen>("DemoEnd", layer, ScreenId.DemoEnd, UILayer.Fullscreen, false, true, false, WindowTransition.Fade, FullBg);
            var t = UIB.Text("Title", w.transform, "DEMO CLEAR", UIB.Title, 30, TextAlignmentOptions.Center, UIB.Accent);
            t.rectTransform.Place(0.5f, 0.5f, 0, 50, 400, 40);
            var m = UIB.Body12("Message", w.transform, "데모를 플레이해 주셔서 감사합니다", TextAlignmentOptions.Center);
            m.rectTransform.Place(0.5f, 0.5f, 0, 16, 400, 14);
            w.titleButton = MenuButton(w.transform, "타이틀로", -24);
            w.firstSelected = w.titleButton;
        }
    }
}
