using System;
using System.Collections.Generic;
using HistoryHeroes.Player;
using HistoryHeroes.Stats;
using UnityEngine;

namespace HistoryHeroes.Skills
{
    /// <summary>패시브: 능력치와 기본 공격 모양을 강화한다. (Skills.md 1장)</summary>
    [CreateAssetMenu(menuName = "History Heroes/Skill/Passive", fileName = "Passive_")]
    public sealed class PassiveSkillData : SkillData
    {
        [Serializable]
        public struct StatEntry
        {
            public StatType stat;
            public ModifierKind kind;
            public float firstLevel;
            public float perLevel;
            [Tooltip("공속·흡혈·멀티샷은 BasicAttack (멈춤의 가치, GameDesign 3-3)")]
            public StatTarget target;

            public float ValueAt(int level) => firstLevel + perLevel * (level - 1);
        }

        [Serializable]
        public struct AttackEntry
        {
            public AttackModifierKind kind;
            public int firstLevel;
            public int perLevel;

            public int ValueAt(int level) => firstLevel + perLevel * (level - 1);
        }

        public List<StatEntry> stats = new List<StatEntry>();
        public List<AttackEntry> attack = new List<AttackEntry>();

        public override SkillKind Kind => SkillKind.Passive;

        /// <summary>
        /// 이 패시브의 효과를 다시 건다. 먼저 owner가 붙인 걸 지우고 **현재 레벨 기준으로** 다시 붙인다.
        /// 그래서 두 번 불러도 중복되지 않는다 (GameDesign 12-2 Apply 계약).
        /// </summary>
        public void Apply(PlayerController player, object owner, int level)
        {
            // 능력치는 한 번에 교체해야 최대체력 회복이 늘어난 만큼만 일어난다
            var modifiers = new List<StatModifier>(stats.Count);
            foreach (StatEntry e in stats)
            {
                modifiers.Add(new StatModifier(e.stat, e.kind, e.ValueAt(level), ModifierSource.Skill, owner, e.target));
            }

            player.Stats.ReplaceFrom(owner, modifiers);
            player.Attack.RemoveAllFrom(owner);

            foreach (AttackEntry e in attack)
            {
                player.Attack.Set(owner, e.kind, e.ValueAt(level));
            }
        }
    }
}
