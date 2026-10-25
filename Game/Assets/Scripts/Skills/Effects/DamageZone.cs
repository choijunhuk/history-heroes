using System;
using System.Collections.Generic;
using HistoryHeroes.Combat;
using HistoryHeroes.Common;
using HistoryHeroes.Stats;
using UnityEngine;

namespace HistoryHeroes.Skills
{
    /// <summary>
    /// 바닥 장판 (불꽃 장판, 뇌화 폭풍의 잔불). 0.5초마다 안에 있는 적에게 피해를 준다.
    /// 피해는 틱마다 따로 굴려서 치명타도 틱마다 판정한다.
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer))]
    public sealed class DamageZone : MonoBehaviour
    {
        public const float TickInterval = 0.5f;

        private static readonly List<Collider2D> _hits = new List<Collider2D>();

        private SpriteRenderer _renderer;
        private Color _color;
        private StatSheet _stats;
        private float _coefficientPerSecond;
        private float _radius;
        private float _duration;
        private float _timeLeft;
        private float _tickTimer;
        private ContactFilter2D _filter;
        private Action<DamageZone> _release;

        public static ComponentPool<DamageZone> CreatePool(Transform root, Color color, int sortingOrder = -1)
        {
            var template = new GameObject("DamageZone");
            template.SetActive(false);
            template.transform.SetParent(root, false);
            var sr = template.AddComponent<SpriteRenderer>();
            sr.sprite = PlaceholderSprites.Circle;
            sr.color = color;
            sr.sortingOrder = sortingOrder;
            return new ComponentPool<DamageZone>(template.AddComponent<DamageZone>(), root, prewarm: 4);
        }

        private void Awake()
        {
            _renderer = GetComponent<SpriteRenderer>();
            _color = _renderer.color;
        }

        /// <param name="coefficientPerSecond">초당 피해 (공격력 배수). 0.3 = 초당 30%</param>
        public void Begin(StatSheet stats, float coefficientPerSecond, float radius, float duration, LayerMask enemies, Action<DamageZone> release)
        {
            _stats = stats;
            _coefficientPerSecond = coefficientPerSecond;
            _radius = radius;
            _duration = duration;
            _timeLeft = duration;
            _tickTimer = 0f; // 깔리자마자 첫 틱
            _filter = new ContactFilter2D { useLayerMask = true, layerMask = enemies, useTriggers = true };
            _release = release;
            transform.localScale = Vector3.one * radius * 2f;
            _renderer.color = _color;
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            _timeLeft -= dt;
            _tickTimer -= dt;

            if (_tickTimer <= 0f)
            {
                _tickTimer += TickInterval;
                Tick();
            }

            // 끝나기 0.5초 전부터 흐려짐 → 언제 사라지는지 보인다
            Color c = _color;
            c.a *= Mathf.Clamp01(_timeLeft / Mathf.Min(0.5f, _duration));
            _renderer.color = c;

            if (_timeLeft <= 0f) _release?.Invoke(this);
        }

        private void Tick()
        {
            Physics2D.OverlapCircle(transform.position, _radius, _filter, _hits);
            for (int i = 0; i < _hits.Count; i++)
            {
                if (_hits[i].TryGetComponent(out Health health))
                {
                    health.TryTakeDamage(DamageCalculator.Roll(_stats, _coefficientPerSecond * TickInterval, DamageKind.Active));
                }
            }
        }
    }
}
