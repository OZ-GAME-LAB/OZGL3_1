using System.Collections;
using System.Collections.Generic;
using OZ.UI.Contracts;
using UnityEngine;
using UnityEngine.InputSystem;

namespace OZ.UI.Samples
{
    /// <summary>
    /// UI_Sandbox 전투 테스트: 가상의 적을 때려 피해 숫자·타격 이펙트·적 체력바를 확인.
    ///   Z 공격 (20% 치명타) / C 치명타 / W 약점 / L 5연타 / D 지속 피해(장판 6틱) / Y 빗나감 / 적 클릭
    ///   마지막 일격은 자동 '처치'. 종류마다 SandboxFeel 수치로 흰 번쩍임·히트스톱·화면 흔들림.
    ///   플레이어 피격(H)·회복(J) → 플레이어 머리 위 빨강/초록 숫자 + 피격 흔들림
    /// </summary>
    [AddComponentMenu("OZ/UI/Samples/Sandbox Combat")]
    public class SandboxCombat : MonoBehaviour
    {
        [SerializeField] internal DummyPlayer player;
        [SerializeField] internal Transform playerAvatar;
        [SerializeField] internal Transform playerHead;
        [SerializeField] internal List<DummyEnemy> enemies = new List<DummyEnemy>();
        [SerializeField] internal Vector2 normalDamage = new Vector2(14, 26);
        [SerializeField, Range(0f, 1f)] internal float critChance = 0.2f;
        [SerializeField] internal float critMultiplier = 2.2f;
        [SerializeField] internal float weaknessMultiplier = 2.8f;
        [SerializeField] internal Vector2 dotDamage = new Vector2(3, 6);
        [SerializeField] internal SandboxFeel feel;

        int _target;

        void OnEnable() { if (player != null) player.HealthChanged += OnPlayerHealth; }
        void OnDisable() { if (player != null) player.HealthChanged -= OnPlayerHealth; }

        void OnPlayerHealth(HealthChange c)
        {
            if (playerHead == null || Mathf.Approximately(c.Delta, 0f)) return;
            if (c.Previous <= 0f && c.Current >= c.Max) return; // 리셋은 숫자 생략
            GameUI.Damage.Show(playerHead.position, Mathf.Abs(c.Delta), c.IsDamage ? DamageKind.PlayerHurt : DamageKind.Heal);
            if (c.IsDamage && feel != null) feel.Apply(DamageKind.PlayerHurt, playerAvatar != null ? playerAvatar.GetComponent<HitFlash>() : null);
        }

        void Update()
        {
            if (UIState.IsGameplayInputBlocked) return;
            var kb = Keyboard.current;
            if (kb != null)
            {
                if (kb.zKey.wasPressedThisFrame) Attack(NextTarget(), Random.value < critChance ? DamageKind.Critical : DamageKind.Normal);
                if (kb.cKey.wasPressedThisFrame) Attack(NextTarget(), DamageKind.Critical);
                if (kb.wKey.wasPressedThisFrame) Attack(NextTarget(), DamageKind.Weakness);
                if (kb.dKey.wasPressedThisFrame) StartCoroutine(Dot(NextTarget(), 6, 0.3f));
                if (kb.lKey.wasPressedThisFrame) StartCoroutine(Combo(NextTarget(), 5));
                if (kb.yKey.wasPressedThisFrame) { var e = NextTarget(); if (e != null) e.Miss(); }
            }
            var mouse = Mouse.current;
            if (mouse != null && mouse.leftButton.wasPressedThisFrame && Camera.main != null)
            {
                Vector3 w = Camera.main.ScreenToWorldPoint(mouse.position.ReadValue());
                foreach (var e in enemies)
                    if (e != null && !e.IsDead && e.Contains(w)) { Attack(e, Random.value < critChance ? DamageKind.Critical : DamageKind.Normal); break; }
            }
        }

        DummyEnemy NextTarget()
        {
            for (int i = 0; i < enemies.Count; i++)
            {
                _target = (_target + 1) % enemies.Count;
                var e = enemies[_target];
                if (e != null && !e.IsDead) return e;
            }
            return null;
        }

        public void Attack(DummyEnemy e, DamageKind kind)
        {
            if (e == null) return;
            float dmg = Mathf.Round(Random.Range(normalDamage.x, normalDamage.y));
            if (kind == DamageKind.Critical) dmg = Mathf.Round(dmg * critMultiplier);
            if (kind == DamageKind.Weakness) dmg = Mathf.Round(dmg * weaknessMultiplier);
            var shown = e.TakeHit(dmg, kind);
            if (feel != null) feel.Apply(shown, e.flash);
        }

        IEnumerator Combo(DummyEnemy e, int hits)
        {
            for (int i = 0; i < hits && e != null && !e.IsDead; i++)
            {
                // 마지막 타는 치명타, 나머지는 낮은 확률로 치명타
                Attack(e, i == hits - 1 || Random.value < critChance * 0.5f ? DamageKind.Critical : DamageKind.Normal);
                yield return new WaitForSecondsRealtime(0.09f);
            }
        }

        IEnumerator Dot(DummyEnemy e, int ticks, float interval)
        {
            for (int i = 0; i < ticks && e != null && !e.IsDead; i++)
            {
                var shown = e.TakeHit(Mathf.Round(Random.Range(dotDamage.x, dotDamage.y)), DamageKind.DamageOverTime);
                if (feel != null) feel.Apply(shown, e.flash);
                yield return new WaitForSeconds(interval);
            }
        }
    }
}
