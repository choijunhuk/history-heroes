using HistoryHeroes.Combat;
using HistoryHeroes.Enemy;
using HistoryHeroes.Skills;
using NUnit.Framework;
using UnityEngine;

namespace HistoryHeroes.Tests
{
    public sealed class BuddhaAdamActiveTests
    {
        [TestCase(1, 0f, 1)]
        [TestCase(2, 0.6f, 2)]
        [TestCase(2, 0.3f, 2)]
        public void PulseCountAndIntervalFollowData(int count, float interval, int expected)
        {
            var sequence = new SkillPulseSequence();
            sequence.Start(count, interval, 10f, 7);
            int pulses = sequence.TryPulse(10f, 7, true) ? 1 : 0;
            Assert.AreEqual(1, pulses);
            if (count > 1)
            {
                Assert.IsFalse(sequence.TryPulse(10f + interval - 0.01f, 7, true));
                if (sequence.TryPulse(10f + interval, 7, true)) pulses++;
            }
            Assert.IsFalse(sequence.TryPulse(100f, 7, true));
            Assert.AreEqual(0, sequence.Remaining);
            Assert.AreEqual(expected, pulses);
        }

        [TestCase(true)]
        [TestCase(false)]
        public void RoomOrHeroGenerationChangeAndDeathCancelPendingPulse(bool alive)
        {
            var sequence = new SkillPulseSequence();
            sequence.Start(2, 0.6f, 0f, 5);
            Assert.IsTrue(sequence.TryPulse(0f, 5, true));
            Assert.IsFalse(sequence.TryPulse(0.6f, alive ? 6 : 5, alive));
            Assert.AreEqual(0, sequence.Remaining);
            Assert.IsFalse(sequence.TryPulse(2f, 5, true), "지난 판의 회복이 되살아나지 않는다");
        }

        [Test]
        public void RestRequiresAnEnemyWithinSixIncludingTheBoundary()
        {
            Assert.IsTrue(BuddhaRestBehaviour.InRange(Vector2.zero, new Vector2(6f, 0f), 6f));
            Assert.IsTrue(BuddhaRestBehaviour.InRange(Vector2.zero, new Vector2(3f, 4f), 6f));
            Assert.IsFalse(BuddhaRestBehaviour.InRange(Vector2.zero, new Vector2(6.01f, 0f), 6f));
        }

        [Test]
        public void RestWaitsForALivingEnemyInRangeAndIgnoresDeadEnemies()
        {
            Assert.IsFalse(BuddhaRestBehaviour.HasEnemyInRange(Vector2.zero, 6f, new EnemyBase[0]));
            var go = new GameObject("RestTarget");
            go.SetActive(false);
            try
            {
                ChargerEnemy enemy = go.AddComponent<ChargerEnemy>();
                Health health = go.GetComponent<Health>();
                health.Init(Team.Enemy, 100f);
                typeof(EnemyBase).GetField("health", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                    .SetValue(enemy, health);
                var targets = new EnemyBase[] { null, enemy };
                go.transform.position = new Vector2(6.01f, 0f);
                Assert.IsFalse(BuddhaRestBehaviour.HasEnemyInRange(Vector2.zero, 6f, targets));
                go.transform.position = new Vector2(6f, 0f);
                Assert.IsTrue(BuddhaRestBehaviour.HasEnemyInRange(Vector2.zero, 6f, targets));
                health.Kill();
                Assert.IsFalse(BuddhaRestBehaviour.HasEnemyInRange(Vector2.zero, 6f, targets));
            }
            finally { Object.DestroyImmediate(go); }
        }

        [Test]
        public void RestHealingUsesMaxHpAndDiscardsOverhealWithoutReviving()
        {
            var go = new GameObject("RestHealingTest");
            try
            {
                Health health = go.AddComponent<Health>();
                health.Init(Team.Player, 1000f);
                health.TryTakeDamage(new DamageInfo { Amount = 10f, AttackerTeam = Team.Enemy });
                health.Heal(health.Max * 0.02f);
                Assert.AreEqual(1000f, health.Current);
                health.TryTakeDamage(new DamageInfo { Amount = 100f, AttackerTeam = Team.Enemy });
                Assert.AreEqual(900f, health.Current, "초과 회복은 보호막이나 다음 회복으로 쌓이지 않는다");
                health.Kill();
                health.Heal(100f);
                Assert.IsTrue(health.IsDead);
            }
            finally { Object.DestroyImmediate(go); }
        }

        [TestCase(140f, 2.8f)]
        [TestCase(160f, 3f)]
        public void PathKeepsTheOriginalCenterAndDirection(float angle, float radius)
        {
            var arc = new FixedArcState();
            var center = new Vector2(2f, 3f);
            var aim = Vector2.right;
            arc.Set(center, aim, radius, angle);
            center += Vector2.up * 10f;
            aim = Vector2.left;
            Assert.AreEqual(new Vector2(2f, 3f), arc.Center);
            Assert.AreEqual(Vector2.right, arc.Direction);
            Assert.IsTrue(arc.Contains(new Vector2(4f, 3f)));
            Assert.IsFalse(arc.Contains(new Vector2(0f, 3f)));
            Assert.IsFalse(arc.Contains(arc.Center + Vector2.right * (radius + 0.01f)));
            Assert.IsTrue(arc.Contains(arc.Center + DirectionAt(angle * 0.5f - 0.1f)));
            Assert.IsFalse(arc.Contains(arc.Center + DirectionAt(angle * 0.5f + 0.1f)));
        }

        private static Vector2 DirectionAt(float degrees)
        {
            double radians = degrees * System.Math.PI / 180.0;
            return new Vector2((float)System.Math.Cos(radians), (float)System.Math.Sin(radians));
        }
    }
}
