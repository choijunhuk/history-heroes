using System.Collections.Generic;
using HistoryHeroes.Combat;
using HistoryHeroes.Enemy;
using UnityEngine;

namespace HistoryHeroes.Skills
{
    /// <summary>자기 회복은 공격력·기술 숙련·치명타를 거치지 않는다.</summary>
    public sealed class BuddhaRestBehaviour : ActiveSkillBehaviour
    {
        private readonly SkillPulseSequence _sequence = new SkillPulseSequence();
        private float _healFraction;

        public static bool InRange(Vector2 origin, Vector2 target, float radius) =>
            (target - origin).sqrMagnitude <= radius * radius;

        public static bool HasEnemyInRange(Vector2 origin, float radius, IReadOnlyList<EnemyBase> enemies)
        {
            foreach (EnemyBase enemy in enemies)
                if (enemy != null && enemy.Health != null && !enemy.Health.IsDead &&
                    InRange(origin, enemy.transform.position, radius)) return true;
            return false;
        }

        protected override bool Activate()
        {
            if (player == null || player.Health.IsDead || player.Hero.id != data.heroOnlyId) return false;
            if (!HasEnemyInRange(player.transform.position, data.RadiusAt(level), EnemyBase.Alive)) return false;
            _healFraction = data.HealFractionAt(level);
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
            player.Health.Heal(player.Health.Max * _healFraction);
            AttackFx.Burst(player.transform.position, 0.45f, new Color(0.85f, 0.76f, 0.48f, 0.25f), 0.2f);
        }

        public override void Dispose()
        {
            _sequence.Cancel();
            base.Dispose();
        }

        private void OnDisable() => _sequence.Cancel();
    }
}
