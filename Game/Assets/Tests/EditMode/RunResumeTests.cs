using System.Collections.Generic;
using HistoryHeroes.Common;
using HistoryHeroes.Records;
using NUnit.Framework;
using UnityEngine;

namespace HistoryHeroes.Tests
{
    /// <summary>v1.0 A4 판 이어하기: 난수 3갈래 상태 저장·복원, 스냅샷 저장</summary>
    public sealed class RunResumeTests
    {
        private static List<int> Draw(System.Random r, int n)
        {
            var list = new List<int>(n);
            for (int i = 0; i < n; i++) list.Add(r.Next(1000));
            return list;
        }

        [Test]
        public void SameSeedSameStreams()
        {
            RunRandom.Begin(1234);
            List<int> rooms = Draw(RunRandom.Rooms, 10), enemies = Draw(RunRandom.Enemies, 10), cards = Draw(RunRandom.Cards, 10);
            RunRandom.Begin(1234);
            CollectionAssert.AreEqual(rooms, Draw(RunRandom.Rooms, 10));
            CollectionAssert.AreEqual(enemies, Draw(RunRandom.Enemies, 10));
            CollectionAssert.AreEqual(cards, Draw(RunRandom.Cards, 10));
            CollectionAssert.AreNotEqual(rooms, cards, "갈래끼리는 다르다");
        }

        [Test]
        public void CaptureRestoreGivesSameNextDraws()
        {
            RunRandom.Begin(77);
            Draw(RunRandom.Rooms, 13);
            Draw(RunRandom.Enemies, 5);
            Draw(RunRandom.Cards, 8);
            List<string> saved = RunRandom.Capture();
            List<int> nextCards = Draw(RunRandom.Cards, 12);
            List<int> nextEnemies = Draw(RunRandom.Enemies, 12);

            // 앱이 꺼졌다 켜짐: 같은 시드로 다시 시작해도 뽑은 만큼은 되돌려야 같다
            RunRandom.Begin(77);
            Assert.IsTrue(RunRandom.Restore(saved));
            CollectionAssert.AreEqual(nextCards, Draw(RunRandom.Cards, 12), "껐다 켜도 같은 카드");
            CollectionAssert.AreEqual(nextEnemies, Draw(RunRandom.Enemies, 12), "같은 적");
        }

        [Test]
        public void RestoreRejectsBrokenState()
        {
            RunRandom.Begin(5);
            Assert.IsFalse(RunRandom.Restore(null));
            Assert.IsFalse(RunRandom.Restore(new List<string> { "1", "2" }));
            Assert.IsFalse(RunRandom.Restore(new List<string> { "a", "b", "c" }));
        }

        [Test]
        public void RangesStayInside()
        {
            var r = new StreamRandom(99);
            for (int i = 0; i < 2000; i++)
            {
                int a = r.Next(7);
                Assert.That(a, Is.InRange(0, 6));
                int b = r.Next(-3, 4);
                Assert.That(b, Is.InRange(-3, 3));
                double d = r.NextDouble();
                Assert.That(d, Is.GreaterThanOrEqualTo(0.0).And.LessThan(1.0));
            }

            Assert.AreEqual(0, r.Next(1));
            Assert.AreEqual(0, r.Next(0));
            Assert.AreEqual(5, r.Next(5, 5));
        }

        [Test]
        public void SnapshotSurvivesJson()
        {
            RunRandom.Begin(42);
            Draw(RunRandom.Cards, 3);
            var save = new SaveData();
            save.run = new RunSnapshot
            {
                valid = true, mode = (int)BattleMode.Dungeon, chapter = 3, seed = 42, room = 7, heroId = "guan",
                level = 9, experience = 12.5f, hpRatio = 0.6f, kills = 33, roomsCleared = 6, hits = 4, elapsed = 210f,
                offersMade = 8, choseLegendary = true, rerollsLeft = 1,
            };
            save.run.skills.Add(new RecordSkill("spin_blade", 3, false));
            save.run.rng = RunRandom.Capture();

            SaveData back = JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(save));
            Assert.IsTrue(back.run.valid);
            Assert.AreEqual(7, back.run.room);
            Assert.AreEqual("guan", back.run.heroId);
            Assert.AreEqual(1, back.run.skills.Count);
            Assert.AreEqual(3, back.run.rng.Count);
            CollectionAssert.AreEqual(save.run.rng, back.run.rng, "64비트 상태가 문자열로 그대로");
            Assert.IsTrue(back.run.choseLegendary);
        }
    }
}
