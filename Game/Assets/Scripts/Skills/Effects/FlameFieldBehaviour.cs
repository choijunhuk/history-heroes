using HistoryHeroes.Common;
using UnityEngine;

namespace HistoryHeroes.Skills
{
    /// <summary>불꽃 장판: 4초마다 발밑에 장판 3초 (초당 30%). 레벨마다 범위 +15%. (Skills.md 2장)</summary>
    public sealed class FlameFieldBehaviour : ActiveSkillBehaviour
    {
        [SerializeField] private Color color = new Color(1f, 0.45f, 0.1f, 0.45f);

        private ComponentPool<DamageZone> _zones;

        protected override bool Activate()
        {
            _zones ??= DamageZone.CreatePool(EffectRoot, color);
            DamageZone zone = _zones.Get(player.transform.position, Quaternion.identity);
            zone.Begin(player.Stats, data.DamageAt(level), data.RadiusAt(level), data.DurationAt(level), enemyMask, _zones.Release);
            return true;
        }
    }
}
