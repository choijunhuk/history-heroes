using System.Collections.Generic;
using HistoryHeroes.Combat;
using HistoryHeroes.Common;
using UnityEngine;

namespace HistoryHeroes.Enemy
{
    /// <summary>
    /// 그림자 표식 (검토 9장 3번, 그림자 쌍둥이): 15초마다 바닥에 표식 3개 (지름 2.5, 4초 뒤 터짐, 피해 최대체력 25%).
    /// 그 4초 안에 맞는 쪽(진한) 쌍둥이를 5번 맞히면 표식이 사라진다 ("표식 해제!"). 공유 체력 · 교대는 그대로
    /// </summary>
    public sealed class ShadowMarksGimmick : BossGimmick
    {
        public const float Interval = 15f;
        public const float First = 8f;
        public const float Fuse = 4f;
        public const float Radius = 1.25f;
        public const int HitsNeeded = 5;
        public const float Damage = 0.25f;

        private readonly List<SpriteRenderer> _marks = new List<SpriteRenderer>();
        private readonly List<Vector2> _points = new List<Vector2>();
        private float _clock;
        private float _next;
        private float _armedAt = -1f;
        private int _hits;
        private bool _hooked;

        public override string Hint => BossGimmicks.HintFor(GimmickKind.ShadowMarks);
        public bool Armed => _armedAt >= 0f;
        public int Hits => _hits;

        protected override void OnBegin()
        {
            _clock = 0f;
            _next = First;
            _armedAt = -1f;
            _hits = 0;
            if (!_hooked)
            {
                boss.Health.Damaged += OnBossDamaged;
                _hooked = true;
            }
        }

        protected override void OnEnd()
        {
            Clear();
            if (_hooked && boss != null) boss.Health.Damaged -= OnBossDamaged;
            _hooked = false;
        }

        private void OnBossDamaged(DamageInfo _, float dealt)
        {
            if (!Running || !Armed || dealt <= 0f) return;
            _hits++;
            if (_hits < HitsNeeded)
            {
                BossGimmicks.Notice?.Invoke($"표식 풀기 {_hits}/{HitsNeeded}");
                return;
            }

            // 해제: 표식이 흩어진다
            foreach (Vector2 p in _points) FxBursts.Puff((Vector3)p + Vector3.back * 0.3f, new Color(0.6f, 0.5f, 1f), 1.4f);
            Clear();
            UI.HypeFx.Banner("표식 해제!", new Color(0.7f, 0.6f, 1f));
        }

        private void Update()
        {
            if (!Running || Paused || hero == null) return;
            _clock += Time.deltaTime;
            if (Armed)
            {
                float t = (_clock - _armedAt) / Fuse;
                foreach (SpriteRenderer m in _marks) Telegraphs.Fill(m, Telegraphs.Danger, t);
                if (t >= 1f) Explode();
                return;
            }

            if (_clock < _next) return;
            Arm();
        }

        private void Arm()
        {
            _armedAt = _clock;
            _next = _clock + Interval; // 발동 시작 기준 15초마다 (코드 검토 6번)
            _hits = 0;
            _points.Clear();
            Vector2 me = hero.position;
            _points.Add(Clamp(me));
            float a = Random.value * Mathf.PI * 2f;
            for (int i = 0; i < 2; i++)
            {
                float angle = a + i * Mathf.PI * 0.8f + 0.6f;
                _points.Add(Clamp(me + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * 2.4f));
            }

            foreach (Vector2 p in _points) _marks.Add(Telegraphs.Circle(p, Radius, Telegraphs.Danger));
            Audio.AudioManager.Play("telegraph", 0.8f);
            BossGimmicks.Notice?.Invoke($"그림자 표식! 4초 안에 진한 쌍둥이를 {HitsNeeded}번");
        }

        private Vector2 Clamp(Vector2 p)
        {
            if (arena.Round)
            {
                Vector2 d = p - arena.Center;
                return d.magnitude > arena.Radius - Radius * 0.5f ? arena.Center + d.normalized * (arena.Radius - Radius * 0.5f) : p;
            }

            return new Vector2(Mathf.Clamp(p.x, arena.Center.x - arena.Half.x + 0.6f, arena.Center.x + arena.Half.x - 0.6f),
                Mathf.Clamp(p.y, arena.Center.y - arena.Half.y + 0.6f, arena.Center.y + arena.Half.y - 0.6f));
        }

        private void Explode()
        {
            bool hit = false;
            Vector2 me = hero.position;
            foreach (Vector2 p in _points)
            {
                FxBursts.Explosion(p, Radius, new Color(0.45f, 0.25f, 0.8f));
                hit |= (me - p).sqrMagnitude <= Radius * Radius;
            }

            if (hit && hero.TryGetComponent(out Health h)) h.TryTakeDamage(BossGimmicks.ToHero(h.Max * Damage, "gimmick_shadow_mark"));
            CameraShake.Add(0.3f);
            Clear();
        }

        private void Clear()
        {
            foreach (SpriteRenderer m in _marks) Telegraphs.Hide(m);
            _marks.Clear();
            _points.Clear();
            _armedAt = -1f;
        }
    }
}
