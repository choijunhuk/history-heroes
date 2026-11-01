using HistoryHeroes.EditorTools;
using HistoryHeroes.Heroes;
using NUnit.Framework;
using UnityEngine;

namespace HistoryHeroes.Tests
{
    /// <summary>첫 명중 한 적만 표시하고 다음 기본 공격에 파동을 한 번 쓴다.</summary>
    public sealed class CurieObservationTests
    {
        private HeroData _hero;
        private CurieObservationState _state;

        [SetUp]
        public void SetUp()
        {
            _hero = ScriptableObject.CreateInstance<HeroData>();
            CurieDefinition.ConfigureHero(_hero);
            _state = new CurieObservationState();
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(_hero);

        [Test]
        public void FirstHitMarksOneEnemyAndNextAttackConsumesIt()
        {
            object enemy = new object();
            int first = _state.BeginAttack(out object before, out _);
            Assert.IsNull(before);
            Assert.IsTrue(_state.TryMark(enemy, 1, first, true));
            _state.BeginAttack(out object marked, out int life);
            Assert.AreSame(enemy, marked);
            Assert.AreEqual(1, life);
            Assert.IsFalse(_state.HasMark);
        }

        [Test]
        public void OneAttackCannotMarkTwoTargets()
        {
            int attack = _state.BeginAttack(out _, out _);
            Assert.IsTrue(_state.TryMark(new object(), 1, attack, true));
            Assert.IsFalse(_state.TryMark(new object(), 1, attack, true));
        }

        [Test]
        public void ExtraProjectileCannotMark()
        {
            int attack = _state.BeginAttack(out _, out _);
            Assert.IsFalse(_state.TryMark(new object(), 1, attack, false));
            Assert.IsFalse(_state.HasMark);
        }

        [Test]
        public void WaveAttackCannotMarkAgain()
        {
            object enemy = new object();
            int first = _state.BeginAttack(out _, out _);
            _state.TryMark(enemy, 1, first, true);
            int waveAttack = _state.BeginAttack(out _, out _);
            Assert.IsFalse(_state.TryMark(enemy, 1, waveAttack, true));
            Assert.IsFalse(_state.HasMark);
        }

        [Test]
        public void DelayedHitFromPreviousAttackCanMarkUntilConsumed()
        {
            int oldAttack = _state.BeginAttack(out _, out _);
            _state.BeginAttack(out _, out _);
            object enemy = new object();
            Assert.IsTrue(_state.TryMark(enemy, 1, oldAttack, true));
            _state.BeginAttack(out object marked, out int life);
            Assert.AreSame(enemy, marked);
            Assert.AreEqual(1, life);
        }

        [Test]
        public void HitFromBeforeConsumedWaveCannotMarkAgain()
        {
            int oldAttack = _state.BeginAttack(out _, out _);
            int markingAttack = _state.BeginAttack(out _, out _);
            Assert.IsTrue(_state.TryMark(new object(), 1, markingAttack, true));
            _state.BeginAttack(out _, out _);
            Assert.IsFalse(_state.TryMark(new object(), 1, oldAttack, true));
            Assert.IsFalse(_state.HasMark);
        }

        [Test]
        public void MovingKeepsExistingMarkButInvalidatesOldProjectile()
        {
            object enemy = new object();
            int attack = _state.BeginAttack(out _, out _);
            _state.TryMark(enemy, 1, attack, true);
            _state.InvalidateAttack();
            Assert.IsFalse(_state.TryMark(new object(), 1, attack, true));
            _state.BeginAttack(out object marked, out int life);
            Assert.AreSame(enemy, marked);
            Assert.AreEqual(1, life);
        }

        [Test]
        public void DeadOrReusedPooledEnemyCannotReleaseWave()
        {
            object enemy = new object();
            Assert.IsTrue(CurieObservationState.CanRelease(enemy, 1, 1, true));
            Assert.IsFalse(CurieObservationState.CanRelease(enemy, 1, 1, false));
            Assert.IsFalse(CurieObservationState.CanRelease(enemy, 1, 2, true));
            Assert.IsFalse(CurieObservationState.CanRelease(null, 1, 1, true));
        }

        [Test]
        public void RoomChangeClearsExistingMark()
        {
            int attack = _state.BeginAttack(out _, out _);
            _state.TryMark(new object(), 1, attack, true);
            _state.Reset();
            _state.BeginAttack(out object marked, out _);
            Assert.IsNull(marked);
        }

        [TestCase(1, 0.4f, 1.2f)]
        [TestCase(3, 0.55f, 1.2f)]
        [TestCase(5, 0.55f, 1.6f)]
        public void StarsChangeWaveDamageThenRadius(int stars, float coefficient, float radius)
        {
            HeroSignature signature = HeroSignature.For(_hero, stars);
            Assert.AreEqual(coefficient, signature.ObservationWaveCoefficient, 1e-5f);
            Assert.AreEqual(radius, signature.ObservationWaveRadius, 1e-5f);
        }

        [Test]
        public void OtherHeroHasNoObservationWave()
        {
            HeroData other = ScriptableObject.CreateInstance<HeroData>();
            try
            {
                HeroSignature signature = HeroSignature.For(other, 5);
                Assert.AreEqual(0f, signature.ObservationWaveCoefficient);
                Assert.AreEqual(0f, signature.ObservationWaveRadius);
            }
            finally { Object.DestroyImmediate(other); }
        }
    }
}
