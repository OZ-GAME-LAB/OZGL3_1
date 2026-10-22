using System;
using System.Collections.Generic;
using UnityEngine;

namespace OZ.UI.Contracts
{
    /// <summary>
    /// [v1.0 · 임시 상점] 상점 한 곳의 판매 목록. NPC(ShopkeeperLink)에 꽂아 두면 F로 상점 창이 열린다.
    ///   GameUI.Shop.Open(shopData);
    /// 가격·보유 상한은 여기서 정하고, 실제 차감·지급은 IShopSource 구현 쪽(코어/플레이어)이 한다.
    /// </summary>
    [CreateAssetMenu(menuName = "OZ/UI/Shop Data", fileName = "Shop_")]
    public class ShopData : ScriptableObject
    {
        public string id;
        [Tooltip("창 제목 (예: 편의점)")]
        public string displayName = "상점";
        [Tooltip("재화 이름 — 기획서 재화는 '포인트'")]
        public string currencyName = "포인트";
        [Tooltip("상인 이름·초상화 (선택)")]
        public SpeakerData keeper;
        [Tooltip("창 위쪽 상인 한마디 (선택)")]
        [TextArea(1, 3)] public string greeting;
        public List<ShopEntry> entries = new List<ShopEntry>();

        public int Count => entries?.Count ?? 0;
        public ShopEntry Get(int index) => entries != null && index >= 0 && index < entries.Count ? entries[index] : null;
    }

    /// <summary>판매 칸 하나</summary>
    [Serializable]
    public class ShopEntry
    {
        public ItemData item;
        [Min(0)] public int price = 100;
        [Tooltip("이 아이템을 최대 몇 개까지 가질 수 있나 (0 = 제한 없음). 기획데이터 9장: 소모품 종류별 최대 5개")]
        [Min(0)] public int maxOwned = 5;
    }
}
