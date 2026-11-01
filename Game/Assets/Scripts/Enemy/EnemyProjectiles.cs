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
        [SerializeField] private Projectile lightningPrefab;
        [SerializeField] private LayerMask wallMask;

        private static EnemyProjectiles _instance;
        private ComponentPool<Projectile> _pool;
        private ComponentPool<Projectile> _lightningPool;
        private readonly List<Projectile> _active = new List<Projectile>();

        private static readonly List<Projectile> Empty = new List<Projectile>();

        /// <summary>날아가는 적 투사체 (읽기만, 검증 벤치의 피하기 AI용)</summary>
        public static IReadOnlyList<Projectile> Active => _instance != null ? _instance._active : Empty;

        /// <summary>적 투사체 빠르기 배수 (무한 주간 변형·일일 도전 변형 규칙). 장면마다 1로 시작</summary>
        public static float SpeedScale { get; set; } = 1f;

        private void Awake()
        {
            _instance = this;
            SpeedScale = 1f;
            _slowRadius = 0f;
            _pool = new ComponentPool<Projectile>(prefab, transform, prewarm: 32);
            if (lightningPrefab != null) _lightningPool = new ComponentPool<Projectile>(lightningPrefab, transform, prewarm: 32);
        }

        /// <param name="owner">쏜 적 (마렝고 Lv5가 되돌려 보낼 곳)</param>
        private static bool _volleying;

        public static void Fire(Vector2 origin, Vector2 direction, float speed, DamageInfo damage, LayerMask playerMask, Component owner = null)
        {
            if (_instance == null) return;
            if (!_volleying && EliteAffixes.IsVolley(owner))
            {
                // 엘리트 "연사하는": 3갈래 (양옆 16°)
                _volleying = true;
                Fire(origin, Quaternion.Euler(0f, 0f, 16f) * direction, speed, damage, playerMask, owner);
                Fire(origin, Quaternion.Euler(0f, 0f, -16f) * direction, speed, damage, playerMask, owner);
                _volleying = false;
            }

            // 모양만 달리하고 막기·둔화·되돌리기는 같은 활성 목록과 판정을 쓴다.
            bool lightning = owner is SteelColossusBoss boss && boss.LightningBarrage && _instance._lightningPool != null;
            Projectile p = (lightning ? _instance._lightningPool : _instance._pool).Get(origin, Quaternion.identity);
            p.Launch(direction, speed * SpeedScale, damage, playerMask, _instance.wallMask, pierce: 0, ricochet: 0,
                lightning ? _instance.OnLightningReleased : _instance.OnReleased);
            p.Owner = owner;
            _instance._active.Add(p);
            if (owner is EnemyBase enemy && enemy.Visual != null) enemy.Visual.Attack(direction); // 쏘는 동작 (탄막은 0.2초에 한 번, 탄마다 찾지 않는다)
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

        // ---------------------------------------------------------------- 제갈량 동남풍 (확장: 새 영웅)

        private static Vector2 _slowCenter;
        private static float _slowRadius;
        private static float _slowFactor = 1f;

        /// <summary>이번 프레임: center 반경 radius 안의 적 탄은 factor 빠르기 (영웅이 멈춰 있는 동안 매 프레임 부른다)</summary>
        public static void SetSlowZone(Vector2 center, float radius, float factor)
        {
            _slowCenter = center;
            _slowRadius = radius;
            _slowFactor = factor;
        }

        public static void ClearSlowZone() => _slowRadius = 0f;

        private void LateUpdate()
        {
            float sqr = _slowRadius * _slowRadius;
            for (int i = 0; i < _active.Count; i++)
            {
                Projectile p = _active[i];
                if (p == null) continue;
                bool inside = _slowRadius > 0f && ((Vector2)p.transform.position - _slowCenter).sqrMagnitude <= sqr;
                p.SlowFactor = inside ? _slowFactor : 1f;
            }
        }

        private void OnReleased(Projectile p)
        {
            _active.Remove(p);
            _pool.Release(p);
        }

        private void OnLightningReleased(Projectile p)
        {
            _active.Remove(p);
            _lightningPool.Release(p);
        }
    }
}
