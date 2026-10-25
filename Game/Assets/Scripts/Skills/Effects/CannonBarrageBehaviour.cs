using System.Collections.Generic;
using HistoryHeroes.Combat;
using HistoryHeroes.Common;
using HistoryHeroes.Enemy;
using UnityEngine;

namespace HistoryHeroes.Skills
{
    /// <summary>
    /// 나폴레옹 전용 — 대포 포격: 7초마다 적이 가장 많은 곳에 포탄 3발 (각 120%, 반지름 1.2), 레벨마다 +1발 (Skills.md 3장).
    /// 포탄은 하늘에서 떨어진다 (떨어질 자리가 먼저 보임). 아우스터리츠의 포화 (합체, leaveField): 떨어진 자리에 불꽃 장판 2초.
    /// </summary>
    public sealed class CannonBarrageBehaviour : ActiveSkillBehaviour
    {
        [SerializeField] private float spread = 1.1f;
        [SerializeField] private float fallTime = 0.5f;
        [SerializeField] private float fallHeight = 5f;
        [SerializeField] private Color shellColor = new Color(0.25f, 0.25f, 0.28f);
        [SerializeField] private Color blastColor = new Color(1f, 0.55f, 0.2f, 0.55f);

        [Header("아우스터리츠의 포화: 떨어진 자리에 장판")]
        [SerializeField] private bool leaveField;
        [SerializeField] private float fieldDamagePerSecond = 0.3f;
        [SerializeField] private float fieldDuration = 2f;
        [SerializeField] private Color fieldColor = new Color(1f, 0.5f, 0.15f, 0.4f);

        private readonly List<EnemyBase> _hits = new List<EnemyBase>();
        private ComponentPool<StrikeVisual> _shells;
        private ComponentPool<DamageZone> _zones;

        protected override bool Activate()
        {
            if (!CombatQuery.DensestPoint(1.6f, out Vector2 center)) return false;

            _shells ??= StrikeVisual.CreatePool(EffectRoot);
            if (leaveField) _zones ??= DamageZone.CreatePool(EffectRoot, fieldColor);

            int count = data.CountAt(level);
            float radius = data.RadiusAt(level);
            float coefficient = data.DamageAt(level);
            float levelScale = coefficient / Mathf.Max(0.01f, data.DamageAt(1));

            for (int i = 0; i < count; i++)
            {
                Vector2 point = center + (i == 0 ? Vector2.zero : Random.insideUnitCircle * spread);
                float delay = fallTime + i * 0.08f;
                _shells.Get(point, Quaternion.identity).Play(point, null, fallHeight, delay, 0.05f,
                    new Vector2(0.45f, 0.45f), Vector2.zero, shellColor, () => Explode(point, radius, coefficient, levelScale), _shells.Release);
            }

            return true;
        }

        private void Explode(Vector2 point, float radius, float coefficient, float levelScale)
        {
            if (player == null) return;
            AttackFx.Burst(point, radius, blastColor);
            CombatQuery.EnemiesWithin(point, radius, _hits);
            foreach (EnemyBase e in _hits) e.Health.TryTakeDamage(RollDamage(coefficient));

            if (leaveField)
            {
                DamageZone zone = _zones.Get(point, Quaternion.identity);
                zone.Begin(player.Stats, fieldDamagePerSecond * levelScale, radius, fieldDuration, enemyMask, _zones.Release);
            }
        }
    }
}
