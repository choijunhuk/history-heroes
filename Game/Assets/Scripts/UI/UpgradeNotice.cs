using System;
using HistoryHeroes.Equipment;
using HistoryHeroes.Heroes;
using HistoryHeroes.Meta;
using HistoryHeroes.Missions;
using HistoryHeroes.Records;

namespace HistoryHeroes.UI
{
    /// <summary>로비 알림 점에 쓰는 읽기 전용 판정.</summary>
    public static class UpgradeNotice
    {
        public static bool HasHeroUpgrade(SaveData save, MetaTable table, HeroRoster roster)
        {
            if (save == null || table == null || roster == null) return false;
            foreach (HeroData hero in roster.heroes)
            {
                if (!HeroUnlock.IsUnlocked(save, hero)) continue;
                HeroProgress progress = save.heroProgress.Find(p => p.heroId == hero.id);
                int level = progress != null ? progress.level : 1;
                int stars = progress != null ? progress.stars : 1;
                if (level < table.maxHeroLevel && save.gold >= table.HeroLevelCost(level)) return true;
                int shards = table.AwakenCost(stars);
                if (shards > 0 && HeroGrowth.Shards(save, hero.id) >= shards) return true;
                if (table.equipment == null || !EquipmentInventory.Unlocked(save)) continue;
                for (int i = 0; i < EquipmentInventory.SlotCount; i++)
                {
                    if (EquipmentInventory.CanEnhance(save, table.equipment, hero.id, (EquipSlot)i, out int cost) && save.gold >= cost) return true;
                }
            }

            return false;
        }

        public static bool HasNewcomerReward(SaveData save, DateTimeOffset now) =>
            save != null && Newcomer.Visible(save, now) && Newcomer.Claimable(save, now) > 0;
    }
}
