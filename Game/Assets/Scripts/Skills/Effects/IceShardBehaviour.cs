using HistoryHeroes.Combat;
using HistoryHeroes.Enemy;
using HistoryHeroes.Stats;
using UnityEngine;

namespace HistoryHeroes.Skills
{
    /// <summary>
    /// 얼음 파편: 3초마다 가장 가까운 적에게 파편 3개 (각 50%, 둔화 30%), 레벨마다 +1개 (Skills.md 2장).
    /// 서리 창 (진화, chill): 파편 7개 각 60%, 둔화 40%. 둔화 3번 쌓이면 1초 빙결 → 빙결한 적에게 치명타 확정 (4-4).
    /// </summary>
    public sealed class IceShardBehaviour : ActiveSkillBehaviour
    {
        [SerializeField] private Projectile shardPrefab;
        [SerializeField] private float fanDegrees = 30f;
        [SerializeField] private float speed = 13f;
        [SerializeField] private float searchRange = 9f;
        [SerializeField, Range(0f, 1f)] private float slow = 0.3f;
        [SerializeField] private float slowDuration = 2f;

        [Header("서리 창")]
        [SerializeField] private bool chill;
        [SerializeField] private int stacksToFreeze = 3;
        [SerializeField] private float freezeDuration = 1f;

        protected override bool Activate()
        {
            EnemyBase target = CombatQuery.Nearest(player.transform.position, searchRange);
            if (target == null) return false;

            Vector2 origin = player.transform.position;
            Vector2 aim = ((Vector2)target.transform.position - origin).normalized;
            int count = data.CountAt(level);
            float step = count > 1 ? fanDegrees / (count - 1) : 0f;
            for (int i = 0; i < count; i++)
            {
                Vector2 direction = Quaternion.Euler(0f, 0f, count > 1 ? -fanDegrees * 0.5f + step * i : 0f) * aim;
                DamageInfo damage = RollDamage(data.DamageAt(level));
                if (shardPrefab != null) player.LaunchSkillProjectile(shardPrefab, origin, direction, speed, damage, 0, OnHit);
                else player.LaunchSkillProjectile(origin, direction, speed, damage, 0, OnHit);
            }

            return true;
        }

        private void OnHit(Health target, Vector2 point)
        {
            if (!target.TryGetComponent(out EnemyStatus status)) return;

            // 서리 창: 이미 얼어 있던 적에게는 치명타 확정 (치명타가 안 떴으면 그만큼 더 준다)
            if (chill && status.IsFrozen && !target.IsDead)
            {
                float critExtra = player.Stats.Get(StatType.CritDamage, StatTarget.Active) - 1f;
                if (critExtra > 0f) target.TryTakeDamage(RollDamage(data.DamageAt(level) * critExtra));
            }

            status.ApplySlow(slow, slowDuration);
            if (chill) status.AddChill(stacksToFreeze, freezeDuration);
        }
    }
}
