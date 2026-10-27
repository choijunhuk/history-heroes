using System.Collections.Generic;
using HistoryHeroes.Common;
using HistoryHeroes.Dungeon;
using HistoryHeroes.Enemy;
using HistoryHeroes.Stats;
using NUnit.Framework;

namespace HistoryHeroes.Tests
{
    /// <summary>확장 1차 3장 특수 방: 갈림길 난수 분리 · 같은 씨앗 같은 계획 · 15방에만 · 석비 확률 · 이어하기 · 축복</summary>
    public sealed class SpecialRoomTests
    {
        private static List<RoomPlan> Dungeon15()
        {
            var plans = new List<RoomPlan>();
            for (int i = 0; i < 15; i++)
            {
                RoomKind kind = i == 4 || i == 9 ? RoomKind.Elite : i == 5 || i == 10 ? RoomKind.Reward : i == 14 ? RoomKind.MidBoss : RoomKind.Normal;
                plans.Add(new RoomPlan { kind = kind, budget = 8, elites = kind == RoomKind.Elite ? 1 : 0 });
            }

            return plans;
        }

        [Test]
        public void ForkStream_DoesNotChangeRoomsEnemiesOrCards()
        {
            RunRandom.Begin(12345);
            var expected = new List<int>();
            for (int i = 0; i < 5; i++) expected.Add(RunRandom.Rooms.Next());
            for (int i = 0; i < 5; i++) expected.Add(RunRandom.Enemies.Next());
            for (int i = 0; i < 5; i++) expected.Add(RunRandom.Cards.Next());

            RunRandom.Begin(12345);
            SpecialRooms.Plan(Dungeon15(), null, RunRandom.Forks); // 갈림길 난수를 마음껏 써도
            for (int i = 0; i < 50; i++) RunRandom.Forks.Next();
            var actual = new List<int>();
            for (int i = 0; i < 5; i++) actual.Add(RunRandom.Rooms.Next());
            for (int i = 0; i < 5; i++) actual.Add(RunRandom.Enemies.Next());
            for (int i = 0; i < 5; i++) actual.Add(RunRandom.Cards.Next());
            CollectionAssert.AreEqual(expected, actual, "방·적·카드 난수는 그대로");
        }

        [Test]
        public void SameSeed_SamePlan()
        {
            for (int seed = 0; seed < 30; seed++)
            {
                SpecialPlan a = SpecialRooms.Plan(Dungeon15(), null, new System.Random(seed));
                SpecialPlan b = SpecialRooms.Plan(Dungeon15(), null, new System.Random(seed));
                Assert.AreEqual(a.Forks.Count, b.Forks.Count);
                for (int i = 0; i < a.Forks.Count; i++) Assert.AreEqual(a.Forks[i].Alternative, b.Forks[i].Alternative);
                CollectionAssert.AreEquivalent(a.Steles.Keys, b.Steles.Keys);
                foreach (int room in a.Steles.Keys) CollectionAssert.AreEqual(a.Steles[room], b.Steles[room]);
                Assert.AreEqual(a.CursedAffixes, b.CursedAffixes);
            }
        }

        [Test]
        public void Plan_HasTwoForks_AndCursedHasTwoAffixes()
        {
            SpecialPlan plan = SpecialRooms.Plan(Dungeon15(), null, new System.Random(7));
            Assert.AreEqual(2, plan.Forks.Count);
            Assert.AreEqual(3, plan.Forks[0].AfterRoom);
            Assert.AreEqual(4, plan.Forks[0].Room);
            Assert.That(plan.Forks[0].Alternative, Is.EqualTo(RoomSpecial.ChallengeSurvive).Or.EqualTo(RoomSpecial.ChallengeNoHit));
            Assert.AreEqual(9, plan.Forks[1].AfterRoom);
            Assert.AreEqual(RoomSpecial.Cursed, plan.Forks[1].Alternative);
            int bits = 0;
            foreach (EliteAffix one in EliteAffixes.All)
            {
                if ((plan.CursedAffixes & one) != 0) bits++;
            }

            Assert.AreEqual(2, bits, "저주받은 엘리트 = 서로 다른 속성 2개");
            Assert.AreEqual(3, plan.SurviveWaves.Count, "버티기 무리 3번");
        }

