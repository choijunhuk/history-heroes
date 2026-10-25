namespace HistoryHeroes.Stats
{
    /// <summary>능력치 보정값 하나. 값 타입이라 만들고 지울 때 쓰레기(GC)가 생기지 않는다.</summary>
    public readonly struct StatModifier
    {
        public readonly StatType Stat;
        public readonly ModifierKind Kind;
        public readonly float Value;
        public readonly ModifierSource Source;
        public readonly StatTarget Target;

        /// <summary>이 보정값을 붙인 주인(스킬 인스턴스, 장비 등). 주인 단위로 한 번에 지울 때 쓴다.</summary>
        public readonly object Owner;

        public StatModifier(StatType stat, ModifierKind kind, float value, ModifierSource source,
            object owner, StatTarget target = StatTarget.Both)
        {
            Stat = stat;
            Kind = kind;
            Value = value;
            Source = source;
            Owner = owner;
            Target = target;
        }

        /// <summary>
        /// 대상이 Both인 보정값은 어디에나 적용된다.
        /// 기본 공격 전용 보정값은 기본 공격을 계산할 때만 들어간다.
        /// </summary>
        public bool AppliesTo(StatTarget context)
        {
            return Target == StatTarget.Both || Target == context;
        }
    }
}
