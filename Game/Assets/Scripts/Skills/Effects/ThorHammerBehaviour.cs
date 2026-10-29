using System.Collections.Generic;
using HistoryHeroes.Combat;
using HistoryHeroes.Common;
using HistoryHeroes.Enemy;
using UnityEngine;

namespace HistoryHeroes.Skills
{
    /// <summary>기존 왕복 판정을 빌려 쓰고, 진화한 망치만 귀환 지점에 작은 낙뢰를 내린다.</summary>
    public sealed class ThorHammerBehaviour : ActiveSkillBehaviour
    {
        [SerializeField] private GameObject hammerModel;
        [SerializeField] private float distance = 4.5f;
        [SerializeField] private float outTime = 0.45f;
        [SerializeField] private float searchRange = 7f;
        [SerializeField] private Color hammerColor = new Color(0.67f, 0.78f, 0.86f, 0.9f);
        [SerializeField] private Color lightningColor = new Color(0.5f, 0.82f, 1f, 0.55f);

        private readonly List<EnemyBase> _hits = new List<EnemyBase>(8);
        private ComponentPool<BoomerangMover> _pool;

        protected override bool Activate()
        {
            if (player.Health.IsDead) return false;
            Vector2 origin = player.transform.position;
            EnemyBase target = CombatQuery.Nearest(origin, searchRange);
            if (target == null) return false;
            Vector2 aim = ((Vector2)target.transform.position - origin).normalized;
            if (aim.sqrMagnitude < 0.0001f) aim = Vector2.up;

            _pool ??= BoomerangMover.CreatePool(EffectRoot, hammerColor, new Vector2(0.6f, 0.35f), hammerModel);
            _pool.Get(origin, Quaternion.identity).Launch(player.transform, player.Stats, enemyMask, origin, aim,
                distance, outTime, data.DamageAt(level), false, null, _pool.Release, distance / outTime * 1.3f,
                sourceId: data.id, onReturn: data.returnImpactCoefficient > 0f ? ReturnStrike : null);
            return true;
        }

        private void ReturnStrike(Vector2 center)
        {
            if (player == null || player.Health.IsDead) return;
            float radius = data.RadiusAt(level);
            AttackFx.Burst(center, radius, lightningColor);
            CombatQuery.EnemiesWithin(center, radius, _hits);
            foreach (EnemyBase enemy in _hits)
                enemy.Health.TryTakeDamage(RollDamage(data.returnImpactCoefficient));
        }
    }
}
