using UnityEngine;

namespace OZ.UI.Contracts
{
    /// <summary>
    /// [v0.7] 이 오브젝트가 화면 밖에 있으면 화면 가장자리에 방향 화살표를 띄운다 (켜져 있는 동안).
    /// 게이트·보스·목표 지점 프리팹에 붙이기만 하면 됨. 끄거나(SetActive false) 파괴하면 자동 해제.
    /// MainScene 게이트는 GateIndicatorBridge가 자동으로 표시하므로 붙일 필요 없음.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("OZ/UI/Links/Objective Marker Link")]
    public class ObjectiveMarkerLink : MonoBehaviour
    {
        [SerializeField] internal IndicatorKind kind = IndicatorKind.Objective;
        [Tooltip("화살표 옆 글자 (비우면 거리만)")]
        [SerializeField] internal string label = "";

        bool _tracked;

        void OnEnable() => Track();
        void Start() { if (!_tracked) Track(); } // UI가 늦게 뜬 경우

        void Track()
        {
            if (!GameUI.IsReady) return;
            GameUI.Indicators.Track(transform, kind, label);
            _tracked = GameUI.Indicators.IsTracking(transform);
        }

        void OnDisable()
        {
            GameUI.Indicators.Untrack(transform);
            _tracked = false;
        }
    }
}
