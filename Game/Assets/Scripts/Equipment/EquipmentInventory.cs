using System.Collections.Generic;
using HistoryHeroes.Economy;
using HistoryHeroes.Records;
using UnityEngine;

namespace HistoryHeroes.Equipment
{
    /// <summary>
    /// 장비 규칙 (Equipment 4·5·9·10장). 저장 데이터만 바꾸고, 저장(SaveSystem.Save)은 부르는 쪽이 한 번에 한다
    /// → 합성처럼 "재료 제거 + 결과 추가 + 골드 차감"이 한 저장에 같이 들어간다 (중간에 꺼져도 재료만 사라지지 않게).
    /// - 강화는 **칸**에 붙는다 (계정 공용). 장비를 바꿔 끼워도 강화가 남는다
    /// - 장착은 영웅마다 3칸, 같은 장비를 여러 영웅이 동시에 낄 수 있다 (참조)
    /// - 합성: 같은 칸·같은 등급 3개 → 다음 등급 중 **고른 것** (희귀까지, 전설은 합성 불가)
    /// </summary>
    public static class EquipmentInventory
    {
        public const int SlotCount = 3;

        /// <summary>1구간 보스 최초 격파 후 열린다 (Systems 3장)</summary>
        public static bool Unlocked(SaveData save) => save.unlockedChapter >= 2 || save.defeatedBosses.Count > 0;

        public static EquipmentItem Find(SaveData save, string uid)
        {
            if (string.IsNullOrEmpty(uid)) return null;
            foreach (EquipmentItem i in save.equipment.items)
            {
                if (i.uid == uid) return i;
            }

            return null;
        }

        public static bool Owns(SaveData save, string dataId)
        {
            foreach (EquipmentItem i in save.equipment.items)
            {
                if (i.dataId == dataId) return true;
            }

            return false;
        }

        public struct AddResult
        {
            public EquipmentItem Item;   // 넣은 장비 (분해했으면 null)
            public int Shards;           // 자동 분해로 받은 🔩
            public bool InventoryFull;
        }

        /// <summary>
        /// 장비를 얻는다. 희귀·전설 **중복**은 자동 분해(설정이 켜져 있으면), 가방(200칸)이 차면 🔩로 바꾼다
        /// (우편함은 v1.0, v0.4-Plan "정해서 진행하는 것").
        /// </summary>
        public static AddResult Add(SaveData save, EquipmentTable table, EquipmentData data)
        {
            var result = new AddResult();
            if (data == null) return result;

            bool duplicate = Owns(save, data.id);
            bool autoDismantle = save.equipment.autoDismantle && duplicate && data.rarity >= EquipRarity.Rare;
            bool full = save.equipment.items.Count >= table.inventoryCap;
            if (autoDismantle || full)
            {
                result.Shards = table.DismantleShards(data.rarity);
                result.InventoryFull = full && !autoDismantle;
                save.AddItem(Wallet.GearShard, result.Shards);
                return result;
            }

            var item = new EquipmentItem { uid = "e_" + save.equipment.nextUid++.ToString("D5"), dataId = data.id };
            save.equipment.items.Add(item);
            result.Item = item;
            return result;
        }

        // ---------------------------------------------------------------- 장착

        public static HeroLoadout LoadoutOf(SaveData save, string heroId)
        {
            foreach (HeroLoadout l in save.equipment.loadouts)
            {
                if (l.heroId == heroId) return l;
            }

            var created = new HeroLoadout { heroId = heroId };
            save.equipment.loadouts.Add(created);
            return created;
        }

        /// <summary>그 영웅이 그 칸에 낀 장비 (없으면 null)</summary>
        public static EquipmentItem Equipped(SaveData save, string heroId, EquipSlot slot)
        {
            foreach (HeroLoadout l in save.equipment.loadouts)
            {
                if (l.heroId == heroId) return Find(save, l.uids[(int)slot]);
            }

            return null;
        }

        public static bool Equip(SaveData save, EquipmentTable table, string heroId, string uid)
        {
            EquipmentItem item = Find(save, uid);
            EquipmentData data = item != null ? table.Find(item.dataId) : null;
            if (data == null || string.IsNullOrEmpty(heroId)) return false;
            LoadoutOf(save, heroId).uids[(int)data.slot] = uid;
            return true;
        }

        public static void Unequip(SaveData save, string heroId, EquipSlot slot) => LoadoutOf(save, heroId).uids[(int)slot] = "";

