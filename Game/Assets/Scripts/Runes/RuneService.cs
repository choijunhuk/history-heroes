using System.Collections.Generic;
using HistoryHeroes.Economy;
using HistoryHeroes.Records;
using HistoryHeroes.Skills;
using UnityEngine;

namespace HistoryHeroes.Runes
{
    /// <summary>
    /// 룬 규칙 (Runes 4~7장). 저장 데이터만 바꾸고 저장은 부르는 쪽이 한다.
    /// - 칸: 2구간 보스 → 1칸 (불·번개·투사체 중 하나 고르기), 4구간 보스 → 2칸 (남은 둘 중 하나)
    /// - 같은 룬을 한 영웅이 두 칸에 낄 수 없다. 영웅끼리는 같은 룬을 같이 낄 수 있다 (참조)
    /// - 상점: 칸이 열리면 근접·생존·수련, 그리고 보스 보상에서 못 고른 룬을 골드 8,000에
    /// - 강화: Lv2 골드 3,000 · Lv3 10,000
    /// - 뽑기 없음, 보석 판매 없음
    /// </summary>
    public static class RuneService
    {
        public static int SlotCount(SaveData save, RuneTable table)
        {
            int n = 0;
            foreach (int chapter in table.slotUnlockChapter)
            {
                if (save.unlockedChapter >= chapter) n++;
            }

            return n;
        }

        public static int Level(SaveData save, string runeId)
        {
            foreach (RuneLevel r in save.runes.owned)
            {
                if (r.id == runeId) return r.level;
            }

            return 0;
        }

        public static bool Owns(SaveData save, string runeId) => Level(save, runeId) > 0;

        private static void Give(SaveData save, string runeId)
        {
            if (!Owns(save, runeId)) save.runes.owned.Add(new RuneLevel { id = runeId, level = 1 });
        }

        // ---------------------------------------------------------------- 보스 보상 (고르기)

        /// <summary>받을 차례인 보스 보상 고르기 ("ch2" / "ch4"), 없으면 null</summary>
        public static string PendingChoice(SaveData save, RuneTable table)
        {
            string[] keys = { "ch2", "ch4" };
            for (int i = 0; i < keys.Length && i < table.slotUnlockChapter.Length; i++)
            {
                if (save.unlockedChapter >= table.slotUnlockChapter[i] && !save.runes.choicesTaken.Contains(keys[i]) && ChoiceOptions(save, table).Count > 0) return keys[i];
            }

            return null;
        }

        /// <summary>
        /// 고를 수 있는 룬: 보스 보상 룬 중 아직 **보상으로** 고르지 않은 것 (상점에서 미리 산 것도 포함).
        /// 보스 룬 3개 중 보상으로 고르는 건 많아야 2개라 고를 것이 늘 남는다
        /// </summary>
        public static List<RuneData> ChoiceOptions(SaveData save, RuneTable table)
        {
            var list = new List<RuneData>();
            foreach (RuneData r in table.runes)
            {
                if (r != null && r.source == RuneSource.BossChoice && !save.runes.choiceRunes.Contains(r.id)) list.Add(r);
            }

            return list;
        }

        /// <summary>이미 상점에서 산 룬을 보상으로 고르면 산 값(골드)을 돌려준다 → 확정 보상이 사라지지 않는다</summary>
        public static bool TakeChoice(SaveData save, RuneTable table, string runeId)
        {
            string key = PendingChoice(save, table);
            RuneData rune = table.Find(runeId);
            if (key == null || rune == null || rune.source != RuneSource.BossChoice || save.runes.choiceRunes.Contains(runeId)) return false;
            save.runes.choicesTaken.Add(key);
            save.runes.choiceRunes.Add(runeId);
            if (Owns(save, runeId)) Wallet.AddGold(save, table.shopPrice);
            else Give(save, runeId);
            return true;
        }

        // ---------------------------------------------------------------- 상점·강화

        /// <summary>상점에서 살 수 있나: 칸이 열렸고, 안 가졌고, 상점 룬이거나 보스 보상에서 못 고른 룬 (인연의 룬은 서바이벌)</summary>
        public static bool CanBuy(SaveData save, RuneTable table, RuneData rune)
        {
            if (rune == null || SlotCount(save, table) == 0 || Owns(save, rune.id)) return false;
            if (rune.source == RuneSource.Shop) return true;
            // 보스 보상 룬: 그 칸의 고르기를 이미 했으면 남은 것도 상점에서 산다
            return rune.source == RuneSource.BossChoice && save.runes.choicesTaken.Count > 0 && PendingChoice(save, table) == null;
        }

        public static bool TryBuy(SaveData save, RuneTable table, string runeId)
        {
            RuneData rune = table.Find(runeId);
            if (!CanBuy(save, table, rune) || !Wallet.TrySpendGold(save, table.shopPrice)) return false;
            Give(save, runeId);
            return true;
        }

        public static bool TryUpgrade(SaveData save, RuneTable table, string runeId)
        {
            int level = Level(save, runeId);
            int cost = table.UpgradeCost(level);
            if (cost < 0 || !Wallet.TrySpendGold(save, cost)) return false;
            foreach (RuneLevel r in save.runes.owned)
            {
                if (r.id == runeId) r.level++;
            }

            return true;
        }

        // ---------------------------------------------------------------- 장착 (영웅마다)

