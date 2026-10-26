using System.Collections.Generic;
using HistoryHeroes.Enemy;
using UnityEngine;

namespace HistoryHeroes.Skills
{
    /// <summary>액티브 스킬 표적 고르기 (화면 안의 적, 무작위 적).</summary>
    public static class CombatQuery
    {
        private static readonly List<EnemyBase> _buffer = new List<EnemyBase>();

        public static bool IsOnScreen(Vector2 position, float margin = 0f)
        {
            Camera camera = Camera.main;
            if (camera == null) return true;
            Vector3 v = camera.WorldToViewportPoint(position);
            return v.x >= -margin && v.x <= 1f + margin && v.y >= -margin && v.y <= 1f + margin;
        }

        public static void EnemiesOnScreen(List<EnemyBase> result)
        {
            result.Clear();
            IReadOnlyList<EnemyBase> alive = EnemyBase.Alive;
            for (int i = 0; i < alive.Count; i++)
            {
                EnemyBase e = alive[i];
                if (e != null && !e.Health.IsDead && IsOnScreen(e.transform.position)) result.Add(e);
            }
        }

        /// <summary>가장 가까운 살아 있는 적 (maxDistance 안). 없으면 null</summary>
        public static EnemyBase Nearest(Vector2 from, float maxDistance = float.MaxValue)
        {
            EnemyBase best = null;
            float bestSqr = maxDistance * maxDistance;
            IReadOnlyList<EnemyBase> alive = EnemyBase.Alive;
            for (int i = 0; i < alive.Count; i++)
            {
                EnemyBase e = alive[i];
                if (e == null || e.Health.IsDead) continue;
                float d = ((Vector2)e.transform.position - from).sqrMagnitude;
                if (d < bestSqr)
                {
                    bestSqr = d;
                    best = e;
                }
            }

            return best;
        }

        /// <summary>화면 안에서 적이 가장 많이 몰린 곳 (그 적의 위치). 없으면 false (대포 포격, 유성 낙하)</summary>
        public static bool DensestPoint(float radius, out Vector2 point)
        {
            EnemiesOnScreen(_buffer);
            point = default;
            if (_buffer.Count == 0) return false;

            float radiusSqr = radius * radius;
            int bestCount = -1;
            for (int i = 0; i < _buffer.Count; i++)
            {
                Vector2 p = _buffer[i].transform.position;
                int count = 0;
                for (int j = 0; j < _buffer.Count; j++)
                {
                    if (((Vector2)_buffer[j].transform.position - p).sqrMagnitude <= radiusSqr) count++;
                }

                if (count > bestCount)
                {
                    bestCount = count;
                    point = p;
                }
            }

            return true;
        }

        /// <summary>반지름 안의 살아 있는 적 (물리 검사 없이 목록에서)</summary>
        public static void EnemiesWithin(Vector2 center, float radius, List<EnemyBase> result)
        {
            result.Clear();
            float radiusSqr = radius * radius;
            IReadOnlyList<EnemyBase> alive = EnemyBase.Alive;
            for (int i = 0; i < alive.Count; i++)
            {
                EnemyBase e = alive[i];
                if (e != null && !e.Health.IsDead && ((Vector2)e.transform.position - center).sqrMagnitude <= radiusSqr) result.Add(e);
            }
        }

        /// <summary>화면 안에서 서로 다른 적을 최대 count명 고른다 (적이 적으면 그만큼만).</summary>
        public static void RandomEnemiesOnScreen(int count, List<EnemyBase> result)
        {
            EnemiesOnScreen(_buffer);
            result.Clear();
            for (int i = 0; i < count && _buffer.Count > 0; i++)
            {
                int index = Random.Range(0, _buffer.Count);
                result.Add(_buffer[index]);
                _buffer[index] = _buffer[_buffer.Count - 1];
                _buffer.RemoveAt(_buffer.Count - 1);
            }
        }
    }
}
