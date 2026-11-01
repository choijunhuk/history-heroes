using HistoryHeroes.EditorTools;
using HistoryHeroes.Enemy;
using HistoryHeroes.Heroes;
using NUnit.Framework;
using UnityEngine;

namespace HistoryHeroes.Tests
{
    /// <summary>같은 적의 첫 기본 공격 명중을 셋 센 뒤 한 대상만 제어한다.</summary>
    public sealed class WuzetianSealTests
    {
        private HeroData _hero;
        private WuzetianSealState _state;

        [SetUp]
        public void SetUp()
        {
            _hero = ScriptableObject.CreateInstance<HeroData>();
            WuzetianDefinition.ConfigureHero(_hero);
            _state = new WuzetianSealState();
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(_hero);

        [Test]
        public void ThirdConsecutiveHitOnSameEnemyTriggersOnce()
        {
            object enemy = new object();
            Assert.IsFalse(_state.RecordHit(enemy, 1, true, true, _hero.sealHits));
            Assert.IsFalse(_state.RecordHit(enemy, 1, true, true, _hero.sealHits));
            Assert.IsTrue(_state.RecordHit(enemy, 1, true, true, _hero.sealHits));
            Assert.AreEqual(0, _state.Hits);
        }

        [Test]
        public void DifferentEnemyOrPoolLifeStartsAnew()
        {
            object first = new object();
            object next = new object();
            _state.RecordHit(first, 1, true, true, 3);
            _state.RecordHit(first, 1, true, true, 3);
            Assert.IsFalse(_state.RecordHit(next, 1, true, true, 3));
            Assert.IsFalse(_state.RecordHit(next, 2, true, true, 3));
            Assert.AreEqual(1, _state.Hits);
        }

        [Test]
        public void ExtraProjectilesAndChainHitsDoNotCount()
        {
            object enemy = new object();
            _state.RecordHit(enemy, 1, true, true, 3);
            for (int i = 0; i < 5; i++) Assert.IsFalse(_state.RecordHit(enemy, 1, true, false, 3));
            Assert.AreEqual(1, _state.Hits);
        }

        [Test]
        public void MovementClearsStreak()
        {
            object enemy = new object();
            _state.RecordHit(enemy, 1, true, true, 3);
            _state.RecordHit(enemy, 1, true, true, 3);
            Assert.IsFalse(_state.RecordHit(enemy, 1, false, true, 3));
            Assert.IsFalse(_state.RecordHit(enemy, 1, true, true, 3));
            Assert.AreEqual(1, _state.Hits);
        }

        [Test]
        public void OnlyOneOtherNearbyNormalEnemyCanBeChosen()
        {
            Assert.IsTrue(WuzetianSealState.CanRoot(false, false, false, 4f, 2.5f));
            Assert.IsFalse(WuzetianSealState.CanRoot(false, false, false, 9f, 2.5f));
            Assert.IsFalse(WuzetianSealState.CanRoot(false, false, true, 0f, 2.5f));
            Assert.IsFalse(WuzetianSealState.CanRoot(true, false, false, 4f, 2.5f));
            Assert.IsFalse(WuzetianSealState.CanRoot(false, true, false, 4f, 2.5f));
        }

        [Test]
        public void NormalTargetRootsAndBossGetsSmallSlow()
        {
            object normal = new object();
            object boss = new object();
            Assert.IsTrue(_state.TryControl(normal, 1, 1f, false, false, _hero, out float root));
            Assert.AreEqual(1f, root);
            Assert.IsTrue(_state.TryControl(boss, 1, 1f, true, false, _hero, out float slow));
            Assert.AreEqual(0.15f, slow, 1e-5f);
        }

        [Test]
        public void BossSlowCannotExceedTwentyPercent()
        {
            _hero.sealBossSlow = 0.8f;
            Assert.IsTrue(_state.TryControl(new object(), 1, 1f, true, false, _hero, out float slow));
            Assert.AreEqual(0.2f, slow, 1e-5f);
        }

        [Test]
        public void EliteIsNotControlled()
        {
            Assert.IsFalse(_state.TryControl(new object(), 1, 1f, false, true, _hero, out float slow));
            Assert.AreEqual(0f, slow);
        }

        [Test]
        public void MovementDoesNotBypassSixSecondControlLimit()
        {
            object target = new object();
            Assert.IsTrue(_state.TryControl(target, 1, 1f, true, false, _hero, out _));
            _state.Reset(false);
            Assert.IsFalse(_state.TryControl(target, 1, 6.99f, true, false, _hero, out _));
            Assert.IsTrue(_state.TryControl(target, 1, 7f, true, false, _hero, out _));
        }

        [Test]
        public void WeakSlowChannelDoesNotExtendStrongRoot()
        {
            var channels = new SlowChannels();
            object root = new object();
            object weak = new object();
            channels.Apply(1f, 0.6f, 1f, root);
            channels.Apply(0.15f, 5f, 1.1f, weak);
            Assert.AreEqual(1f, channels.FractionAt(1.5f));
            Assert.AreEqual(0.15f, channels.FractionAt(1.61f), 1e-5f);
        }

        [TestCase(1, 0.6f, 2.5f)]
        [TestCase(3, 0.8f, 2.5f)]
        [TestCase(5, 0.8f, 3f)]
        public void StarsChangeDurationThenSearchRadius(int stars, float duration, float radius)
        {
            HeroSignature signature = HeroSignature.For(_hero, stars);
            Assert.AreEqual(duration, signature.SealDuration, 1e-5f);
            Assert.AreEqual(radius, signature.SealRadius, 1e-5f);
            Assert.AreEqual(3, _hero.sealHits);
            Assert.AreEqual(6f, _hero.sealCooldown);
        }

        [Test]
        public void OtherHeroHasNoSealSignature()
        {
            HeroData other = ScriptableObject.CreateInstance<HeroData>();
            try
            {
                Assert.IsFalse(_state.RecordHit(new object(), 1, true, true, other.sealHits));
                Assert.AreEqual(0f, HeroSignature.For(other, 5).SealDuration);
                Assert.AreEqual(0f, HeroSignature.For(other, 5).SealRadius);
            }
            finally { Object.DestroyImmediate(other); }
        }
    }
}
