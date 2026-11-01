using System.Collections;
using System.Collections.Generic;
using HistoryHeroes.Combat;
using HistoryHeroes.Enemy;
using UnityEngine;

namespace HistoryHeroes.Skills
{
    /// <summary>발동할 때 읽은 적 자리와 방향에 별빛을 놓는다. 이동하는 적을 계속 따라가지 않는다.</summary>
    public sealed class ConstellationBehaviour : ActiveSkillBehaviour
    {
        [SerializeField] private float searchRange = 9f;
        [SerializeField] private float starSpacing = 1.2f;
        [SerializeField] private float starInterval = 0.25f;
        [SerializeField] private Color starColor = new Color(0.85f, 0.65f, 1f, 0.28f);
        [SerializeField] private Color lineColor = new Color(1f, 0.85f, 0.5f, 0.45f);

        private readonly List<EnemyBase> _hits = new List<EnemyBase>();

        protected override bool Activate()
        {
            if (player.Health.IsDead) return false;
            Vector2 origin = player.transform.position;
            EnemyBase target = CombatQuery.Nearest(origin, searchRange);
            if (target == null) return false;
            Vector2 center = target.transform.position;
            StartCoroutine(Stars(center, center - origin, data.CountAt(level), data.RadiusAt(level), data.DamageAt(level)));
            return true;
        }

        /// <summary>목표점을 가운데 두고 같은 간격으로 배치한다. 적이 몸에 겹치면 위쪽을 기준으로 삼는다.</summary>
        public static Vector2 StarPosition(Vector2 center, Vector2 direction, int index, int count, float spacing)
        {
            Vector2 aim = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector2.up;
            return center + aim * ((index - (Mathf.Max(1, count) - 1) * 0.5f) * spacing);
        }

        private IEnumerator Stars(Vector2 center, Vector2 direction, int count, float radius, float coefficient)
        {
            Vector2 previous = center;
            for (int i = 0; i < count; i++)
            {
                while (IsPaused) yield return null;
                if (player == null || player.Health.IsDead) yield break;
                Vector2 point = StarPosition(center, direction, i, count, starSpacing);
                if (i > 0) AttackFx.Line(previous, point, 0.06f, lineColor, starInterval);
                AttackFx.Burst(point, radius, starColor, starInterval);
                CombatQuery.EnemiesWithin(point, radius, _hits);
                foreach (EnemyBase enemy in _hits) enemy.Health.TryTakeDamage(RollDamage(coefficient));
                previous = point;
                if (i < count - 1) yield return new WaitForSeconds(starInterval);
            }
        }
    }
}