        /// <summary>누군가 끼고 있는가 (합성 재료·분해에서 뺀다)</summary>
        public static bool IsEquipped(SaveData save, string uid)
        {
            if (string.IsNullOrEmpty(uid)) return false;
            foreach (HeroLoadout l in save.equipment.loadouts)
            {
                if (l.uids.Contains(uid)) return true;
            }

            return false;
        }

        public static void SetLocked(SaveData save, string uid, bool locked)
        {
            EquipmentItem item = Find(save, uid);
            if (item != null) item.locked = locked;
        }

        // ---------------------------------------------------------------- 칸 강화

        public static int SlotLevel(SaveData save, EquipSlot slot) => save.equipment.slotLevels[(int)slot];

        /// <summary>
        /// 이 영웅이 지금 낀 장비 기준으로 칸을 한 번 더 올릴 수 있는가: 장비가 있어야 하고, 그 등급 상한 아래여야 한다
        /// ("상한 넘는 강화는 막는다, 미리 올려둘 수 없음").
        /// </summary>
        public static bool CanEnhance(SaveData save, EquipmentTable table, string heroId, EquipSlot slot, out int cost)
        {
            cost = table.EnhanceCost(SlotLevel(save, slot));
            EquipmentItem item = Equipped(save, heroId, slot);
            EquipmentData data = item != null ? table.Find(item.dataId) : null;
            if (data == null) return false;
            return SlotLevel(save, slot) < table.CapOf(data.rarity);
        }

        public static bool TryEnhance(SaveData save, EquipmentTable table, string heroId, EquipSlot slot)
        {
            if (!CanEnhance(save, table, heroId, slot, out int cost) || !Wallet.TrySpendGold(save, cost)) return false;
            save.equipment.slotLevels[(int)slot]++;
            return true;
        }

        /// <summary>"최대까지": 가진 골드로 갈 수 있는 레벨까지 (몇 레벨, 총 비용). 실제로 올리지는 않는다</summary>
        public static (int levels, int cost) PreviewMax(SaveData save, EquipmentTable table, string heroId, EquipSlot slot)
        {
            EquipmentItem item = Equipped(save, heroId, slot);
            EquipmentData data = item != null ? table.Find(item.dataId) : null;
            if (data == null) return (0, 0);
            int level = SlotLevel(save, slot), gold = save.gold, levels = 0, total = 0;
            while (level < table.CapOf(data.rarity))
            {
                int c = table.EnhanceCost(level);
                if (total + c > gold) break;
                total += c;
                level++;
                levels++;
            }

            return (levels, total);
        }

        public static int EnhanceMax(SaveData save, EquipmentTable table, string heroId, EquipSlot slot)
        {
            int done = 0;
            while (TryEnhance(save, table, heroId, slot)) done++;
            return done;
        }

        // ---------------------------------------------------------------- 합성

        /// <summary>재료 3개 검사: 서로 다른 장비, 같은 칸·같은 등급, 고급 이하, 잠금 아님, 아무도 안 낌</summary>
        public static bool CanSynthesize(SaveData save, EquipmentTable table, IReadOnlyList<string> uids, out EquipSlot slot, out EquipRarity rarity)
        {
            slot = EquipSlot.Weapon;
            rarity = EquipRarity.Common;
            if (uids == null || uids.Count != table.synthesisCount) return false;

            var seen = new HashSet<string>();
            for (int i = 0; i < uids.Count; i++)
            {
                EquipmentItem item = Find(save, uids[i]);
                EquipmentData data = item != null ? table.Find(item.dataId) : null;
                if (data == null || !seen.Add(item.uid) || item.locked || IsEquipped(save, item.uid)) return false;
                if (i == 0)
                {
                    slot = data.slot;
                    rarity = data.rarity;
                }
                else if (data.slot != slot || data.rarity != rarity)
                {
                    return false;
                }
            }

            return table.SynthesisCost(rarity) >= 0;
        }

        /// <summary>재료 자동 추천: 잠금·장착 아닌 것 중 같은 칸·등급 3개 (없으면 null)</summary>
        public static List<string> SuggestMaterials(SaveData save, EquipmentTable table, EquipSlot slot, EquipRarity rarity)
        {
            var list = new List<string>();
            foreach (EquipmentItem item in save.equipment.items)
            {
                EquipmentData data = table.Find(item.dataId);
                if (data == null || data.slot != slot || data.rarity != rarity || item.locked || IsEquipped(save, item.uid)) continue;
                list.Add(item.uid);
                if (list.Count == table.synthesisCount) return list;
            }

            return null;
        }

