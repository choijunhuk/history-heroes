using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using HistoryHeroes.Common;
using HistoryHeroes.Economy;
using HistoryHeroes.Records;

namespace HistoryHeroes.Relics
{
    /// <summary>유물 뽑기. 결과를 먼저 계산하고 지불·보유 상태를 함께 반영한다. 실제 저장은 호출자가 한다.</summary>
    public static class RelicGachaService
    {
        public enum Pay { Shards, Gems, Free }

        public struct Pull
        {
            public RelicData Data;
            public int Stars;
            public int Duplicates;
            public bool IsNew;
            public bool StarUp;
            public int Shards;
            public bool FromPity;
        }

        private const int CatalogSize = 24;
        private const int LogLimit = 100;

        public static bool CanOpen(RelicTable table)
        {
            if (table == null || table.relics == null || table.relics.Count != CatalogSize
                || table.pullShardCost <= 0 || table.tenPullShardCost <= 0
                || table.pullGemCost <= 0 || table.tenPullGemCost <= 0 || table.newRelicPity <= 0
                || table.overflowShards < 0 || table.duplicateCosts == null || table.duplicateCosts.Length != 2
                || table.duplicateCosts[0] <= 0 || table.duplicateCosts[1] <= 0) return false;
            var ids = new HashSet<string>();
            foreach (RelicData relic in table.relics)
                if (relic == null || string.IsNullOrEmpty(relic.id) || !ids.Add(relic.id)) return false;
            return true;
        }

        public static bool CanFree(SaveData save, string today) => save != null && save.relicGacha != null
            && ValidDay(today) && save.relicGacha.freeDate != today;

        public static int PullsUntilPity(SaveData save, RelicTable table)
        {
            if (!CanOpen(table) || Missing(save, table).Count == 0) return 0;
            return Math.Max(1, table.newRelicPity - save.relicGacha.newRelicPity);
        }

        /// <summary>보유 상태에 따라 달라지는 확률도 추첨과 같은 후보에서 구한다.</summary>
        public static float Rate(SaveData save, RelicTable table, string relicId, bool guaranteed = false)
        {
            if (!CanOpen(table) || table.Find(relicId) == null) return 0f;
            List<RelicData> missing = Missing(save, table);
            if (!guaranteed || missing.Count == 0) return 1f / table.relics.Count;
            return RelicService.Stars(save, relicId) == 0 ? 1f / missing.Count : 0f;
        }

