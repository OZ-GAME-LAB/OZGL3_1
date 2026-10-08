using System;
using UnityEngine;

namespace OZ.UI.Contracts
{
    /// <summary>
    /// [GateManager용] 인터페이스 구현 없이 메서드만 부르면 HUD 게이트 표시가 따라온다.
    ///
    ///   [SerializeField] GateUILink gateUI;           // 씬의 "UI Links" 오브젝트를 드래그
    ///   StartGatePhase()   → gateUI.BeginStage(stage, 목표수);
    ///   ActivateNextGate() → gateUI.GateOpened();
    ///   게이트 봉쇄 시      → gateUI.GateSealed();      // "게이트 파괴" 띠 + 숫자 자동, 목표 달성 시 "보스 구역 개방"
    ///
    /// 이미 GateManager가 IGateSource를 구현했다면 이 컴포넌트는 필요 없음 (GameUI.Bind(this)).
    /// </summary>
    [AddComponentMenu("OZ/UI/Links/Gate UI Link")]
    public class GateUILink : MonoBehaviour, IGateSource
    {
        int _stage = 1, _sealed, _target = 3;
        bool _active;

        public int StageNumber => _stage;
        public int SealedCount => _sealed;
        public int TargetCount => _target;
        public bool IsGateActive => _active;
        public bool IsBossAreaUnlocked => _target > 0 && _sealed >= _target;

        // 이벤트는 인터페이스로만 노출 (메서드 이름과 겹치지 않게)
        Action _opened, _reset, _unlocked;
        Action<int, int> _sealedEvt;
        event Action IGateSource.GateOpened { add => _opened += value; remove => _opened -= value; }
        event Action<int, int> IGateSource.GateSealed { add => _sealedEvt += value; remove => _sealedEvt -= value; }
        event Action IGateSource.BossAreaUnlocked { add => _unlocked += value; remove => _unlocked -= value; }
        event Action IGateSource.GateStateReset { add => _reset += value; remove => _reset -= value; }

        void OnEnable() => GameUI.Bind(this);
        void OnDisable() => GameUI.Unbind(this);

        /// <summary>스테이지 시작·재시작: 봉쇄 수 0으로, 목표 수 지정</summary>
        public void BeginStage(int stage, int targetCount)
        {
            _stage = stage;
            _target = Mathf.Max(1, targetCount);
            _sealed = 0;
            _active = false;
            _reset?.Invoke();
        }

        /// <summary>게이트 하나가 열림 (HUD "게이트 활성")</summary>
        public void GateOpened()
        {
            if (IsBossAreaUnlocked) return;
            _active = true;
            _opened?.Invoke();
        }

        /// <summary>열린 게이트 봉쇄 (HUD 숫자 + "게이트 파괴" 띠, 목표 달성이면 보스 구역 개방)</summary>
        public void GateSealed()
        {
            if (IsBossAreaUnlocked) return;
            _active = false;
            _sealed++;
            _sealedEvt?.Invoke(_sealed, _target);
            if (IsBossAreaUnlocked) _unlocked?.Invoke();
        }

        /// <summary>봉쇄 수를 직접 맞춤 (세이브 불러오기 등)</summary>
        public void SetProgress(int sealedCount, int targetCount, bool gateActive)
        {
            _sealed = Mathf.Max(0, sealedCount);
            _target = Mathf.Max(1, targetCount);
            _active = gateActive;
            _reset?.Invoke();
        }
    }
}
