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

        public void Launch(Vector2 direction, float speed, DamageInfo damage, LayerMask hitMask, LayerMask wallMask,
            int pierce, int ricochet, Action<Projectile> release, Action<DamageInfo, float> onHit = null,
            float pierceFalloff = 0.2f)
        {
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
            RaycastHit2D hit = Physics2D.CircleCast(position, radius, _direction, distance, _hitMask | _wallMask);

            if (hit.collider == null || hit.collider == _lastHit)
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

            if (hit.collider.TryGetComponent(out Health health) && health.TryTakeDamage(_damage))
            {
                _onHit?.Invoke(_damage, _damage.Amount);
            }

            if (_pierceLeft <= 0)
            {
                Release();
                return;
            }

            _pierceLeft--;
            _damage.Amount *= 1f - _pierceFalloff;
        }

        private void Release()
        {
            if (_release != null) _release(this);
            else gameObject.SetActive(false);
        }
    }
}
