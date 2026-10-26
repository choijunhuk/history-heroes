using System.Collections.Generic;
using HistoryHeroes.Common;
using HistoryHeroes.Enemy;
using UnityEngine;

namespace HistoryHeroes.Skills
{
    /// <summary>
    /// 낙뢰: 3초마다 화면 안 무작위 적에게 번개 (120%). 레벨마다 번개 +1. (Skills.md 2장)
    /// 뇌화 폭풍 (합체, leaveField 켬): 번개 3개 각 130% + 떨어진 자리 장판 2초 (초당 30%). (Skills.md 4-4)
    /// 뇌신의 분노 (진화, chainTargets 3): 번개 5개 각 120%, 각 번개가 근처 3명에게 연쇄 (연쇄마다 -20%). (Skills.md 4-4)
    /// </summary>
    public sealed class LightningBehaviour : ActiveSkillBehaviour
    {
        [SerializeField] private Color boltColor = new Color(1f, 0.95f, 0.45f);
        [SerializeField] private Vector2 boltSize = new Vector2(0.25f, 7f);

        [Header("뇌신의 분노: 연쇄")]
        [SerializeField] private int chainTargets;
        [SerializeField] private float chainRadius = 3f;
        [SerializeField] private float chainFalloff = 0.2f;
        [SerializeField] private Color chainColor = new Color(1f, 0.95f, 0.5f, 0.9f);

        [Header("뇌화 폭풍: 떨어진 자리에 장판")]
        [SerializeField] private bool leaveField;
        [SerializeField] private float fieldDamagePerSecond = 0.3f;
        [SerializeField] private float fieldDuration = 2f;
        [SerializeField] private float fieldRadius = 1.2f;
        [SerializeField] private Color fieldColor = new Color(1f, 0.55f, 0.15f, 0.45f);

        private readonly List<EnemyBase> _targets = new List<EnemyBase>();
        private ComponentPool<StrikeVisual> _bolts;
        private ComponentPool<DamageZone> _zones;

        protected override bool Activate()
        {
            CombatQuery.RandomEnemiesOnScreen(data.CountAt(level), _targets);
            if (_targets.Count == 0) return false;

            _bolts ??= StrikeVisual.CreatePool(EffectRoot);
            if (leaveField) _zones ??= DamageZone.CreatePool(EffectRoot, fieldColor, -1, data.id);

            // 합체 결과의 레벨 효과(Lv2 +25%, Lv3 +50%)를 장판에도 같은 비율로
            float levelScale = data.DamageAt(level) / Mathf.Max(0.01f, data.DamageAt(1));

            foreach (EnemyBase enemy in _targets)
            {
                Vector2 point = enemy.transform.position;
                _bolts.Get(point, Quaternion.identity).Play(point, null, 0f, 0f, 0.15f,
                    boltSize, new Vector2(0f, boltSize.y * 0.5f), boltColor, null, _bolts.Release);

                enemy.Health.TryTakeDamage(RollDamage(data.DamageAt(level)));
                if (chainTargets > 0) Chain(enemy);

                if (leaveField)
                {
                    DamageZone zone = _zones.Get(point, Quaternion.identity);
                    zone.Begin(player.Stats, fieldDamagePerSecond * levelScale, fieldRadius, fieldDuration, enemyMask, _zones.Release);
                }
            }

            return true;
        }

        private readonly HashSet<EnemyBase> _chained = new HashSet<EnemyBase>();
        private readonly List<EnemyBase> _near = new List<EnemyBase>();

        private void Chain(EnemyBase first)
        {
            _chained.Clear();
            _chained.Add(first);
            EnemyBase current = first;
            float coefficient = data.DamageAt(level);
            for (int i = 0; i < chainTargets; i++)
            {
                CombatQuery.EnemiesWithin(current.transform.position, chainRadius, _near);
                EnemyBase next = null;
                float best = float.MaxValue;
                foreach (EnemyBase e in _near)
                {
                    if (_chained.Contains(e)) continue;
                    float d = ((Vector2)e.transform.position - (Vector2)current.transform.position).sqrMagnitude;
                    if (d < best)
                    {
                        best = d;
                        next = e;
                    }
                }

                if (next == null) return;
                coefficient *= 1f - chainFalloff;
                HistoryHeroes.Combat.AttackFx.Line(current.transform.position, next.transform.position, 0.1f, chainColor);
                next.Health.TryTakeDamage(RollDamage(coefficient));
                _chained.Add(next);
                current = next;
            }
        }
    }
}
