using System.Collections;
using OZ.UI.Contracts;
using UnityEngine;

namespace OZ.UI.Samples
{
    /// <summary>
    /// 타격감 수치표 + 샌드박스용 히트스톱·화면 흔들림 (전투·카메라 담당 참고 구현).
    /// 수치는 자료 조사 기준 (Docs/CombatFeedback.md):
    ///   일반 1~2프레임 정지·1px / 치명타·약점 4~5프레임·3px / 처치 7프레임·4px / 플레이어 피격 4프레임·3px
    /// 화면 흔들림은 옵션(UISettings.ScreenShake)을 따른다. 흔들림 단위는 UI 픽셀(640×360 기준).
    /// </summary>
    [AddComponentMenu("OZ/UI/Samples/Sandbox Feel")]
    public class SandboxFeel : MonoBehaviour
    {
        [System.Serializable]
        public struct Tier
        {
            public DamageKind kind;
            [Tooltip("히트스톱 (초, 실시간). 60fps 1프레임 ≈ 0.0167")]
            public float hitStop;
            [Tooltip("화면 흔들림 세기 (UI px)")]
            public float shake;
            public float shakeTime;
            [Tooltip("적 번쩍임 유지 시간")]
            public float flashHold;
            public Color flashColor;
        }

        [SerializeField] internal Camera cam;
        [SerializeField] internal Tier[] tiers =
        {
            new Tier { kind = DamageKind.Normal,         hitStop = 0.03f, shake = 1f, shakeTime = 0.08f, flashHold = 0.05f, flashColor = Color.white },
            new Tier { kind = DamageKind.Critical,       hitStop = 0.07f, shake = 3f, shakeTime = 0.14f, flashHold = 0.07f, flashColor = Color.white },
            new Tier { kind = DamageKind.Weakness,       hitStop = 0.08f, shake = 3f, shakeTime = 0.16f, flashHold = 0.08f, flashColor = new Color(1f, 0.85f, 0.6f) },
            new Tier { kind = DamageKind.Finisher,       hitStop = 0.12f, shake = 4f, shakeTime = 0.22f, flashHold = 0.10f, flashColor = Color.white },
            new Tier { kind = DamageKind.DamageOverTime, hitStop = 0f,    shake = 0f, shakeTime = 0f,    flashHold = 0.03f, flashColor = new Color(0.85f, 0.65f, 1f) },
            new Tier { kind = DamageKind.PlayerHurt,     hitStop = 0.06f, shake = 3f, shakeTime = 0.15f, flashHold = 0f,    flashColor = Color.white },
        };

        Coroutine _stop, _shake;
        Vector3 _camHome;
        bool _camHomeSet;

        public bool IsHitStopping { get; private set; }

        public Tier Get(DamageKind kind)
        {
            foreach (var t in tiers) if (t.kind == kind) return t;
            return tiers.Length > 0 ? tiers[0] : default;
        }

        public void Apply(DamageKind kind, HitFlash flash = null)
        {
            var t = Get(kind);
            if (flash != null && t.flashHold > 0f) flash.Play(t.flashColor, t.flashHold);
            if (t.hitStop > 0f) HitStop(t.hitStop);
            if (t.shake > 0f) Shake(t.shake, t.shakeTime);
        }

        public void HitStop(float seconds)
        {
            if (UIState.IsPausedByUI) return;          // 메뉴가 시간을 멈춘 중이면 건드리지 않음
            if (_stop != null) StopCoroutine(_stop);   // 겹치면 마지막 것 기준으로 연장
            _stop = StartCoroutine(StopRoutine(seconds));
        }

        IEnumerator StopRoutine(float seconds)
        {
            IsHitStopping = true;
            Time.timeScale = 0f;
            yield return new WaitForSecondsRealtime(seconds);
            if (!UIState.IsPausedByUI) Time.timeScale = 1f;
            IsHitStopping = false;
            _stop = null;
        }

        public void Shake(float uiPixels, float duration)
        {
            if (!UISettings.ScreenShake) return;
            var c = cam != null ? cam : Camera.main;
            if (c == null || !c.orthographic) return;
            if (!_camHomeSet) { _camHome = c.transform.position; _camHomeSet = true; }
            if (_shake != null) StopCoroutine(_shake);
            _shake = StartCoroutine(ShakeRoutine(c, uiPixels, duration));
        }

        IEnumerator ShakeRoutine(Camera c, float uiPixels, float duration)
        {
            float unit = c.orthographicSize * 2f / 360f; // UI 1px = 월드 단위
            for (float t = 0f; t < duration; t += Time.unscaledDeltaTime)
            {
                float amp = uiPixels * (1f - t / duration);
                var off = new Vector2(Mathf.Round(Random.Range(-amp, amp)), Mathf.Round(Random.Range(-amp, amp) * 0.5f)) * unit;
                c.transform.position = _camHome + (Vector3)off;
                yield return null;
            }
            c.transform.position = _camHome;
            _shake = null;
        }

        void OnDisable()
        {
            if (IsHitStopping && !UIState.IsPausedByUI) Time.timeScale = 1f;
            var c = cam != null ? cam : Camera.main;
            if (_camHomeSet && c != null) c.transform.position = _camHome;
        }
    }
}
