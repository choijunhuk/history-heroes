using System;
using System.Collections.Generic;
using HistoryHeroes.Economy;
using HistoryHeroes.Records;

namespace HistoryHeroes.Equipment
{
    /// <summary>
    /// 상자 열기 (Gacha 2·6장). 처리 순서: 재화 차감 → 추첨 → 결과·천장 카운터를 한 번에 → (부르는 쪽이 저장) → 연출.
    /// - 고급 상자: 50회째 전설 확정, 희귀 이상 없이 9회 열면 다음 회차에 희귀 이상 확정 (천장이 먼저)
    /// - 일반 상자: 천장 없음, 하루 1번 광고 보고 무료
    /// - 같은 등급 안에서는 장비를 균등하게 고른다
    /// v0.4는 기기 안 추첨이다 (서버 추첨은 Monetization 8장 결정 때).
    /// </summary>
    public static class GachaService
    {
        public struct Pull
        {
            public EquipmentData Data;
            public EquipmentInventory.AddResult Added;
            public bool FromPity;
            public bool FromTenGuarantee;
        }

        public enum Pay
        {
            Gold,
            Gems,
            Ticket,
            FreeAd,
        }

        /// <summary>등급 하나 뽑기 (r = 0~1)</summary>
        public static EquipRarity RollRarity(float[] rates, float r)
        {
            float acc = 0f;
            for (int i = 0; i < rates.Length; i++)
            {
                acc += rates[i];
                if (r < acc && rates[i] > 0f) return (EquipRarity)i;
            }

            // 반올림 오차: 확률이 0보다 큰 가장 높은 등급
            for (int i = rates.Length - 1; i >= 0; i--)
            {
                if (rates[i] > 0f) return (EquipRarity)i;
            }

            return EquipRarity.Common;
        }

        /// <summary>
        /// 열 수 있는 상자인가: 보통 확률·천장(전설)·누적 희귀 보장 표에서 나올 수 있는 등급마다 장비가 1개 이상 (Codex v0.4 4번).
        /// 데이터가 빠져 빈 등급이 뽑히면 값만 치르고 장비가 안 나오는 일을 막는다.
        /// </summary>
        public static bool CanOpen(GachaTable table, BoxRates box)
        {
            if (table == null || table.equipment == null || box == null) return false;
            var needed = new List<EquipRarity>();
            for (int r = 0; r < box.rarity.Length; r++)
            {
                if (box.rarity[r] > 0f) needed.Add((EquipRarity)r);
            }

            if (box.pity > 0)
            {
                needed.Add(EquipRarity.Legendary);
                for (int r = 0; r < table.tenPullGuarantee.Length; r++)
                {
                    if (table.tenPullGuarantee[r] > 0f) needed.Add((EquipRarity)r);
                }
            }

            foreach (EquipRarity rarity in needed)
            {
                bool any = false;
                foreach (EquipmentData d in table.equipment.items)
                {
                    if (d != null && d.rarity == rarity) any = true;
                }

                if (!any) return false;
            }

            return true;
        }

        /// <summary>"전설 확정까지 n회" (천장이 없으면 -1)</summary>
        public static int PullsUntilPity(SaveData save, BoxRates box) => box.pity > 0 ? box.pity - save.gacha.premiumPity : -1;

        /// <summary>마지막 희귀 이상 이후 고급 상자를 몇 번 열었는지 뽑기 기록에서 복원한다.</summary>
        public static int PullsUntilRareGuarantee(SaveData save, GachaTable table)
        {
            if (save == null || table == null || table.equipment == null || table.premium == null) return -1;
            int misses = 0;
            for (int i = save.gacha.log.Count - 1; i >= 0 && misses < 9; i--)
            {
                GachaLogEntry entry = save.gacha.log[i];
                if (entry.box != table.premium.id) continue;
                EquipmentData data = table.equipment.Find(entry.dataId);
                // 삭제되거나 이름이 바뀐 정의는 이전 결과를 알 수 없으므로 연속 실패로 세지 않는다.
                if (data == null || data.rarity >= EquipRarity.Rare) break;
                misses++;
            }

            return 10 - misses;
        }

