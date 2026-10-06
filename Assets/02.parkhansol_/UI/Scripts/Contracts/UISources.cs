using System;
using UnityEngine;

namespace OZ.UI.Contracts
{
    /// <summary>
    /// 현재 HUD가 그릴 데이터 소스 보관소.
    /// 게임플레이 쪽은 GameUI.Bind(this) 또는 UISourceBinder 컴포넌트로 등록만 하면 되고,
    /// HUD는 Changed 이벤트를 듣고 알아서 다시 구독한다.
    /// </summary>
    public static class UISources
    {
        public static IHealthSource Health { get; private set; }
        public static IProgressionSource Progression { get; private set; }
        public static ISkillSource Skills { get; private set; }
        public static IItemSource Items { get; private set; }
        public static IGateSource Gate { get; private set; }
        public static IInventorySource Inventory { get; private set; }

        /// <summary>소스가 등록/해제될 때마다 호출</summary>
        public static event Action Changed;

        /// <summary>source가 구현한 인터페이스를 모두 등록한다. 같은 종류는 마지막 등록이 이긴다.</summary>
        public static void Bind(object source)
        {
            if (source == null) return;
            bool any = false;
            if (source is IHealthSource h) { Health = h; any = true; }
            if (source is IProgressionSource p) { Progression = p; any = true; }
            if (source is ISkillSource s) { Skills = s; any = true; }
            if (source is IItemSource i) { Items = i; any = true; }
            if (source is IGateSource g) { Gate = g; any = true; }
            if (source is IInventorySource inv) { Inventory = inv; any = true; }
            if (any) Changed?.Invoke();
        }

        /// <summary>source가 등록돼 있던 칸만 비운다 (다른 오브젝트가 등록한 칸은 유지).</summary>
        public static void Unbind(object source)
        {
            if (source == null) return;
            bool any = false;
            if (ReferenceEquals(Health, source)) { Health = null; any = true; }
            if (ReferenceEquals(Progression, source)) { Progression = null; any = true; }
            if (ReferenceEquals(Skills, source)) { Skills = null; any = true; }
            if (ReferenceEquals(Items, source)) { Items = null; any = true; }
            if (ReferenceEquals(Gate, source)) { Gate = null; any = true; }
            if (ReferenceEquals(Inventory, source)) { Inventory = null; any = true; }
            if (any) Changed?.Invoke();
        }

        // Enter Play Mode 옵션(도메인 리로드 끔)에서도 이전 플레이 값이 남지 않게
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            Health = null; Progression = null; Skills = null; Items = null; Gate = null; Inventory = null;
            Changed = null;
        }
    }
}
