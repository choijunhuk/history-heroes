using HistoryHeroes.EditorTools;
using HistoryHeroes.Heroes;
using NUnit.Framework;
using UnityEngine;

namespace HistoryHeroes.Tests
{
    /// <summary>화살 수가 아니라 실제 정지 기본 공격 묶음을 두 번 센다.</summary>
    public sealed class TomoeCrossfireTests
    {
        private HeroData _hero;
        private TomoeCrossfireState _state;

        [SetUp]
        public void SetUp()
        {
            _hero = ScriptableObject.CreateInstance<HeroData>();
            TomoeDefinition.ConfigureHero(_hero);
            _state = new TomoeCrossfireState();
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(_hero);

        [Test]
        public void EverySecondStationaryPrimaryAttackSplits()
        {
            for (int attack = 1; attack <= 6; attack++)
                Assert.AreEqual(attack % 2 == 0, _state.RecordAttack(true, true, _hero), "공격 " + attack);
            Assert.AreEqual(0, _state.Count);
        }

        [Test]
        public void ExtraProjectilesAndSplitBranchesNeverAdvanceTheCounter()
        {
            Assert.IsFalse(_state.RecordAttack(true, true, _hero));
            for (int i = 0; i < 6; i++) Assert.IsFalse(_state.RecordAttack(true, false, _hero));
            Assert.AreEqual(1, _state.Count);
            Assert.IsTrue(_state.RecordAttack(true, true, _hero));
            for (int i = 0; i < 6; i++) Assert.IsFalse(_state.RecordAttack(true, false, _hero));
            Assert.AreEqual(0, _state.Count);
        }

        [Test]
        public void MovingBeforeSecondAttackRestartsThePair()
        {
            Assert.IsFalse(_state.RecordAttack(true, true, _hero));
            Assert.IsFalse(_state.RecordAttack(false, false, _hero));
            Assert.IsFalse(_state.RecordAttack(true, true, _hero));
            Assert.IsTrue(_state.RecordAttack(true, true, _hero));
        }

        [Test]
        public void NoTargetDoesNotAdvanceTheCounter()
        {
            Assert.IsFalse(_state.RecordAttack(true, false, _hero));
            Assert.AreEqual(0, _state.Count);
        }

        [Test]
        public void RoomChangeClearsThePartialPair()
        {
            _state.RecordAttack(true, true, _hero);
            _state.Reset();
            Assert.IsFalse(_state.RecordAttack(true, true, _hero));
        }

        [TestCase(1, 0.6f)]
        [TestCase(3, 0.7f)]
        [TestCase(5, 0.8f)]
        public void EachStarTierKeepsTwoShotsAndChangesOnlyBranchDamage(int stars, float coefficient)
        {
            HeroSignature signature = HeroSignature.For(_hero, stars);
            Assert.AreEqual(coefficient, signature.CrossShotCoefficient, 1e-5f);
            Assert.AreEqual(2, _hero.crossShotEvery);
            Assert.AreEqual(8f, _hero.crossShotHalfAngle);
            Assert.IsFalse(_state.RecordAttack(true, true, _hero));
            Assert.IsTrue(_state.RecordAttack(true, true, _hero));
        }

        [Test]
        public void OtherHeroCannotStartCrossfire()
        {
            HeroData other = ScriptableObject.CreateInstance<HeroData>();
            try
            {
                Assert.IsFalse(_state.RecordAttack(true, true, other));
                Assert.IsFalse(_state.RecordAttack(true, true, other));
                Assert.AreEqual(0f, HeroSignature.For(other, 5).CrossShotCoefficient);
            }
            finally { Object.DestroyImmediate(other); }
        }
    }
}
