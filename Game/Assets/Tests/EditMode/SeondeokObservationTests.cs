using HistoryHeroes.Heroes;
using NUnit.Framework;
using UnityEngine;

namespace HistoryHeroes.Tests
{
    /// <summary>선덕여왕의 관측은 실제 기본 공격 한 번에만 쓰인다.</summary>
    public sealed class SeondeokObservationTests
    {
        private HeroData _hero;
        private SeondeokObservationState _observation;

        [SetUp]
        public void SetUp()
        {
            _hero = ScriptableObject.CreateInstance<HeroData>();
            _hero.id = "seondeok";
            _hero.observationSeconds = 1f;
            _hero.observationAttackBonus = 0f;
            _hero.star3ObservationAttackBonus = 0.15f;
            _hero.star5ObservationAttackBonus = 0.3f;
            _observation = new SeondeokObservationState();
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(_hero);

        [TestCase(1, 0f)]
        [TestCase(2, 0f)]
        [TestCase(3, 0.15f)]
        [TestCase(4, 0.15f)]
        [TestCase(5, 0.3f)]
        public void OnlySpecifiedStarsBoostThePreparedBasicAttack(int stars, float bonus)
        {
            Assert.AreEqual(bonus, HeroSignature.For(_hero, stars).ObservationAttackBonus, 1e-5f);
            Assert.IsFalse(HeroSignature.For(_hero, stars).FirstShotPierce,
                "관측 관통은 멈춘 뒤 매번 준비하며 첫 발 각성과 별개다");
        }

        [Test]
        public void StarTextDescribesThePreparedShotBonus()
        {
            string text = HeroSignature.StarText(_hero);
            StringAssert.Contains("★3 관측 공격 피해 +15%", text);
            StringAssert.Contains("★5 관측 공격 피해 +30%", text);
        }

        [Test]
        public void OneContinuousSecondPreparesOnePiercingAttack()
        {
            _observation.Tick(0.99f, true, _hero);
            Assert.IsFalse(_observation.IsReady);
            Assert.AreEqual(0.99f, _observation.Progress, 1e-5f);
            Assert.IsFalse(_observation.TryConsume());
            _observation.Tick(0.01f, true, _hero);
            Assert.IsTrue(_observation.IsReady);
            Assert.AreEqual(1f, _observation.Progress);
            Assert.IsTrue(_observation.TryConsume());
            Assert.IsFalse(_observation.TryConsume(), "한 번 발사한 뒤 같은 준비를 다시 쓰지 못한다");
        }

        [Test]
        public void MovementCancelsPartialAndReadyObservation()
        {
            _observation.Tick(0.7f, true, _hero);
            _observation.Tick(0.01f, false, _hero);
            Assert.AreEqual(0f, _observation.Progress);
            _observation.Tick(0.9f, true, _hero);
            Assert.IsFalse(_observation.IsReady);
            _observation.Tick(0.1f, true, _hero);
            Assert.IsTrue(_observation.IsReady);
            _observation.Tick(0f, false, _hero);
            Assert.IsFalse(_observation.IsReady);
            Assert.IsFalse(_observation.TryConsume());
        }

        [Test]
        public void ReadyObservationWaitsForARealAttackAndNeedsOneSecondAgain()
        {
            _observation.Tick(1f, true, _hero);
            _observation.Tick(5f, true, _hero); // 표적이 없으면 준비를 계속 보관한다.
            Assert.IsTrue(_observation.IsReady);
            Assert.IsTrue(_observation.TryConsume());
            _observation.Tick(0.9f, true, _hero);
            Assert.IsFalse(_observation.IsReady);
            _observation.Tick(0.1f, true, _hero);
            Assert.IsTrue(_observation.IsReady);
        }

        [Test]
        public void PausedOrNegativeTimeDoesNotAdvanceObservation()
        {
            _observation.Tick(0f, true, _hero);
            _observation.Tick(-1f, true, _hero);
            Assert.AreEqual(0f, _observation.Progress);
            Assert.IsFalse(_observation.IsReady);
            _observation.Tick(1f, true, _hero);
            _observation.Tick(-1f, true, _hero);
            Assert.IsTrue(_observation.IsReady);
        }

        [Test]
        public void ResetAndHeroesWithoutObservationCannotKeepAPreparedAttack()
        {
            _observation.Tick(1f, true, _hero);
            _observation.Reset();
            Assert.IsFalse(_observation.IsReady);
            Assert.AreEqual(0f, _observation.Progress);

            _observation.Tick(1f, true, _hero);
            var other = ScriptableObject.CreateInstance<HeroData>();
            try
            {
                _observation.Tick(1f, true, other);
                Assert.IsFalse(_observation.IsReady);
                Assert.IsFalse(_observation.TryConsume());
                _observation.Tick(1f, true, null);
                Assert.AreEqual(0f, _observation.Progress);
            }
            finally
            {
                Object.DestroyImmediate(other);
            }
        }
    }
}
