using System.Collections.Generic;
using System.Reflection;
using System.Text;
using HistoryHeroes.Economy;
using HistoryHeroes.UI;
using NUnit.Framework;
using TMPro;
using UnityEngine;

namespace HistoryHeroes.Tests
{
    public sealed class RunResultPresentationTests
    {
        private static string Present(List<string> lines)
        {
            var owner = new GameObject("Result");
            owner.SetActive(false);
            var label = new GameObject("Body", typeof(RectTransform), typeof(TextMeshProUGUI)).GetComponent<TextMeshProUGUI>();
            try
            {
                var screen = owner.AddComponent<RunResultScreen>();
                typeof(RunResultScreen).GetField("bodyLabel", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(screen, label);
                var output = new StringBuilder();
                typeof(RunResultScreen).GetMethod("AppendRewards", BindingFlags.NonPublic | BindingFlags.Instance)
                    .Invoke(screen, new object[] { output, lines });
                return output.ToString();
            }
            finally
            {
                Object.DestroyImmediate(owner);
                Object.DestroyImmediate(label.gameObject);
            }
        }

        [Test]
        public void CardsAndLevelUp_ShowSeparateExperienceLevelAndCurrencyWithoutChangingSource()
        {
            string card = new Reward { Gold = 700 }.ToString();
            string level = RunText.Level(new AccountLevel.Result
            {
                Exp = 80, FromLevel = 4, ToLevel = 5,
                Reward = new Reward { Gold = 550, Gems = 20, Tickets = 1 }
            });
            var lines = new List<string> { card, level };
            string output = Present(lines);

            Assert.AreEqual(card, lines[0]);
            Assert.AreEqual(level, lines[1]);
            StringAssert.Contains("추가 보상", output);
            StringAssert.Contains("계정 경험치 +80" + System.Environment.NewLine, output);
            StringAssert.Contains("\u0001Lv 4 → 5\u0002" + System.Environment.NewLine, output);
            StringAssert.Contains("골드 +550", output);
            StringAssert.Contains("보석 +20", output);
            StringAssert.Contains("소환권 +1", output);
            StringAssert.Contains("하트 가득", output);
            Assert.AreEqual(1, output.Split(new[] { "골드 +550" }, System.StringSplitOptions.None).Length - 1);
            Assert.AreEqual(1, output.Split(new[] { "보석 +20" }, System.StringSplitOptions.None).Length - 1);
            Assert.AreEqual(1, output.Split(new[] { "소환권 +1" }, System.StringSplitOptions.None).Length - 1);
            Assert.IsFalse(output.Contains("\u0001" + level + "\u0002"));
        }

        [Test]
        public void NoCardAndNoLevelUp_KeepOrdinaryRewardText()
        {
            string level = RunText.Level(new AccountLevel.Result
                { Exp = 0, FromLevel = AccountLevel.MaxLevel, ToLevel = AccountLevel.MaxLevel });
            string output = Present(new List<string> { level });
            StringAssert.Contains("받은 것", output);
            StringAssert.Contains(level, output);
            Assert.IsFalse(output.Contains("\u0001"));
        }

        [Test]
        public void UnknownLevelWording_IsPreservedInFull()
        {
            const string changed = "계정 경험치 +80 · Lv 2 → 3 (새 보상) · 안내 문구 변경";
            StringAssert.Contains(changed, Present(new List<string> { changed }));
        }
    }
}
