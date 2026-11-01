using HistoryHeroes.EditorTools;
using HistoryHeroes.Heroes;
using NUnit.Framework;
using UnityEngine;

namespace HistoryHeroes.Tests
{
    /// <summary>수호상은 영웅의 실제 정지 기본 공격을 센다.</summary>
    public sealed class CleopatraGuardianTests
    {
        private HeroData _hero;
        private CleopatraGuardianState _guardian;

        [SetUp]
        public void SetUp()
        {
            _hero = ScriptableObject.CreateInstance<HeroData>();
            CleopatraDefinition.ConfigureHero(_hero);
            _guardian = new CleopatraGuardianState();
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(_hero);

        [Test]
        public void ThreeStationaryAttacksPrepareButDoNotHit()
        {
            for (int i = 1; i <= 3; i++)
            {
                Assert.IsFalse(_guardian.RecordAttack(true, true, _hero));
                Assert.AreEqual(i, _guardian.Count);
            }
            Assert.IsTrue(_guardian.IsReady);
        }

        [Test]
        public void FourthStationaryAttackConsumesExactlyOneGuardianHit()
        {
            for (int i = 0; i < 3; i++) _guardian.RecordAttack(true, true, _hero);
            Assert.IsTrue(_guardian.RecordAttack(true, true, _hero));
            Assert.IsFalse(_guardian.IsReady);
            Assert.AreEqual(0, _guardian.Count);
            Assert.IsFalse(_guardian.RecordAttack(true, false, _hero));
            Assert.IsFalse(_guardian.RecordAttack(true, true, _hero));
        }

        [Test]
        public void RepeatedStationaryAttacksRequireAnotherFullPreparation()
        {
            for (int i = 0; i < 8; i++)
                Assert.AreEqual(i == 3 || i == 7, _guardian.RecordAttack(true, true, _hero), "기본 공격 " + (i + 1));
            Assert.AreEqual(0, _guardian.Count);
        }

        [Test]
        public void MovingResetsPartialPreparation()
        {
            _guardian.RecordAttack(true, true, _hero);
            _guardian.RecordAttack(true, true, _hero);
            Assert.IsFalse(_guardian.RecordAttack(false, false, _hero));
            Assert.AreEqual(0, _guardian.Count);
            Assert.IsFalse(_guardian.IsReady);
            Assert.IsFalse(_guardian.RecordAttack(true, true, _hero));
            Assert.AreEqual(1, _guardian.Count);
        }

        [Test]
        public void MovingCancelsReadyGuardianWithoutAHit()
        {
            for (int i = 0; i < 3; i++) _guardian.RecordAttack(true, true, _hero);
            Assert.IsFalse(_guardian.RecordAttack(false, true, _hero));
            Assert.IsFalse(_guardian.IsReady);
            Assert.AreEqual(0, _guardian.Count);
        }

        [Test]
        public void ExtraProjectilesAndChainHitsDoNotChangePreparation()
        {
            _guardian.RecordAttack(true, true, _hero);
            for (int i = 0; i < 5; i++) Assert.IsFalse(_guardian.RecordAttack(true, false, _hero));
            Assert.AreEqual(1, _guardian.Count);
            _guardian.RecordAttack(true, true, _hero);
            _guardian.RecordAttack(true, true, _hero);
            for (int i = 0; i < 5; i++) Assert.IsFalse(_guardian.RecordAttack(true, false, _hero));
            Assert.IsTrue(_guardian.IsReady);
        }

        [Test]
        public void WaitingForTargetDoesNotConsumeReadyGuardian()
        {
            for (int i = 0; i < 3; i++) _guardian.RecordAttack(true, true, _hero);
            Assert.IsFalse(_guardian.RecordAttack(true, false, _hero));
            Assert.IsTrue(_guardian.IsReady);
            Assert.IsTrue(_guardian.RecordAttack(true, true, _hero));
        }

        [Test]
        public void RoomChangeOrHeroChangeResetPreparation()
        {
            for (int i = 0; i < 3; i++) _guardian.RecordAttack(true, true, _hero);
            _guardian.Reset();
            Assert.IsFalse(_guardian.IsReady);
            Assert.AreEqual(0, _guardian.Count);
            Assert.IsFalse(_guardian.RecordAttack(true, true, _hero));
        }

        [TestCase(1, 0.6f, 1)]
        [TestCase(2, 0.6f, 1)]
        [TestCase(3, 0.9f, 1)]
        [TestCase(4, 0.9f, 1)]
        [TestCase(5, 0.9f, 2)]
        public void StarsChangeOnlyGuardianDamageAndTargetCount(int stars, float coefficient, int targets)
        {
            HeroSignature signature = HeroSignature.For(_hero, stars);
            Assert.AreEqual(coefficient, signature.GuardianCoefficient, 1e-5f);
            Assert.AreEqual(targets, signature.GuardianTargets);
            Assert.AreEqual(3, _hero.guardianAttacks, "별 강화가 공유 영웅 정의의 준비 횟수를 바꾸면 안 된다");
            Assert.AreEqual(0.6f, _hero.guardianCoefficient, 1e-5f);
        }

        [TestCase(1, 0.6f, 1)]
        [TestCase(3, 0.9f, 1)]
        [TestCase(5, 0.9f, 2)]
        public void EachStarTierRequiresThreeUninterruptedPrimaryAttacksBeforeOneGuardianHit(int stars, float coefficient, int targets)
        {
            HeroSignature signature = HeroSignature.For(_hero, stars);
            Assert.AreEqual(coefficient, signature.GuardianCoefficient, 1e-5f);
            Assert.AreEqual(targets, signature.GuardianTargets);
            _guardian.RecordAttack(true, true, _hero);
            _guardian.RecordAttack(true, true, _hero);
            Assert.IsFalse(_guardian.RecordAttack(false, false, _hero), "이동이 준비 횟수를 지운다");
            for (int i = 0; i < 3; i++)
            {
                Assert.IsFalse(_guardian.RecordAttack(true, true, _hero));
                Assert.IsFalse(_guardian.RecordAttack(true, false, _hero), "멀티샷·연쇄 타격은 횟수를 쌓지 않는다");
            }
            Assert.IsTrue(_guardian.IsReady);
            Assert.IsTrue(_guardian.RecordAttack(true, true, _hero));
            Assert.IsFalse(_guardian.RecordAttack(true, false, _hero), "수호상 타격으로 다시 준비하지 않는다");
            Assert.AreEqual(0, _guardian.Count);
        }

        [Test]
        public void OtherHeroesCannotPrepareTheGuardian()
        {
            HeroData other = ScriptableObject.CreateInstance<HeroData>();
            try
            {
                for (int i = 0; i < 4; i++) Assert.IsFalse(_guardian.RecordAttack(true, true, other));
                Assert.AreEqual(0, _guardian.Count);
                Assert.IsFalse(_guardian.IsReady);
                Assert.AreEqual(0f, HeroSignature.For(other, 5).GuardianCoefficient);
                Assert.AreEqual(0, HeroSignature.For(other, 5).GuardianTargets);
            }
            finally { Object.DestroyImmediate(other); }
        }

        [Test]
        public void NullHeroCannotRetainPreparedGuardian()
        {
            for (int i = 0; i < 3; i++) _guardian.RecordAttack(true, true, _hero);
            Assert.IsFalse(_guardian.RecordAttack(true, true, null));
            Assert.IsFalse(_guardian.IsReady);
            Assert.AreEqual(0, _guardian.Count);
        }

        [Test]
        public void StarTextExplainsTheTwoGuardianUpgrades()
        {
            string text = HeroSignature.StarText(_hero);
            StringAssert.Contains("★3", text);
            StringAssert.Contains("90%", text);
            StringAssert.Contains("★5", text);
            StringAssert.Contains("2", text);
        }
    }
}