        [Test]
        public void ForkChoice_DecidesRoom_AndOldSavesDefaultToNormal()
        {
            SpecialPlan plan = SpecialRooms.Plan(Dungeon15(), null, new System.Random(3));
            Assert.AreEqual(RoomSpecial.None, plan.SpecialOf(4), "고르기 전 = 평범");
            plan.Forks[0].Choice = 1;
            Assert.AreEqual(plan.Forks[0].Alternative, plan.SpecialOf(4));
            plan.Forks[1].Choice = 0;
            Assert.AreEqual(RoomSpecial.None, plan.SpecialOf(10));

            plan.RestoreChoices(new List<int> { 1, -1 }, 7);
            Assert.AreEqual(1, plan.Forks[0].Choice);
            Assert.AreEqual(-1, plan.Forks[1].Choice, "아직 안 지난 갈림길은 그대로 고를 수 있다");
            plan.RestoreChoices(new List<int>(), 12);
            Assert.AreEqual(0, plan.Forks[0].Choice, "옛 저장 + 지나온 갈림길 = 평범한 쪽");
            Assert.AreEqual(0, plan.Forks[1].Choice);
        }

        [Test]
        public void OnlyTheFifteenRoomDungeon()
        {
            List<RoomPlan> plans = Dungeon15();
            Assert.IsTrue(SpecialRooms.Eligible(plans, false, false, false, false));
            Assert.IsFalse(SpecialRooms.Eligible(plans, true, false, false, false), "일일 도전 아님");
            Assert.IsFalse(SpecialRooms.Eligible(plans, false, true, false, false), "균열 아님");
            Assert.IsFalse(SpecialRooms.Eligible(plans, false, false, true, false), "튜토리얼 아님");
            Assert.IsFalse(SpecialRooms.Eligible(plans, false, false, false, true), "직접 짠 판 아님");
            Assert.IsFalse(SpecialRooms.Eligible(plans.GetRange(0, 7), false, false, false, false), "7방 아님");
        }

        [Test]
        public void Steles_OnlyReplaceRewardRooms_AboutQuarter()
        {
            int steles = 0, slots = 0;
            for (int seed = 0; seed < 800; seed++)
            {
                SpecialPlan plan = SpecialRooms.Plan(Dungeon15(), null, new System.Random(seed));
                foreach (int room in plan.Steles.Keys)
                {
                    Assert.That(room, Is.EqualTo(6).Or.EqualTo(11), "6·11번 보상 방 자리만");
                    Assert.AreNotEqual(plan.Steles[room][0], plan.Steles[room][1], "서로 다른 축복 2개");
                }

                steles += plan.Steles.Count;
                slots += 2;
            }

            Assert.That(steles / (float)slots, Is.InRange(0.2f, 0.3f), "보상 방의 약 25%");
        }

        [Test]
        public void RunRandom_RestoresOldThreeStreamSaves()
        {
            RunRandom.Begin(99);
            List<string> saved = RunRandom.Capture();
            Assert.AreEqual(4, saved.Count, "4갈래 (방·적·카드·갈림길)");
            int forkNext = RunRandom.Forks.Next();
            int roomNext = RunRandom.Rooms.Next();

            RunRandom.Begin(5);
            Assert.IsTrue(RunRandom.Restore(saved));
            Assert.AreEqual(forkNext, RunRandom.Forks.Next());
            Assert.AreEqual(roomNext, RunRandom.Rooms.Next());

            RunRandom.Begin(5);
            Assert.IsTrue(RunRandom.Restore(saved.GetRange(0, 3)), "옛 저장(3갈래)도 읽는다");
            Assert.AreEqual(roomNext, RunRandom.Rooms.Next());
        }

        [Test]
        public void Blessings_ApplyGoodAndBad()
        {
            var owner = new object();
            var stats = new StatSheet();
            stats.SetBase(StatType.Attack, 100f);
            stats.SetBase(StatType.MaxHp, 1000f);
            stats.SetBase(StatType.MoveSpeed, 5f);
            Blessings.Apply("fight", stats, owner);
            Assert.AreEqual(120f, stats.Get(StatType.Attack), 0.01f);
            Assert.AreEqual(0.15f, stats.Get(StatType.DamageTaken), 0.001f, "받는 피해 +15% (피해 감소와 따로)");
            Assert.AreEqual(0f, stats.Get(StatType.DamageReduction), 0.001f);
            Blessings.Apply("steel", stats, owner);
            Assert.AreEqual(1250f, stats.Get(StatType.MaxHp), 0.01f);
            Assert.AreEqual(4.5f, stats.Get(StatType.MoveSpeed), 0.01f);
            foreach (Blessings.Info b in Blessings.All)
            {
                Assert.IsNotEmpty(Blessings.Text(b.Id));
                Assert.GreaterOrEqual(Blessings.IndexOf(b.Id), 0);
            }

            for (int seed = 0; seed < 100; seed++)
            {
                string[] two = Blessings.PickTwo(new System.Random(seed));
                Assert.AreNotEqual(two[0], two[1]);
            }
        }
    }
}
