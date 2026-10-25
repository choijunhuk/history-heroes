using System.Collections;
using System.Collections.Generic;
using HistoryHeroes.Combat;
using HistoryHeroes.Enemy;
using HistoryHeroes.Stats;
using UnityEngine;

namespace HistoryHeroes.Skills
{
    /// <summary>
    /// 관우 전용 — 청룡 회전 베기: 6초마다 제자리 회전 베기 (주변 반지름 2.5, 180%), 레벨마다 피해 +25% (Skills.md 3장).
    /// 오관참장 (진화, spins 5): 회전 베기 5번 연속 (회당 120%), 도는 동안 받는 피해 -50% (Skills.md 4-4).
    /// 주변에 적이 없으면 아끼고 다시 기다린다.
    /// </summary>
    public sealed class SpinSlashBehaviour : ActiveSkillBehaviour
    {
        [SerializeField, Min(1)] private int spins = 1;
        [SerializeField] private float spinInterval = 0.3f;
        [Tooltip("도는 동안 받는 피해 감소 (오관참장 0.5)")]
        [SerializeField, Range(0f, 0.9f)] private float guardWhileSpinning;
        [SerializeField] private Color slashColor = new Color(0.55f, 1f, 0.6f, 0.6f);

        private readonly List<EnemyBase> _hits = new List<EnemyBase>();
        private readonly object _guard = new object();

        protected override bool Activate()
        {
            float radius = data.RadiusAt(level);
            CombatQuery.EnemiesWithin(player.transform.position, radius, _hits);
            if (_hits.Count == 0) return false;
            StartCoroutine(Spin(radius));
            return true;
        }

        private IEnumerator Spin(float radius)
        {
            if (guardWhileSpinning > 0f)
            {
                player.Stats.Add(new StatModifier(StatType.DamageReduction, ModifierKind.Flat, guardWhileSpinning, ModifierSource.Skill, _guard));
            }

            for (int s = 0; s < spins; s++)
            {
                Vector2 center = player.transform.position;
                AttackFx.Arc(center, Quaternion.Euler(0f, 0f, s * 72f) * Vector2.right, radius, 360f, slashColor, 0.2f);
                CombatQuery.EnemiesWithin(center, radius, _hits);
                foreach (EnemyBase e in _hits) e.Health.TryTakeDamage(RollDamage(data.DamageAt(level)));
                if (s < spins - 1) yield return new WaitForSeconds(spinInterval);
            }

            player.Stats.RemoveAllFrom(_guard);
        }

        protected override void OnDestroy()
        {
            if (player != null) player.Stats.RemoveAllFrom(_guard);
            base.OnDestroy();
        }
    }
}
