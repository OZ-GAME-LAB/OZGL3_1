using UnityEngine;

namespace OZ.UI.Contracts
{
    /// <summary>
    /// [v0.7] 이 오브젝트가 화면 밖에 있으면 화면 가장자리에 방향 화살표를 띄운다 (켜져 있는 동안).
    /// 게이트·보스·목표 지점 프리팹에 붙이기만 하면 됨. 끄거나(SetActive false) 파괴하면 자동 해제.
    /// MainScene 게이트는 GateIndicatorBridge가 자동으로 표시하므로 붙일 필요 없음.
    ///
    /// [v0.9] 보스 구역 입구에 붙이고 onlyAfterBossUnlock을 켜면, 게이트 목표를 채운 뒤에만 화살표가 뜬다
    ///   (기획서: "목표 봉쇄 수를 채운 뒤에는 보스 구역으로 진행하도록 안내"). 게이트 상태는 IGateSource에서 읽음.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("OZ/UI/Links/Objective Marker Link")]
    public class ObjectiveMarkerLink : MonoBehaviour
    {
        [SerializeField] internal IndicatorKind kind = IndicatorKind.Objective;
        [Tooltip("화살표 옆 글자 (비우면 거리만)")]
        [SerializeField] internal string label = "";
        [Tooltip("켜면 보스 구역이 열린 뒤에만 표시 (보스 구역 입구용, kind = Boss 추천)")]
        [SerializeField] internal bool onlyAfterBossUnlock;

        bool _tracked;
        float _nextPoll;

        void OnEnable() => Sync();
        void Start() { if (!_tracked) Sync(); } // UI가 늦게 뜬 경우

        void Update()
        {
            if (!onlyAfterBossUnlock && _tracked) return;
            if (Time.unscaledTime < _nextPoll) return;
            _nextPoll = Time.unscaledTime + 0.25f;
            Sync();
        }

        bool ShouldShow
        {
            get
            {
                if (!onlyAfterBossUnlock) return true;
                var gate = UISources.Gate;
                return gate != null && gate.IsBossAreaUnlocked;
            }
        }

        void Sync()
        {
            if (!GameUI.IsReady) return;
            bool want = ShouldShow;
            if (want && !_tracked)
            {
                GameUI.Indicators.Track(transform, kind, label);
                _tracked = GameUI.Indicators.IsTracking(transform);
            }
            else if (!want && _tracked)
            {
                GameUI.Indicators.Untrack(transform);
                _tracked = false;
            }
        }

        void OnDisable()
        {
            if (GameUI.IsReady) GameUI.Indicators.Untrack(transform);
            _tracked = false;
        }
    }
}
