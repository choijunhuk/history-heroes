using System;
using System.Collections.Generic;
using HistoryHeroes.Combat;
using HistoryHeroes.Common;
using UnityEngine;

namespace HistoryHeroes.Enemy
{
    /// <summary>
    /// 적 상태 이상 (Skills.md 2장·4-4): 둔화, 빙결, 독(중첩), 끌어당김.
    /// - 둔화: 이동 속도 −비율 (여러 개면 가장 센 것 하나). 서리 창은 둔화가 3번 쌓이면 1초 빙결
    /// - 빙결: 움직이지도 공격하지도 않는다 (예고 중이던 공격도 멈춤). 빙결한 적에게 서리 창은 치명타 확정
    /// - 독: 초당 피해, 최대 3번 중첩
    /// - 끌어당김: 회오리·눈보라 중심으로 끌려간다
    /// 보스는 빙결·끌어당김에 덜 걸린다 (빙결 시간 절반, 끌어당김 30%).
    /// </summary>
    public sealed class EnemyStatus : MonoBehaviour
    {
        public const int MaxPoisonStacks = 3;
        public const float PoisonTick = 0.5f;

        private EnemyBase _enemy;
        private Rigidbody2D _body;
        private SpriteRenderer _tint;
        private Color _baseColor;

        private float _slowFraction;
        private float _slowUntil;
        private float _freezeUntil;
        private int _chillStacks;
        private float _chillUntil;

        /// <summary>지금 이 적에게 독을 거는 것들 (구름 하나 = 한 겹). 가장 센 3개까지만 피해를 준다</summary>
        private struct PoisonSource
        {
            public object Owner;
            public float DamagePerSecond;
            public float Until;
            public Action<Vector2> OnDeathInside;
        }

        private readonly List<PoisonSource> _poison = new List<PoisonSource>(4);
        private float _poisonTimer;
        private static readonly List<float> _strengths = new List<float>(8);

        private Vector2 _pullCenter;
        private float _pullSpeed;
        private float _pullUntil;

        public bool IsFrozen => Time.time < _freezeUntil;
        public float SpeedMultiplier => Time.time < _slowUntil ? 1f - _slowFraction : 1f;
        public bool IsPulled => Time.time < _pullUntil;

        private bool IsBoss => _enemy != null && !_enemy.CanBeKnockedBack && !_enemy.IsElite;

        private void Awake()
        {
            _enemy = GetComponent<EnemyBase>();
            _body = GetComponent<Rigidbody2D>();
            _tint = GetComponent<SpriteRenderer>();
            if (_tint != null) _baseColor = _tint.color;
        }

        /// <summary>다시 나올 때 (오브젝트 풀) 상태를 비운다</summary>
        public void Clear()
        {
            _slowUntil = _freezeUntil = _chillUntil = _pullUntil = 0f;
            _chillStacks = 0;
            _poison.Clear();
            if (_tint != null) _tint.color = _baseColor;
        }

        public void ApplySlow(float fraction, float duration)
        {
            if (Time.time >= _slowUntil || fraction >= _slowFraction) _slowFraction = Mathf.Clamp01(fraction);
            _slowUntil = Mathf.Max(_slowUntil, Time.time + duration);
        }

        /// <summary>서리 창: 둔화를 1번 쌓는다. stacksToFreeze번 쌓이면 빙결</summary>
        public void AddChill(int stacksToFreeze, float freezeDuration)
        {
            if (Time.time > _chillUntil) _chillStacks = 0;
            _chillStacks++;
            _chillUntil = Time.time + 3f;
            if (_chillStacks < stacksToFreeze) return;
            _chillStacks = 0;
            ApplyFreeze(freezeDuration);
        }

        public void ApplyFreeze(float duration)
        {
            if (IsBoss) duration *= 0.5f;
            _freezeUntil = Mathf.Max(_freezeUntil, Time.time + duration);
        }

