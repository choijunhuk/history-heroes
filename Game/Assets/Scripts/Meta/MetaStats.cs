using System.Collections.Generic;
using HistoryHeroes.Equipment;
using HistoryHeroes.Records;
using HistoryHeroes.Stats;
using UnityEngine;

namespace HistoryHeroes.Meta
{
    /// <summary>
    /// 판 밖 성장 → 능력치 보정값 (v0.4 A1). "무엇을 가졌는지"(MetaSnapshot: 장비 ID·강화값, 영웅 레벨·별, 수련 레벨)를 받아
    /// "얼마나 센지"는 언제나 최신 표(MetaTable)로 계산한다 (GameDesign 7-5).
    /// - 던전: 지금 계정으로 스냅샷을 만들어 쓴다
    /// - 보스: 기록에 고정 저장된 스냅샷으로 싸운다 (현재 장비를 덧붙이지 않는다, 7-3)
    /// 공격력·체력 %는 판 안 패시브 %와 같은 칸에서 더해진다 (Stats 2장 "같은 종류끼리 합").
    /// </summary>
    public static class MetaStats
    {
        /// <summary>지금 계정 상태로 그 영웅의 스냅샷</summary>
        public static MetaSnapshot FromAccount(SaveData save, MetaTable table, string heroId)
        {
            var snap = new MetaSnapshot();
            if (save == null || table == null || string.IsNullOrEmpty(heroId)) return snap;

            HeroProgress hero = save.Hero(heroId);
            snap.heroLevel = Mathf.Clamp(hero.level, 1, table.maxHeroLevel);
            snap.heroStars = Mathf.Clamp(hero.stars, 1, 5);
            if (table.equipment != null && EquipmentInventory.Unlocked(save)) snap.equipment = EquipmentInventory.Snapshot(save, table.equipment, heroId);
            snap.training = new List<int>(save.training);
            snap.relics = Relics.RelicService.Active(save);
            return snap;
        }

        /// <summary>스냅샷 → 보정값 목록 (owner로 한 번에 갈아 끼운다)</summary>
        public static List<StatModifier> Build(MetaSnapshot snap, MetaTable table, object owner)
        {
            var list = new List<StatModifier>();
            if (snap == null || table == null) return list;

            // 영웅 레벨: 공격력·체력 +1%/Lv
            float level = table.HeroLevelBonus(snap.heroLevel);
            if (level > 0f)
            {
                list.Add(new StatModifier(StatType.Attack, ModifierKind.Percent, level, ModifierSource.HeroLevel, owner));
                list.Add(new StatModifier(StatType.MaxHp, ModifierKind.Percent, level, ModifierSource.HeroLevel, owner));
            }

            // 각성: 누적 공격력·체력 % (★3·★5 대표 효과 강화는 PlayerController가 별로 따로)
            float awaken = table.AwakenBonus(snap.heroStars);
            if (awaken > 0f)
            {
                list.Add(new StatModifier(StatType.Attack, ModifierKind.Percent, awaken, ModifierSource.Awakening, owner));
                list.Add(new StatModifier(StatType.MaxHp, ModifierKind.Percent, awaken, ModifierSource.Awakening, owner));
            }

            // 장비: 주 능력치 % + 고정 부가 능력치
            if (table.equipment != null && snap.equipment != null)
            {
                foreach (EquippedItem e in snap.equipment)
                {
                    EquipmentData data = table.equipment.Find(e.dataId);
                    if (data == null) continue; // 없어진 장비는 빼고 싸운다
                    float main = table.equipment.MainPercent(data, e.level);
                    list.Add(new StatModifier(EquipmentTable.MainStat(data.slot), ModifierKind.Percent, main, ModifierSource.Equipment, owner));
                    if (data.hasSecondary && data.secondaryValue != 0f)
                    {
                        list.Add(new StatModifier(data.secondaryStat, data.secondaryKind, data.secondaryValue, ModifierSource.Equipment, owner));
                    }
                }
            }

            // 수련
            if (snap.training != null)
            {
                for (int i = 0; i < snap.training.Count && i < table.training.Length; i++)
                {
                    TrainingRow row = table.training[i];
                    int lv = Mathf.Clamp(snap.training[i], 0, row.maxLevel);
                    if (lv > 0) list.Add(new StatModifier(row.stat, row.kind, lv * row.perLevel, ModifierSource.Training, owner));
                }
            }

            // 유물 컬렉션: ★ 합 × 0.1% + 완성 세트 × 0.8% (공격력·체력), 세트마다 피해 감소 0.8%
            if (table.relics != null && snap.relics != null && snap.relics.Count > 0)
            {
                Relics.RelicService.Bonus relic = Relics.RelicService.Calculate(snap.relics, table.relics);
                if (relic.AttackHp > 0f)
                {
                    list.Add(new StatModifier(StatType.Attack, ModifierKind.Percent, relic.AttackHp, ModifierSource.Relic, owner));
                    list.Add(new StatModifier(StatType.MaxHp, ModifierKind.Percent, relic.AttackHp, ModifierSource.Relic, owner));
                }

                if (relic.DamageReduction > 0f) list.Add(new StatModifier(StatType.DamageReduction, ModifierKind.Flat, relic.DamageReduction, ModifierSource.Relic, owner));
            }

            return list;
        }

        /// <summary>한 능력치의 보정값 합 (화면 표시·예산 테스트용). kind가 다르면 따로 센다</summary>
        public static float Sum(IReadOnlyList<StatModifier> mods, StatType stat, ModifierKind kind)
        {
            float sum = 0f;
            foreach (StatModifier m in mods)
            {
                if (m.Stat == stat && m.Kind == kind) sum += m.Value;
            }

            return sum;
        }
    }
}