        /// <summary>다음 고급 상자 한 회의 전설 확률. 천장과 누적 희귀 이상 보장을 모두 반영한다.</summary>
        public static float NextLegendaryRate(SaveData save, GachaTable table)
        {
            if (save == null || table == null || table.premium == null) return 0f;
            BoxRates box = table.premium;
            if (PullsUntilPity(save, box) == 1) return 1f;
            float ordinary = box.rarity[(int)EquipRarity.Legendary];
            if (PullsUntilRareGuarantee(save, table) != 1) return ordinary;
            float low = box.rarity[(int)EquipRarity.Common] + box.rarity[(int)EquipRarity.Uncommon];
            return ordinary + low * table.tenPullGuarantee[(int)EquipRarity.Legendary];
        }

        public static bool CanFreeNormal(SaveData save, string today) => save.gacha.freeNormalDate != today;

        /// <summary>값을 치를 수 있는지 보고 치른다 (실패하면 아무것도 안 바뀜)</summary>
        public static bool TryPay(SaveData save, GachaTable table, BoxRates box, int count, Pay pay, string today)
        {
            switch (pay)
            {
                case Pay.Gold:
                    return count == 1 && box.goldCost > 0 && Wallet.TrySpendGold(save, box.goldCost);
                case Pay.FreeAd:
                    if (box != table.normal || count != 1 || !CanFreeNormal(save, today)) return false;
                    save.gacha.freeNormalDate = today;
                    return true;
                case Pay.Ticket:
                    if (box != table.premium || count != 1 || save.ItemCount(Wallet.Ticket) < 1) return false;
                    save.AddItem(Wallet.Ticket, -1);
                    return true;
                case Pay.Gems:
                    int cost = count == 10 ? box.gem10Cost : count == 1 ? box.gemCost : 0;
                    return cost > 0 && Wallet.TrySpendGems(save, cost);
                default:
                    return false;
            }
        }

        /// <summary>
        /// 값을 치른 뒤 count번 연다. rng = 0~1 난수 (테스트에서 정해 넣는다).
        /// 결과·카운터·기록이 save에 한 번에 들어간다 → 부르는 쪽이 바로 SaveSystem.Save().
        /// </summary>
        public static List<Pull> Open(SaveData save, GachaTable table, BoxRates box, int count, long now, Func<float> rng = null)
        {
            rng ??= () => UnityEngine.Random.value;
            var pulls = new List<Pull>(count);
            bool premium = box.pity > 0;
            for (int i = 0; i < count; i++)
            {
                int pityBefore = save.gacha.premiumPity;
                EquipRarity rarity;
                bool fromPity = false, fromTen = false;

                if (premium && save.gacha.premiumPity + 1 >= box.pity)
                {
                    rarity = EquipRarity.Legendary; // 천장 회차 (희귀 이상 보장보다 먼저)
                    fromPity = true;
                }
                else
                {
                    rarity = RollRarity(box.rarity, rng());
                    if (premium && PullsUntilRareGuarantee(save, table) == 1 && rarity < EquipRarity.Rare)
                    {
                        rarity = RollRarity(table.tenPullGuarantee, rng()); // 누적 보장 회차
                        fromTen = true;
                    }
                }

                if (premium) save.gacha.premiumPity = rarity == EquipRarity.Legendary ? 0 : save.gacha.premiumPity + 1;

                EquipmentData data = PickUniform(table.equipment, rarity, rng());
                var pull = new Pull { Data = data, FromPity = fromPity, FromTenGuarantee = fromTen };
                if (data != null) pull.Added = EquipmentInventory.Add(save, table.equipment, data);
                pulls.Add(pull);

                save.gacha.log.Add(new GachaLogEntry { at = now, box = box.id, dataId = data != null ? data.id : "", pityBefore = pityBefore });
            }

            TrimLog(save.gacha.log, table.premium.id, table.logLimit);
            return pulls;
        }

