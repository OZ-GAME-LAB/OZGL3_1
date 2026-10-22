using UnityEngine;


// 지연 후 범위 폭발 1회 (연쇄 폭발의 폭발 1개)
public class SkillExplosion : MonoBehaviour
{
    [Header("Hit")]
    [SerializeField, Min(0.01f)] private float _radius = 1.5f;
    [SerializeField] private LayerMask _enemyLayer;

    [Header("Timing")]
    [SerializeField, Min(0f)] private float _lingerTime = 0.3f; // 터진 뒤 연출이 남아 있는 시간

    [Header("Visual")]
    [SerializeField] private GameObject _warningVisual; // 터지기 전 예고 (바닥 원 등)
    [SerializeField] private GameObject _blastVisual;   // 폭발 연출

    private float _damage;
    private float _detonateTime;
    private float _releaseTime;

    //_waiting(터지기 전) → _exploded(터진 후)
    private bool _waiting;
    private bool _exploded;

    private Vector3 Center { get { return transform.position + Vector3.up * _radius * 0.5f; } }

    private void OnDisable()
    {
        _waiting = false;
        _exploded = false;
    }
    private void Update()
    {
        if (_waiting && Time.time >= _detonateTime)
        {
            Detonate();
            return;
        }
        if (_exploded && Time.time >= _releaseTime)
        {
            _exploded = false;
            SkillSpawner.Release(gameObject);
        }
    }

    public void Init(float damage, float delay)
    {
        _damage = damage;
        _detonateTime = Time.time + delay;
        _waiting = true;
        _exploded = false;
        SetVisual(warning: true);
    }

    private void Detonate()
    {
        _waiting = false;
        _exploded = true;
        _releaseTime = Time.time + _lingerTime;
        SkillUtil.DamageSphere(Center, _radius, _enemyLayer, _damage);
        SetVisual(warning: false);
    }
    private void SetVisual(bool warning)
    {
        // 이펙트없어도 판정 테스트는 되도록
        if (_warningVisual != null)
        {
            _warningVisual.SetActive(warning);
        }
        if (_blastVisual != null)
        {
            _blastVisual.SetActive(!warning);
        }
    }
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(Center, _radius);
    }
}
