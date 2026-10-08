using DG.Tweening;
using OZ.UI.Contracts;
using TMPro;
using UnityEngine;

namespace OZ.UI
{
    /// <summary>게이트 봉쇄 현황 + 보스 구역 개방 표시</summary>
    [AddComponentMenu("OZ/UI/HUD/Gate Status View")]
    public class GateStatusView : MonoBehaviour
    {
        [SerializeField] internal TMP_Text countText;
        [SerializeField] internal TMP_Text stateText;
        [SerializeField] internal RectTransform punchTarget;
        [SerializeField] internal GameObject bossReady;

        IGateSource _source;

        internal void Bind(IGateSource source)
        {
            Unsubscribe();
            _source = source;
            if (_source != null)
            {
                _source.GateOpened += Refresh;
                _source.GateSealed += OnSealed;
                _source.BossAreaUnlocked += OnBossUnlocked;
                _source.GateStateReset += Refresh;
            }
            gameObject.SetActive(_source != null);
            Refresh();
        }

        void Unsubscribe()
        {
            if (_source != null)
            {
                _source.GateOpened -= Refresh;
                _source.GateSealed -= OnSealed;
                _source.BossAreaUnlocked -= OnBossUnlocked;
                _source.GateStateReset -= Refresh;
            }
        }

        void OnDestroy() => Unsubscribe();

        void OnSealed(int sealedCount, int target)
        {
            Refresh();
            if (punchTarget != null) punchTarget.Punch(0.3f, 0.35f);
        }

        void OnBossUnlocked()
        {
            Refresh();
            if (bossReady != null) ((RectTransform)bossReady.transform).Punch(0.5f, 0.5f);
        }

        void Refresh()
        {
            if (_source == null) return;
            if (countText != null) countText.text = $"게이트 {_source.SealedCount}/{_source.TargetCount}";
            if (stateText != null)
                stateText.text = _source.IsBossAreaUnlocked ? "보스 구역 개방"
                    : _source.IsGateActive ? "게이트 활성" : "다음 게이트 탐색";

            bool ready = _source.IsBossAreaUnlocked;
            if (bossReady != null)
            {
                var rt = (RectTransform)bossReady.transform;
                if (ready && !bossReady.activeSelf)
                {
                    bossReady.SetActive(true);
                    rt.Pulse(1.08f, 0.4f);
                }
                else if (!ready && bossReady.activeSelf)
                {
                    rt.DOKill();
                    rt.localScale = Vector3.one;
                    bossReady.SetActive(false);
                }
            }
        }
    }
}
