using UnityEngine;


// 판정 없는 연출용 이펙트(베기 궤적, 폭발 불꽃 등). 일정 시간 뒤 자동 회수
public class SkillAutoRelease : MonoBehaviour
{
    [SerializeField, Min(0f)] private float _lifeTime = 0.3f;
    private float _releaseTime;

    private void OnEnable()
    {
        _releaseTime = Time.time + _lifeTime;
    }

    private void Update()
    {
        if (Time.time >= _releaseTime)
        {
            SkillSpawner.Release(gameObject);
        }
    }
}
