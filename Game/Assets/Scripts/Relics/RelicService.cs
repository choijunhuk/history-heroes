using System.Collections.Generic;
using HistoryHeroes.Economy;
using HistoryHeroes.Records;
using UnityEngine;

namespace HistoryHeroes.Relics
{
    /// <summary>
    /// 유물 규칙 (Relics 2·4·5장). 저장 데이터만 바꾸고 저장은 부르는 쪽이 한다.
    /// - 영웅 첫 던전 클리어로 연결 유물 1개. 보스·별·업적·서바이벌의 종전 고정 유물은 파편 보상으로 바꾼다.
    /// - 같은 유물을 다시 받으면 ★를 올린다. ★3 초과는 파편으로 바꾼다.
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

        /// <summary>뽑기와 영웅 첫 클리어가 쓰는 동일한 중복 규칙</summary>
        public static RelicGachaService.Pull Add(SaveData save, RelicTable table, RelicData relic)
        {
            foreach (RelicStar owned in save.relics.owned)
            {
                if (owned.id != relic.id) continue;
                var result = new RelicGachaService.Pull { Data = relic, Stars = owned.stars };
                if (owned.stars >= RelicTable.MaxStars)
                {
                    save.AddItem(RelicTable.AncientShard, table.overflowShards);
                    result.Shards = table.overflowShards;
                }
                else
                {
                    owned.duplicates++;
                    if (owned.duplicates >= table.DuplicatesNeeded(owned.stars))
                    {
                        owned.stars++;
                        owned.duplicates = 0;
                        result.StarUp = true;
                    }

                    result.Stars = owned.stars;
                    result.Duplicates = owned.duplicates;
                }

                return result;
            }

            save.relics.owned.Add(new RelicStar { id = relic.id, stars = 1 });
            save.relicGacha.newRelicPity = 0;
            return new RelicGachaService.Pull { Data = relic, Stars = 1, IsNew = true };
        }

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

        /// <summary>영웅 첫 클리어는 유물, 종전 고정 유물 조건은 파편으로 한 번 지급한다</summary>
        public static List<string> CheckAll(SaveData save, RelicTable table, int heroesUnlocked = -1)
        {
            var lines = new List<string>();
            if (table == null) return lines;
            foreach (RelicData r in table.relics)
            {
                if (r == null || save.relics.sourcesClaimed.Contains(r.id) || !Earned(save, r, heroesUnlocked)) continue;
                save.relics.sourcesClaimed.Add(r.id);
                if (r.source != RelicSource.HeroFirstClear)
                {
                    save.AddItem(RelicTable.AncientShard, table.sourceShards);
                    lines.Add($"조건 달성 보상 · <sprite name=ancient_shard>고대 파편 +{table.sourceShards}");
                    continue;
                }

                RelicGachaService.Pull got = Add(save, table, r);
                string outcome = got.IsNew ? "NEW ★1" : got.StarUp ? $"★UP ★{got.Stars}" : got.Shards > 0 ? $"고대 파편 +{got.Shards}" : $"★{got.Stars} 중복 {got.Duplicates}/{table.DuplicatesNeeded(got.Stars)}";
                lines.Add($"유물  {r.displayName} · {outcome}");
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

    }
}
