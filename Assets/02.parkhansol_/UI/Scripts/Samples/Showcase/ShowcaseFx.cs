using System.Collections;
using UnityEngine;

namespace OZ.UI.Samples
{
    /// <summary>
    /// 쇼케이스용 임시 이펙트 (기본 도형 + 발광 머티리얼). 진짜 이펙트가 나오기 전까지 UI와 어울리는지 보는 용도.
    /// </summary>
    [AddComponentMenu("OZ/UI/Samples/Showcase Fx")]
    public class ShowcaseFx : MonoBehaviour
    {
        [SerializeField] internal Material swordMat;
        [SerializeField] internal Material magicMat;
        [SerializeField] internal Material hitMat;

        public static ShowcaseFx Instance { get; private set; }

        void Awake() => Instance = this;
        void OnDestroy() { if (Instance == this) Instance = null; }

        GameObject Shape(PrimitiveType type, Material mat, Vector3 pos, Vector3 scale, Quaternion rot)
        {
            var go = GameObject.CreatePrimitive(type);
            Destroy(go.GetComponent<Collider>());
            go.transform.SetPositionAndRotation(pos, rot);
            go.transform.localScale = scale;
            var r = go.GetComponent<Renderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return go;
        }

        /// <summary>기본 공격 반달 베기 (납작한 원기둥이 휘둘러지며 사라짐)</summary>
        public void Slash(Vector3 center, float dir, bool big = false) => StartCoroutine(SlashRoutine(center, dir, big));

        IEnumerator SlashRoutine(Vector3 c, float dir, bool big)
        {
            float s = big ? 1.5f : 1f;
            var go = Shape(PrimitiveType.Cube, big ? hitMat : swordMat, c, new Vector3(0.12f, 1.6f * s, 0.12f), Quaternion.identity);
            for (float t = 0f; t < 0.14f; t += Time.deltaTime)
            {
                float k = t / 0.14f;
                go.transform.rotation = Quaternion.Euler(0f, 0f, dir * Mathf.Lerp(70f, -70f, k));
                go.transform.localScale = new Vector3(Mathf.Lerp(0.18f, 0.04f, k), 1.6f * s, 0.12f);
                yield return null;
            }
            Destroy(go);
        }

        /// <summary>앞으로 날아가는 검기·탄</summary>
        public void Projectile(Vector3 from, float dir, float distance, float speed, bool magic, System.Action<Vector3> onTick = null) =>
            StartCoroutine(ProjectileRoutine(from, dir, distance, speed, magic, onTick));

        IEnumerator ProjectileRoutine(Vector3 from, float dir, float distance, float speed, bool magic, System.Action<Vector3> onTick)
        {
            var go = magic
                ? Shape(PrimitiveType.Sphere, magicMat, from, Vector3.one * 0.45f, Quaternion.identity)
                : Shape(PrimitiveType.Cube, swordMat, from, new Vector3(0.15f, 1.3f, 0.3f), Quaternion.Euler(0, 0, dir * -15f));
            for (float d = 0f; d < distance; d += speed * Time.deltaTime)
            {
                go.transform.position = from + new Vector3(dir * d, 0f, 0f);
                onTick?.Invoke(go.transform.position);
                yield return null;
            }
            Destroy(go);
        }

        /// <summary>바닥에서 퍼지는 고리 (범위 베기·폭발)</summary>
        public void Ring(Vector3 center, float radius, bool magic, float time = 0.3f) => StartCoroutine(RingRoutine(center, radius, magic, time));

        IEnumerator RingRoutine(Vector3 c, float radius, bool magic, float time)
        {
            var go = Shape(PrimitiveType.Cylinder, magic ? magicMat : swordMat, c, new Vector3(0.2f, 0.04f, 0.2f), Quaternion.identity);
            for (float t = 0f; t < time; t += Time.deltaTime)
            {
                float k = t / time, d = Mathf.Lerp(0.3f, radius * 2f, 1f - (1f - k) * (1f - k));
                go.transform.localScale = new Vector3(d, Mathf.Lerp(0.6f, 0.02f, k), d);
                yield return null;
            }
            Destroy(go);
        }

        /// <summary>터지는 구 (마법 폭발)</summary>
        public void Burst(Vector3 center, float radius, bool magic, float time = 0.25f) => StartCoroutine(BurstRoutine(center, radius, magic, time));

        IEnumerator BurstRoutine(Vector3 c, float radius, bool magic, float time)
        {
            var go = Shape(PrimitiveType.Sphere, magic ? magicMat : hitMat, c, Vector3.one * 0.2f, Quaternion.identity);
            for (float t = 0f; t < time; t += Time.deltaTime)
            {
                float k = t / time;
                go.transform.localScale = Vector3.one * Mathf.Lerp(0.3f, radius * 2f, Mathf.Sqrt(k)) * (1f - k * 0.6f);
                yield return null;
            }
            Destroy(go);
        }

        /// <summary>바닥 장판 (지속 피해)</summary>
        public void Zone(Vector3 center, float halfWidth, float duration) => StartCoroutine(ZoneRoutine(center, halfWidth, duration));

        IEnumerator ZoneRoutine(Vector3 c, float hw, float duration)
        {
            var go = Shape(PrimitiveType.Cube, magicMat, c + Vector3.up * 0.03f, new Vector3(0.1f, 0.06f, 1.6f), Quaternion.identity);
            for (float t = 0f; t < duration; t += Time.deltaTime)
            {
                float open = Mathf.Clamp01(t / 0.2f), close = Mathf.Clamp01((duration - t) / 0.25f);
                float pulse = 1f + Mathf.Sin(t * 12f) * 0.05f;
                go.transform.localScale = new Vector3(hw * 2f * Mathf.Min(open, close), 0.06f * pulse, 1.6f);
                yield return null;
            }
            Destroy(go);
        }

        /// <summary>위에서 떨어지는 구 (광역 연속 공격)</summary>
        public void Meteor(Vector3 ground, System.Action onLand) => StartCoroutine(MeteorRoutine(ground, onLand));

        IEnumerator MeteorRoutine(Vector3 g, System.Action onLand)
        {
            var go = Shape(PrimitiveType.Sphere, magicMat, g + new Vector3(-1.5f, 6f, 0f), Vector3.one * 0.5f, Quaternion.identity);
            Vector3 from = go.transform.position;
            for (float t = 0f; t < 0.3f; t += Time.deltaTime)
            {
                go.transform.position = Vector3.Lerp(from, g, t / 0.3f);
                yield return null;
            }
            Destroy(go);
            Burst(g + Vector3.up * 0.3f, 1.1f, true, 0.2f);
            onLand?.Invoke();
        }
    }
}
