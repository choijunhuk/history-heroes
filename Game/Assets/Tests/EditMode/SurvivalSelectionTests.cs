using HistoryHeroes.Records;
using HistoryHeroes.UI;
using NUnit.Framework;

namespace HistoryHeroes.Tests
{
    public sealed class SurvivalSelectionTests
    {
        [Test]
        public void DefaultMapFollowsProgressRatherThanDevelopmentUnlocks()
        {
            var save = new SaveData { unlockedChapter = 2 };
            Assert.AreEqual(0, SurvivalMapPanel.DefaultMapIndex(save, 3));
            save.survival.MapOf("field").clears = 1;
            Assert.AreEqual(1, SurvivalMapPanel.DefaultMapIndex(save, 3));
            save.survival.MapOf("city").clears = 1;
            Assert.AreEqual(2, SurvivalMapPanel.DefaultMapIndex(save, 3));
        }

        [Test]
        public void DefaultMapStaysWithinAvailableCardsAndHandlesLockedSave()
        {
            var save = new SaveData();
            Assert.AreEqual(0, SurvivalMapPanel.DefaultMapIndex(save, 3));
            save.survival.MapOf("field").clears = 1;
            Assert.AreEqual(0, SurvivalMapPanel.DefaultMapIndex(save, 1));
            Assert.AreEqual(0, SurvivalMapPanel.DefaultMapIndex(save, 0));
            Assert.AreEqual(0, SurvivalMapPanel.DefaultMapIndex(null, 3));
        }
    }
}
