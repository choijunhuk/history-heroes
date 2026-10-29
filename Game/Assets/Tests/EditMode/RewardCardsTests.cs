using System.Collections.Generic;
using HistoryHeroes.Economy;
using NUnit.Framework;

namespace HistoryHeroes.Tests
{
    /// <summary>결과 화면 보상 카드: 그림 + 숫자만 있는 줄은 카드로, 설명이 붙은 줄은 글로</summary>
    public sealed class RewardCardsTests
    {
        [Test]
        public void PureLines_BecomeCards_SummedByKey_InFixedOrder()
        {
            var lines = new List<string>
            {
                new Reward { Gold = 1200, Gems = 20 }.ToString(),
                "<sprite name=ancient_shard>고대 파편 +5",
                "<sprite name=exp>계정 경험치 +80",
                "<sprite name=gold>골드 +300",
            };

            List<RewardCards.Card> cards = RewardCards.Split(lines, out List<string> rest);

            Assert.AreEqual(0, rest.Count);
            Assert.AreEqual(new[] { "gold", "gem", "exp", "ancient_shard" }, cards.ConvertAll(c => c.Key).ToArray());
            Assert.AreEqual(1500, cards[0].Amount, "골드 1,200 (쉼표) + 300");
            Assert.AreEqual(20, cards[1].Amount);
        }

        [Test]
        public void LinesWithExplanation_StayText_AndAreNotCounted()
        {
            var lines = new List<string>
            {
                "별 상자  " + new Reward { Gold = 200 },
                "<sprite name=exp>계정 경험치 +80 · Lv 2 → 3 (<sprite name=gold>골드 +400) · 하트 가득",
                new Reward { Gold = 900 } + " (기록을 저장하면)",
                "새 별  1구간 클리어",
                "오늘 반복 격파 보상은 다 받았어요 (하루 3번)",
                "",
            };

            List<RewardCards.Card> cards = RewardCards.Split(lines, out List<string> rest);

            Assert.AreEqual(0, cards.Count, "조건부 · 설명 줄은 카드로 세지 않는다");
            Assert.AreEqual(lines, rest);
        }

        [Test]
        public void Null_IsEmpty()
        {
            Assert.AreEqual(0, RewardCards.Split(null, out List<string> rest).Count);
            Assert.AreEqual(0, rest.Count);
        }
    }
}
