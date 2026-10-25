using System.Collections.Generic;
using HistoryHeroes.Economy;
using HistoryHeroes.Records;
using HistoryHeroes.Skills;
using NUnit.Framework;
using UnityEngine;

namespace HistoryHeroes.Tests
{
    /// <summary>하트·재화·계정 레벨·별·판 보상 (Economy.md, Stats.md 7장, Modes.md 3장)</summary>
    public sealed class EconomyTests
    {
        private const long T0 = 1_800_000_000; // 기준 시각 (유닉스 초)
        private const string Day1 = "2027-01-01";
        private const string Day2 = "2027-01-02";

        private static SaveData Fresh()
        {
            // 첫날 무료 3판은 따로 시험한다. 나머지 시험에서는 이미 쓴 것으로
            return new SaveData { hearts = Hearts.Max, heartsUpdatedAt = T0, firstPlayDate = "2026-12-31", freeRunsUsed = 3 };
        }

        // ---------------- 하트 ----------------

        [Test]
        public void Hearts_RegenOnePer20Minutes_UpToMax()
        {
            SaveData d = Fresh();
            Assert.IsTrue(Hearts.TrySpend(d, 3, T0, Day1, out _));
            Assert.AreEqual(2, d.hearts);

            Hearts.Tick(d, T0 + Hearts.RegenSeconds - 1);
            Assert.AreEqual(2, d.hearts);
            Hearts.Tick(d, T0 + Hearts.RegenSeconds);
            Assert.AreEqual(3, d.hearts);
            Assert.AreEqual(Hearts.RegenSeconds, Hearts.SecondsToNext(d, T0 + Hearts.RegenSeconds));

            Hearts.Tick(d, T0 + Hearts.RegenSeconds * 100);
            Assert.AreEqual(Hearts.Max, d.hearts, "자연 충전은 최대치까지만");
        }

        [Test]
        public void Hearts_TimerStartsWhenDroppingBelowMax()
        {
            SaveData d = Fresh();
            Hearts.Tick(d, T0 + 10_000); // 가득 찬 동안은 시계가 멈춤
            Assert.IsTrue(Hearts.TrySpend(d, 1, T0 + 10_000, Day1, out _));
            Assert.AreEqual(Hearts.RegenSeconds, Hearts.SecondsToNext(d, T0 + 10_000), "쓰는 순간부터 20분");
        }

        [Test]
        public void Hearts_NotEnough_FailsWithoutChange()
        {
            SaveData d = Fresh();
            d.hearts = 0;
            Assert.IsFalse(Hearts.TrySpend(d, 1, T0, Day1, out _));
            Assert.AreEqual(0, d.hearts);
        }

        [Test]
        public void Hearts_FirstDay_ThreeFreeRuns()
        {
            var d = new SaveData { hearts = 1, heartsUpdatedAt = T0 };
            for (int i = 0; i < 3; i++)
            {
                Assert.IsTrue(Hearts.TrySpend(d, 1, T0, Day1, out bool free));
                Assert.IsTrue(free);
            }

            Assert.AreEqual(1, d.hearts, "무료 판은 하트를 쓰지 않는다");
            Assert.IsTrue(Hearts.TrySpend(d, 1, T0, Day1, out bool fourth));
            Assert.IsFalse(fourth);
            Assert.AreEqual(0, d.hearts);
        }

        [Test]
        public void Hearts_GiftsCanExceedMax_UpTo99_LevelUpRefillKeepsOverflow()
        {
            SaveData d = Fresh();
            Hearts.Add(d, 200, T0);
            Assert.AreEqual(Hearts.Cap, d.hearts);

            d.hearts = 7;
            Hearts.Refill(d, T0);
            Assert.AreEqual(7, d.hearts, "넘쳐 있으면 그대로");
            d.hearts = 2;
            Hearts.Refill(d, T0);
            Assert.AreEqual(Hearts.Max, d.hearts);
        }

        [Test]
        public void Hearts_ClockGoesBackward_RestartsTimerWithoutLoss()
        {
            SaveData d = Fresh();
            Hearts.TrySpend(d, 2, T0, Day1, out _);
            Hearts.Tick(d, T0 - 50_000); // 시계를 되돌림
            Assert.AreEqual(3, d.hearts);
            Hearts.Tick(d, T0 - 50_000 + Hearts.RegenSeconds);
            Assert.AreEqual(4, d.hearts);
        }