        public static HeroRunes LoadoutOf(SaveData save, string heroId)
        {
            foreach (HeroRunes h in save.runes.loadouts)
            {
                if (h.heroId == heroId) return h;
            }

            var created = new HeroRunes { heroId = heroId };
            save.runes.loadouts.Add(created);
            return created;
        }

        public static string Equipped(SaveData save, string heroId, int slot)
        {
            foreach (HeroRunes h in save.runes.loadouts)
            {
                if (h.heroId == heroId) return slot < h.ids.Count ? h.ids[slot] : string.Empty;
            }

            return string.Empty;
        }

        /// <summary>그 칸에 낀다 (빈 문자열이면 뺀다). 다른 칸에 같은 룬이 있으면 그쪽을 비운다</summary>
        public static bool Equip(SaveData save, RuneTable table, string heroId, int slot, string runeId)
        {
            if (slot < 0 || slot >= SlotCount(save, table)) return false;
            if (!string.IsNullOrEmpty(runeId) && !Owns(save, runeId)) return false;
            HeroRunes h = LoadoutOf(save, heroId);
            for (int i = 0; i < h.ids.Count; i++)
            {
                if (i != slot && h.ids[i] == runeId) h.ids[i] = string.Empty;
            }

            h.ids[slot] = runeId ?? string.Empty;
            return true;
        }

        /// <summary>그 영웅이 낀 룬으로 카드 뽑기 배율 (없으면 null = 영향 없음)</summary>
        public static RuneWeightModifier ModifierFor(SaveData save, RuneTable table, string heroId, IReadOnlyList<FusionRecipe> recipes)
        {
            if (save == null || table == null) return null;
            var equipped = new List<(RuneData rune, float multiplier)>();
            int slots = SlotCount(save, table);
            for (int s = 0; s < slots; s++)
            {
                RuneData rune = table.Find(Equipped(save, heroId, s));
                int level = rune != null ? Level(save, rune.id) : 0;
                if (rune != null && level > 0) equipped.Add((rune, table.MultiplierAt(level)));
            }

            return equipped.Count > 0 ? new RuneWeightModifier(equipped, table.maxMultiplier, recipes, heroId) : null;
        }

        /// <summary>
        /// 상세 화면 글자 (Runes 8장): "희귀 카드 중 불 스킬 25% → 40%" — 그 영웅의 스킬 풀(새 판 기준)로 실제 계산. 인연의 룬은 설명만.
        /// </summary>
        public static string ShareText(RuneTable table, RuneData rune, int level, IEnumerable<SkillData> pool, string heroId)
        {
            if (rune == null) return string.Empty;
            if (rune.bond) return "가진 스킬과 조합되는 재료(발견한 조합만)가 같은 등급 안에서 더 잘 나와요";
            float m = table.MultiplierAt(Mathf.Max(1, level));
            var lines = new List<string>();
            foreach (Rarity rarity in new[] { Rarity.Common, Rarity.Rare })
            {
                int total = 0, tagged = 0;
                foreach (SkillData s in pool)
                {
                    if (s == null || s.rarity != rarity || !s.IsAvailableFor(heroId)) continue;
                    total++;
                    if ((s.tags & rune.tag) != 0) tagged++;
                }

                if (total == 0 || tagged == 0) continue;
                float before = (float)tagged / total;
                float after = tagged * m / (tagged * m + (total - tagged));
                string name = rarity == Rarity.Common ? "일반" : "희귀";
                lines.Add($"{name} 카드 중 {rune.displayName.Replace("의 룬", string.Empty)} 스킬 {before * 100f:0}% → {after * 100f:0}%");
            }

            lines.Add("전설이 나올 확률은 바뀌지 않아요");
            return string.Join("\n", lines);
        }
    }

    /// <summary>
    /// 낀 룬 → 같은 등급 안 후보 배율 (Runes 2·5장). 여러 룬이 겹치면 곱하고 최대 ×3.0.
    /// 인연의 룬: 후보가 **발견한** 레시피의 재료이고, 짝 재료를 지금 가지고 있으면.
    /// </summary>
    public sealed class RuneWeightModifier : IWeightModifier
    {
        private readonly List<(RuneData rune, float multiplier)> _runes;
        private readonly float _max;
        private readonly IReadOnlyList<FusionRecipe> _recipes;
        private readonly string _heroId;

        public RuneWeightModifier(List<(RuneData rune, float multiplier)> runes, float max, IReadOnlyList<FusionRecipe> recipes, string heroId)
        {
            _runes = runes;
            _max = max;
            _recipes = recipes;
            _heroId = heroId;
        }

        public float GetMultiplier(SkillData candidate, SkillLoadout currentLoadout)
        {
            if (candidate == null) return 1f;
            float m = 1f;
            foreach ((RuneData rune, float multiplier) in _runes)
            {
                bool match = rune.bond ? IsBondMaterial(candidate, currentLoadout) : (candidate.tags & rune.tag) != 0;
                if (match) m *= multiplier;
            }

            return Mathf.Min(m, _max);
        }

        private bool IsBondMaterial(SkillData candidate, SkillLoadout loadout)
        {
            if (_recipes == null || loadout == null) return false;
            foreach (FusionRecipe r in _recipes)
            {
                if (r == null || !RecipeBook.IsDiscovered(r)) continue;
                if (!string.IsNullOrEmpty(r.heroOnlyId) && r.heroOnlyId != _heroId) continue;
                if (r.materialA == candidate && loadout.Find(r.materialB) != null) return true;
                if (r.materialB == candidate && loadout.Find(r.materialA) != null) return true;
            }

            return false;
        }
    }
}
