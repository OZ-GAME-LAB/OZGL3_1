using OZ.UI.Contracts;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace OZ.UI.EditorTools
{
    /// <summary>
    /// v1.0 임시 상점 NPC 배치. 메뉴 OZ > UI > Place Temp Shop NPC
    ///   열린 씬의 플레이어(UIPlayerTag 또는 Player 태그) 오른쪽에 놓는다. 없으면 씬 뷰 가운데.
    ///   구성: 스프라이트(임시 그림) + 이름표 + InteractPromptLink("[F] 상점") + ShopkeeperLink(Shop_Potion)
    /// 아트가 나오면 Visual 자식만 바꾸면 된다 (상호작용·상점 연결은 루트에 있음).
    /// </summary>
    internal static class OZShopNpcBuilder
    {
        const string NpcName = "TempShopNPC";

        [MenuItem("OZ/UI/Place Temp Shop NPC", priority = 30)]
        static void PlaceInScene()
        {
            if (!EnsureShopWindowInPrefab()) return;
            var shop = OZSampleData.EnsureShop();
            if (shop == null) { Debug.LogError("[OZ UI] Shop_Potion 데이터를 만들지 못했습니다."); return; }

            Transform player = FindPlayer();
            float height = 1.8f;
            Vector3 foot;
            if (player != null)
            {
                var b = BoundsOf(player);
                if (b.size.y > 0.1f) height = b.size.y;
                foot = new Vector3(b.size.y > 0.1f ? b.max.x : player.position.x, b.size.y > 0.1f ? b.min.y : player.position.y, player.position.z) + Vector3.right * 2f;
            }
            else
            {
                var sv = SceneView.lastActiveSceneView;
                foot = sv != null ? sv.pivot : Vector3.zero;
            }

            var npc = Create(foot, height, shop);
            Undo.RegisterCreatedObjectUndo(npc, "Place Temp Shop NPC");
            Selection.activeGameObject = npc;
            EditorGUIUtility.PingObject(npc);
            EditorSceneManager.MarkSceneDirty(npc.scene);
            Debug.Log($"[OZ UI] 임시 상점 NPC 배치 → {npc.name} ({foot}). Play 후 가까이 가서 F = 상점 · 판매 목록은 {AssetDatabase.GetAssetPath(shop)}", npc);
        }

        /// <summary>발 위치(foot)에 키 height짜리 임시 상인. 샌드박스 빌더도 이걸 쓴다.</summary>
        internal static GameObject Create(Vector3 foot, float height, ShopData shop, Sprite sprite = null)
        {
            UIB.LoadFonts();
            var root = new GameObject(NpcName);
            root.transform.position = foot;

            // 그림 (임시: 초상화 스프라이트를 초록빛으로) — 발이 root에 닿게
            sprite = sprite != null ? sprite : UIB.Px("Portraits/PlayerLarge");
            var visual = new GameObject("Visual", typeof(SpriteRenderer));
            visual.transform.SetParent(root.transform, false);
            var sr = visual.GetComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.color = new Color(0.65f, 1f, 0.75f);
            sr.flipX = true; // 플레이어 쪽(왼쪽)을 보게
            float spriteH = sprite != null ? sprite.bounds.size.y : 1f;
            float scale = height / Mathf.Max(0.01f, spriteH);
            visual.transform.localScale = new Vector3(scale, scale, 1f);
            visual.transform.localPosition = new Vector3(0f, height * 0.5f, 0f);

            // 이름표
            var label = new GameObject("NameLabel", typeof(RectTransform));
            label.transform.SetParent(root.transform, false);
            label.transform.localPosition = new Vector3(0f, height + 0.25f, 0f);
            var tmp = label.AddComponent<TextMeshPro>();
            if (UIB.Body != null) tmp.font = UIB.Body;
            tmp.text = shop != null && shop.keeper != null ? shop.keeper.displayName : "상인";
            tmp.fontSize = Mathf.Clamp(height * 1.6f, 2f, 6f);
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = new Color(0.55f, 1f, 0.7f);
            tmp.textWrappingMode = TextWrappingModes.NoWrap;
            ((RectTransform)label.transform).sizeDelta = new Vector2(6f, 1f);

            // 상호작용 + 상점
            var prompt = root.AddComponent<InteractPromptLink>();
            prompt.action = "상점";
            prompt.offset = new Vector3(0f, height + 0.9f, 0f);
            prompt.radius = Mathf.Max(1.8f, height * 1.1f);
            var keeper = root.AddComponent<ShopkeeperLink>();
            keeper.shop = shop;
            root.AddComponent<UINote>().note =
                "[임시 상점 NPC · parkhansol UI]\n" +
                "가까이 가서 F → 상점 창 (GameUI.Shop). 판매 목록·가격은 ShopkeeperLink의 Shop 에셋에서 수정.\n" +
                "구매 처리는 IShopSource 구현이 필요 — 지금은 UI 샌드박스의 DummyPlayer만 구현돼 있음.\n" +
                "그림은 Visual 자식만 교체하면 됨.";
            return root;
        }

        static Transform FindPlayer()
        {
            var tag = Object.FindAnyObjectByType<UIPlayerTag>();
            if (tag != null) return tag.transform;
            try { var go = GameObject.FindGameObjectWithTag("Player"); if (go != null) return go.transform; }
            catch (UnityException) { }
            return null;
        }

        static Bounds BoundsOf(Transform t)
        {
            var rs = t.GetComponentsInChildren<Renderer>();
            if (rs.Length == 0) return new Bounds(t.position, Vector3.zero);
            var b = rs[0].bounds;
            for (int i = 1; i < rs.Length; i++) b.Encapsulate(rs[i].bounds);
            return b;
        }

        /// <summary>UIRoot 프리팹에 상점 창이 없으면 다시 만들지 묻는다 (예전 프리팹 대비)</summary>
        static bool EnsureShopWindowInPrefab()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(OZUIRootBuilder.PrefabPath);
            if (prefab != null && prefab.GetComponentInChildren<ShopWindow>(true) != null) return true;
            bool rebuild = EditorUtility.DisplayDialog("OZ UI",
                "UIRoot 프리팹에 상점 창이 없습니다.\n지금 UIRoot 프리팹을 다시 만들까요? (OZ > UI > Rebuild UIRoot Prefab 과 같음)",
                "다시 만들기", "NPC만 놓기");
            if (rebuild) OZUIRootBuilder.Run();
            return true;
        }
    }
}
