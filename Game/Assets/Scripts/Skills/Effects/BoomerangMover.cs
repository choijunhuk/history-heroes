using System;
using System.Collections.Generic;
using HistoryHeroes.Combat;
using HistoryHeroes.Common;
using HistoryHeroes.Stats;
using UnityEngine;

namespace HistoryHeroes.Skills
{
    /// <summary>
    /// 부메랑 하나: 앞으로 날아갔다가 (감속) 주인에게 돌아온다. 지나가는 적을 전부 뚫고 때린다 (가는 길·오는 길에 한 번씩).
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer))]
    public sealed class BoomerangMover : MonoBehaviour
    {
        private static readonly List<Collider2D> _overlap = new List<Collider2D>(16);

        private readonly HashSet<Health> _hitThisPass = new HashSet<Health>();
        private Transform _owner;
        private StatSheet _stats;
        private ContactFilter2D _filter;
        private Vector2 _origin;
        private Vector2 _direction;
        private float _distance;
        private float _outTime;
        private float _coefficient;
        private float _age;
        private bool _returning;
        private float _returnSpeed;
        private Action<Vector2, Vector2> _onApex;
        private Action<BoomerangMover> _release;

        public static ComponentPool<BoomerangMover> CreatePool(Transform root, Color color, Vector2 size)
        {
            var template = new GameObject("Boomerang");
            template.SetActive(false);
            template.transform.SetParent(root, false);
            template.transform.localScale = new Vector3(size.x, size.y, 1f);
            var sr = template.AddComponent<SpriteRenderer>();
            sr.sprite = PlaceholderSprites.Square;
            sr.color = color;
            sr.sortingOrder = 5;
            return new ComponentPool<BoomerangMover>(template.AddComponent<BoomerangMover>(), root, prewarm: 4);
        }

        /// <param name="startReturning">처음부터 돌아오는 중 (쌍 부메랑이 갈라진 조각)</param>
        /// <param name="onApex">가장 멀리 갔을 때 (자리, 방향) — 쌍 부메랑이 갈라짐</param>
        public void Launch(Transform owner, StatSheet stats, LayerMask enemies, Vector2 origin, Vector2 direction, float distance, float outTime,
            float coefficient, bool startReturning, Action<Vector2, Vector2> onApex, Action<BoomerangMover> release)
        {
            _owner = owner;
            _stats = stats;
            _filter = new ContactFilter2D { useLayerMask = true, layerMask = enemies, useTriggers = true };
            _origin = origin;
            _direction = direction.normalized;
            _distance = distance;
            _outTime = Mathf.Max(0.05f, outTime);
            _coefficient = coefficient;
            _age = 0f;
            _returning = startReturning;
            _returnSpeed = distance / _outTime * 1.3f;
            _onApex = onApex;
            _release = release;
            _hitThisPass.Clear();
            transform.position = origin;
        }

        private void Update()
        {
            if (GamePause.IsPaused) return;
            float dt = Time.deltaTime;
            _age += dt;
            transform.Rotate(0f, 0f, 900f * dt);

            if (!_returning)
            {
                // 감속하며 나간다 (끝에서 잠깐 머무는 느낌)
                float t = Mathf.Clamp01(_age / _outTime);
                float eased = 1f - (1f - t) * (1f - t);
                transform.position = _origin + _direction * (_distance * eased);
                if (t >= 1f)
                {
                    _returning = true;
                    _hitThisPass.Clear();
                    _onApex?.Invoke(transform.position, _direction);
                }
            }
            else
            {
                if (_owner == null)
                {
                    _release?.Invoke(this);
                    return;
                }

                Vector2 to = (Vector2)_owner.position - (Vector2)transform.position;
                if (to.magnitude < 0.4f || _age > 4f)
                {
                    _release?.Invoke(this);
                    return;
                }

                transform.position = (Vector2)transform.position + to.normalized * Mathf.Min(_returnSpeed * dt, to.magnitude);
            }

            HitAround();
        }

        private void HitAround()
        {
            _overlap.Clear();
            Physics2D.OverlapCircle(transform.position, 0.4f, _filter, _overlap);
            for (int i = 0; i < _overlap.Count; i++)
            {
                if (!_overlap[i].TryGetComponent(out Health h) || h.IsDead || !_hitThisPass.Add(h)) continue;
                h.TryTakeDamage(DamageCalculator.Roll(_stats, _coefficient, DamageKind.Active));
            }
        }
    }
}
