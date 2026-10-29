using HistoryHeroes.Stats;
using UnityEngine;

namespace HistoryHeroes.Equipment
{
    /// <summary>장비 칸 (Equipment 3-1). 순서가 저장 칸 번호다 — 바꾸지 않는다</summary>
    public enum EquipSlot
    {
        Weapon = 0,    // 🗡️ 무기: 공격력 %
        Armor = 1,     // 🛡️ 갑옷: 최대체력 %
        Necklace = 2, // 목걸이: 옛 장신구 번호·공격속도를 잇는다
        Gloves = 3,   // 장갑: 공격력 %
        Belt = 4,     // 허리띠: 최대체력 %
        Boots = 5,    // 신발: 최대체력 %
    }

    /// <summary>장비 등급 (판 안 스킬 등급과 따로). 일반 ⚪ · 고급 🟢 · 희귀 🔵 · 전설 🟡</summary>
    public enum EquipRarity
    {
        Common = 0,
        Uncommon = 1,
        Rare = 2,
        Legendary = 3,
    }

    /// <summary>
    /// 장비 하나의 정의 (Equipment 7장, 출시 42종). 주 능력치는 등급과 슬롯 몫이 정한다 (EquipmentTable),
    /// 같은 등급 두 장비는 **고정 부가 능력치**만 다르다. 무작위 옵션·고유 효과는 없다.
    /// </summary>
    [CreateAssetMenu(menuName = "History Heroes/Equipment", fileName = "Equip_")]
    public sealed class EquipmentData : ScriptableObject
    {
        [Tooltip("저장용 ID. 한 번 정하면 바꾸지 않는다")]
        public string id;
        public string displayName;
        public EquipSlot slot;
        public EquipRarity rarity;

        [Header("고정 부가 능력치 (강화로 오르지 않음)")]
        public bool hasSecondary;
        public StatType secondaryStat;
        public ModifierKind secondaryKind = ModifierKind.Flat;
        [Tooltip("0.05 = 5%")]
        public float secondaryValue;

        /// <summary>"치명타 피해 +5%" (부가 능력치가 없으면 빈 문자열)</summary>
        public string SecondaryText => hasSecondary ? $"{EquipmentText.StatName(secondaryStat)} +{secondaryValue * 100f:0.###}%" : string.Empty;
    }

    /// <summary>장비 화면 글자 (능력치·등급 이름)</summary>
    public static class EquipmentText
    {
        public static string StatName(StatType stat)
        {
            switch (stat)
            {
                case StatType.Attack: return "공격력";
                case StatType.MaxHp: return "최대체력";
                case StatType.AttackSpeed: return "공격속도";
                case StatType.CritRate: return "치명타 확률";
                case StatType.CritDamage: return "치명타 피해";
                case StatType.CooldownReduction: return "쿨다운 감소";
                case StatType.MoveSpeed: return "이동속도";
                case StatType.DamageReduction: return "피해 감소";
                case StatType.HpRegen: return "체력 재생";
                case StatType.HpRegenFlat: return "초당 회복";
                case StatType.Range: return "사거리";
                case StatType.Dodge: return "회피";
                case StatType.Lifesteal: return "흡혈";
                case StatType.ActiveDamage: return "액티브 피해";
                case StatType.ExpGain: return "경험치";
                case StatType.GoldGain: return "골드";
                case StatType.DamageTaken: return "받는 피해";
                default: return stat.ToString();
            }
        }

        public static string RarityName(EquipRarity rarity)
        {
            switch (rarity)
            {
                case EquipRarity.Uncommon: return "고급";
                case EquipRarity.Rare: return "희귀";
                case EquipRarity.Legendary: return "전설";
                default: return "일반";
            }
        }

        public static string SlotName(EquipSlot slot)
        {
            switch (slot)
            {
                case EquipSlot.Armor: return "갑옷";
                case EquipSlot.Necklace: return "목걸이";
                case EquipSlot.Gloves: return "장갑";
                case EquipSlot.Belt: return "허리띠";
                case EquipSlot.Boots: return "신발";
                default: return "무기";
            }
        }

        /// <summary>등급 색 (TMP 리치 텍스트)</summary>
        public static string RarityColor(EquipRarity rarity)
        {
            switch (rarity)
            {
                case EquipRarity.Uncommon: return "#7CE38B";
                case EquipRarity.Rare: return "#6FA8FF";
                case EquipRarity.Legendary: return "#FFC94A";
                default: return "#D5DAE1";
            }
        }
    }
}
