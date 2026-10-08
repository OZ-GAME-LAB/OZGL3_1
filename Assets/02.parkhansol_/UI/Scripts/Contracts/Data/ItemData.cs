using UnityEngine;

namespace OZ.UI.Contracts
{
    /// <summary>
    /// 소모품 아이템 표시 데이터. 기획서 v0.2: 1~4 고정 슬롯, 종류별 수량 표시.
    /// 실제 효과 적용은 게임플레이 쪽이 담당하고, UI는 아이콘/수량/버프 타이머만 그린다.
    /// </summary>
    [CreateAssetMenu(menuName = "OZ/UI/Item Data", fileName = "Item_")]
    public class ItemData : ScriptableObject
    {
        public string id;
        public string displayName;
        [TextArea(2, 4)] public string description;
        public Sprite icon;

        [Tooltip("퀵슬롯 번호 0~3 (화면 1~4). -1이면 퀵슬롯 없음 (인벤토리 전용)")]
        [Range(-1, 3)] public int slotIndex;
        [Tooltip("인벤토리 한 칸 최대 수량")]
        [Min(1)] public int maxStack = 9;

        public ItemEffectType effectType;
        [Tooltip("효과 수치(%) — 회복 30, 공격 20, 방어 25, 신속 20 (검증값)")]
        public float effectPercent;
        [Tooltip("버프 지속시간(초). 0이면 즉시 효과(회복)")]
        [Min(0f)] public float buffDuration;

        [Tooltip("사용 연출 색 (회복=초록, 공격=빨강 등)")]
        public Color fxColor = Color.white;

        public bool IsBuff => buffDuration > 0f;
        public bool IsUsable => effectType != ItemEffectType.KeyItem;
    }
}
