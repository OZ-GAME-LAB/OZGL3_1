using System.Collections;
using UnityEngine;

namespace OZ.UI.Contracts
{
    /// <summary>
    /// [적·플레이어 담당이 붙여 쓰는 컴포넌트] 피격 흰색 번쩍임 (자료: 피격 순간 1~3프레임 흰색 덮기가 표준).
    ///   - 렌더러 머티리얼에 "OZ/Sprite Flash" 셰이더가 필요 (UI/Art/Shaders, 머티리얼: UI/Art/FX/OZ_SpriteFlash.mat)
    ///   - MaterialPropertyBlock 사용 → 머티리얼을 공유해도 맞은 적만 번쩍임
    /// 사용: GetComponent&lt;HitFlash&gt;().Play();  /  .Play(Color.white, 0.1f)
    /// </summary>
    [AddComponentMenu("OZ/UI/Damage/Hit Flash")]
    public class HitFlash : MonoBehaviour
    {
        static readonly int FlashAmountId = Shader.PropertyToID("_FlashAmount");
        static readonly int FlashColorId = Shader.PropertyToID("_FlashColor");

        [Tooltip("비우면 자식 포함 모든 Renderer")]
        [SerializeField] internal Renderer[] renderers = new Renderer[0];
        [SerializeField] internal Color defaultColor = Color.white;
        [Tooltip("완전히 덮는 시간 (초). 60fps 기준 0.05 ≈ 3프레임")]
        [SerializeField] internal float defaultHold = 0.05f;
        [Tooltip("덮은 뒤 원래 색으로 돌아오는 시간")]
        [SerializeField] internal float fadeOut = 0.06f;

        MaterialPropertyBlock _mpb;
        Coroutine _co;

        public float CurrentAmount { get; private set; }

        void Awake()
        {
            _mpb = new MaterialPropertyBlock();
            if (renderers == null || renderers.Length == 0) renderers = GetComponentsInChildren<Renderer>(true);
            Apply(0f, defaultColor);
        }

        public void Play() => Play(defaultColor, defaultHold);

        public void Play(Color color, float hold)
        {
            if (!isActiveAndEnabled) return;
            if (_co != null) StopCoroutine(_co);
            _co = StartCoroutine(Run(color, hold));
        }

        IEnumerator Run(Color color, float hold)
        {
            Apply(1f, color);
            // 히트스톱(timeScale 0) 중에도 진행되도록 실시간 사용
            yield return new WaitForSecondsRealtime(hold);
            for (float t = 0f; t < fadeOut; t += Time.unscaledDeltaTime)
            {
                Apply(1f - t / fadeOut, color);
                yield return null;
            }
            Apply(0f, color);
            _co = null;
        }

        void Apply(float amount, Color color)
        {
            CurrentAmount = amount;
            if (_mpb == null) _mpb = new MaterialPropertyBlock();
            foreach (var r in renderers)
            {
                if (r == null) continue;
                r.GetPropertyBlock(_mpb);
                _mpb.SetFloat(FlashAmountId, amount);
                _mpb.SetColor(FlashColorId, color);
                r.SetPropertyBlock(_mpb);
            }
        }
    }
}
