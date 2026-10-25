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

        private void Awake()
        {
            _instance = this;
            _pool = new ComponentPool<Projectile>(prefab, transform, prewarm: 32);
        }

        public static void Fire(Vector2 origin, Vector2 direction, float speed, DamageInfo damage, LayerMask playerMask)
        {
            if (_instance == null) return;
            Projectile p = _instance._pool.Get(origin, Quaternion.identity);
            p.Launch(direction, speed, damage, playerMask, _instance.wallMask, pierce: 0, ricochet: 0, _instance._pool.Release);
        }
    }
}
