using System.Collections.Generic;
using UnityEngine;

namespace OZ.UI.Samples
{
    /// <summary>
    /// [팀 설계 PoolManager 모양 샘플] Get&lt;T&gt;() / Release().
    /// UI 쪽에서 볼 점: 풀에서 꺼내고 넣는 적도 EnemyHealthBarTracker만 붙어 있으면 체력바가 알아서 따라붙고 떨어진다.
    /// </summary>
    [AddComponentMenu("OZ/UI/Samples/Flow/Sample Pool Manager")]
    public class SamplePoolManager : MonoBehaviour
    {
        public static SamplePoolManager Instance { get; private set; }

        readonly Dictionary<Component, Stack<Component>> _free = new Dictionary<Component, Stack<Component>>();
        readonly Dictionary<Component, Component> _prefabOf = new Dictionary<Component, Component>();
        Transform _root;

        public int CreatedCount { get; private set; }

        void Awake()
        {
            Instance = this;
            _root = new GameObject("Pool").transform;
            _root.SetParent(transform, false);
        }

        void OnDestroy() { if (Instance == this) Instance = null; }

        /// <summary>미리 만들어 두기 (로딩 화면 동안)</summary>
        public void Prewarm<T>(T prefab, int count) where T : Component
        {
            var list = new List<T>();
            for (int i = 0; i < count; i++) list.Add(Get(prefab, new Vector3(0f, -100f, 0f)));
            foreach (var o in list) Release(o);
        }

        public T Get<T>(T prefab, Vector3 position) where T : Component
        {
            if (!_free.TryGetValue(prefab, out var stack)) _free[prefab] = stack = new Stack<Component>();
            T obj;
            if (stack.Count > 0) obj = (T)stack.Pop();
            else
            {
                obj = Instantiate(prefab, _root);
                obj.name = prefab.name;
                _prefabOf[obj] = prefab;
                CreatedCount++;
            }
            obj.transform.position = position;
            obj.gameObject.SetActive(true);
            return obj;
        }

        public void Release(Component obj)
        {
            if (obj == null) return;
            if (!_prefabOf.TryGetValue(obj, out var prefab)) { Destroy(obj.gameObject); return; }
            if (!obj.gameObject.activeSelf) return; // 두 번 반납 방지
            obj.gameObject.SetActive(false);
            _free[prefab].Push(obj);
        }
    }
}
