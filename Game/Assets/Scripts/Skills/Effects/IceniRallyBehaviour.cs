using System.Collections;
using System.Collections.Generic;
using HistoryHeroes.Combat;
using HistoryHeroes.Enemy;
using UnityEngine;

namespace HistoryHeroes.Skills
{
    /// <summary>부디카의 함성은 발동 순간의 자리를 지키며, 밀침은 기본 공격 대표 효과에만 맡긴다.</summary>
    public sealed class IceniRallyBehaviour : ActiveSkillBehaviour
    {
        [SerializeField] private float waveInterval = 0.3f;
        [SerializeField] private Color waveColor = new Color(0.55f, 0.78f, 0.42f, 0.35f);

        private readonly List<EnemyBase> _hits = new List<EnemyBase>();

        protected override bool Activate()
        {
            if (player.Health.IsDead) return false;
            Vector2 center = player.transform.position;
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
                AttackFx.Burst(center, radius, waveColor, 0.25f);
                CombatQuery.EnemiesWithin(center, radius, _hits);
                foreach (EnemyBase enemy in _hits)
                    enemy.Health.TryTakeDamage(RollDamage(coefficient));
                if (i < count - 1) yield return new WaitForSeconds(waveInterval);
            }
        }
    }
}
