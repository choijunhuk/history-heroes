using System.Collections;
using System.Collections.Generic;
using HistoryHeroes.Combat;
using HistoryHeroes.Common;
using HistoryHeroes.Enemy;
using UnityEngine;

namespace HistoryHeroes.Skills
{
    /// <summary>
    /// 이순신 전용 액티브 "거북선 돌격" (확장 결정 12: 새 영웅): 가장 가까운 적 쪽으로 거북선이 곧게 돌진 — 지나간 적 150% + 밀어냄 (적마다 한 번).
    /// 전용 조합 "학익진" (진화: 거북선 돌격 Lv5 + 최대체력): 거북선 3척이 V자(학의 날개)로 펼쳐 돌격하고, 지나간 자리에 불길 2초.
    /// 거북선은 영웅과 떨어져 혼자 달린다 (이동 중에도 발동, 액티브 규칙).
    /// </summary>
    public sealed class TurtleShipBehaviour : ActiveSkillBehaviour
    {
        [SerializeField] private GameObject shipModel;
        [SerializeField] private float speed = 11f;
        [SerializeField] private float distance = 7.5f;
        [SerializeField] private float hitRadius = 0.8f;
        [SerializeField] private float knockback = 0.6f;
        [SerializeField] private float searchRange = 8f;
        [Tooltip("배 수 (학익진 3)")]
        [SerializeField] private int ships = 1;
        [Tooltip("V자 날개: 옆 배가 옆으로 · 뒤로 떨어진 거리")]
        [SerializeField] private Vector2 wing = new Vector2(1.5f, 1.1f);
        [Header("학익진: 지나간 자리 불길")]
        [SerializeField] private bool fireTrail;
        [SerializeField] private float trailEvery = 0.9f;
        [SerializeField] private float trailDamagePerSecond = 0.35f;
        [SerializeField] private float trailDuration = 2f;
        [SerializeField] private Color trailColor = new Color(1f, 0.5f, 0.15f, 0.4f);

        private readonly List<EnemyBase> _near = new List<EnemyBase>();
        private static int _wallMask = -1;
        private static int WallMask => _wallMask >= 0 ? _wallMask : (_wallMask = LayerMask.GetMask("Wall"));
        private ComponentPool<DamageZone> _zones;

        protected override bool Activate()
        {
            EnemyBase target = CombatQuery.Nearest(player.transform.position, searchRange);
            if (target == null) return false;
            Vector2 origin = player.transform.position;
            Vector2 dir = ((Vector2)target.transform.position - origin).normalized;
            if (dir.sqrMagnitude < 0.01f) dir = Vector2.up;
            if (fireTrail) _zones ??= DamageZone.CreatePool(EffectRoot, trailColor, -1, data.id);

            float coefficient = data.DamageAt(level);
            var side = new Vector2(-dir.y, dir.x);
            for (int i = 0; i < Mathf.Max(1, ships); i++)
            {
                // 0 = 가운데(앞), 1·2 = 양 날개 (뒤로 · 옆으로)
                Vector2 offset = i == 0 ? Vector2.zero : -dir * wing.y + side * (i % 2 == 1 ? wing.x : -wing.x) * ((i + 1) / 2);
                StartCoroutine(Sail(origin + offset, dir, coefficient));
            }

            Audio.AudioManager.Play("slam", 0.6f);
            return true;
        }

        private IEnumerator Sail(Vector2 start, Vector2 dir, float coefficient)
        {
            var ship = new GameObject("TurtleShip").transform;
            ship.SetParent(EffectRoot, false);
            ship.position = start;
            ship.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg);
            GameObject model = shipModel != null ? SkillArt.Attach(shipModel, ship, 1.3f) : null;
            if (model == null)
            {
                var sr = ship.gameObject.AddComponent<SpriteRenderer>();
                sr.sprite = PlaceholderSprites.Square;
                sr.color = new Color(0.45f, 0.3f, 0.2f);
                ship.localScale = new Vector3(1.3f, 0.6f, 1f);
            }

            SkillArt.Trail(ship.gameObject, new Color(0.75f, 0.9f, 1f), 0.45f, 0.35f); // 물보라 꼬리
            var hit = new HashSet<EnemyBase>();
            float travelled = 0f, nextTrail = 0f;
            Vector2 pos = start;
            while (travelled < distance)
            {
                if (player == null) break;
                if (IsPaused)
                {
                    yield return null;
                    continue;
                }

                float step = speed * Time.deltaTime;
                // 벽에 닿으면 멈춘다 (방 밖으로 나가 불길을 깔지 않게)
                if (Physics2D.CircleCast(pos, 0.35f, dir, step, WallMask).collider != null) break;
                pos += dir * step;
                travelled += step;
                ship.position = new Vector3(pos.x, pos.y, -0.05f - 0.06f * Mathf.Abs(Mathf.Sin(travelled * 3f))); // 출렁
                CombatQuery.EnemiesWithin(pos, hitRadius, _near);
                foreach (EnemyBase e in _near)
                {
                    if (!hit.Add(e)) continue;
                    e.Health.TryTakeDamage(RollDamage(coefficient));
                    if (e.CanBeKnockedBack && e.TryGetComponent(out Rigidbody2D rb)) rb.MovePosition(rb.position + dir * knockback);
                    FxBursts.Puff((Vector3)(Vector2)e.transform.position + Vector3.back * 0.5f, new Color(1f, 0.7f, 0.35f), 0.8f);
                }

                if (fireTrail && travelled >= nextTrail)
                {
                    nextTrail = travelled + trailEvery;
                    DamageZone zone = _zones.Get(pos, Quaternion.identity);
                    zone.Begin(player.Stats, trailDamagePerSecond * (coefficient / Mathf.Max(0.01f, data.DamageAt(1))), 0.9f, trailDuration, enemyMask, _zones.Release);
                }

                yield return null;
            }

            FxBursts.Puff(ship.position + Vector3.back * 0.3f, new Color(0.8f, 0.9f, 1f), 1.2f);
            Destroy(ship.gameObject);
        }
    }
}
