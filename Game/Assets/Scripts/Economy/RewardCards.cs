using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace HistoryHeroes.Economy
{
    /// <summary>
    /// 결과 화면 보상 카드 (2026-10-29 개발자: "보상에 그림이 너무 없다").
    /// 보상 글 줄 중 "그림 + 이름 +숫자"만으로 된 줄(예: "&lt;sprite name=gold&gt;골드 +700 · &lt;sprite name=gem&gt;보석 +20")을
    /// 그림 키별로 더해 카드로 바꾸고, 설명이 붙은 줄(별 상자 · 레벨 업 · 장비 · 조건부 보상)은 글로 남긴다.
    /// 모든 보상 글이 같은 모양(Reward.ToString 등)이라 한 곳에서 읽는다.
    /// </summary>
    public static class RewardCards
    {
        public struct Card
        {
            public string Key;
            public int Amount;
        }

        /// <summary>카드 순서 (없는 키는 맨 뒤)</summary>
        private static readonly string[] Order = { "gold", "gem", "exp", "ticket", "hero_shard", "equip_shard", "ancient_shard" };

        private static readonly Regex Part = new Regex(@"<sprite name=(\w+)>[^<+·]*\+([\d,]+)", RegexOptions.Compiled);
        private static readonly Regex Leftover = new Regex(@"[\s·]", RegexOptions.Compiled);

        /// <param name="lines">보상 글 줄</param>
        /// <param name="rest">카드로 안 바뀐 줄 (글로 보여 줄 것)</param>
        public static List<Card> Split(IList<string> lines, out List<string> rest)
        {
            var totals = new Dictionary<string, int>();
            rest = new List<string>();
            if (lines == null) return new List<Card>();

            foreach (string line in lines)
            {
                MatchCollection parts = string.IsNullOrEmpty(line) ? null : Part.Matches(line);
                bool pure = parts != null && parts.Count > 0 && Leftover.Replace(Part.Replace(line, string.Empty), string.Empty).Length == 0;
                if (!pure)
                {
                    rest.Add(line);
                    continue;
                }

                foreach (Match m in parts)
                {
                    string key = m.Groups[1].Value;
                    int amount = int.Parse(m.Groups[2].Value.Replace(",", string.Empty));
                    totals[key] = totals.TryGetValue(key, out int sum) ? sum + amount : amount;
                }
            }

            var cards = new List<Card>();
            foreach (var pair in totals) cards.Add(new Card { Key = pair.Key, Amount = pair.Value });
            cards.Sort((a, b) => Rank(a.Key).CompareTo(Rank(b.Key)));
            return cards;
        }

        private static int Rank(string key)
        {
            int i = System.Array.IndexOf(Order, key);
            return i < 0 ? Order.Length : i;
        }
    }
}
