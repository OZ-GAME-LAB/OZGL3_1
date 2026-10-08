using System;
using System.Collections;
using System.Collections.Generic;
using OZ.UI.Contracts;
using UnityEngine;

namespace OZ.UI.Samples
{
    /// <summary>
    /// [팀 설계 GateManager 모양 샘플] StartGatePhase() / ActivateNextGate().
    ///
    /// UI 연결은 IGateSource 구현 하나로 끝난다 (GameUI.Bind(this)):
    ///   GateOpened        → HUD "게이트 활성"
    ///   GateSealed(n, 목표) → HUD 숫자 + 화면 가운데 "게이트 파괴" 띠 (자동)
    ///   BossAreaUnlocked   → HUD "보스 구역 개방"
    ///   GateStateReset     → HUD 다시 그림 (재시작·스테이지 시작)
    /// </summary>
    [AddComponentMenu("OZ/UI/Samples/Flow/Sample Gate Manager")]
    public class SampleGateManager : MonoBehaviour, IGateSource
    {
        [SerializeField] internal List<SampleGate> gates = new List<SampleGate>();
        [SerializeField] internal float nextGateDelay = 2.5f;

        int _stage, _sealed, _target, _next, _enemiesPerGate = 3;
        SampleGate _active;
        Coroutine _delay;

        /// <summary>목표 수만큼 봉쇄했을 때 (StageManager가 보스 단계로 넘어감)</summary>
        public event Action AllGatesSealed;

        // ── IGateSource ──
        public int StageNumber => _stage;
        public int SealedCount => _sealed;
        public int TargetCount => _target;
        public bool IsGateActive => _active != null;
        public bool IsBossAreaUnlocked => _target > 0 && _sealed >= _target;
        public event Action GateOpened;
        public event Action<int, int> GateSealed;
        public event Action BossAreaUnlocked;
        public event Action GateStateReset;

        void OnEnable() => GameUI.Bind(this);          // [UI] HUD 게이트 표시 연결
        void OnDisable() => GameUI.Unbind(this);

        void Start() => GameUI.Bind(this); // 다른 IGateSource(테스트용 더미 등)보다 나중에 등록 → 이게 이김

        public void StartGatePhase(int stage, int target, int enemiesPerGate)
        {
            StopAll();
            _stage = stage;
            _target = Mathf.Max(1, target);
            _enemiesPerGate = Mathf.Max(1, enemiesPerGate);
            _sealed = 0;
            _next = 0;
            GateStateReset?.Invoke(); // [UI]
            _delay = StartCoroutine(Delayed(1.2f, ActivateNextGate));
        }

        public void ActivateNextGate()
        {
            if (gates.Count == 0 || IsBossAreaUnlocked || _active != null) return;
            _active = gates[_next % gates.Count];
            _next++;
            _active.Sealed -= OnGateSealed;
            _active.Sealed += OnGateSealed;
            _active.Open(_enemiesPerGate);
            GateOpened?.Invoke(); // [UI]
            GameUI.Notify.Toast("게이트가 열렸다", ToastType.Warning); // [UI] 짧은 알림 (선택)
        }

        void OnGateSealed(SampleGate gate)
        {
            gate.Sealed -= OnGateSealed;
            if (gate != _active) return;
            _active = null;
            _sealed++;
            GateSealed?.Invoke(_sealed, _target); // [UI] "게이트 파괴" 띠는 UI가 알아서
            if (IsBossAreaUnlocked)
            {
                BossAreaUnlocked?.Invoke(); // [UI]
                AllGatesSealed?.Invoke();
            }
            else _delay = StartCoroutine(Delayed(nextGateDelay, ActivateNextGate));
        }

        /// <summary>열린 게이트·남은 적 정리 (재시작·스테이지 교체)</summary>
        public void StopAll()
        {
            if (_delay != null) StopCoroutine(_delay);
            _delay = null;
            foreach (var g in gates) if (g != null) { g.Sealed -= OnGateSealed; g.Close(); }
            _active = null;
        }

        IEnumerator Delayed(float seconds, Action action)
        {
            yield return new WaitForSeconds(seconds);
            _delay = null;
            action();
        }
    }
}
