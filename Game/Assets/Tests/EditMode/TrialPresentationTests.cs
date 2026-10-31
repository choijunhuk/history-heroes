using System;
using System.Linq;
using HistoryHeroes.Modes;
using HistoryHeroes.Records;
using HistoryHeroes.UI;
using NUnit.Framework;

namespace HistoryHeroes.Tests
{
    public sealed class TrialPresentationTests
    {
        private static readonly DateTimeOffset Now = new DateTimeOffset(2026, 11, 3, 12, 0, 0, TimeSpan.FromHours(9));

        private static SaveData Save()
        {
            var save = new SaveData();
            save.defeatedBosses.Add("oblivion_king");
            save.records.Add(new RunRecord { id = "one", heroId = "khan" });
            save.records.Add(new RunRecord { id = "two", heroId = "guan" });
            return save;
        }

        private static GodTrialResult Win(SaveData save, string recordId, params string[] actives) =>
            GodTrialRules.Complete(save, GodTrialRules.ThorTrialId, save.FindRecord(recordId),
                actives, true, false, true, Now);

        [Test]
        public void TrialVoiceSkipsChapterBossIntro()
        {
            Assert.IsTrue(HeroVoice.UsesChapterBossIntro(null));
            Assert.IsTrue(HeroVoice.UsesChapterBossIntro(string.Empty));
            foreach (GodTrialDefinition trial in GodTrialRules.Trials)
                Assert.IsFalse(HeroVoice.UsesChapterBossIntro(trial.Id), trial.Id);
        }

        [Test]
        public void OblivionKingFirstClearAnnouncesTrialInsteadOfSixthChapter()
        {
            Assert.AreEqual("처음 격파! 신의 시련이 열렸어요", RunResultScreen.FirstBossClearText(
                new BossSummary { BossName = "망각의 왕", UnlockedChapter = 6 }));
            Assert.AreEqual("처음 격파! 2구간이 열렸어요", RunResultScreen.FirstBossClearText(
                new BossSummary { BossName = "균열의 수문장", UnlockedChapter = 2 }));
            Assert.That(RunResultScreen.FirstBossClearText(
                new BossSummary { BossName = "토르", IsGodTrial = true, UnlockedChapter = 0 }),
                Does.Contain("첫 시련"));
        }

        [Test]
        public void WeeklyGoalExplainsWhyWinsDoNotCount()
        {
            SaveData save = Save();
            Assert.That(Win(save, "one", "blade").Lines.Any(line => line.Contains("목표를 고르세요")), Is.True);
            Assert.IsTrue(GodTrialRules.TryChooseGoal(save, GodTrialRules.ThorTrialId, Now));
            Assert.That(Win(save, "one", "blade").Lines.Any(line => line.Contains("다른 본인 기록")), Is.True);
            Assert.That(Win(save, "one", "fire").Lines.Any(line => line.Contains("이미 이긴 기록")), Is.True);

            SaveData sameBuild = Save();
            Assert.IsTrue(GodTrialRules.TryChooseGoal(sameBuild, GodTrialRules.ThorTrialId, Now));
            Win(sameBuild, "one", "blade");
            Assert.That(Win(sameBuild, "two", "blade").Lines.Any(line => line.Contains("액티브 구성이 2개 이상")), Is.True);
            Assert.AreEqual(5, Win(sameBuild, "two", "fire").WeeklyShards);
        }
    }
}
