namespace HistoryHeroes.Skills
{
    /// <summary>판 안에서 가진 스킬 하나의 상태 (레벨 등). 판마다 새로 만든다.</summary>
    public sealed class SkillInstance
    {
        public SkillData Data { get; }
        public int Level { get; set; }

        /// <summary>액티브라면 플레이어에 붙은 동작. 패시브는 null.</summary>
        public ActiveSkillBehaviour Behaviour { get; set; }

        /// <summary>조합으로 만들어진 스킬인가 (진화·합체 결과)</summary>
        public bool IsFusionResult { get; set; }

        public bool IsMaxLevel => Level >= Data.maxLevel;

        public SkillInstance(SkillData data, int level = 1)
        {
            Data = data;
            Level = level;
        }
    }
}
