using HistoryHeroes.EditorTools;
using HistoryHeroes.Heroes;
using NUnit.Framework;
using UnityEngine;

namespace HistoryHeroes.Tests
{
    public sealed class BoudicaReclaimTests
    {
        private HeroData _hero;
        private BoudicaReclaimState _reclaim;

        [SetUp]
        public void SetUp()
        {
            _hero = ScriptableObject.CreateInstance<HeroData>();
            BoudicaDefinition.ConfigureHero(_hero);
            _reclaim = new BoudicaReclaimState();
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(_hero);

        [TestCase(1, 0.6f, 0f)]
        [TestCase(3, 0.9f, 0f)]
        [TestCase(5, 0.9f, 0.2f)]
        public void StarsKeepOneSecondPreparationAndSixSecondReuse(int stars, float distance, float bonus)
        {
            HeroSignature signature = HeroSignature.For(_hero, stars);
            Assert.AreEqual(distance, signature.ReclaimKnockback, 1e-5f);
            Assert.AreEqual(bonus, signature.ReclaimAttackBonus, 1e-5f);
            _reclaim.Tick(0.99f, true, _hero);
            Assert.IsFalse(_reclaim.TryConsume(_hero));
            _reclaim.Tick(0.01f, true, _hero);
            Assert.IsTrue(_reclaim.IsReady);
            Assert.IsTrue(_reclaim.TryConsume(_hero));
            Assert.IsFalse(_reclaim.TryConsume(_hero), "한 공격에 한 번만 사용한다");
            Assert.AreEqual(6f, _reclaim.CooldownRemaining);
            _reclaim.Tick(5f, true, _hero);
            Assert.IsFalse(_reclaim.IsReady);
            Assert.AreEqual(0f, _reclaim.Progress, "재사용 중에는 정지 시간을 쌓지 않는다");
            _reclaim.Tick(1f, true, _hero);
            Assert.AreEqual(0f, _reclaim.CooldownRemaining);
            Assert.IsFalse(_reclaim.IsReady);
            _reclaim.Tick(1f, true, _hero);
            Assert.IsTrue(_reclaim.TryConsume(_hero));
        }

        [TestCase(1, 0.18f)]
        [TestCase(3, 0.27f)]
        [TestCase(5, 0.27f)]
        public void BossKnockbackIsThirtyPercentAtEveryUpgrade(int stars, float expected)
        {
            HeroSignature signature = HeroSignature.For(_hero, stars);
            Assert.AreEqual(expected, BoudicaReclaimState.KnockbackDistance(_hero, signature, true), 1e-5f);
            Assert.AreEqual(signature.ReclaimKnockback,
                BoudicaReclaimState.KnockbackDistance(_hero, signature, false), 1e-5f);
        }

        [Test]
        public void MovementCancelsBothPartialAndReadyPreparation()
        {
            _reclaim.Tick(0.8f, true, _hero);
            _reclaim.Tick(0f, false, _hero);
            Assert.AreEqual(0f, _reclaim.Progress);
            _reclaim.Tick(0.9f, true, _hero);
            Assert.IsFalse(_reclaim.IsReady);
            _reclaim.Tick(0.1f, true, _hero);
            Assert.IsTrue(_reclaim.IsReady);
            _reclaim.Tick(0f, false, _hero);
            Assert.IsFalse(_reclaim.TryConsume(_hero));
            Assert.AreEqual(0f, _reclaim.CooldownRemaining);
        }

        [Test]
        public void ReadyAttackWaitsForATargetWithoutStartingCooldown()
        {
            _reclaim.Tick(1f, true, _hero);
            _reclaim.Tick(30f, true, _hero);
            Assert.IsTrue(_reclaim.IsReady);
            Assert.AreEqual(1f, _reclaim.Progress);
            Assert.AreEqual(0f, _reclaim.CooldownRemaining);
            Assert.IsTrue(_reclaim.TryConsume(_hero));
            Assert.AreEqual(6f, _reclaim.CooldownRemaining);
        }

        [Test]
        public void MovementDoesNotResetOrPauseReuseTime()
        {
            _reclaim.Tick(1f, true, _hero);
            _reclaim.TryConsume(_hero);
            _reclaim.Tick(5.5f, false, _hero);
            Assert.AreEqual(0.5f, _reclaim.CooldownRemaining, 1e-5f);
            _reclaim.Tick(1f, true, _hero);
            Assert.AreEqual(0.5f, _reclaim.Progress, 1e-5f,
                "같은 프레임에서 재사용이 끝난 뒤 남은 시간만 준비에 쓴다");
            Assert.IsFalse(_reclaim.IsReady);
            _reclaim.Tick(0.5f, true, _hero);
            Assert.IsTrue(_reclaim.IsReady);
        }

        [Test]
        public void PausedAndNegativeTimeCannotPrepareOrShortenCooldown()
        {
            _reclaim.Tick(0f, true, _hero);
            _reclaim.Tick(-1f, true, _hero);
            Assert.AreEqual(0f, _reclaim.Progress);
            _reclaim.Tick(1f, true, _hero);
            _reclaim.TryConsume(_hero);
            _reclaim.Tick(0f, true, _hero);
            _reclaim.Tick(-1f, false, _hero);
            Assert.AreEqual(6f, _reclaim.CooldownRemaining);
        }

        [Test]
        public void ResetClearsPreparationAndCooldownForTheNextRoomOrHero()
        {
            _reclaim.Tick(1f, true, _hero);
            _reclaim.Reset();
            Assert.IsFalse(_reclaim.IsReady);
            Assert.AreEqual(0f, _reclaim.Progress);
            _reclaim.Tick(1f, true, _hero);
            _reclaim.TryConsume(_hero);
            _reclaim.Reset();
            Assert.AreEqual(0f, _reclaim.CooldownRemaining);
            _reclaim.Tick(1f, true, _hero);
            Assert.IsTrue(_reclaim.IsReady);
        }

        [Test]
        public void HeroesWithoutReclaimCannotPrepareConsumeOrKnockBack()
        {
            _reclaim.Tick(1f, true, _hero);
            _hero.reclaimStopSeconds = 0f;
            Assert.IsFalse(_reclaim.TryConsume(_hero));
            _reclaim.Tick(1f, true, _hero);
            Assert.IsFalse(_reclaim.IsReady);
            Assert.AreEqual(0f, _reclaim.Progress);
            Assert.AreEqual(0f, HeroSignature.For(_hero, 5).ReclaimKnockback);
            Assert.AreEqual(0f, HeroSignature.For(_hero, 5).ReclaimAttackBonus);
            Assert.AreEqual(0f, BoudicaReclaimState.KnockbackDistance(_hero, HeroSignature.For(_hero, 5), false));
            _reclaim.Tick(1f, true, null);
            Assert.IsFalse(_reclaim.TryConsume(null));
        }

        [Test]
        public void StarTextDescribesBothUpgrades()
        {
            string text = HeroSignature.StarText(_hero);
            StringAssert.Contains("★3 밀침 거리 0.9", text);
            StringAssert.Contains("★5 준비 공격 피해 +20%", text);
        }
    }
}
