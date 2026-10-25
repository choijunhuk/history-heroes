namespace HistoryHeroes.Stats
{
    /// <summary>능력치 종류. Docs/Design/Stats.md 1장 목록과 같다.</summary>
    public enum StatType
    {
        MaxHp,
        Attack,
        AttackSpeed,       // 기본 공격 횟수/초
        MoveSpeed,
        Range,
        CritRate,          // 0.05 = 5%
        CritDamage,        // 1.5 = 150%
        DamageReduction,   // 0.1 = 10%
        Dodge,
        Lifesteal,         // 기본 공격에만 (StatTarget.BasicAttack)
        HpRegen,           // 초당 최대체력 비율 (0.005 = 초당 0.5%, Skills.md 체력 재생)
        CooldownReduction, // 액티브에만
        ActiveDamage,      // 액티브 피해 증가
        ExpGain,           // 판 안 스킬(배움)만 줄 수 있다. 판 밖 출처는 0 (Stats.md 4-1)
        GoldGain,
    }

    /// <summary>
    /// Flat = 기본값에 더한다. Percent = 같은 능력치의 % 보너스끼리 더해서 한 번에 곱한다.
    /// 확률·비율 능력치(치명타 확률, 피해 감소 등)는 Flat으로 더한다.
    /// </summary>
    public enum ModifierKind
    {
        Flat,
        Percent,
    }

    /// <summary>보정값의 출처. 판이 끝나거나 기록을 복원할 때 출처 단위로 지운다.</summary>
    public enum ModifierSource
    {
        Hero,
        Skill,
        Equipment,
        Rune,
        Relic,
        Pet,
        Training,   // 수련
        Awakening,  // 영웅 각성
    }

    /// <summary>
    /// 적용 대상. 공격속도·흡혈·멀티샷처럼 기본 공격에만 붙는 보정값이 있어서
    /// "멈춰야 강해지는" 규칙을 지킨다. (GameDesign 3-3)
    /// </summary>
    public enum StatTarget
    {
        Both,
        BasicAttack,
        Active,
    }
}
