namespace HistoryHeroes.Skills
{
    public enum CardType
    {
        NewSkill,
        LevelUp,
        Fusion,
        Fallback, // 기본 카드 (체력 회복 등). 중복해서 나와도 된다.
    }

    /// <summary>레벨업 화면에 뜨는 카드 한 장.</summary>
    public sealed class SkillCard
    {
        public CardType Type;
        public SkillData Skill;      // NewSkill, LevelUp
        public FusionRecipe Recipe;  // Fusion
        public int NextLevel;        // NewSkill이면 1

        public static SkillCard Fallback() => new SkillCard { Type = CardType.Fallback };
    }
}
