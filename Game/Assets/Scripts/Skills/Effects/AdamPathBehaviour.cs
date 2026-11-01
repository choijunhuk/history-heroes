using System.Collections.Generic;
using HistoryHeroes.Combat;
using HistoryHeroes.Enemy;
using UnityEngine;

namespace HistoryHeroes.Skills
{
    /// <summary>목봉 타격은 몸을 옮기거나 적을 밀지 않으며, 두 번째 타격도 처음 자리에 남는다.</summary>
    public sealed class AdamPathBehaviour : ActiveSkillBehaviour
    {
        [SerializeField] private float arcDegrees = 140f;
        private readonly SkillPulseSequence _sequence = new SkillPulseSequence();
        private readonly FixedArcState _arc = new FixedArcState();
        private readonly List<EnemyBase> _hits = new List<EnemyBase>();
        private float _coefficient;

        protected override bool Activate()
        {
            if (player == null || player.Health.IsDead || player.Hero.id != data.heroOnlyId) return false;
            Vector2 center = player.transform.position;
            EnemyBase target = CombatQuery.Nearest(center);
            if (target == null || ((Vector2)target.transform.position - center).sqrMagnitude > data.RadiusAt(level) * data.RadiusAt(level)) return false;
            _arc.Set(center, (Vector2)target.transform.position - center, data.RadiusAt(level), arcDegrees);
            _coefficient = data.DamageAt(level);
            _sequence.Start(data.CountAt(level), data.DurationAt(level), Time.time, player.SignatureLifecycleGeneration);
            Pulse();
            return true;
        }

        protected override void Update()
        {
            if (IsPaused) return;
            Pulse();
            base.Update();
        }

        private void Pulse()
        {
            if (player == null) { _sequence.Cancel(); return; }
            if (!_sequence.TryPulse(Time.time, player.SignatureLifecycleGeneration,
                !player.Health.IsDead && player.Hero.id == data.heroOnlyId)) return;
            AttackFx.Arc(_arc.Center, _arc.Direction, _arc.Radius, _arc.Degrees,
                new Color(0.76f, 0.69f, 0.5f, 0.3f), 0.16f);
            CombatQuery.EnemiesWithin(_arc.Center, _arc.Radius, _hits);
            foreach (EnemyBase enemy in _hits)
                if (enemy != null && !enemy.Health.IsDead && _arc.Contains(enemy.transform.position))
                    enemy.Health.TryTakeDamage(RollDamage(_coefficient));
        }

        public override void Dispose()
        {
            _sequence.Cancel();
            base.Dispose();
        }

        private void OnDisable() => _sequence.Cancel();
    }
}
