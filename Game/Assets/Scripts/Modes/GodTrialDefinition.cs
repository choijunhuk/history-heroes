namespace HistoryHeroes.Modes
{
    /// <summary>시련의 고정 순서와 보상. 영웅이 없는 시련은 조각을 주지 않는다.</summary>
    public sealed class GodTrialDefinition
    {
        public string Id { get; }
        public int Order { get; }
        public int ArenaChapter { get; }
        public string SourceBossId { get; }
        public string HeroId { get; }
        public string DisplayName { get; }
        public string Title { get; }
        public int FirstClearShards { get; }

        public GodTrialDefinition(string id, int order, int arenaChapter, string sourceBossId, string heroId,
            string displayName, string title, int firstClearShards)
        {
            Id = id;
            Order = order;
            ArenaChapter = arenaChapter;
            SourceBossId = sourceBossId;
            HeroId = heroId;
            DisplayName = displayName;
            Title = title;
            FirstClearShards = firstClearShards;
        }
    }
}