        public static bool TryPull(SaveData save, RelicTable table, int count, Pay pay, string today, long now,
            out List<Pull> pulls, Func<float> rng = null)
        {
            pulls = null;
            if (save == null || !CanOpen(table) || !RelicService.Unlocked(save) || !ValidDay(today)
                || (count != 1 && count != 10) || save.relicGacha.totalPulls > int.MaxValue - count) return false;

            int cost;
            switch (pay)
            {
                case Pay.Shards:
                    cost = count == 1 ? table.pullShardCost : table.tenPullShardCost;
                    if (RelicService.Shards(save) < cost) return false;
                    break;
                case Pay.Gems:
                    cost = count == 1 ? table.pullGemCost : table.tenPullGemCost;
                    if (Wallet.Gems(save) < cost) return false;
                    break;
                case Pay.Free:
                    cost = 0;
                    if (count != 1 || !CanFree(save, today)) return false;
                    break;
                default: return false;
            }

            // 난수 오류나 중간 실패가 지갑·무료 횟수를 바꾸지 않도록 작은 작업 사본에서 뽑는다.
            var staged = new SaveData();
            foreach (RelicStar owned in save.relics.owned)
                staged.relics.owned.Add(new RelicStar { id = owned.id, stars = owned.stars, duplicates = owned.duplicates });
            staged.relicGacha.newRelicPity = save.relicGacha.newRelicPity;
            var results = new List<Pull>(count);
            if (rng == null)
            {
                // 상점 추첨이 판의 카드·적 난수를 밀지 않도록 기존 난수 도구로 별도 갈래를 만든다.
                var random = new StreamRandom(Environment.TickCount ^ Guid.NewGuid().GetHashCode());
                rng = () => (float)random.NextDouble();
            }
            for (int i = 0; i < count; i++)
            {
                List<RelicData> missing = Missing(staged, table);
                int pityBefore = staged.relicGacha.newRelicPity;
                bool guarantee = missing.Count > 0 && pityBefore >= table.newRelicPity - 1;
                List<RelicData> pool = guarantee ? missing : table.relics;
                float roll = rng();
                if (float.IsNaN(roll) || float.IsInfinity(roll) || roll < 0f || roll > 1f) return false;
                RelicData relic = pool[Math.Min(pool.Count - 1, (int)(roll * pool.Count))];
                Pull result = RelicService.Add(staged, table, relic);
                result.FromPity = guarantee;
                results.Add(result);
                staged.relicGacha.newRelicPity = result.IsNew || Missing(staged, table).Count == 0 ? 0 : pityBefore + 1;
                staged.relicGacha.log.Add(new GachaLogEntry { at = now, box = "relic", dataId = relic.id, pityBefore = pityBefore });
            }

            bool paid = pay == Pay.Free || (pay == Pay.Gems
                ? Wallet.TrySpendGems(save, cost) : Wallet.TrySpendItem(save, RelicTable.AncientShard, cost));
            if (!paid) return false;
            save.relics.owned = staged.relics.owned;
            save.AddItem(RelicTable.AncientShard, RelicService.Shards(staged));
            save.relicGacha.newRelicPity = staged.relicGacha.newRelicPity;
            save.relicGacha.totalPulls += count;
            if (pay == Pay.Free) save.relicGacha.freeDate = today;
            save.relicGacha.log.AddRange(staged.relicGacha.log);
            int over = save.relicGacha.log.Count - LogLimit;
            if (over > 0) save.relicGacha.log.RemoveRange(0, over);
            pulls = results;
            return true;
        }

        public static string RatesText(SaveData save, RelicTable table)
        {
            if (!CanOpen(table)) return "유물 데이터에 문제가 있어 뽑을 수 없어요.";
            var text = new StringBuilder();
            int missing = Missing(save, table).Count;
            text.AppendLine("<b>1. 종류와 개별 확률</b>\n등급 구분 없음 · 모든 유물의 보통 확률은 같아요.");
            foreach (RelicData relic in table.relics)
                text.AppendLine($"{relic.displayName}: 보통 {Percent(Rate(save, table, relic.id))}% · 보장 {Percent(Rate(save, table, relic.id, true))}%");
            text.AppendLine($"\n<b>2. 조건별 확률과 보장</b>\n보통: 각각 1/{table.relics.Count}. 새 유물 없이 {table.newRelicPity - 1}회면 다음 {table.newRelicPity}번째는 미보유 중 균등 추첨해요.");
            text.AppendLine(missing > 0
                ? $"현재 미보유 {missing}종은 보장 회차에 각각 1/{missing}, 보유 유물은 0%. 보장까지 {PullsUntilPity(save, table)}회."
                : $"24종 모두 보유: 새 유물 보장은 쉬고 모든 회차에 각각 1/{table.relics.Count}이에요.");
            text.AppendLine("새 유물이 나오면 횟수가 0이 돼요. 무료·파편·보석·10회가 같은 횟수를 쓰며 10회도 한 장마다 적용해요. ★3 완성 보장은 없어요.");
            text.AppendLine($"\n<b>3. 중복 처리</b>\n★1에서 중복 {table.duplicateCosts[0]}개로 ★2, 추가 {table.duplicateCosts[1]}개로 ★3. ★3 중복은 고대 파편 {table.overflowShards}으로 바뀌어요.");
            return text.ToString();
        }

        private static List<RelicData> Missing(SaveData save, RelicTable table)
        {
            var missing = new List<RelicData>();
            foreach (RelicData relic in table.relics)
                if (RelicService.Stars(save, relic.id) == 0) missing.Add(relic);
            return missing;
        }

        private static bool ValidDay(string today) => DateTime.TryParseExact(today, "yyyy-MM-dd", CultureInfo.InvariantCulture,
            DateTimeStyles.None, out _);
        private static string Percent(float rate) => (rate * 100f).ToString("0.######", CultureInfo.InvariantCulture);
    }
}
