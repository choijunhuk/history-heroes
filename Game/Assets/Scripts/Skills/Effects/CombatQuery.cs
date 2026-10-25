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
