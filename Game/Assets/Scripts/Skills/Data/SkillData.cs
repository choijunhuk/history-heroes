using System;
using UnityEngine;

namespace HistoryHeroes.Skills
{
    public enum SkillKind
    {
        Active,
        Passive,
        Fusion,
    }

    public enum Rarity
    {
        Common,
        Rare,
        Legendary,
    }

    /// <summary>스킬 태그 (GameDesign 6-3). 룬·시너지·영웅 전용 판정에 쓴다.</summary>
    [Flags]
    public enum SkillTag
    {
        None = 0,
        Stat = 1 << 0,
        Survival = 1 << 1,
        Projectile = 1 << 2,
        Melee = 1 << 3,
        Fire = 1 << 4,
        Lightning = 1 << 5,
        Ice = 1 << 6,
        Poison = 1 << 7,
        Wind = 1 << 8,
        ActiveBoost = 1 << 9,
        Defense = 1 << 10,
        Growth = 1 << 11,
    }

    /// <summary>
    /// 스킬 정의. **변하지 않는 값만** 둔다 (GameDesign 12-2 "정의 vs 상태").
    /// 레벨·쿨다운 같은 판별 상태는 SkillInstance에 둔다. 플레이 중에 이 에셋을 바꾸면 원본이 바뀌어 버린다.
    /// </summary>
    public abstract class SkillData : ScriptableObject
    {
        [Tooltip("저장용 ID. 한 번 정하면 바꾸지 않는다 (DevRules 5장)")]
        public string id;
        public string displayName;
        [TextArea] public string description;
        public Sprite icon;
        public Rarity rarity;
        public SkillTag tags;
        [Min(1)] public int maxLevel = 5;

        [Header("기록 (GameDesign 7-2)")]
        [Tooltip("Lv1일 때 저장 비용")]
        public int saveCostFirstLevel = 1;
        [Tooltip("Lv2부터 레벨마다 더해지는 비용")]
        public int saveCostPerLevel = 1;
        public bool recordable = true;

        [Header("영웅 전용")]
        [Tooltip("비어 있으면 모든 영웅. 영웅 ID로 지정한다 (태그로 정하지 않음)")]
        public string heroOnlyId;

        public abstract SkillKind Kind { get; }

        /// <summary>이 레벨로 기록에 저장할 때 드는 용량. 예: 패시브 일반 Lv3 = 1 + 1×2 = 3, 뇌화 폭풍 Lv2 = 15 + 3 = 18</summary>
        public int SaveCostAt(int level) => saveCostFirstLevel + saveCostPerLevel * Mathf.Max(0, level - 1);

        public bool IsAvailableFor(string heroId)
        {
            return string.IsNullOrEmpty(heroOnlyId) || heroOnlyId == heroId;
        }
    }
}
