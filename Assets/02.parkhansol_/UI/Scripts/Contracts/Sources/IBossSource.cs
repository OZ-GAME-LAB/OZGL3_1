using System;

namespace OZ.UI.Contracts
{
    /// <summary>
    /// [보스 담당 구현] 보스 체력. 보스전 시작 시 GameUI.Boss.Show(this) 한 줄로 연결.
    /// </summary>
    public interface IBossSource
    {
        BossData Data { get; }
        float HP { get; }
        float MaxHP { get; }
        int Phase { get; }

        /// <summary>(현재 HP, 최대 HP)</summary>
        event Action<float, float> HPChanged;
        event Action<int> PhaseChanged;
        event Action Defeated;
    }
}