        /// <summary>합성: 검사 → 골드 차감 → 재료 제거 + 결과 추가 (같은 저장에 들어가게 부르는 쪽이 바로 저장)</summary>
        public static EquipmentItem TrySynthesize(SaveData save, EquipmentTable table, IReadOnlyList<string> uids, string resultDataId)
        {
            if (!CanSynthesize(save, table, uids, out EquipSlot slot, out EquipRarity rarity)) return null;
            EquipmentData result = table.Find(resultDataId);
            if (result == null || result.slot != slot || result.rarity != rarity + 1) return null;
            if (!Wallet.TrySpendGold(save, table.SynthesisCost(rarity))) return null;

            foreach (string uid in uids) save.equipment.items.RemoveAll(i => i.uid == uid);
            var item = new EquipmentItem { uid = "e_" + save.equipment.nextUid++.ToString("D5"), dataId = result.id };
            save.equipment.items.Add(item); // 합성 결과는 가방이 차도 넣는다 (재료 3개가 빠졌으므로 늘 자리가 있다)
            return item;
        }

        // ---------------------------------------------------------------- 분해·교환

        public static int TryDismantle(SaveData save, EquipmentTable table, string uid)
        {
            EquipmentItem item = Find(save, uid);
            EquipmentData data = item != null ? table.Find(item.dataId) : null;
            if (data == null || item.locked || IsEquipped(save, uid)) return 0;
            int shards = table.DismantleShards(data.rarity);
            save.equipment.items.Remove(item);
            save.AddItem(Wallet.GearShard, shards);
            return shards;
        }

        /// <summary>🔩 교환소: 원하는 희귀 60 / 전설 300 (확정, 확률 없음)</summary>
        public static int ExchangeCost(EquipmentTable table, EquipmentData data)
        {
            if (data == null) return -1;
            if (data.rarity == EquipRarity.Rare) return table.exchangeRareCost;
            if (data.rarity == EquipRarity.Legendary) return table.exchangeLegendaryCost;
            return -1;
        }

        public static AddResult? TryExchange(SaveData save, EquipmentTable table, string dataId)
        {
            EquipmentData data = table.Find(dataId);
            int cost = ExchangeCost(table, data);
            if (cost < 0 || save.ItemCount(Wallet.GearShard) < cost) return null;
            save.AddItem(Wallet.GearShard, -cost);
            // 교환은 원하는 것을 고른 거라 중복이어도 자동 분해하지 않는다
            bool auto = save.equipment.autoDismantle;
            save.equipment.autoDismantle = false;
            AddResult r = Add(save, table, data);
            save.equipment.autoDismantle = auto;
            return r;
        }

        // ---------------------------------------------------------------- 기록 스냅샷

        /// <summary>그 영웅이 지금 낀 장비 + 칸 강화 (등급 상한으로 자름). 기록에 고정 저장한다 (GameDesign 7-5)</summary>
        public static List<EquippedItem> Snapshot(SaveData save, EquipmentTable table, string heroId)
        {
            var list = new List<EquippedItem>();
            for (int s = 0; s < SlotCount; s++)
            {
                EquipmentItem item = Equipped(save, heroId, (EquipSlot)s);
                EquipmentData data = item != null ? table.Find(item.dataId) : null;
                if (data == null) continue;
                list.Add(new EquippedItem { slot = s, dataId = data.id, level = table.EffectiveLevel(data, save.equipment.slotLevels[s]) });
            }

            return list;
        }

        /// <summary>
        /// 던전 클리어 드랍 (Equipment 8장): 30% 확률, 일반 70 / 고급 25 / 희귀 5. 해금 전에는 없음.
        /// roll01·rarityRoll01을 넣으면 그 값으로 (테스트용).
        /// </summary>
        public static EquipmentData RollDungeonDrop(SaveData save, EquipmentTable table, float? roll01 = null, float? rarityRoll01 = null, int? pick = null)
        {
            if (!Unlocked(save)) return null;
            float r = roll01 ?? Random.value;
            if (r >= 0.3f) return null;
            float g = rarityRoll01 ?? Random.value;
            EquipRarity rarity = g < 0.70f ? EquipRarity.Common : g < 0.95f ? EquipRarity.Uncommon : EquipRarity.Rare;
            var pool = new List<EquipmentData>();
            foreach (EquipmentData d in table.items)
            {
                if (d != null && d.rarity == rarity) pool.Add(d);
            }

            if (pool.Count == 0) return null;
            return pool[Mathf.Clamp(pick ?? Random.Range(0, pool.Count), 0, pool.Count - 1)];
        }
    }
}
