using HistoryHeroes.Records;
using HistoryHeroes.Story;
using NUnit.Framework;

namespace HistoryHeroes.Tests
{
    /// <summary>망각의 왕 첫 격파 뒤 신의 심판 예고 (GodsVsHumanity 2장): 왕을 깬 뒤 한 번만</summary>
    public sealed class GodsTeaserTests
    {
        [Test]
        public void Pending_OnlyAfterKing_AndOnlyOnce()
        {
            var d = new SaveData();
            Assert.IsFalse(GodsTeaser.Pending(d), "왕을 깨기 전엔 없다");

            d.defeatedBosses.Add("shadow_twins");
            Assert.IsFalse(GodsTeaser.Pending(d), "다른 보스로는 안 뜬다");

            d.defeatedBosses.Add(GodsTeaser.KingBossId);
            Assert.IsTrue(GodsTeaser.Pending(d));

            d.godsTeaserSeen = true;
            Assert.IsFalse(GodsTeaser.Pending(d), "본 뒤엔 다시 안 뜬다");
        }

        [Test]
        public void OldSave_WithoutFlag_StillShowsOnce()
        {
            // 예고가 생기기 전에 이미 왕을 깬 저장: 깃발이 없으면 false로 읽혀서 한 번 보여 준다
            SaveData d = UnityEngine.JsonUtility.FromJson<SaveData>("{\"defeatedBosses\":[\"oblivion_king\"]}");
            Assert.IsTrue(GodsTeaser.Pending(d));
            Assert.AreEqual(GodsTeaser.Scenes.Length, GodsTeaser.Tints.Length);
        }
    }
}
