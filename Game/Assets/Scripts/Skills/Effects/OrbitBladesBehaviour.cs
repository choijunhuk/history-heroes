using System.Collections.Generic;
using HistoryHeroes.Combat;
using HistoryHeroes.Common;
using HistoryHeroes.Enemy;
using UnityEngine;

namespace HistoryHeroes.Skills
{
    /// <summary>
    /// 회전 칼날 (상시): 몸 주위를 도는 칼날. 레벨마다 칼날 +1. (Skills.md 2장)
    /// 강철 회오리 (진화): 칼날 8개, 칼날마다 1초에 적 투사체 1발을 막는다. (Skills.md 4-4)
    /// 같은 적은 **스킬 전체에서** hitInterval(0.3초)마다 한 번만 맞는다 (Skills.md 4-4).
    /// 칼날이 많을수록 더 자주 닿으므로 피해가 늘지만, 두 칼날이 겹쳐도 한 번에 두 번 맞지는 않는다.
    /// </summary>
    public sealed class OrbitBladesBehaviour : ActiveSkillBehaviour
    {
        [SerializeField] private Color color = new Color(0.85f, 0.9f, 1f);
        [SerializeField] private Vector2 bladeSize = new Vector2(0.55f, 0.18f);
        [SerializeField] private float degreesPerSecond = 220f;
        [SerializeField] private float hitRadius = 0.3f;
        [SerializeField] private float hitInterval = 0.3f;

        [Header("강철 회오리")]
        [SerializeField] private bool blockProjectiles;
        [SerializeField] private float blockInterval = 1f;

        private sealed class Blade
        {
            public Transform Transform;
            public float NextBlock; // 투사체 막기는 칼날마다 1초에 1발
        }

        private readonly Dictionary<Health, float> _nextHit = new Dictionary<Health, float>();

        private static readonly List<Collider2D> _hits = new List<Collider2D>();
        private readonly List<Blade> _blades = new List<Blade>();
        private ContactFilter2D _filter;
        private float _angle;

        protected override void OnLevelChanged()
        {
            _filter = new ContactFilter2D { useLayerMask = true, layerMask = enemyMask, useTriggers = true };

            int count = data.CountAt(level);
            while (_blades.Count < count) _blades.Add(CreateBlade(_blades.Count));
            while (_blades.Count > count)
            {
                Destroy(_blades[_blades.Count - 1].Transform.gameObject);
                _blades.RemoveAt(_blades.Count - 1);
            }
        }

        private Blade CreateBlade(int index)
        {
            var go = new GameObject($"Blade {index}");
            go.transform.SetParent(transform, false);
            go.transform.localScale = new Vector3(bladeSize.x, bladeSize.y, 1f);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = PlaceholderSprites.Square;
            sr.color = color;
            sr.sortingOrder = 6;
            // 3D 칼 (끝이 도는 쪽을 본다) + 원을 그리는 꼬리 빛 (그래픽 평가 2번: "흰 막대 같은 칼날"). 모델이 없으면 예전 네모
            SkillArt art = SkillArt.Get;
            if (art != null && art.blade != null)
            {
                SkillArt.Attach(art.blade, go.transform, 1.1f);
                SkillArt.Trail(go, new Color(0.35f, 0.65f, 1f), 0.22f, 0.16f);
                sr.enabled = false;
            }

            return new Blade { Transform = go.transform };
        }

        // 상시 스킬이라 Activate는 쓰지 않는다 (cooldown 0)
        protected override bool Activate() => true;

        protected override void Update()
        {
            base.Update();
            if (player == null || _blades.Count == 0 || IsPaused) return;

            float now = Time.time;
            _angle = (_angle + degreesPerSecond * Time.deltaTime) % 360f;
            float orbit = data.RadiusAt(level);
            Vector2 center = player.transform.position;

            for (int i = 0; i < _blades.Count; i++)
            {
                float degrees = _angle + 360f * i / _blades.Count;
                Vector2 offset = Quaternion.Euler(0f, 0f, degrees) * Vector2.right * orbit;
                Vector2 position = center + offset;

                Blade blade = _blades[i];
                // 3D: 칼날은 가슴 높이(-Z)에서 돈다 (땅에 누워 보이지 않게, Art3D-Plan 6번). 판정은 XY 그대로
                blade.Transform.SetPositionAndRotation(new Vector3(position.x, position.y, -0.75f), Quaternion.Euler(0f, 0f, degrees + 90f));

                HitEnemies(position, now);

                if (blockProjectiles && now >= blade.NextBlock && EnemyProjectiles.TryBlock(position, hitRadius * 1.5f))
                {
                    blade.NextBlock = now + blockInterval;
                }
            }
        }

        private void HitEnemies(Vector2 position, float now)
        {
            Physics2D.OverlapCircle(position, hitRadius, _filter, _hits);
            for (int h = 0; h < _hits.Count; h++)
            {
                if (!_hits[h].TryGetComponent(out Health health) || health.IsDead) continue;
                if (_nextHit.TryGetValue(health, out float next) && now < next) continue;

                health.TryTakeDamage(RollDamage(data.DamageAt(level)));
                _nextHit[health] = now + hitInterval;
            }

            // 죽은 적 기록이 쌓이지 않게 가끔 정리
            if (_nextHit.Count > 64) _nextHit.Clear();
        }
    }
}
