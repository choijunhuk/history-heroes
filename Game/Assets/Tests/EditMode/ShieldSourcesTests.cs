using HistoryHeroes.Combat;
using HistoryHeroes.Stats;
using NUnit.Framework;
using UnityEngine;

namespace HistoryHeroes.Tests
{
    public sealed class ShieldSourcesTests
    {
        [Test]
        public void LargestSourceAbsorbsAndEverySourceSpendsTheSameAmount()
        {
            var shields = new ShieldSources();
            var guard = new object();
            var breath = new object();
            shields.SetAtLeast(guard, 100f, float.PositiveInfinity);
            shields.SetAtLeast(breath, 50f, 2f);
            Assert.AreEqual(100f, shields.Maximum(0f));
            Assert.AreEqual(30f, shields.Absorb(30f, 0f));
            Assert.AreEqual(70f, shields.Remaining(guard, 0f));
            Assert.AreEqual(20f, shields.Remaining(breath, 0f));
            Assert.AreEqual(70f, shields.Absorb(90f, 0f));
            Assert.AreEqual(0f, shields.Maximum(0f));
        }

        [Test]
        public void ExpiryAndRemovalNeverRefillOrRemoveAnotherSource()
        {
            var shields = new ShieldSources();
            var guard = new object();
            var breath = new object();
            shields.SetAtLeast(guard, 100f, float.PositiveInfinity);
            shields.SetAtLeast(breath, 50f, 2f);
            shields.Absorb(30f, 1f);
            shields.Expire(2f);
            Assert.AreEqual(0f, shields.Remaining(breath, 2f));
            Assert.AreEqual(70f, shields.Remaining(guard, 2f));
            shields.SetAtLeast(breath, 50f, 5f);
            shields.Remove(breath);
            Assert.AreEqual(70f, shields.Maximum(2f));
        }

        [Test]
        public void RemovingLargerSourceExposesOnlyUnspentSmallerAmount()
        {
            var shields = new ShieldSources();
            var guard = new object();
            var breath = new object();
            shields.SetAtLeast(guard, 30f, float.PositiveInfinity);
            shields.SetAtLeast(breath, 50f, 2f);
            shields.Absorb(10f, 0f);
            shields.Remove(breath);
            Assert.AreEqual(20f, shields.Maximum(0f));
        }

        [Test]
        public void HealthReducesDamageBeforeShieldAndOnlyGuardDepletionNotifiesBreak()
        {
            var go = new GameObject("ShieldSourcesTest");
            try
            {
                var stats = new StatSheet();
                stats.SetBase(StatType.DamageReduction, 0.5f);
                Health health = go.AddComponent<Health>();
                health.Init(Team.Player, 100f, stats);
                int breaks = 0;
                health.ShieldBroken += () => breaks++;
                var breath = new object();
                health.SetShieldAtLeast(20f);
                health.SetShieldFrom(breath, 50f, 2f, Time.time);
                var hit = new DamageInfo { Amount = 60f, AttackerTeam = Team.Enemy };
                Assert.IsTrue(health.TryTakeDamage(hit, out float dealt));
                Assert.AreEqual(0f, dealt);
                Assert.AreEqual(100f, health.Current);
                Assert.AreEqual(20f, health.Shield, 0.001f);
                Assert.AreEqual(1, breaks, "철벽 자신의 잔량이 피해로 소진될 때 한 번 알린다");
                health.ExpireShields(Time.time + 2f);
                Assert.AreEqual(0f, health.Shield);
                Assert.AreEqual(1, breaks, "만료는 파괴 반응을 일으키지 않는다");
            }
            finally { Object.DestroyImmediate(go); }
        }

        [Test]
        public void BuddhaOnlyShieldDoesNotTriggerIronwallBreakAndLegacyApiStillUsesMaximum()
        {
            var go = new GameObject("LegacyShieldTest");
            try
            {
                Health health = go.AddComponent<Health>();
                health.Init(Team.Player, 100f);
                int breaks = 0;
                health.ShieldBroken += () => breaks++;
                var breath = new object();
                health.SetShieldFrom(breath, 10f, 2f, Time.time);
                health.TryTakeDamage(new DamageInfo { Amount = 15f, AttackerTeam = Team.Enemy });
                Assert.AreEqual(95f, health.Current);
                Assert.AreEqual(0, breaks);
                health.SetShieldAtLeast(20f);
                health.SetShieldAtLeast(10f);
                Assert.AreEqual(20f, health.Shield);
                health.SetShieldFrom(breath, 10f, 2f, Time.time);
                health.ClearShieldFrom(breath);
                Assert.AreEqual(20f, health.Shield);
                health.ClearShield();
                Assert.AreEqual(0f, health.Shield);
                Assert.AreEqual(0, breaks);
            }
            finally { Object.DestroyImmediate(go); }
        }

        [Test]
        public void NewLifeDoesNotInheritAnyShieldSource()
        {
            var go = new GameObject("ShieldLifeTest");
            try
            {
                Health health = go.AddComponent<Health>();
                health.Init(Team.Player, 100f);
                health.SetShieldAtLeast(20f);
                health.SetShieldFrom(new object(), 50f, 2f, Time.time);
                health.Init(Team.Player, 100f);
                Assert.AreEqual(0f, health.Shield);
            }
            finally { Object.DestroyImmediate(go); }
        }
    

        [Test]
        public void GuardShieldIgnoresSourcedShields_ForTheGuardVisual()
        {
            var go = new GameObject("GuardShieldProbe");
            try
            {
                Health health = go.AddComponent<Health>();
                health.Init(Team.Player, 100f);
                health.SetShieldAtLeast(20f);
                var buddha = new object();
                health.SetShieldFrom(buddha, 50f, 10f, Time.time);
                Assert.AreEqual(20f, health.GuardShield, 0.001f);
                health.TryTakeDamage(new DamageInfo { Amount = 30f, AttackerTeam = Team.Enemy, Kind = DamageKind.BasicAttack });
                Assert.AreEqual(0f, health.GuardShield, 0.001f, "철벽이 닳으면 방패 연출을 끈다");
                Assert.Greater(health.Shield, 0f, "부처 보호막은 남는다");
            }
            finally { Object.DestroyImmediate(go); }
        }
    }
}
