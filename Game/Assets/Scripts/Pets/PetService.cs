using System.Collections.Generic;
using HistoryHeroes.Economy;
using HistoryHeroes.Records;

namespace HistoryHeroes.Pets
{
    /// <summary>
    /// 펫 저장·규칙 (Pets 4·5장, Systems 3장): 획득(플레이로 확정, 뽑기 없음), 레벨업(골드만), 영웅마다 1칸 장착,
    /// 판에 가져가는 값은 스냅샷(펫 ID·레벨·인연)으로 고정한다 — 보스맵은 기록의 펫으로 싸운다.
    /// </summary>
    public static class PetService
    {
        /// <summary>펫 칸이 열렸나: 첫 펫(사냥매)과 함께 열린다 (튜토리얼 보스 대신 첫 던전 클리어 — v0.5-Plan)</summary>
        public static bool Unlocked(SaveData save) => save.pets.owned.Count > 0;

        public static int Level(SaveData save, string petId)
        {
            if (string.IsNullOrEmpty(petId)) return 0;
            foreach (PetLevel p in save.pets.owned)
            {
                if (p.id == petId) return p.level;
            }

            return 0;
        }

        public static bool Owns(SaveData save, string petId) => Level(save, petId) > 0;

        /// <summary>조건을 채운 펫을 준다 (결과 화면·로비에서 부른다, 한 번만). 받은 줄을 돌려준다.
        /// 고른 영웅에게 펫이 없으면 받은 펫을 바로 끼워 준다 (처음 보는 펫이 판에서 바로 보이게)</summary>
        public static List<string> CheckAll(SaveData save, PetTable table)
        {
            var lines = new List<string>();
            if (table == null) return lines;
            foreach (PetData p in table.pets)
            {
                if (p == null || Owns(save, p.id) || !Earned(save, p)) continue;
                save.pets.owned.Add(new PetLevel { id = p.id, level = 1 });
                string hero = save.selectedHero;
                if (!string.IsNullOrEmpty(hero) && Equipped(save, hero) == null) Equip(save, table, hero, p.id);
                lines.Add($"펫 획득!  {p.displayName} (영웅 → 펫)");
            }

            return lines;
        }

        private static bool Earned(SaveData save, PetData p)
        {
            switch (p.source)
            {
                case PetSource.FirstDungeonClear: return save.tutorialDone; // 튜토리얼 보스 격파 (v1.0, Pets 7-1)
                case PetSource.BossFirstClear: return save.unlockedChapter > p.sourceChapter;
                case PetSource.SurvivalFirstClear: return save.survival.clears > 0;
                default: return false;
            }
        }

        /// <summary>레벨업 골드 (최대거나 없는 펫이면 -1)</summary>
        public static int LevelCost(SaveData save, PetTable table, string petId)
        {
            int level = Level(save, petId);
            return level <= 0 ? -1 : table.LevelCost(level);
        }

        public static bool TryLevelUp(SaveData save, PetTable table, string petId)
        {
            int cost = LevelCost(save, table, petId);
            if (cost < 0 || !Wallet.TrySpendGold(save, cost)) return false;
            foreach (PetLevel p in save.pets.owned)
            {
                if (p.id == petId) p.level++;
            }

            return true;
        }

        /// <summary>그 영웅이 낀 펫 ID (없거나 안 가진 펫이면 null)</summary>
        public static string Equipped(SaveData save, string heroId)
        {
            foreach (HeroPet l in save.pets.loadouts)
            {
                if (l.heroId == heroId) return Owns(save, l.petId) ? l.petId : null;
            }

            return null;
        }

        /// <summary>끼기 (petId가 null이면 빼기). 같은 펫을 여러 영웅이 같이 써도 된다 (Pets 5장)</summary>
        public static bool Equip(SaveData save, PetTable table, string heroId, string petId)
        {
            if (string.IsNullOrEmpty(heroId)) return false;
            if (petId != null && (table.Find(petId) == null || !Owns(save, petId))) return false;
            HeroPet loadout = save.pets.loadouts.Find(l => l.heroId == heroId);
            if (loadout == null)
            {
                loadout = new HeroPet { heroId = heroId };
                save.pets.loadouts.Add(loadout);
            }

            loadout.petId = petId;
            return true;
        }

        public static bool IsBond(PetData pet, string heroId) => pet != null && !string.IsNullOrEmpty(heroId) && pet.bondHeroId == heroId;

        /// <summary>판·기록에 가져갈 펫 (ID·레벨·인연을 지금 값으로 고정)</summary>
        public static void FillSnapshot(MetaSnapshot snap, SaveData save, PetTable table, string heroId)
        {
            snap.petId = null;
            snap.petLevel = 0;
            snap.petBond = false;
            if (table == null) return;
            string id = Equipped(save, heroId);
            PetData pet = table.Find(id);
            if (pet == null) return;
            snap.petId = id;
            snap.petLevel = Level(save, id);
            snap.petBond = IsBond(pet, heroId);
        }

        /// <summary>피해 출처 이름 ("pet_hawk" → "사냥매")</summary>
        public static string SourceName(string sourceId)
        {
            switch (sourceId)
            {
                case "pet_hawk": return "사냥매";
                case "pet_redhare": return "적토마";
                case "pet_marengo": return "마렝고";
                case "pet_dove": return "흰 비둘기";
                default: return "펫";
            }
        }
    }
}
