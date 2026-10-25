using System.Collections.Generic;
using HistoryHeroes.Common;
using HistoryHeroes.Enemy;
using UnityEngine;

namespace HistoryHeroes.Skills
{
    /// <summary>
    /// 낙뢰: 3초마다 화면 안 무작위 적에게 번개 (120%). 레벨마다 번개 +1. (Skills.md 2장)
    /// 뇌화 폭풍 (합체, leaveField 켬): 번개 3개 각 130% + 떨어진 자리 장판 2초 (초당 30%). (Skills.md 4-4)
    /// </summary>
    public sealed class LightningBehaviour : ActiveSkillBehaviour
    {
        [SerializeField] private Color boltColor = new Color(1f, 0.95f, 0.45f);
        [SerializeField] private Vector2 boltSize = new Vector2(0.25f, 7f);

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
            if (leaveField) _zones ??= DamageZone.CreatePool(EffectRoot, fieldColor);

            // 합체 결과의 레벨 효과(Lv2 +25%, Lv3 +50%)를 장판에도 같은 비율로
            float levelScale = data.DamageAt(level) / Mathf.Max(0.01f, data.DamageAt(1));

            foreach (EnemyBase enemy in _targets)
            {
                Vector2 point = enemy.transform.position;
                _bolts.Get(point, Quaternion.identity).Play(point, null, 0f, 0f, 0.15f,
                    boltSize, new Vector2(0f, boltSize.y * 0.5f), boltColor, null, _bolts.Release);

                enemy.Health.TryTakeDamage(RollDamage(data.DamageAt(level)));

                if (leaveField)
                {
                    DamageZone zone = _zones.Get(point, Quaternion.identity);
                    zone.Begin(player.Stats, fieldDamagePerSecond * levelScale, fieldRadius, fieldDuration, enemyMask, _zones.Release);
                }
            }

            return true;
        }
    }
}
