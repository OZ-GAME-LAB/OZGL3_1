using UnityEngine;


// 지속 피해 장판 (마력장)
public class SkillField : MonoBehaviour
{
    [Header("Area")]
    [SerializeField] private Vector3 _size = new Vector3(4f, 1.5f, 2f); // 바닥 기준 박스 크기
    [SerializeField] private LayerMask _enemyLayer;

    [Header("Tick")]
    [SerializeField, Min(1)] private int _tickCount = 5;
    [SerializeField, Min(0.05f)] private float _tickInterval = 0.5f; // 5틱 x 0.5초 = 약 2초 유지
    //틱 간격은 기획데이터에 없는 임시값. 플레이 테스트 후 조정

    private float _damagePerTick;
    private int _ticksDone;
    private float _nextTickTime;
    private bool _active;

    private Vector3 Center { get { return transform.position + Vector3.up * (_size.y * 0.5f); } }

    private void OnDisable()
    {
        _active = false;
    }

    private void Update()
    {
        if (!_active || Time.time < _nextTickTime)
        {
            return;
        }
        SkillUtil.DamageBox(Center, _size, _enemyLayer, _damagePerTick);
        _ticksDone++;
        _nextTickTime = Time.time + _tickInterval;
        if (_ticksDone >= _tickCount)
        {
            _active = false;
            SkillSpawner.Release(gameObject);
        }
    }

    public void Init(float totalDamage)
    {
        _damagePerTick = SkillUtil.SplitDamage(totalDamage, _tickCount);
        _ticksDone = 0;
        _nextTickTime = Time.time; //깔자마자 피해가 들어감
        _active = true;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.6f, 0.3f, 1f);
        Gizmos.DrawWireCube(Center, _size);
    }
}
