using System;
using UnityEngine;

namespace OZ.UI.Contracts
{
    /// <summary>
    /// [적 담당 구현] 적 머리 위 체력바용. GameUI.Damage.TrackEnemy(this)로 등록하면
    /// 맞을 때만 체력바가 나타났다가 잠시 뒤 사라진다 (엘리트는 항상 표시).
    /// </summary>
    public interface IEnemyHealthSource
    {
        /// <summary>체력바를 띄울 위치 (머리 위 빈 오브젝트 권장)</summary>
        Transform BarAnchor { get; }
        float HP { get; }
        float MaxHP { get; }
        /// <summary>true면 체력바를 항상 표시 + 조금 더 크게</summary>
        bool IsElite { get; }
        event Action<HealthChange> HealthChanged;
    }
}
