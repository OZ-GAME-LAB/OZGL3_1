using UnityEngine;

namespace OZ.UI.Contracts
{
    /// <summary>
    /// 적 프리팹에 붙이면 머리 위 체력바가 자동으로 붙었다 떨어진다. 코드 한 줄도 필요 없음.
    ///   - 같은 오브젝트(또는 부모)에 IEnemyHealthSource 구현 컴포넌트가 있어야 함
    ///   - PoolManager로 꺼냈다 넣는 적도 그대로 동작: 꺼낼 때(OnEnable) 등록, 넣을 때(OnDisable) 해제
    ///   - UI가 적보다 늦게 뜨는 경우를 위해 UI 등록 시점에 한 번 더 시도
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("OZ/UI/Enemy Health Bar Tracker")]
    public class EnemyHealthBarTracker : MonoBehaviour
    {
        IEnemyHealthSource _source;
        bool _tracked;

        void Awake() => _source = FindSource();

        IEnemyHealthSource FindSource()
        {
            var s = GetComponent<IEnemyHealthSource>();
            return s ?? GetComponentInParent<IEnemyHealthSource>();
        }

        void OnEnable()
        {
            if (_source == null) _source = FindSource();
            if (_source == null)
            {
                Debug.LogWarning($"[EnemyHealthBarTracker] {name}: IEnemyHealthSource 구현 컴포넌트가 없어 체력바를 붙이지 못했습니다.", this);
                return;
            }
            Track();
        }

        void Start()
        {
            // UIRoot가 이 적보다 늦게 생성된 씬: 처음 OnEnable 때는 Null API였을 수 있음
            if (!_tracked && _source != null) Track();
        }

        void Track()
        {
            if (!GameUI.IsReady) return;
            GameUI.Damage.TrackEnemy(_source);
            _tracked = true;
        }

        void OnDisable()
        {
            if (_source != null) GameUI.Damage.UntrackEnemy(_source);
            _tracked = false;
        }
    }
}
