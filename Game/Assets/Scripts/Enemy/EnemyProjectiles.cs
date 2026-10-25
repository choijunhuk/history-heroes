using System.Collections.Generic;
using HistoryHeroes.Combat;
using HistoryHeroes.Common;
using UnityEngine;

namespace HistoryHeroes.Enemy
{
    /// <summary>적 투사체 풀. 장면에 하나 둔다. (적 투사체는 빨강·보라 계열, Art.md 5장)</summary>
    public sealed class EnemyProjectiles : MonoBehaviour
    {
        [SerializeField] private Projectile prefab;
        [SerializeField] private LayerMask wallMask;

        private static EnemyProjectiles _instance;
        private ComponentPool<Projectile> _pool;
        private readonly List<Projectile> _active = new List<Projectile>();

        private void Awake()
        {
            _instance = this;
            _pool = new ComponentPool<Projectile>(prefab, transform, prewarm: 32);
        }

        public static void Fire(Vector2 origin, Vector2 direction, float speed, DamageInfo damage, LayerMask playerMask)
        {
            if (_instance == null) return;
            Projectile p = _instance._pool.Get(origin, Quaternion.identity);
            p.Launch(direction, speed, damage, playerMask, _instance.wallMask, pierce: 0, ricochet: 0, _instance.OnReleased);
            _instance._active.Add(p);
        }

        /// <summary>point 반경 안의 적 투사체 하나를 없앤다 (강철 회오리 칼날의 투사체 막기).</summary>
        public static bool TryBlock(Vector2 point, float radius)
        {
            if (_instance == null) return false;
            List<Projectile> active = _instance._active;
            float sqr = radius * radius;
            for (int i = active.Count - 1; i >= 0; i--)
            {
                if (((Vector2)active[i].transform.position - point).sqrMagnitude > sqr) continue;
                active[i].Cancel(); // → OnReleased에서 목록에서 빠진다
                return true;
            }

            return false;
        }

        private void OnReleased(Projectile p)
        {
            _active.Remove(p);
            _pool.Release(p);
        }
    }
}
