using HistoryHeroes.Combat;
using NUnit.Framework;
using UnityEngine;

namespace HistoryHeroes.Tests
{
    public sealed class GodTrialsFinalHealthTests
    {
        [Test]
        public void ProxyReturnsRealDamageWithoutSpendingItsOwnLife()
        {
            var bossObject = new GameObject("TrialBossHealth");
            var formObject = new GameObject("TrialFormHealth");
            try
            {
                Health boss = bossObject.AddComponent<Health>();
                boss.Init(Team.Enemy, 200f);
                Health form = formObject.AddComponent<Health>();
                form.Init(Team.Enemy, 100f);
                form.DamageRedirect = boss.TryTakeDamage;
                DamageLog.Reset();

                var hit = new DamageInfo { Amount = 60f, AttackerTeam = Team.Player, Kind = DamageKind.BasicAttack };
                Assert.IsTrue(form.TryTakeDamage(hit, out float dealt));
                Assert.AreEqual(60f, dealt);
                Assert.AreEqual(140f, boss.Current);
                Assert.AreEqual(100f, form.Current);
                Assert.AreEqual(60f, DamageLog.BasicAttack);
            }
            finally
            {
                Object.DestroyImmediate(formObject);
                Object.DestroyImmediate(bossObject);
            }
        }

        [Test]
        public void FinalCapKeepsOtherTrialFormAliveAfterIncomingBonus()
        {
            var go = new GameObject("TrialHealthTest");
            try
            {
                Health health = go.AddComponent<Health>();
                health.Init(Team.Enemy, 200f);
                health.Guard = hit =>
                {
                    hit.Amount = Mathf.Min(hit.Amount, 100f);
                    return hit;
                };
                health.IncomingModifier = hit =>
                {
                    hit.Amount *= 2f;
                    return hit;
                };
                health.DamageCap = _ => 100f;

                var hit = new DamageInfo { Amount = 200f, AttackerTeam = Team.Player, Kind = DamageKind.BasicAttack };
                Assert.IsTrue(health.TryTakeDamage(hit, out float dealt));
                Assert.AreEqual(100f, dealt);
                Assert.AreEqual(100f, health.Current);
                Assert.IsFalse(health.IsDead);
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }
    }
}
