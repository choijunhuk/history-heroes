using System;
using System.Collections.Generic;
using HistoryHeroes.Economy;
using HistoryHeroes.Equipment;
using HistoryHeroes.Heroes;
using HistoryHeroes.Meta;
using HistoryHeroes.Records;

namespace HistoryHeroes.Progress
{
    /// <summary>기능을 쓸 수 있을 때만 안내한다. 판정은 저장이나 화면을 바꾸지 않는다.</summary>
    public static class FirstUseHints
    {
        public const string Newcomer = "newcomer";
        public const string HeroLevel = "hero_level";
        public const string Gear = "gear";
        public const string Missions = "missions";
        public const string Shop = "shop";

        public static string Next(SaveData save, bool inCombat, bool blockingPanel, DateTimeOffset now,
            bool canLevelUp, bool hasGear, int claimableMissions)
        {
            if (save == null || !save.tutorialDone || inCombat || blockingPanel) return null;
            if (!Seen(save, Newcomer) && HistoryHeroes.Missions.Newcomer.Visible(save, now)) return Newcomer;
            if (!Seen(save, HeroLevel) && canLevelUp) return HeroLevel;
            if (!Seen(save, Gear) && hasGear) return Gear;
            if (!Seen(save, Missions) && claimableMissions > 0) return Missions;
            if (!Seen(save, Shop) && save.ItemCount(Wallet.Ticket) > 0) return Shop;
            return null;
        }

        private static bool Seen(SaveData save, string id) => save.seenFirstUseHints != null && save.seenFirstUseHints.Contains(id);

        public static bool MarkShown(SaveData save, string id)
        {
            if (save == null || (id != Newcomer && id != HeroLevel && id != Gear && id != Missions && id != Shop) || Seen(save, id)) return false;
            save.seenFirstUseHints ??= new List<string>();
            save.seenFirstUseHints.Add(id);
            return true;
        }

        /// <summary>HeroGrowth.CanLevelUp은 성장 항목을 만들기 때문에 안내에서는 읽기만 한다.</summary>
        public static bool CanLevelUp(SaveData save, MetaTable table, HeroRoster roster)
        {
            if (save == null || table == null || roster == null) return false;
            foreach (HeroData hero in roster.heroes)
            {
                if (!HeroUnlock.IsUnlocked(save, hero)) continue;
                HeroProgress progress = save.heroProgress.Find(h => h.heroId == hero.id);
                int level = progress != null ? progress.level : 1;
                if (level < table.maxHeroLevel && save.gold >= table.HeroLevelCost(level)) return true;
            }
            return false;
        }

        public static bool HasGear(SaveData save, EquipmentTable table)
        {
            if (save == null || table == null || !EquipmentInventory.Unlocked(save)) return false;
            foreach (EquipmentItem item in save.equipment.items)
            {
                if (item != null && table.Find(item.dataId) != null) return true;
            }
            return false;
        }

        public static string Text(string id)
        {
            switch (id)
            {
                case Newcomer: return "<b>7일 미션</b>에서 다음 목표를 확인해요. 원하는 때에 열어 보세요!";
                case HeroLevel: return "골드가 모였어요! <b>영웅 탭</b>에서 레벨을 올릴 수 있어요.";
                case Gear: return "장비를 얻었어요! <b>영웅 탭의 장비 칸</b>에서 끼우고 강화해요.";
                case Missions: return "받을 보상이 있어요! <b>미션 탭</b>에서 확인해요.";
                case Shop: return "무료 소환권이 있어요! <b>상점 탭</b>에서 고급 상자를 열 수 있어요.";
                default: return string.Empty;
            }
        }
    }
}
