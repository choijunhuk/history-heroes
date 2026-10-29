using System.Collections.Generic;
using HistoryHeroes.Combat;
using UnityEngine;

namespace HistoryHeroes.Player
{
    /// <summary>
    /// 기본 공격 표적 고르기 (Docs/GameDesign.md 3-2).
    /// 사거리 안 + 벽에 가리지 않은 적 중 가장 가까운 적. 표적이 너무 자주 바뀌지 않게
    /// 지금 표적이 가장 가까운 적보다 25% 이내로 멀면 그대로 둔다.
    /// </summary>
    public sealed class TargetSelector
    {
        private const float StickinessRatio = 1.25f;

        private readonly List<Collider2D> _buffer = new List<Collider2D>(64);
        private readonly LayerMask _enemyMask;
        private readonly LayerMask _wallMask;
        private ContactFilter2D _filter;

        public Health Current { get; private set; }

        public TargetSelector(LayerMask enemyMask, LayerMask wallMask)
        {
            _enemyMask = enemyMask;
            _wallMask = wallMask;
            _filter = new ContactFilter2D { useLayerMask = true, layerMask = _enemyMask, useTriggers = true };
        }

        /// <summary>찌르기는 표적 탐색과 명중에 같은 중심 거리를 쓴다.</summary>
        public static bool IsWithinRange(Vector2 origin, Vector2 point, float range)
            => (point - origin).sqrMagnitude <= range * range;

        public Health Find(Vector2 origin, float range, bool requireCenterInRange = false)
        {
            _buffer.Clear();
            Physics2D.OverlapCircle(origin, range, _filter, _buffer);

            Health nearest = null;
            float nearestSqr = float.MaxValue;
            float currentSqr = float.MaxValue;

            for (int i = 0; i < _buffer.Count; i++)
            {
                if (!_buffer[i].TryGetComponent(out Health health) || health.IsDead) continue;

                Vector2 to = (Vector2)health.transform.position - origin;
                if (requireCenterInRange && !IsWithinRange(origin, health.transform.position, range)) continue;
                if (Physics2D.Raycast(origin, to.normalized, to.magnitude, _wallMask).collider != null) continue;

                float sqr = to.sqrMagnitude;
                if (health == Current) currentSqr = sqr;
                if (sqr < nearestSqr)
                {
                    nearestSqr = sqr;
                    nearest = health;
                }
            }

            bool keepCurrent = Current != null && currentSqr < float.MaxValue &&
                               currentSqr <= nearestSqr * StickinessRatio * StickinessRatio;
            Current = keepCurrent ? Current : nearest;
            return Current;
        }
    }
}
