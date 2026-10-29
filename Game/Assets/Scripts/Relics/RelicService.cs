using System.Collections.Generic;
using HistoryHeroes.Economy;
using HistoryHeroes.Records;
using UnityEngine;

namespace HistoryHeroes.Relics
{
    /// <summary>
    /// 유물 규칙 (Relics 2·4·5장). 저장 데이터만 바꾸고 저장은 부르는 쪽이 한다.
    /// - 획득: 유물마다 획득처 하나. 조건을 채우면 ★1로 (탭은 2구간 보스 뒤에 열리지만, 그전 것도 보관했다가 그때 적용)
    /// - 교환소: 🏺120으로 못 얻은 유물을 미리 → 나중에 원래 획득처를 채우면 중복 대신 🏺120 돌려줌
    /// - ★ 올리기: 🏺30 / 80. 뽑기·판매 없음
    /// </summary>
    public static class RelicService
    {
        /// <summary>유물 탭(효과)은 2구간 보스 격파 후 (Systems 3장)</summary>
        public static bool Unlocked(SaveData save) => save.unlockedChapter >= 3;

        public static int Stars(SaveData save, string relicId)
        {
            foreach (RelicStar r in save.relics.owned)
            {
                if (r.id == relicId) return r.stars;
            }

            return 0;
        }

        public static int Shards(SaveData save) => save.ItemCount(RelicTable.AncientShard);

        // ---------------------------------------------------------------- 보너스

        public struct Bonus
        {
            public float AttackHp;
            public float DamageReduction;
            public int CompletedSets;
        }

        /// <summary>★ 합 × 표의 별 보너스 + 완성 세트 × 표의 세트 보너스. 피해 감소는 완성 세트에서만</summary>
        public static Bonus Calculate(IReadOnlyList<RelicStar> owned, RelicTable table)
        {
            var bonus = new Bonus();
            if (owned == null || table == null) return bonus;
            int stars = 0;
            var setCounts = new int[table.setNames.Length];
            foreach (RelicStar r in owned)
            {
                RelicData data = table.Find(r.id);
                if (data == null || r.stars <= 0) continue;
                stars += Mathf.Clamp(r.stars, 0, RelicTable.MaxStars);
                if (data.set >= 0 && data.set < setCounts.Length) setCounts[data.set]++;
            }

            foreach (int count in setCounts)
            {
                if (count >= 4) bonus.CompletedSets++;
            }

            bonus.AttackHp = stars * table.perStar + bonus.CompletedSets * table.setBonus;
            bonus.DamageReduction = bonus.CompletedSets * table.setDamageReduction;
            return bonus;
        }

        /// <summary>지금 효과가 있는 유물 (탭이 열리기 전이면 비어 있음) — 스냅샷에 넣는다</summary>
        public static List<RelicStar> Active(SaveData save)
        {
            var list = new List<RelicStar>();
            if (!Unlocked(save)) return list;
            foreach (RelicStar r in save.relics.owned) list.Add(new RelicStar { id = r.id, stars = r.stars });
            return list;
        }

        // ---------------------------------------------------------------- 얻기

        /// <summary>조건을 채운 획득처를 모두 확인해 준다. 받은 줄 글자들</summary>
        public static List<string> CheckAll(SaveData save, RelicTable table, int heroesUnlocked = -1)
        {
            var lines = new List<string>();
            if (table == null) return lines;
            foreach (RelicData r in table.relics)
            {
                if (r == null || save.relics.sourcesClaimed.Contains(r.id) || !Earned(save, r, heroesUnlocked)) continue;
                save.relics.sourcesClaimed.Add(r.id);
                if (Stars(save, r.id) > 0)
                {
                    // 교환소로 먼저 얻은 유물: 중복 대신 돌려준다
                    save.AddItem(RelicTable.AncientShard, table.exchangeCost);
                    lines.Add($"유물  {r.displayName} (이미 교환함) → <sprite name=ancient_shard>고대 파편 +{table.exchangeCost}");
                    continue;
                }

                save.relics.owned.Add(new RelicStar { id = r.id, stars = 1 });
                lines.Add($"유물 발견!  {r.displayName}");
            }

            return lines;
        }

        private static bool Earned(SaveData save, RelicData r, int heroesUnlocked)
        {
            switch (r.source)
            {
                case RelicSource.HeroFirstClear: return save.relics.heroDungeonClears.Contains(r.sourceValue);
                case RelicSource.BossFirstClear: return save.unlockedChapter > r.sourceChapter;
                case RelicSource.ChapterStars:
                    return Economy.Stars.Count(save, r.sourceChapter, Economy.Stars.Dungeon) + Economy.Stars.Count(save, r.sourceChapter, Economy.Stars.Boss) >= 6;
                case RelicSource.Achievement:
                    switch (r.sourceValue)
                    {
                        case "recipes5": return save.discoveredRecipes.Count >= 5;
                        case "recipes10": return save.discoveredRecipes.Count >= 10;
                        case "records10": return save.relics.recordsSavedTotal >= 10;
                        case "heroes4": return heroesUnlocked >= 4;
                        default: return false;
                    }
                case RelicSource.Survival:
                    switch (r.sourceValue)
                    {
                        case "survive5": return save.survival.longestSeconds >= 300f;
                        case "clear10": return save.survival.clears > 0;
                        case "two_heroes": return save.survival.clearHeroes.Count >= 2;
                        default: return false;
                    }
                default:
                    return false;
            }
        }

        /// <summary>그 영웅으로 던전을 처음 깼다 (결과 화면에서 유물 확인 전에 부른다)</summary>
        public static void MarkHeroClear(SaveData save, string heroId)
        {
            if (!string.IsNullOrEmpty(heroId) && !save.relics.heroDungeonClears.Contains(heroId)) save.relics.heroDungeonClears.Add(heroId);
        }

        // ---------------------------------------------------------------- ★·교환

        public static bool TryStar(SaveData save, RelicTable table, string relicId)
        {
            int stars = Stars(save, relicId);
            int cost = table.StarCost(stars);
            if (cost < 0 || Shards(save) < cost) return false;
            save.AddItem(RelicTable.AncientShard, -cost);
            foreach (RelicStar r in save.relics.owned)
            {
                if (r.id == relicId) r.stars++;
            }

            return true;
        }

        /// <summary>🏺120: 아직 없는 유물 (원래 획득처를 이미 채운 유물은 교환할 필요가 없으니 이미 가지고 있다)</summary>
        public static bool TryExchange(SaveData save, RelicTable table, string relicId)
        {
            if (table.Find(relicId) == null || Stars(save, relicId) > 0 || Shards(save) < table.exchangeCost) return false;
            save.AddItem(RelicTable.AncientShard, -table.exchangeCost);
            save.relics.owned.Add(new RelicStar { id = relicId, stars = 1 });
            return true;
        }
    }
}
