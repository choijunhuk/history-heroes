using System.Collections.Generic;
using HistoryHeroes.Economy;
using HistoryHeroes.Records;
using UnityEngine;

namespace HistoryHeroes.Heroes
{
    /// <summary>게임에 있는 영웅 전부 (로비 영웅 창, 기록의 영웅 ID → 데이터).</summary>
    [CreateAssetMenu(menuName = "History Heroes/Hero Roster", fileName = "HeroRoster")]
    public sealed class HeroRoster : ScriptableObject
    {
        public List<HeroData> heroes = new List<HeroData>();

        /// <summary>처음부터 쓰는 영웅 (칭기즈 칸)</summary>
        public HeroData Default => heroes.Count > 0 ? heroes[0] : null;

        public HeroData Find(string id)
        {
            if (!string.IsNullOrEmpty(id))
            {
                foreach (HeroData h in heroes)
                {
                    if (h != null && h.id == id) return h;
                }
            }

            return Default;
        }
    }

    /// <summary>
    /// 영웅 해금 (Heroes.md 1·2장): 진행으로 무료 해금하거나 보석 1,500으로 바로. 영웅 뽑기는 없다.
    /// </summary>
    public static class HeroUnlock
    {
        public static bool IsUnlocked(SaveData d, HeroData hero)
        {
            if (hero == null) return false;
            if (hero.unlockAfterBossChapter <= 0) return true;
            if (d.unlockedChapter > hero.unlockAfterBossChapter) return true; // 그 구간 보스를 깼다
            return d.unlockedHeroes.Contains(hero.id);
        }

        /// <summary>"1구간 보스를 깨면 열려요" 같은 안내</summary>
        public static string Condition(HeroData hero)
        {
            return hero.unlockAfterBossChapter <= 0 ? "처음부터" : $"{hero.unlockAfterBossChapter}구간 보스를 깨면 열려요";
        }

        public static bool TryBuy(SaveData d, HeroData hero)
        {
            if (hero == null || IsUnlocked(d, hero) || !Wallet.TrySpendGems(d, hero.unlockGems)) return false;
            d.unlockedHeroes.Add(hero.id);
            return true;
        }

        /// <summary>고른 영웅 (잠겨 있으면 처음 영웅)</summary>
        public static HeroData Selected(SaveData d, HeroRoster roster)
        {
            HeroData hero = roster != null ? roster.Find(d.selectedHero) : null;
            return hero != null && IsUnlocked(d, hero) ? hero : roster != null ? roster.Default : null;
        }
    }
}