        [Test]
        public void Hearts_AdAndGemLimitsResetDaily()
        {
            SaveData d = Fresh();
            d.hearts = 0;
            for (int i = 0; i < Hearts.AdPerDay; i++)
            {
                Assert.IsTrue(Hearts.CanWatchAd(d, Day1));
                Hearts.GrantAd(d, T0, Day1);
            }

            Assert.IsFalse(Hearts.CanWatchAd(d, Day1));
            Assert.AreEqual(Hearts.AdPerDay * Hearts.AdReward, d.hearts);
            Assert.IsTrue(Hearts.CanWatchAd(d, Day2), "다음 날 다시");

            d.gemsFree = 100;
            Assert.IsTrue(Hearts.TryBuyWithGems(d, T0, Day1));
            Assert.AreEqual(40, d.gemsFree);
            Assert.IsFalse(Hearts.TryBuyWithGems(d, T0, Day1), "보석이 모자람");
        }

        [Test]
        public void Hearts_BossLossRefund_FivePerBossPerDay()
        {
            SaveData d = Fresh();
            d.hearts = 0;
            for (int i = 0; i < Hearts.RefundsPerDay; i++) Assert.IsTrue(Hearts.TryRefundBossLoss(d, "boss_a", T0, Day1));
            Assert.IsFalse(Hearts.TryRefundBossLoss(d, "boss_a", T0, Day1));
            Assert.IsTrue(Hearts.TryRefundBossLoss(d, "boss_b", T0, Day1), "다른 보스는 따로 센다");
            Assert.IsTrue(Hearts.TryRefundBossLoss(d, "boss_a", T0, Day2), "다음 날 다시");
            Assert.AreEqual(Hearts.RefundsPerDay + 2, d.hearts);
        }

        // ---------------- 재화 ----------------

        [Test]
        public void Gems_SpendFreeFirst()
        {
            var d = new SaveData { gemsFree = 30, gemsPaid = 100 };
            Assert.IsTrue(Wallet.TrySpendGems(d, 50));
            Assert.AreEqual(0, d.gemsFree);
            Assert.AreEqual(80, d.gemsPaid);
            Assert.IsFalse(Wallet.TrySpendGems(d, 81));
        }

        [Test]
        public void Grant_PutsItemsInInventory()
        {
            var d = new SaveData();
            Wallet.Grant(d, RunRewards.BossFirstClear("khan"));
            Assert.AreEqual(2000, d.gold);
            Assert.AreEqual(500, d.gemsFree);
            Assert.AreEqual(1, d.ItemCount(Wallet.Ticket));
            Assert.AreEqual(5, d.ItemCount(Wallet.HeroShard("khan")));
        }

        // ---------------- 판 보상 ----------------

        [Test]
        public void DungeonGold_ScalesWithChapterAndRooms()
        {
            Assert.AreEqual(500, RunRewards.Dungeon(1, 10, 10, true).Gold);
            Assert.AreEqual(700, RunRewards.Dungeon(3, 10, 10, true).Gold);
            Assert.AreEqual(250, RunRewards.Dungeon(1, 5, 10, false).Gold);
            Assert.AreEqual(0, RunRewards.Dungeon(1, 0, 10, false).Gold);
        }

        [Test]
        public void BossRepeat_ThreeTimesPerDay()
        {
            var d = new SaveData();
            for (int i = 0; i < 3; i++) Assert.AreEqual(300, RunRewards.BossRepeat(d, Day1).Gold);
            Assert.IsTrue(RunRewards.BossRepeat(d, Day1).IsEmpty);
            Assert.AreEqual(300, RunRewards.BossRepeat(d, Day2).Gold);
        }

        // ---------------- 계정 레벨 ----------------

        [Test]
        public void AccountLevel_ExpTableAndRewards()
        {
            Assert.AreEqual(140, AccountLevel.ExpToNext(1));
            Assert.AreEqual(80, AccountLevel.DungeonExp(10));
            Assert.AreEqual(120, AccountLevel.DungeonExp(20));

            SaveData d = Fresh();
            d.hearts = 1;
            AccountLevel.Result r = AccountLevel.AddExp(d, 140 + 180 + 10, T0); // Lv1 → 3, 남은 10
            Assert.AreEqual(1, r.FromLevel);
            Assert.AreEqual(3, r.ToLevel);
            Assert.AreEqual(10, d.accountExp);
            Assert.AreEqual((300 + 100) + (300 + 150), d.gold);
            Assert.AreEqual(Hearts.Max, d.hearts, "레벨업하면 하트를 채운다");
        }

