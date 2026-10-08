using UnityEngine;
using UnityEngine.UI;

namespace OZ.UI
{
    /// <summary>타격 이펙트 한 개 (풀링). 프레임 스프라이트를 순서대로 넘긴다.</summary>
    [AddComponentMenu("OZ/UI/Damage/Hit Spark View")]
    public class HitSparkView : MonoBehaviour
    {
        [SerializeField] internal Image image;

        internal Vector3 worldPos;
        internal bool active;

        Sprite[] _frames;
        float _fps, _t;
        System.Action<HitSparkView> _onDone;

        internal void Play(Sprite[] frames, float fps, float rotationDeg, System.Action<HitSparkView> onDone)
        {
            _frames = frames; _fps = Mathf.Max(1f, fps); _t = 0f; _onDone = onDone;
            active = frames != null && frames.Length > 0;
            gameObject.SetActive(active);
            if (!active) { onDone?.Invoke(this); return; }
            image.sprite = frames[0];
            image.SetNativeSize();
            transform.localRotation = Quaternion.Euler(0, 0, rotationDeg); // 90° 단위만 → 픽셀 유지
        }

        void Update()
        {
            if (!active) return;
            _t += Time.unscaledDeltaTime;
            int f = Mathf.FloorToInt(_t * _fps);
            if (f >= _frames.Length) { Stop(); _onDone?.Invoke(this); return; }
            if (image.sprite != _frames[f]) image.sprite = _frames[f];
        }

        internal void Stop()
        {
            active = false;
            gameObject.SetActive(false);
        }
    }
}
