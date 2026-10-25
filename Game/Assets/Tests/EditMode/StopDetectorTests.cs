using HistoryHeroes.Player;
using NUnit.Framework;
using UnityEngine;

namespace HistoryHeroes.Tests
{
    /// <summary>멈춤 판정 규칙 확인 (Docs/Design/Stats.md 1-1).</summary>
    public class StopDetectorTests
    {
        [Test]
        public void StartsStopped()
        {
            Assert.IsTrue(new StopDetector().IsStopped);
        }

        [Test]
        public void MovingInput_ImmediatelyCountsAsMoving()
        {
            var d = new StopDetector();
            d.Tick(Vector2.right, 0.016f);
            Assert.IsFalse(d.IsStopped);
            Assert.AreEqual(0f, d.StoppedTime);
        }

        [Test]
        public void BecomesStoppedAfter100ms_WithoutInput()
        {
            var d = new StopDetector();
            d.Tick(Vector2.right, 0.016f);

            d.Tick(Vector2.zero, 0.09f);
            Assert.IsFalse(d.IsStopped, "0.09초는 아직 이동");

            d.Tick(Vector2.zero, 0.02f);
            Assert.IsTrue(d.IsStopped, "0.11초면 멈춤");
            Assert.AreEqual(0.01f, d.StoppedTime, 0.0001f);
        }

        [Test]
        public void MovingResetsStoppedTime()
        {
            var d = new StopDetector();
            d.Tick(Vector2.zero, 2f);
            Assert.Greater(d.StoppedTime, 1.5f);

            d.Tick(Vector2.up, 0.016f);
            d.Tick(Vector2.zero, 0.2f);
            Assert.AreEqual(0.1f, d.StoppedTime, 0.0001f);
        }

        [Test]
        public void TinyInput_IsTreatedAsNoInput()
        {
            var d = new StopDetector();
            d.Tick(new Vector2(0.05f, 0f), 0.2f);
            Assert.IsTrue(d.IsStopped);
        }

        [Test]
        public void Events_FireOnTransitions()
        {
            var d = new StopDetector();
            int stopped = 0, moved = 0;
            d.Stopped += () => stopped++;
            d.Moved += () => moved++;

            d.Tick(Vector2.right, 0.016f);
            d.Tick(Vector2.zero, 0.2f);
            d.Tick(Vector2.zero, 0.2f);

            Assert.AreEqual(1, moved);
            Assert.AreEqual(1, stopped);
        }

        [Test]
        public void ApplyDeadZone_ZeroesWeakInputSoMovementMatchesStopRule()
        {
            Assert.AreEqual(Vector2.zero, StopDetector.ApplyDeadZone(new Vector2(0.05f, 0f)));
            Assert.AreEqual(new Vector2(0.5f, 0f), StopDetector.ApplyDeadZone(new Vector2(0.5f, 0f)));
        }
    }
}
