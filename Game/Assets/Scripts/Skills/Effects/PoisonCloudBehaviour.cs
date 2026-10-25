using System.Collections.Generic;
using HistoryHeroes.Common;
using HistoryHeroes.Enemy;
using UnityEngine;

namespace HistoryHeroes.Skills
{
    /// <summary>
    /// 독 구름: 5초마다 적이 있는 곳에 독 구름 4초 (초당 25%, 겹치면 중첩), 레벨마다 지속 +1초 (Skills.md 2장).
    /// 맹독 개화 (진화, bloom): 초당 30%, 구름 안에서 죽은 적 자리에 작은 구름 2초 (번짐은 최대 3번 연쇄, 4-4).
    /// </summary>
    public sealed class PoisonCloudBehaviour : ActiveSkillBehaviour
    {
        [SerializeField] private Color color = new Color(0.45f, 0.85f, 0.3f, 0.4f);
        [SerializeField] private bool bloom;
        [SerializeField] private int maxBloomChain = 3;
        [SerializeField] private float bloomRadius = 0.9f;
        [SerializeField] private float bloomDuration = 2f;

        private readonly List<EnemyBase> _targets = new List<EnemyBase>();
        private ComponentPool<DamageZone> _zones;

        protected override bool Activate()
        {
            CombatQuery.RandomEnemiesOnScreen(data.CountAt(level), _targets);
            if (_targets.Count == 0) return false;

            _zones ??= DamageZone.CreatePool(EffectRoot, color);
            foreach (EnemyBase e in _targets) Spawn(e.transform.position, data.RadiusAt(level), data.DurationAt(level), 0);
            return true;
        }

        private void Spawn(Vector2 point, float radius, float duration, int chain)
        {
            DamageZone zone = _zones.Get(point, Quaternion.identity);
            System.Action<Vector2> onKill = null;
            if (bloom && chain < maxBloomChain) onKill = p => Spawn(p, bloomRadius, bloomDuration, chain + 1);
            zone.Begin(player.Stats, data.DamageAt(level), radius, duration, enemyMask, _zones.Release, onKill);
        }
    }
}
