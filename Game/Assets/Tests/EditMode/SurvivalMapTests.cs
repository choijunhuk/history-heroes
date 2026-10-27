using HistoryHeroes.Economy;
using HistoryHeroes.Records;
using HistoryHeroes.Survival;
using NUnit.Framework;

namespace HistoryHeroes.Tests
{
    /// <summary>확장 1차 6장 서바이벌 큰 모드: 맵 해금 순서 · 별 3칸 · 첫 클리어 1번 · 별 상자 · 옛 저장 옮기기</summary>
    public sealed class SurvivalMapTests
    {
        [Test]
        public void Unlock_Map1AfterChapter1Boss_ThenEachMapAfterThePrevious()
        {
            var d = new SaveData { unlockedChapter = 1 };
            Assert.IsFalse(SurvivalProgress.Unlocked(d, 0), "1구간 보스 전");
            d.unlockedChapter = 2;
            Assert.IsTrue(SurvivalProgress.Unlocked(d, 0), "1구간 보스 격파 = 맵 1");
            Assert.IsFalse(SurvivalProgress.Unlocked(d, 1));
            SurvivalProgress.Finish(d, 0, 420f, victory: true, missionDone: false, choseLegendary: true, out _);
            Assert.IsTrue(SurvivalProgress.Unlocked(d, 1), "맵 1 클리어 = 맵 2");
            Assert.IsFalse(SurvivalProgress.Unlocked(d, 2));
        }

        [Test]
        public void Finish_GivesStarsAndFirstClearOnce()
        {
            var d = new SaveData { unlockedChapter = 2 };
            int gems0 = Wallet.Gems(d);
            SurvivalProgress.Finish(d, 0, 200f, victory: false, missionDone: true, choseLegendary: false, out bool best);
            Assert.IsTrue(best);
            Assert.AreEqual(0, d.survival.MapOf("field").stars, "지면 별 없음");
            Assert.AreEqual(gems0, Wallet.Gems(d));

            SurvivalProgress.Finish(d, 0, 420f, victory: true, missionDone: false, choseLegendary: true, out _);
            Assert.AreEqual(1, SurvivalProgress.StarCount(d.survival.MapOf("field").stars), "★1 클리어만");
            int gems1 = Wallet.Gems(d);
            Assert.AreEqual(gems0 + SurvivalProgress.FirstClearGems(0), gems1, "첫 클리어 보석");

            SurvivalProgress.Finish(d, 0, 420f, victory: true, missionDone: true, choseLegendary: false, out bool again);
            Assert.IsFalse(again, "같은 시간은 새 기록 아님");
            Assert.AreEqual(3, SurvivalProgress.StarCount(d.survival.MapOf("field").stars), "★2 임무 · ★3 전설 없이");
            Assert.AreEqual(gems1, Wallet.Gems(d), "첫 클리어 보석은 한 번만");
        }

        [Test]
        public void StarChest_EveryFiveStars_Once()
        {
            var d = new SaveData { unlockedChapter = 2 };
            SurvivalProgress.Finish(d, 0, 420f, true, true, false, out _); // 3
            Assert.AreEqual(0, d.survival.starChestsTaken);
            SurvivalProgress.Finish(d, 1, 600f, true, true, true, out _); // +2 = 5
            Assert.AreEqual(1, d.survival.starChestsTaken, "별 5개 = 상자 1");
            SurvivalProgress.Finish(d, 1, 600f, true, true, true, out _); // 그대로 5
            Assert.AreEqual(1, d.survival.starChestsTaken, "다시 받지 않는다");
            Assert.AreEqual(5, d.survival.TotalStars());
        }

        [Test]
        public void OldSave_MovesBestAndClearToMap1()
        {
            var d = new SaveData();
            d.survival.longestSeconds = 512f;
            d.survival.clears = 2;
            d.survival.BestOf("khan").seconds = 512f;
            SurvivalMapSave field = d.survival.MapOf("field");
            Assert.AreEqual(512f, field.bestSeconds, "옛 최고 시간 = 맵 1 기록");
            Assert.AreEqual(1, field.clears, "옛 클리어가 있으면 맵 2가 열린다");
            Assert.IsTrue(SurvivalProgress.Unlocked(d, 1));
            Assert.AreEqual(0f, d.survival.MapOf("city").bestSeconds, "다른 맵은 새로");
        }
    }
}