        /// <summary>
        /// 독 구름 안에 있다 (구름이 틱마다 부른다). 같은 구름은 한 겹으로 센다. 겹친 구름은 3겹까지만 (Skills 2장 "중첩").
        /// onDeathInside: 이 구름 안에서 죽으면 (막타가 무엇이든) — 맹독 개화 번짐
        /// </summary>
        public void TouchPoison(object owner, float damagePerSecond, float duration, Action<Vector2> onDeathInside = null)
        {
            float until = Time.time + duration;
            for (int i = 0; i < _poison.Count; i++)
            {
                if (_poison[i].Owner != owner) continue;
                PoisonSource s = _poison[i];
                s.DamagePerSecond = damagePerSecond;
                s.Until = until;
                s.OnDeathInside = onDeathInside;
                _poison[i] = s;
                return;
            }

            _poison.Add(new PoisonSource { Owner = owner, DamagePerSecond = damagePerSecond, Until = until, OnDeathInside = onDeathInside });
        }

        public int PoisonLayers
        {
            get
            {
                int n = 0;
                foreach (PoisonSource s in _poison) if (Time.time < s.Until) n++;
                return Mathf.Min(n, MaxPoisonStacks);
            }
        }

        /// <summary>죽을 때 (EnemyBase가 부른다): 독 구름 안이었으면 그 자리에서 번진다 (한 번만)</summary>
        public void NotifyDeath(Vector2 position)
        {
            float now = Time.time;
            foreach (PoisonSource s in _poison)
            {
                if (now >= s.Until || s.OnDeathInside == null) continue;
                Action<Vector2> spread = s.OnDeathInside;
                _poison.Clear();
                spread(position);
                return;
            }

            _poison.Clear();
        }

        public void Pull(Vector2 center, float speed, float duration)
        {
            if (IsBoss) speed *= 0.3f;
            _pullCenter = center;
            _pullSpeed = speed;
            _pullUntil = Mathf.Max(_pullUntil, Time.time + duration);
        }

        private void Update()
        {
            if (GamePause.IsPaused || _enemy == null || _enemy.Health.IsDead) return;
            float now = Time.time;

            // 독: 0.5초마다, 지금 걸린 구름 중 가장 센 3개의 초당 피해 합 × 0.5
            _poison.RemoveAll(p => now >= p.Until);
            if (_poison.Count > 0)
            {
                _poisonTimer -= Time.deltaTime;
                if (_poisonTimer <= 0f)
                {
                    _poisonTimer += PoisonTick;
                    _strengths.Clear();
                    foreach (PoisonSource s in _poison) _strengths.Add(s.DamagePerSecond);
                    _strengths.Sort((a, b) => b.CompareTo(a));
                    float dps = 0f;
                    for (int i = 0; i < _strengths.Count && i < MaxPoisonStacks; i++) dps += _strengths[i];
                    var info = new DamageInfo { Amount = dps * PoisonTick, Kind = DamageKind.Active, AttackerTeam = Team.Player };
                    _enemy.Health.TryTakeDamage(info);
                }
            }
            else
            {
                _poisonTimer = 0f;
            }

            // 색으로 보여주기: 빙결 하늘색, 독 초록, 둔화 연한 파랑
            if (_tint != null && !IsFlashing())
            {
                Color target = _baseColor;
                if (IsFrozen) target = new Color(0.6f, 0.85f, 1f);
                else if (_poison.Count > 0) target = Color.Lerp(_baseColor, new Color(0.45f, 0.9f, 0.35f), 0.5f);
                else if (now < _slowUntil) target = Color.Lerp(_baseColor, new Color(0.6f, 0.8f, 1f), 0.4f);
                if (_tint.color != target) _tint.color = target;
            }
        }

        private void FixedUpdate()
        {
            if (GamePause.IsPaused || !IsPulled || _body == null) return;
            Vector2 to = _pullCenter - _body.position;
            if (to.sqrMagnitude < 0.04f) return;
            _body.MovePosition(_body.position + to.normalized * Mathf.Min(_pullSpeed * Time.fixedDeltaTime, to.magnitude));
        }

        /// <summary>맞았을 때 번쩍이는 중이면 색을 건드리지 않는다 (HitFlash가 원래 색으로 돌려놓는다)</summary>
        private bool IsFlashing() => TryGetComponent(out HitFlash flash) && flash.IsFlashing;
    }
}
