using OZ.UI.Contracts;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace OZ.UI.EditorTools
{
    /// <summary>
    /// Setup 4단계: UIRoot 프리팹 자동 생성 (UI/Prefabs/UIRoot.prefab).
    /// HUD · 보스바 · 토스트 · 미니맵 + 모든 창(타이틀/계열선택/스킬/인벤토리/지도/일시정지/대화/사망/클리어/데모종료).
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
                var mapCtrl = root.AddComponent<MapController>();

                // UIManager.Awake가 레이어를 만들지만, 프리팹 안에서 미리 배치하려고 직접 생성
                Transform hudLayer = Layer(root.transform, UILayer.HUD);
                Transform screenLayer = Layer(root.transform, UILayer.Screen);
                Transform popupLayer = Layer(root.transform, UILayer.Popup);
                Transform cineLayer = Layer(root.transform, UILayer.Cinematic);
                Transform fullLayer = Layer(root.transform, UILayer.Fullscreen);
                Transform toastLayer = Layer(root.transform, UILayer.Toast);
                Transform overlayLayer = Layer(root.transform, UILayer.Overlay);

                var minimap = BuildHud(hudLayer, overlayLayer);
                BuildBoss(cineLayer);
                BuildToast(toastLayer);

                BuildTitle(fullLayer);
                BuildClassSelect(fullLayer);
                BuildSkillWindow(screenLayer);
                BuildInventory(screenLayer);
                var fullMap = BuildMapWindow(screenLayer, mapCtrl);
                BuildPause(screenLayer);
                BuildDialogue(popupLayer);
                BuildDeath(fullLayer);
                BuildStageClear(fullLayer);
                BuildDemoEnd(fullLayer);

                mapCtrl.renderers = new[] { minimap, fullMap };

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

        static TweenFillBar Bar(string name, Transform parent, string spriteBase, float w, float h, int inset, Color? fillColor = null)
        {
            var bg = UIB.Img(name, parent, UIB.Px($"ValueBars/Blue/{spriteBase}Background"), UIB.Px($"ValueBars/Blue/{spriteBase}Background") != null ? Color.white : new Color(0, 0, 0, 0.6f));
            bg.rectTransform.sizeDelta = new Vector2(w, h);
            var trailSprite = UIB.Px($"ValueBars/Blue/{spriteBase}FollowFill");
            var trail = UIB.Img("Trail", bg.transform, trailSprite, trailSprite != null ? Color.white : new Color(1f, 1f, 1f, 0.8f)).Filled(Image.FillMethod.Horizontal);
            trail.rectTransform.Stretch(inset, inset, inset, inset);
            var fillSprite = UIB.Px($"ValueBars/Blue/{spriteBase}Fill");
            var fill = UIB.Img("Fill", bg.transform, fillSprite, fillColor ?? (fillSprite != null ? Color.white : new Color(0.9f, 0.25f, 0.3f))).Filled(Image.FillMethod.Horizontal);
            fill.rectTransform.Stretch(inset, inset, inset, inset);
            var fgSprite = UIB.Px($"ValueBars/Blue/{spriteBase}Foreground");
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

        // ───────────────────────────── HUD ─────────────────────────────
        static MapRenderer BuildHud(Transform layer, Transform overlayLayer)
        {
            var hudRt = UIB.Rect("HUD", layer).Stretch();
            var hud = hudRt.gameObject.AddComponent<HudView>();
            hud.group = UIB.Group(hudRt.gameObject);
            hud.group.blocksRaycasts = false;

            // ── 좌상단: Flask + HP 바 + 레벨/경험치/랭크 ──
            var tl = UIB.Rect("TopLeft", hudRt).Place(0, 1, 8, -8, 150, 60);
            var health = tl.gameObject.AddComponent<HealthView>();

            var flask = UIB.Rect("Flask", tl).Place(0, 1, 0, 0, 26, 26);
            // Flask 프리팹 구조 그대로: 13×13 통 + 9×9 세로 Fill (×2 배율)
            var flaskBg = UIB.Img("Background", flask, UIB.Px("ValueBars/Blue/FlaskBackground"));
            flaskBg.type = Image.Type.Simple;
            flaskBg.rectTransform.Stretch();
            var flaskFill = UIB.Img("Fill", flask, UIB.Px("ValueBars/Blue/FlaskFill9")).Filled(Image.FillMethod.Vertical);
            flaskFill.rectTransform.Place(0.5f, 0.5f, 0, 0, 18, 18);
            var flaskFg = UIB.Img("Foreground", flask, UIB.Px("ValueBars/Blue/FlaskForeground"));
            flaskFg.type = Image.Type.Simple;
            flaskFg.rectTransform.Stretch();
            health.flaskRoot = flask;
            health.flaskFill = flaskFill;
            health.flaskSteps = new Sprite[0];
            health.flashTarget = flaskFill;

            var hpBar = Bar("HPBar", tl, "RegularBarA", 96, 8, 2);
            hpBar.GetComponent<RectTransform>().Place(0, 1, 30, -3, 96, 8);
            health.bar = hpBar;
            var hpText = UIB.Small10("HPText", tl, "100/100", TextAlignmentOptions.Left);
            hpText.rectTransform.Place(0, 1, 30, -12, 96, 12);
            health.valueText = hpText;

            var prog = tl.gameObject.AddComponent<ProgressionView>();
            var lv = UIB.Body12("Level", tl, "Lv.1", TextAlignmentOptions.Left, UIB.Accent);
            lv.rectTransform.Place(0, 1, 0, -30, 40, 13);
            prog.levelText = lv;
            var rankBadge = UIB.Panel("RankBadge", tl, "Panels/Blue/Panel");
            rankBadge.raycastTarget = false;
            rankBadge.rectTransform.Place(0, 1, 40, -29, 30, 15);
            var rank = UIB.Body12("Rank", rankBadge.transform, "F급", TextAlignmentOptions.Center);
            rank.rectTransform.Stretch();
            prog.rankText = rank;
            prog.rankBadge = rankBadge.rectTransform;
            var exp = Bar("EXPBar", tl, "MinimalBar", 126, 6, 1, new Color(0.55f, 0.85f, 1f));
            exp.GetComponent<RectTransform>().Place(0, 1, 0, -46, 126, 6);
            exp.trailDelay = 0.05f;
            prog.expBar = exp;
            var sp = UIB.Rect("PointsHint", tl).Place(0, 1, 74, -30, 60, 13);
            var spText = UIB.Small10("Text", sp, "SP 1  [K]", TextAlignmentOptions.Left, UIB.Accent);
            spText.rectTransform.Stretch();
            prog.pointsHint = sp.gameObject;
            prog.pointsText = spText;

            // ── 상단 중앙: 게이트 현황 + 안내 문구 ──
            var gateRt = UIB.Panel("GateStatus", hudRt, "Panels/Blue/Panel");
            gateRt.raycastTarget = false;
            gateRt.rectTransform.Place(0.5f, 1, 0, -6, 124, 30);
            var gate = gateRt.gameObject.AddComponent<GateStatusView>();
            var gc = UIB.Body12("Count", gateRt.transform, "게이트 0/3", TextAlignmentOptions.Center);
            gc.rectTransform.Place(0.5f, 1, 0, -2, 120, 13);
            var gs = UIB.Small10("State", gateRt.transform, "게이트 활성", TextAlignmentOptions.Center, UIB.Dim);
            gs.rectTransform.Place(0.5f, 1, 0, -15, 120, 11);
            gate.countText = gc;
            gate.stateText = gs;
            gate.punchTarget = gateRt.rectTransform;
            var ready = UIB.Body12("BossReady", hudRt, "▶ 보스 구역 개방", TextAlignmentOptions.Center, new Color(1f, 0.45f, 0.45f));
            ready.rectTransform.Place(0.5f, 1, 0, -38, 160, 13);
            ready.gameObject.SetActive(false);
            gate.bossReady = ready.gameObject;

            var guide = UIB.Rect("Guide", hudRt).Place(0.5f, 1, 0, -64, 400, 20);
            hud.guideGroup = UIB.Group(guide.gameObject);
            var guideText = UIB.Title15("Text", guide, "", TextAlignmentOptions.Center, Color.white);
            guideText.rectTransform.Stretch();
            hud.guideText = guideText;

            // ── 우상단: 미니맵 ──
            var mmFrame = UIB.Panel("Minimap", hudRt, "Panels/Blue/Panel");
            mmFrame.raycastTarget = false;
            mmFrame.rectTransform.Place(1, 1, -8, -8, 104, 70);
            var minimap = MapView(mmFrame.transform, 6f, true, 4);

            // ── 좌하단: Q/E/R + 1~4 + 버프 ──
            var bottom = UIB.Rect("BottomLeft", hudRt).Place(0, 0, 8, 8, 220, 40);
            var skillBar = bottom.gameObject.AddComponent<SkillBarView>();
            for (int i = 0; i < 3; i++) skillBar.slots[i] = SkillSlot(bottom, (SkillSlot)i, i * 25f);

            var itemsRt = UIB.Rect("Items", bottom).Place(0, 0, 84, 0, 100, 22);
            var itemBar = itemsRt.gameObject.AddComponent<ItemBarView>();
            for (int i = 0; i < 4; i++) itemBar.slots[i] = ItemSlot(itemsRt, i, i * 23f);

            var buffRt = UIB.Rect("Buffs", hudRt).Place(0, 0, 8, 38, 120, 16);
            var hl = buffRt.gameObject.AddComponent<HorizontalLayoutGroup>();
            hl.spacing = 2; hl.childAlignment = TextAnchor.LowerLeft;
            hl.childControlWidth = hl.childControlHeight = false;
            hl.childForceExpandWidth = hl.childForceExpandHeight = false;
            var buffs = buffRt.gameObject.AddComponent<BuffTrayView>();
            var tplImg = UIB.Panel("BuffTemplate", buffRt, "Panels/Blue/Panel");
            tplImg.raycastTarget = false;
            tplImg.rectTransform.sizeDelta = new Vector2(16, 16);
            var tplIcon = UIB.Img("Icon", tplImg.transform, null);
            tplIcon.rectTransform.Place(0.5f, 0.5f, 0, 0, 12, 12);
            var buffIcon = tplImg.gameObject.AddComponent<BuffIconView>();
            buffIcon.icon = tplIcon;
            buffIcon.timer = Radial(tplImg.transform, null, null, 14, false);
            buffs.template = buffIcon;

            hud.health = health;
            hud.progression = prog;
            hud.gate = gate;
            hud.skills = skillBar;
            hud.items = itemBar;
            hud.buffs = buffs;

            // 화면 플래시 (Overlay 레이어)
            var flash = UIB.Solid("ScreenFlash", overlayLayer, new Color(1, 1, 1, 0));
            flash.rectTransform.Stretch();
            hud.flashOverlay = flash;

            return minimap;
        }

        static SkillSlotView SkillSlot(Transform parent, SkillSlot slot, float x)
        {
            var frame = UIB.Panel("Skill_" + slot, parent, "Panels/Blue/Panel");
            frame.raycastTarget = false;
            frame.rectTransform.Place(0, 0, x, 8, 22, 22);
            var view = frame.gameObject.AddComponent<SkillSlotView>();
            view.slot = slot;
            var icon = UIB.Img("Icon", frame.transform, null);
            icon.rectTransform.Place(0.5f, 0.5f, 0, 0, 16, 16);
            view.icon = icon;
            var lockImg = UIB.Solid("Locked", frame.transform, new Color(0, 0, 0, 0.55f));
            lockImg.rectTransform.Place(0.5f, 0.5f, 0, 0, 16, 16);
            view.lockOverlay = lockImg.gameObject;
            view.cooldown = Radial(frame.transform, icon.rectTransform, frame, 17);
            var key = UIB.Small10("Key", frame.transform, slot.ToString(), TextAlignmentOptions.Center, UIB.Accent);
            key.rectTransform.Place(0.5f, 1, 0, 11, 22, 10);
            view.keyLabel = key;
            var rank = UIB.Small10("Rank", frame.transform, "", TextAlignmentOptions.Center, UIB.Accent);
            rank.rectTransform.Place(0.5f, 0, 0, -9, 21, 10);
            view.rankText = rank;
            view.shakeTarget = frame.rectTransform;
            view.tintTarget = frame;
            return view;
        }

        static ItemSlotView ItemSlot(Transform parent, int index, float x)
        {
            var frame = UIB.Panel("Item_" + (index + 1), parent, "Panels/Blue/Panel");
            frame.raycastTarget = false;
            frame.rectTransform.Place(0, 0, x, 8, 22, 22);
            var view = frame.gameObject.AddComponent<ItemSlotView>();
            view.slotIndex = index;
            var fx = UIB.Img("UseFx", frame.transform, UIB.Art("Map_Marker"));
            fx.rectTransform.Place(0.5f, 0.5f, 0, 0, 20, 20);
            fx.gameObject.SetActive(false);
            view.useFx = fx;
            var icon = UIB.Img("Icon", frame.transform, null);
            icon.rectTransform.Place(0.5f, 0.5f, 0, 0, 16, 16);
            view.icon = icon;
            var count = UIB.Small10("Count", frame.transform, "0", TextAlignmentOptions.BottomRight, Color.white);
            count.rectTransform.Place(1, 0, -2, 1, 20, 10);
            view.countText = count;
            var key = UIB.Small10("Key", frame.transform, (index + 1).ToString(), TextAlignmentOptions.Center, UIB.Accent);
            key.rectTransform.Place(0.5f, 1, 0, 11, 21, 10);
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

            var barRoot = UIB.Rect("BossBar", rt).Place(0.5f, 1, 0, -88, 260, 22);
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
            var rt = UIB.Rect("Toasts", layer).Place(1, 0, -8, 48, 200, 120);
            var vl = rt.gameObject.AddComponent<VerticalLayoutGroup>();
            vl.childAlignment = TextAnchor.LowerRight;
            vl.spacing = 2;
            vl.childControlWidth = vl.childControlHeight = true;
            vl.childForceExpandWidth = false;
            vl.childForceExpandHeight = false;
            var toast = rt.gameObject.AddComponent<ToastView>();

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
            w.quitButton = MenuButton(w.transform, "종료", -52);
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
            var c = Content(w, 380, 220);
            var title = UIB.Title15("Title", c, "인벤토리", TextAlignmentOptions.Left, UIB.Accent);
            title.rectTransform.Place(0, 1, 12, -8, 120, 18);

            var gridRt = UIB.Rect("Grid", c).Place(0, 1, 12, -32, 6 * 24, 4 * 24);
            var grid = gridRt.gameObject.AddComponent<GridLayoutGroup>();
            grid.cellSize = new Vector2(22, 22);
            grid.spacing = new Vector2(2, 2);
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = 6;
            w.grid = gridRt;

            var slotBg = UIB.Img("SlotTemplate", gridRt, UIB.Px("Panels/Blue/GridPanel") ?? UIB.Fallback, null, true);
            var slot = slotBg.gameObject.AddComponent<InventorySlotView>();
            var sel = slotBg.gameObject.AddComponent<Button>();
            sel.targetGraphic = slotBg;
            var colors = sel.colors; colors.normalColor = new Color(0.38f, 0.44f, 0.58f); colors.highlightedColor = new Color(0.65f, 0.75f, 0.9f); colors.selectedColor = new Color(0.95f, 0.85f, 0.55f); colors.pressedColor = new Color(0.8f, 0.8f, 0.8f); colors.colorMultiplier = 1f; sel.colors = colors;
            var icon = UIB.Img("Icon", slotBg.transform, null);
            icon.rectTransform.Place(0.5f, 0.5f, 0, 0, 16, 16);
            slot.icon = icon;
            var cnt = UIB.Small10("Count", slotBg.transform, "", TextAlignmentOptions.BottomRight, Color.white);
            cnt.rectTransform.Place(1, 0, 0, -2, 20, 10);
            slot.countText = cnt;
            var hi = UIB.Img("Highlight", slotBg.transform, UIB.Px("Grid/Blue/SelectorA") ?? UIB.White);
            hi.rectTransform.Stretch(-1, -1, -1, -1);
            hi.enabled = false;
            slot.highlight = hi;
            var held = UIB.Solid("Held", slotBg.transform, new Color(1f, 0.85f, 0.3f, 0.35f));
            held.rectTransform.Stretch();
            held.enabled = false;
            slot.heldMark = held;
            w.slotTemplate = slot;

            var empty = UIB.Small10("Empty", c, "인벤토리 소스가 연결되지 않았습니다\n(IInventorySource)", TextAlignmentOptions.Center, UIB.Dim);
            empty.rectTransform.Place(0, 1, 12, -60, 144, 30);
            w.emptyText = empty;

            // 툴팁 (오른쪽 고정 패널)
            var tipBg = UIB.Panel("Tooltip", c, "Panels/Blue/Panel");
            tipBg.raycastTarget = false;
            tipBg.rectTransform.Place(1, 1, -10, -30, 200, 160);
            var tip = tipBg.gameObject.AddComponent<ItemTooltipView>();
            tip.group = UIB.Group(tipBg.gameObject);
            var tIcon = UIB.Img("Icon", tipBg.transform, null);
            tIcon.rectTransform.Place(0, 1, 8, -8, 16, 16);
            tip.icon = tIcon;
            var tName = UIB.Body12("Name", tipBg.transform, "", TextAlignmentOptions.Left);
            tName.rectTransform.Place(0, 1, 30, -9, 160, 14);
            tip.nameText = tName;
            var tType = UIB.Small10("Type", tipBg.transform, "", TextAlignmentOptions.Left, UIB.Dim);
            tType.rectTransform.Place(0, 1, 8, -30, 184, 11);
            tip.typeText = tType;
            var tEff = UIB.Small10("Effect", tipBg.transform, "", TextAlignmentOptions.TopLeft, new Color(0.6f, 1f, 0.7f));
            tEff.rectTransform.Place(0, 1, 8, -46, 184, 24);
            tip.effectText = tEff;
            var tDesc = UIB.Small10("Desc", tipBg.transform, "", TextAlignmentOptions.TopLeft);
            tDesc.rectTransform.Place(0, 1, 8, -74, 184, 48);
            tip.descriptionText = tDesc;
            var tFoot = UIB.Small10("Footer", tipBg.transform, "", TextAlignmentOptions.Left, UIB.Accent);
            tFoot.rectTransform.Place(0, 0, 8, 6, 184, 11);
            tip.footerText = tFoot;
            w.tooltip = tip;

            var hint = UIB.Small10("Hint", c, "[좌클릭] 집기/놓기  [우클릭] 사용  [I]/[ESC] 닫기", TextAlignmentOptions.Left, UIB.Dim);
            hint.rectTransform.Place(0, 0, 12, 6, 300, 12);

            var cursor = UIB.Img("HeldCursor", w.transform, null);
            cursor.rectTransform.Place(0.5f, 0.5f, 0, 0, 16, 16);
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
            var c = Content(w, 160, 170);
            var title = UIB.Title15("Title", c, "일시정지", TextAlignmentOptions.Center, UIB.Accent);
            title.rectTransform.Place(0.5f, 1, 0, -8, 140, 18);
            w.resumeButton = MenuButton(c, "계속하기", 44);
            w.skillButton = MenuButton(c, "스킬", 18);
            w.inventoryButton = MenuButton(c, "인벤토리", -8);
            w.mapButton = MenuButton(c, "지도", -34);
            w.titleButton = MenuButton(c, "타이틀로", -60);
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

        static void BuildStageClear(Transform layer)
        {
            var w = Window<StageClearScreen>("StageClear", layer, ScreenId.StageClear, UILayer.Fullscreen, false, true, false, WindowTransition.Fade, new Color(0.03f, 0.06f, 0.14f, 0.9f));
            var t = UIB.Text("Stage", w.transform, "STAGE 1 CLEAR", UIB.Title, 30, TextAlignmentOptions.Center, UIB.Accent);
            t.rectTransform.Place(0.5f, 0.5f, 0, 60, 400, 40);
            w.stageText = t;
            var badge = UIB.Panel("RankBadge", w.transform, "Panels/Blue/Panel");
            badge.raycastTarget = false;
            badge.rectTransform.Place(0.5f, 0.5f, 0, 14, 160, 30);
            var r = UIB.Title15("Rank", badge.transform, "헌터 랭크 E급", TextAlignmentOptions.Center, Color.white);
            r.rectTransform.Stretch();
            w.rankText = r;
            w.rankBadge = badge.rectTransform;
            w.nextButton = MenuButton(w.transform, "다음 스테이지", -36, 130);
            w.firstSelected = w.nextButton;
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