        [Test]
        public void AccountLevel_StopsAtMax()
        {
            var d = new SaveData { accountLevel = AccountLevel.MaxLevel };
            AccountLevel.Result r = AccountLevel.AddExp(d, 99999, T0);
            Assert.IsFalse(r.LeveledUp);
            Assert.AreEqual(AccountLevel.MaxLevel, d.accountLevel);
        }

        // ---------------- 별 ----------------

        [Test]
        public void DungeonStars_EarnedSeparately_NeverLost()
        {
            var d = new SaveData();
            List<string> first = Stars.EvaluateDungeon(d, 1, cleared: true, hits: 20, choseLegendary: true);
            Assert.AreEqual(1, first.Count, "★1만");

            List<string> second = Stars.EvaluateDungeon(d, 1, cleared: true, hits: 3, choseLegendary: true);
            Assert.AreEqual(1, second.Count, "★2 (★1은 이미 있음)");

            List<string> third = Stars.EvaluateDungeon(d, 1, cleared: true, hits: 30, choseLegendary: false);
            Assert.AreEqual(1, third.Count, "★3은 따로 딴다");
            Assert.AreEqual(3, Stars.Count(d, 1, Stars.Dungeon));

            Assert.AreEqual(0, Stars.EvaluateDungeon(d, 1, cleared: false, hits: 0, choseLegendary: false).Count);
            Assert.AreEqual(15, Stars.DungeonHitLimit(1));
            Assert.AreEqual(8, Stars.DungeonHitLimit(9));
        }

        [Test]
        public void BossStar3_ComparesBuildsActuallyFought()
        {
            RunRecord Rec(string id) => new RunRecord { id = id };
            var d = new SaveData();
            RunRecord a = Rec("a"), copy = Rec("b"), other = Rec("c");
            d.records.AddRange(new[] { a, copy, other });

            var bladeFlame = new[] { "spin_blade", "flame_field" };
            Stars.RecordWin(a, "rift_warden", bladeFlame);
            Stars.RecordWin(copy, "rift_warden", bladeFlame); // 패시브만 다른 복사본: 싸운 액티브가 같다
            Assert.IsFalse(Stars.HasDifferentWin(d, "rift_warden", copy, bladeFlame), "같은 액티브 구성은 같은 기록");

            var bladeBolt = new[] { "spin_blade", "lightning" }; // 액티브 하나 바꿈 = 2개 차이
            Stars.RecordWin(other, "rift_warden", bladeBolt);
            List<string> earned = Stars.EvaluateBoss(d, 1, "rift_warden", true, 3, 8, other, bladeBolt);
            Assert.AreEqual(3, earned.Count, "★1 격파 · ★2 피격 · ★3 다른 기록");

            Stars.RecordWin(other, "rift_warden", bladeBolt);
            Assert.AreEqual(1, other.bossWins.Count, "같은 구성으로 또 이기면 한 번만 남긴다");
        }

        [Test]
        public void ActiveIds_SkipsPassives()
        {
            var blade = ScriptableObject.CreateInstance<ActiveSkillData>(); blade.id = "spin_blade";
            var atk = ScriptableObject.CreateInstance<PassiveSkillData>(); atk.id = "atk_up";
            var db = ScriptableObject.CreateInstance<SkillDatabase>();
            db.skills = new List<SkillData> { blade, atk };
            List<string> ids = Stars.ActiveIds(new[] { new RecordSkill("spin_blade", 1, false), new RecordSkill("atk_up", 2, false) }, db);
            CollectionAssert.AreEqual(new[] { "spin_blade" }, ids);
        }

        [Test]
        public void StarBoxes_EveryFiveStars_ClaimedOnce()
        {
            var d = new SaveData();
            for (int i = 0; i < 11; i++) d.stars.Add("s" + i);

            Reward r = Stars.ClaimBoxes(d);
            Assert.AreEqual(2, d.starBoxesClaimed);
            Assert.AreEqual(5000, r.Gold);
            Assert.AreEqual(200, r.Gems);
            Assert.AreEqual(1, d.ItemCount(Wallet.Ticket));
            Assert.IsTrue(Stars.ClaimBoxes(d).IsEmpty, "다시 받지 않는다");
        }
    }
}
