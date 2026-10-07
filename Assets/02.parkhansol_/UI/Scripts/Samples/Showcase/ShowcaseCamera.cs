using UnityEngine;

namespace OZ.UI.Samples
{
    /// <summary>쇼케이스 카메라: 옆에서 보는 원근 카메라, 플레이어를 부드럽게 따라감 + 흔들림 (실시간 → 히트스톱 중에도 동작)</summary>
    [AddComponentMenu("OZ/UI/Samples/Showcase Camera")]
    public class ShowcaseCamera : MonoBehaviour
    {
        [SerializeField] internal Transform target;
        [SerializeField] internal Vector3 offset = new Vector3(0f, 3.4f, -12.5f);
        [Tooltip("위아래로는 이만큼만 따라감 (점프할 때 화면이 덜 출렁이게)")]
        [SerializeField, Range(0f, 1f)] internal float verticalFollow = 0.45f;
        [SerializeField] internal float pitch = 10f;
        [SerializeField] internal float lookAhead = 1.5f;
        [SerializeField] internal float followSharpness = 6f;
        [SerializeField] internal Vector2 xLimits = new Vector2(-24f, 24f);

        Vector3 _pos;
        float _shake, _shakeTime, _shakeDur;

        void Start()
        {
            if (target != null) _pos = Desired();
            else _pos = transform.position;
        }

        Vector3 Desired()
        {
            var p = ShowcasePlayer.Instance;
            float ahead = p != null ? p.Facing * lookAhead : 0f;
            var t = target.position;
            var d = new Vector3(t.x, t.y * verticalFollow, t.z) + offset + new Vector3(ahead, 0f, 0f);
            d.x = Mathf.Clamp(d.x, xLimits.x, xLimits.y);
            return d;
        }

        public void Shake(float amount, float duration)
        {
            _shake = Mathf.Max(_shake, amount);
            _shakeDur = Mathf.Max(0.01f, duration);
            _shakeTime = _shakeDur;
        }

        void LateUpdate()
        {
            if (target == null) return;
            float dt = Time.unscaledDeltaTime;
            _pos = Vector3.Lerp(_pos, Desired(), 1f - Mathf.Exp(-followSharpness * dt));
            Vector3 off = Vector3.zero;
            if (_shakeTime > 0f && OZ.UI.Contracts.UISettings.ScreenShake)
            {
                _shakeTime -= dt;
                float a = _shake * Mathf.Clamp01(_shakeTime / _shakeDur);
                off = new Vector3(Random.Range(-a, a), Random.Range(-a, a) * 0.5f, 0f);
            }
            transform.position = _pos + off;
            transform.rotation = Quaternion.Euler(pitch, 0f, 0f);
        }
    }
}
