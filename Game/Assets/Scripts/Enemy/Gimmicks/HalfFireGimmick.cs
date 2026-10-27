using HistoryHeroes.Combat;
using HistoryHeroes.Common;
using UnityEngine;

namespace HistoryHeroes.Enemy
{
    /// <summary>
    /// 불길 반쪽 (확장 1차 5장, 망각의 집행자 · 균열 강철 거상): 12초마다 보스가 있던 반쪽이 불길 (1.5초 예고) — 보스는 반대편으로 건너간다.
    /// 불길 2초 동안 그 반쪽에 있으면 0.5초마다 최대체력 12%. 보스를 따라 건너가면 안전하다
    /// </summary>
    public sealed class HalfFireGimmick : BossGimmick
    {
        public const float Interval = 12f;
        public const float First = 6f;
        public const float Telegraph = 1.5f;
        public const float Burn = 2f;
        public const float Tick = 0.5f;
        public const float Damage = 0.12f;

        private float _clock;
        private float _next;
        private float _startedAt = -1f;
        private float _nextTick;
        private int _side; // -1 왼쪽 · +1 오른쪽
        private SpriteRenderer _area;

        public override string Hint => BossGimmicks.HintFor(GimmickKind.HalfFire);
        public bool Burning => _startedAt >= 0f && _clock - _startedAt >= Telegraph;

        protected override void OnBegin()
        {
            _clock = 0f;
            _next = First;
            _startedAt = -1f;
        }

        protected override void OnEnd() => HideArea();

        private void Update()
        {
            if (!Running || Paused) return;
            _clock += Time.deltaTime;
            if (_startedAt < 0f)
            {
                if (_clock >= _next) StartFire();
                return;
            }

            float t = _clock - _startedAt;
            if (t < Telegraph)
            {
                Telegraphs.Fill(_area, Telegraphs.Danger, t / Telegraph);
                return;
            }

            if (t < Telegraph + Burn)
            {
                if (_area != null) _area.color = new Color(1f, 0.45f + 0.15f * Mathf.Sin(_clock * 20f), 0.1f, 0.55f);
                if (_clock >= _nextTick)
                {
                    _nextTick = _clock + Tick;
                    BurnTick();
                }

                return;
            }

            HideArea();
            _startedAt = -1f;
            _next = _clock + Interval;
        }

        private void StartFire()
        {
            _startedAt = _clock;
            _nextTick = _clock + Telegraph;
            _side = boss.transform.position.x >= arena.Center.x ? 1 : -1;

            // 보스가 반대편으로 건너간다 (불길을 끌고 간다)
            Vector2 from = boss.transform.position;
            var to = new Vector2(arena.Center.x - _side * arena.Half.x * 0.5f, Mathf.Clamp(from.y, arena.Center.y - arena.Half.y * 0.6f, arena.Center.y + arena.Half.y * 0.6f));
            FxBursts.Puff((Vector3)from + Vector3.back * 0.8f, new Color(1f, 0.5f, 0.2f), 1.6f);
            if (boss.TryGetComponent(out Rigidbody2D body)) body.position = to;
            boss.transform.position = new Vector3(to.x, to.y, boss.transform.position.z);
            FxBursts.Puff((Vector3)to + Vector3.back * 0.8f, new Color(1f, 0.5f, 0.2f), 1.6f);

            Vector2 size = new Vector2(arena.Half.x, arena.Half.y * 2f);
            Vector2 center = arena.Center + new Vector2(_side * arena.Half.x * 0.5f, 0f);
            _area = Telegraphs.Box(center, size, 0f, Telegraphs.Danger);
            Audio.AudioManager.Play("telegraph", 0.9f);
            BossGimmicks.Notice?.Invoke(_side > 0 ? "오른쪽에 불길! 왼쪽으로" : "왼쪽에 불길! 오른쪽으로");
        }

        private void BurnTick()
        {
            if (hero == null) return;
            Vector2 p = hero.position;
            bool onSide = _side > 0 ? p.x >= arena.Center.x : p.x < arena.Center.x;
            if (onSide && arena.Contains(p) && hero.TryGetComponent(out Health h)) h.TryTakeDamage(BossGimmicks.ToHero(h.Max * Damage, "gimmick_half_fire"));
            Vector2 c = arena.Center + new Vector2(_side * arena.Half.x * 0.5f, Random.Range(-arena.Half.y, arena.Half.y) * 0.8f);
            FxBursts.Puff((Vector3)c + Vector3.back * 0.3f, new Color(1f, 0.55f, 0.15f), 1.3f);
        }

        private void HideArea()
        {
            if (_area != null) Telegraphs.Hide(_area);
            _area = null;
        }
    }
}
