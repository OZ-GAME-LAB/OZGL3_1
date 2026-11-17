using System.Collections.Generic;
using UnityEngine;

public class PlayerHitBlink : MonoBehaviour
{
    [SerializeField, Min(0.02f)] private float _interval = 0.08f; // 깜빡이는 간격(초). 작을수록 빠르게 깜빡임

    private readonly List<Renderer> _renderers = new List<Renderer>();
    private float _endTime;
    private float _nextToggleTime;
    private bool _visible = true;

    private void Awake()
    {
        // true: 지금 꺼져 있는 무기(검·스태프) 모델도 같이 모아둠
        //  -> 무기는 PlayerWeapon이 GameObject를 SetActive(false)로 숨기기 때문에
        //     여기서 renderer.enabled를 켜도 안 보임 = 무기 상태를 건드리지 않음
        foreach (Renderer r in GetComponentsInChildren<Renderer>(true))
        {
            // 이펙트는 제외 -> 깜빡이면 공격, 스킬 이펙트가 끊겨 보임
            if (r is ParticleSystemRenderer || r is TrailRenderer || r is LineRenderer)
            {
                continue;
            }

            // 원래 꺼져 있던 렌더러는 제외 -> 깜빡임이 끝날 때 일부러 숨긴 메시를 켜버리지 않게
            if (!r.enabled)
            {
                continue;
            }

            _renderers.Add(r);
        }
    }

    // PlayerHealth가 피격 무적을 걸 때 호출
    public void Play(float duration)
    {
        // 무적과 같은 Time.time 기준 -> 무적이 끝나는 순간 깜빡임도 같이 끝남
        // 일시정지(timeScale 0) 때도 무적과 같이 멈춤
        _endTime = Time.time + duration;
        _nextToggleTime = Time.time;
    }

    // 재시작 등 즉시 원래대로 돌려야 할 때
    public void Stop()
    {
        _endTime = 0f;
        SetVisible(true);
    }

    private void Update()
    {
        if (Time.time >= _endTime)
        {
            // 끝났으면 반드시 보이는 상태로 마무리 (안 보이는 채로 끝나는 것 방지)
            if (!_visible)
            {
                SetVisible(true);
            }
            return;
        }

        if (Time.time >= _nextToggleTime)
        {
            SetVisible(!_visible);
            _nextToggleTime = Time.time + _interval;
        }
    }

    private void OnDisable()
    {
        // 깜빡이는 도중 플레이어가 꺼지면(스테이지 재시작 등) 투명한 상태로 남지 않게
        Stop();
    }

    private void SetVisible(bool visible)
    {
        _visible = visible;
        for (int i = 0; i < _renderers.Count; i++)
        {
            if (_renderers[i] != null)
            {
                _renderers[i].enabled = visible;
            }
        }
    }
}
