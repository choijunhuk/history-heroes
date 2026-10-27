using System;
using UnityEngine;

namespace HistoryHeroes.Combat
{
    /// <summary>
    /// 직선 투사체. 물리 엔진 대신 매 프레임 CircleCast로 앞을 검사한다.
    /// 그래서 빠른 화살도 벽을 뚫지 않고, 벽 튕김 방향(반사)을 정확히 구할 수 있다.
    /// </summary>
    public sealed class Projectile : MonoBehaviour
    {
        [SerializeField] private float radius = 0.15f;
        [SerializeField] private float lifetime = 3f;

        private Vector2 _direction;
        private float _speed;
        private DamageInfo _damage;
        private int _pierceLeft;
        private bool _piercing; // 관통 투사체인가 (마지막 대상도 방패를 뚫는다)
        private int _ricochetLeft;
        private float _pierceFalloff;
        private float _age;
        private LayerMask _hitMask;
        private LayerMask _wallMask;
        private Collider2D _lastHit;
        private Action<Projectile> _release;
        private Action<DamageInfo, float> _onHit;
        private Action<Health, Vector2> _onHitTarget;
        private TrailRenderer[] _trails;

        // 한 번에 여러 개를 받아서, 방금 뚫고 지나온 적은 빼고 가장 가까운 것을 고른다
        // (첫 결과가 방금 그 적이면 뒤의 벽을 못 보고 통과하던 문제 방지)
        private static readonly RaycastHit2D[] _hits = new RaycastHit2D[8];

        /// <summary>날아가는 방향·빠르기·피해 (마렝고가 막고 되돌리는 데, 벤치가 피하는 데 쓴다)</summary>
        public Vector2 Direction => _direction;
        public float Speed => _speed;

        /// <summary>빠르기 배수 (제갈량 동남풍: 둘레 적 탄 0.6). 쏠 때 1로</summary>
        public float SlowFactor { get; set; } = 1f;
        public DamageInfo Damage => _damage;
        /// <summary>쏜 쪽 (적 투사체: 쏜 적. 없으면 null)</summary>
        public Component Owner { get; set; }

        public void Launch(Vector2 direction, float speed, DamageInfo damage, LayerMask hitMask, LayerMask wallMask,
            int pierce, int ricochet, Action<Projectile> release, Action<DamageInfo, float> onHit = null,
            float pierceFalloff = 0.2f, Action<Health, Vector2> onHitTarget = null)
        {
            _onHitTarget = onHitTarget;
            _direction = direction.normalized;
            _speed = speed;
            SlowFactor = 1f;
            _damage = damage;
            _hitMask = hitMask;
            _wallMask = wallMask;
            _pierceLeft = pierce;
            _piercing = pierce > 0;
            _ricochetLeft = ricochet;
            _pierceFalloff = pierceFalloff;
            _release = release;
            _onHit = onHit;
            _age = 0f;
            _lastHit = null;
            Owner = null;
            transform.right = _direction;
            // 꼬리 빛 (그래픽 평가 2번): 풀에서 다시 나온 투사체가 예전 자리에서 줄을 끌고 오지 않게
            _trails ??= GetComponentsInChildren<TrailRenderer>(true);
            foreach (TrailRenderer trail in _trails) trail.Clear();
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f) return;

            _age += dt;
            if (_age >= lifetime)
            {
                Release();
                return;
            }

            float distance = _speed * SlowFactor * dt;
            Vector2 position = transform.position;
            var filter = new ContactFilter2D { useLayerMask = true, layerMask = _hitMask | _wallMask, useTriggers = true };
            int count = Physics2D.CircleCast(position, radius, _direction, filter, _hits, distance);

            RaycastHit2D hit = default;
            float nearest = float.MaxValue;
            for (int i = 0; i < count; i++)
            {
                if (_hits[i].collider == _lastHit || _hits[i].distance >= nearest) continue;
                nearest = _hits[i].distance;
                hit = _hits[i];
            }

            if (hit.collider == null)
            {
                transform.position = position + _direction * distance;
                return;
            }

            if (((1 << hit.collider.gameObject.layer) & _wallMask) != 0)
            {
                if (hit.collider.TryGetComponent(out Dungeon.IShotTarget target))
                {
                    target.Hit(); // 부서지는 장애물·폭약 통·레버: 튕기지 않고 맞기만 (Modes 2-3, 확장 1차 2장)
                    Release();
                    return;
                }

                if (_ricochetLeft <= 0)
                {
                    Release();
                    return;
                }

                _ricochetLeft--;
                _direction = Vector2.Reflect(_direction, hit.normal).normalized;
                transform.position = hit.centroid + hit.normal * 0.01f;
                transform.right = _direction;
                _lastHit = null;
                return;
            }

            transform.position = hit.centroid;
            _lastHit = hit.collider;

            _damage.Direction = _direction;
            _damage.HasDirection = true;
            _damage.Piercing = _piercing;
            if (hit.collider.TryGetComponent(out Health health) && health.TryTakeDamage(_damage, out float dealt))
            {
                _onHit?.Invoke(_damage, dealt);
                _onHitTarget?.Invoke(health, hit.centroid);
            }

            if (_pierceLeft <= 0)
            {
                Release();
                return;
            }

            _pierceLeft--;
            _damage.Amount *= 1f - _pierceFalloff;
        }

        /// <summary>막혔을 때 (강철 회오리 칼날 등) 바로 사라진다.</summary>
        public void Cancel() => Release();

        private void Release()
        {
            if (_release != null) _release(this);
            else gameObject.SetActive(false);
        }
    }
}
