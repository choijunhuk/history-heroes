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
        private int _ricochetLeft;
        private float _pierceFalloff;
        private float _age;
        private LayerMask _hitMask;
        private LayerMask _wallMask;
        private Collider2D _lastHit;
        private Action<Projectile> _release;
        private Action<DamageInfo, float> _onHit;
        private Action<Health, Vector2> _onHitTarget;

        // 한 번에 여러 개를 받아서, 방금 뚫고 지나온 적은 빼고 가장 가까운 것을 고른다
        // (첫 결과가 방금 그 적이면 뒤의 벽을 못 보고 통과하던 문제 방지)
        private static readonly RaycastHit2D[] _hits = new RaycastHit2D[8];

        public void Launch(Vector2 direction, float speed, DamageInfo damage, LayerMask hitMask, LayerMask wallMask,
            int pierce, int ricochet, Action<Projectile> release, Action<DamageInfo, float> onHit = null,
            float pierceFalloff = 0.2f, Action<Health, Vector2> onHitTarget = null)
        {
            _onHitTarget = onHitTarget;
            _direction = direction.normalized;
            _speed = speed;
            _damage = damage;
            _hitMask = hitMask;
            _wallMask = wallMask;
            _pierceLeft = pierce;
            _ricochetLeft = ricochet;
            _pierceFalloff = pierceFalloff;
            _release = release;
            _onHit = onHit;
            _age = 0f;
            _lastHit = null;
            transform.right = _direction;
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

            float distance = _speed * dt;
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
