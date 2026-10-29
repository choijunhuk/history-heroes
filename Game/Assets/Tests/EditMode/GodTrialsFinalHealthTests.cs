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
                health.FinalDamage = incoming => Mathf.Min(incoming, 100f);

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

        [Test]
        public void TwoFormTrial_LastHitClearsFloatResidue_SoTheBossCanDie()
        {
            // 형상별로 깎은 합과 본체 체력이 float 오차로 다르면 0.004가 남아 못 이겼다 (Codex 리뷰 🔴)
            var state = new HistoryHeroes.Enemy.ShadowTwinTrialState();
            state.Reset(112800f);
            float body = 112800f;
            var rng = new System.Random(7);
            bool second = false;
            for (int guard = 0; guard < 100000 && body > 0f; guard++)
            {
                if (!state.FirstAlive) second = true;
                else if (!state.SecondAlive) second = false;
                else second = rng.Next(2) == 1;
                float incoming = (float)(rng.NextDouble() * 777.77 + 0.1);
                bool otherDown = second ? !state.FirstAlive : !state.SecondAlive;
                float damage = HistoryHeroes.Enemy.ShadowTwinTrialState.FinalDamage(incoming, state.Remaining(second), otherDown, body);
                float dealt = Mathf.Min(body, damage);
                body -= dealt;
                state.Apply(second, dealt);
            }

            Assert.AreEqual(0f, body, "마지막 한 방이 본체를 정확히 0으로");
            Assert.IsTrue(state.Complete);
        }
    }
}
