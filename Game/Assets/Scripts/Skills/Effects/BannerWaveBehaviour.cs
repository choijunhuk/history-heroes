using System.Collections;
using System.Collections.Generic;
using HistoryHeroes.Combat;
using HistoryHeroes.Enemy;
using UnityEngine;

namespace HistoryHeroes.Skills
{
    /// <summary>잔 다르크 전용 파동. 깃발이 없을 때도 영웅 자리에서 이동 중 발동한다.</summary>
    public sealed class BannerWaveBehaviour : ActiveSkillBehaviour
    {
        [SerializeField] private float waveInterval = 0.4f;
        [SerializeField] private float pushDistance = 0.4f;
        [SerializeField] private Color waveColor = new Color(0.65f, 0.8f, 1f, 0.35f);

        private readonly List<EnemyBase> _hits = new List<EnemyBase>();

        protected override bool Activate()
        {
            if (player.Health.IsDead) return false;
            Vector2 center = player.HasBanner ? player.BannerCenter : (Vector2)player.transform.position;
            float radius = data.RadiusAt(level);
            CombatQuery.EnemiesWithin(center, radius, _hits);
            if (_hits.Count == 0) return false;
            StartCoroutine(Waves(center, radius, data.CountAt(level), data.DamageAt(level)));
            return true;
        }

        private IEnumerator Waves(Vector2 center, float radius, int count, float coefficient)
        {
            for (int i = 0; i < count; i++)
            {
                while (IsPaused) yield return null;
                if (player == null || player.Health.IsDead) yield break;
                // 발동 때의 중심을 고정해, 이동해도 파동이 적을 따라가지 않게 한다.
                AttackFx.Burst(center, radius, waveColor, 0.25f);
                CombatQuery.EnemiesWithin(center, radius, _hits);
                foreach (EnemyBase enemy in _hits)
                {
                    if (!enemy.Health.TryTakeDamage(RollDamage(coefficient)) || enemy.Health.IsDead) continue;
                    if (!enemy.CanBeKnockedBack || !enemy.TryGetComponent(out Rigidbody2D body)) continue;
                    Vector2 direction = (body.position - center).normalized;
                    RaycastHit2D wall = Physics2D.Raycast(body.position, direction, pushDistance + 0.4f, player.WallMask);
                    float distance = wall.collider != null ? Mathf.Max(0f, wall.distance - 0.4f) : pushDistance;
                    enemy.Push(direction * distance);
                }

                if (i < count - 1) yield return new WaitForSeconds(waveInterval);
            }
        }
    }
}
