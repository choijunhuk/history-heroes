using System.Collections.Generic;
using HistoryHeroes.Stats;
using UnityEngine;

namespace HistoryHeroes.Equipment
{
    /// <summary>
    /// 장비 숫자 표 (Equipment 3~5·9장). 장비 목록, 등급별 주 능력치 기본값, 칸 강화 상한·Lv당 값, 비용, 합성, 분해.
    /// 힘 예산: 전부 최대로 올려도 공격력·체력 +25%, 공격속도 +8% (테스트로 고정).
    /// </summary>
    [CreateAssetMenu(menuName = "History Heroes/Equipment Table", fileName = "EquipmentTable")]
    public sealed class EquipmentTable : ScriptableObject
    {
        public List<EquipmentData> items = new List<EquipmentData>();

        [Header("주 능력치 기본값 (등급 순서: 일반·고급·희귀·전설)")]
        public float[] weaponBase = { 0.03f, 0.06f, 0.10f, 0.15f };
        public float[] armorBase = { 0.03f, 0.06f, 0.10f, 0.15f };
        public float[] accessoryBase = { 0.015f, 0.03f, 0.045f, 0.06f };

        [Header("칸 강화")]
        [Tooltip("등급별 강화 상한 (이 레벨까지만 효과)")]
        public int[] enhanceCap = { 10, 20, 30, 40 };
        public float weaponPerLevel = 0.0025f;
        public float armorPerLevel = 0.0025f;
        public float accessoryPerLevel = 0.0005f;
        [Tooltip("Lv n → n+1 = 이 값 × n (Lv0 → 1도 이 값)")]
        public int enhanceCostPerLevel = 100;
        public int MaxSlotLevel => enhanceCap[enhanceCap.Length - 1];

        [Header("합성 (같은 칸·같은 등급 3개 → 다음 등급, 희귀까지)")]
        public int synthesisCount = 3;
        public int toUncommonCost = 500;
        public int toRareCost = 3000;

        [Header("분해 🔩 (등급 순서)")]
        public int[] dismantleShards = { 1, 3, 10, 50 };

        [Header("조각 교환소 🔩")]
        public int exchangeRareCost = 60;
        public int exchangeLegendaryCost = 300;

        [Header("인벤토리")]
        public int inventoryCap = 200;

        public EquipmentData Find(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            foreach (EquipmentData d in items)
            {
                if (d != null && d.id == id) return d;
            }

            return null;
        }

        public List<EquipmentData> Of(EquipSlot slot, EquipRarity rarity)
        {
            var list = new List<EquipmentData>();
            foreach (EquipmentData d in items)
            {
                if (d != null && d.slot == slot && d.rarity == rarity) list.Add(d);
            }

            return list;
        }

        public static StatType MainStat(EquipSlot slot)
        {
            switch (slot)
            {
                case EquipSlot.Armor: return StatType.MaxHp;
                case EquipSlot.Accessory: return StatType.AttackSpeed;
                default: return StatType.Attack;
            }
        }

        public int CapOf(EquipRarity rarity) => enhanceCap[(int)rarity];

        /// <summary>칸 강화 레벨이 이 장비에 실제로 먹는 만큼 (등급 상한으로 자름)</summary>
        public int EffectiveLevel(EquipmentData data, int slotLevel) => data == null ? 0 : Mathf.Clamp(slotLevel, 0, CapOf(data.rarity));

        /// <summary>주 능력치 % = 등급 기본값 + min(칸 강화, 등급 상한) × Lv당 값 (Equipment 3-2·3-3)</summary>
        public float MainPercent(EquipmentData data, int slotLevel)
        {
            if (data == null) return 0f;
            int r = (int)data.rarity;
            int level = EffectiveLevel(data, slotLevel);
            switch (data.slot)
            {
                case EquipSlot.Armor: return armorBase[r] + level * armorPerLevel;
                case EquipSlot.Accessory: return accessoryBase[r] + level * accessoryPerLevel;
                default: return weaponBase[r] + level * weaponPerLevel;
            }
        }

        /// <summary>칸 강화 Lv n → n+1 비용 (🪙 100 × n, 실패 없음)</summary>
        public int EnhanceCost(int currentLevel) => enhanceCostPerLevel * Mathf.Max(1, currentLevel);

        /// <summary>등급을 올리는 합성 비용 (일반→고급 500, 고급→희귀 3,000). 희귀·전설은 합성 불가 → -1</summary>
        public int SynthesisCost(EquipRarity from)
        {
            switch (from)
            {
                case EquipRarity.Common: return toUncommonCost;
                case EquipRarity.Uncommon: return toRareCost;
                default: return -1;
            }
        }

        public int DismantleShards(EquipRarity rarity) => dismantleShards[(int)rarity];
    }
}
