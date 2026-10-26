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
        /// <summary>룬 덕분에 같은 등급 안에서 더 잘 뽑힌 카드 (카드에 작은 룬 표시, Runes 8장)</summary>
        public bool RuneBoosted;

        public static SkillCard Fallback() => new SkillCard { Type = CardType.Fallback };
    }
}
