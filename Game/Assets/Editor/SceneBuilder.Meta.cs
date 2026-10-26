using HistoryHeroes.Equipment;
using HistoryHeroes.Meta;
using HistoryHeroes.Stats;
using UnityEditor;

namespace HistoryHeroes.EditorTools
{
    /// <summary>v0.4 판 밖 성장 데이터: 장비 21종 (Equipment 7장) + 장비 표 + 판 밖 성장 표</summary>
    public static partial class SceneBuilder
    {
        private static MetaTable CreateMetaData()
        {
            var table = AssetUtil.LoadOrCreate<EquipmentTable>($"{DataDir}/Equipment/EquipmentTable.asset");
            table.items.Clear();

            // 🗡️ 무기: 공격력 % (부가: 치명타 피해 / 쿨다운 감소 / 치명타 확률)
            table.items.Add(Equip("weapon_recruit_c", "수습병의 무기", EquipSlot.Weapon, EquipRarity.Common));
            table.items.Add(Equip("weapon_elite_u", "정예병의 무기", EquipSlot.Weapon, EquipRarity.Uncommon, StatType.CritDamage, 0.05f));
            table.items.Add(Equip("weapon_scout_u", "척후병의 무기", EquipSlot.Weapon, EquipRarity.Uncommon, StatType.CooldownReduction, 0.01f));
            table.items.Add(Equip("weapon_general_r", "장군의 무기", EquipSlot.Weapon, EquipRarity.Rare, StatType.CritDamage, 0.10f));
            table.items.Add(Equip("weapon_strategist_r", "책사의 무기", EquipSlot.Weapon, EquipRarity.Rare, StatType.CooldownReduction, 0.02f));
            table.items.Add(Equip("weapon_timeguard_l", "시간 수호자의 무기", EquipSlot.Weapon, EquipRarity.Legendary, StatType.CritRate, 0.03f));
            table.items.Add(Equip("weapon_conqueror_l", "정복자의 무기", EquipSlot.Weapon, EquipRarity.Legendary, StatType.CritDamage, 0.20f));

            // 🛡️ 갑옷: 최대체력 % (부가: 피해 감소 / 이동속도)
            table.items.Add(Equip("armor_recruit_c", "수습병의 갑옷", EquipSlot.Armor, EquipRarity.Common));
            table.items.Add(Equip("armor_elite_u", "정예병의 갑옷", EquipSlot.Armor, EquipRarity.Uncommon, StatType.DamageReduction, 0.01f));
            table.items.Add(Equip("armor_pilgrim_u", "순례자의 외투", EquipSlot.Armor, EquipRarity.Uncommon, StatType.MoveSpeed, 0.01f, ModifierKind.Percent));
            table.items.Add(Equip("armor_general_r", "장군의 갑옷", EquipSlot.Armor, EquipRarity.Rare, StatType.DamageReduction, 0.03f));
            table.items.Add(Equip("armor_explorer_r", "탐험가의 외투", EquipSlot.Armor, EquipRarity.Rare, StatType.MoveSpeed, 0.015f, ModifierKind.Percent));
            table.items.Add(Equip("armor_timeguard_l", "시간 수호자의 갑옷", EquipSlot.Armor, EquipRarity.Legendary, StatType.DamageReduction, 0.06f));
            table.items.Add(Equip("armor_unyielding_l", "불굴의 망토", EquipSlot.Armor, EquipRarity.Legendary, StatType.MoveSpeed, 0.02f, ModifierKind.Percent));

            // 💍 장신구: 공격속도 % (부가: 치명타 확률 / 이동속도 / 쿨다운 감소). "부적"은 행운 장식으로 그린다
            table.items.Add(Equip("acc_charm_c", "낡은 부적", EquipSlot.Accessory, EquipRarity.Common));
            table.items.Add(Equip("acc_feather_u", "매의 깃털 장식", EquipSlot.Accessory, EquipRarity.Uncommon, StatType.CritRate, 0.01f));
            table.items.Add(Equip("acc_compass_u", "여행자의 나침반", EquipSlot.Accessory, EquipRarity.Uncommon, StatType.MoveSpeed, 0.02f, ModifierKind.Percent));
            table.items.Add(Equip("acc_signet_r", "황금 인장 반지", EquipSlot.Accessory, EquipRarity.Rare, StatType.CritRate, 0.015f));
            table.items.Add(Equip("acc_watch_r", "학자의 회중시계", EquipSlot.Accessory, EquipRarity.Rare, StatType.CooldownReduction, 0.02f));
            table.items.Add(Equip("acc_hourglass_l", "시간의 모래시계", EquipSlot.Accessory, EquipRarity.Legendary, StatType.CritRate, 0.02f));
            table.items.Add(Equip("acc_stirrup_l", "바람의 등자", EquipSlot.Accessory, EquipRarity.Legendary, StatType.MoveSpeed, 0.03f, ModifierKind.Percent));
            EditorUtility.SetDirty(table);

            var meta = AssetUtil.LoadOrCreate<MetaTable>($"{DataDir}/Meta/MetaTable.asset");
            meta.equipment = table;
            EditorUtility.SetDirty(meta);

            // 상자 표 (Gacha 2장): 화면 확률과 추첨이 같이 읽는다. 숫자는 GachaTable 기본값
            var gacha = AssetUtil.LoadOrCreate<GachaTable>($"{DataDir}/Equipment/GachaTable.asset");
            gacha.equipment = table;
            EditorUtility.SetDirty(gacha);
            return meta;
        }

        private static EquipmentData Equip(string id, string name, EquipSlot slot, EquipRarity rarity,
            StatType? secondary = null, float value = 0f, ModifierKind kind = ModifierKind.Flat)
        {
            var e = AssetUtil.LoadOrCreate<EquipmentData>($"{DataDir}/Equipment/Equip_{id}.asset");
            e.id = id;
            e.displayName = name;
            e.slot = slot;
            e.rarity = rarity;
            e.hasSecondary = secondary.HasValue;
            e.secondaryStat = secondary ?? StatType.Attack;
            e.secondaryKind = kind;
            e.secondaryValue = value;
            EditorUtility.SetDirty(e);
            return e;
        }
    }
}
