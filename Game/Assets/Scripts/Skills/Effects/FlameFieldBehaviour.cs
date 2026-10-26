using HistoryHeroes.Common;
using UnityEngine;

namespace HistoryHeroes.Skills
{
    /// <summary>
    /// 불꽃 장판: 4초마다 발밑에 장판 3초 (초당 30%). 레벨마다 범위 +15%. (Skills.md 2장)
    /// 불꽃 발자국 (진화, trail): 움직인 자리에 불길 2초 (초당 35%, 폭 1.2). 원래 불꽃 장판도 계속 깔린다 (4-4).
    /// </summary>
    public sealed class FlameFieldBehaviour : ActiveSkillBehaviour
    {
        [SerializeField] private Color color = new Color(1f, 0.45f, 0.1f, 0.45f);

        private ComponentPool<DamageZone> _zones;

        [Header("불꽃 발자국")]
        [SerializeField] private bool trail;
        [SerializeField] private float trailInterval = 0.2f;
        [SerializeField] private float trailRadius = 0.6f;
        [SerializeField] private float trailDuration = 2f;
        [SerializeField] private float trailDamagePerSecond = 0.35f;
        private float _trailTimer;
        private Vector2 _lastTrail;

        protected override void Update()
        {
            base.Update();
            if (!trail || player == null || IsPaused || player.IsStopped) return;
            _trailTimer -= Time.deltaTime;
            Vector2 here = player.transform.position;
            if (_trailTimer > 0f || (here - _lastTrail).sqrMagnitude < trailRadius * trailRadius) return;
            _trailTimer = trailInterval;
            _lastTrail = here;
            _zones ??= DamageZone.CreatePool(EffectRoot, color, -1, data.id);
            _zones.Get(here, Quaternion.identity).Begin(player.Stats, trailDamagePerSecond, trailRadius, trailDuration, enemyMask, _zones.Release);
        }

        protected override bool Activate()
        {
            _zones ??= DamageZone.CreatePool(EffectRoot, color, -1, data.id);
            DamageZone zone = _zones.Get(player.transform.position, Quaternion.identity);
            zone.Begin(player.Stats, data.DamageAt(level), data.RadiusAt(level), data.DurationAt(level), enemyMask, _zones.Release);
            return true;
        }
    }
}