        private static void TrimLog(List<GachaLogEntry> log, string premiumId, int limit)
        {
            // 일반 상자를 오래 열어도 희귀 보장에 필요한 마지막 고급 9회는 보존한다.
            int keepPremium = 0;
            var protectedEntries = new HashSet<GachaLogEntry>();
            for (int i = log.Count - 1; i >= 0 && keepPremium < 9; i--)
            {
                if (log[i].box != premiumId) continue;
                protectedEntries.Add(log[i]);
                keepPremium++;
            }

            for (int i = 0; log.Count > Math.Max(9, limit) && i < log.Count;)
            {
                if (protectedEntries.Contains(log[i])) i++;
                else log.RemoveAt(i);
            }
        }

        private static EquipmentData PickUniform(EquipmentTable equipment, EquipRarity rarity, float r)
        {
            var pool = new List<EquipmentData>();
            foreach (EquipmentData d in equipment.items)
            {
                if (d != null && d.rarity == rarity) pool.Add(d);
            }

            if (pool.Count == 0) return null;
            int index = Math.Min(pool.Count - 1, (int)(r * pool.Count));
            return pool[Math.Max(0, index)];
        }

        /// <summary>
        /// 확률 보기 (Gacha 2-2·4장): 등급별 확률 + 장비별 개별 확률 + 보장 규칙. 실제 추첨과 같은 표에서 만든다.
        /// </summary>
        public static string RatesText(GachaTable table, BoxRates box)
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"<b>{box.displayName}</b>");
            sb.AppendLine("무기 · 목걸이 · 장갑 / 갑옷 · 허리띠 · 신발 (같은 등급 장비 균등)");
            for (int r = 0; r < box.rarity.Length; r++)
            {
                if (box.rarity[r] <= 0f) continue;
                var rarity = (EquipRarity)r;
                var pool = new List<EquipmentData>();
                foreach (EquipmentData d in table.equipment.items)
                {
                    if (d != null && d.rarity == rarity) pool.Add(d);
                }

                sb.AppendLine($"<color={EquipmentText.RarityColor(rarity)}>{EquipmentText.RarityName(rarity)} {box.rarity[r] * 100f:0.##}%</color>  (장비마다 {box.rarity[r] * 100f / Math.Max(1, pool.Count):0.###}%)");
                foreach (EquipmentData data in pool)
                {
                    float rate = box.rarity[r] * 100f / Math.Max(1, pool.Count);
                    sb.Append($"  {EquipmentText.SlotName(data.slot)} · {data.displayName}: {rate:0.######}%");
                    if (box.pity > 0 && data.rarity >= EquipRarity.Rare)
                    {
                        float lowRate = box.rarity[(int)EquipRarity.Common] + box.rarity[(int)EquipRarity.Uncommon];
                        float guaranteedRate = (box.rarity[r] + lowRate * table.tenPullGuarantee[r]) * 100f / Math.Max(1, pool.Count);
                        sb.Append($" (누적 10회 실제 {guaranteedRate:0.######}%");
                        if (data.rarity == EquipRarity.Legendary) sb.Append($", 천장 {100f / Math.Max(1, pool.Count):0.######}%");
                        sb.Append(")");
                    }

                    sb.AppendLine();
                }
            }

            if (box.pity > 0)
            {
                sb.AppendLine($"보장: {box.pity}번째 상자는 전설 확정 (전설이 나오면 다시 1부터). 보석·소환권으로 연 고급 상자가 같은 횟수를 씁니다");
                sb.AppendLine($"누적 10회: 희귀 이상 없이 고급 상자를 9회 열면 다음 회차를 희귀 {table.tenPullGuarantee[2] * 100f:0.#}% · 전설 {table.tenPullGuarantee[3] * 100f:0.#}%로 다시 뽑습니다 (천장이 먼저)");
            }
            else
            {
                sb.AppendLine("보장 없음");
            }

            return sb.ToString();
        }
    }
}
