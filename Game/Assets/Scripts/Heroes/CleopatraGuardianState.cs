namespace HistoryHeroes.Heroes
{
    /// <summary>기본 공격의 시작만 센다. 준비를 쓴 공격은 다음 준비의 첫 회로 세지 않는다.</summary>
    public sealed class CleopatraGuardianState
    {
        public int Count { get; private set; }
        public bool IsReady { get; private set; }

        public bool RecordAttack(bool stopped, bool primaryAttack, HeroData hero)
        {
            if (!stopped || hero == null || hero.guardianAttacks <= 0)
            {
                Reset();
                return false;
            }

            if (!primaryAttack) return false;
            if (IsReady)
            {
                Reset();
                return true;
            }

            Count++;
            IsReady = Count >= hero.guardianAttacks;
            return false;
        }

        public void Reset()
        {
            Count = 0;
            IsReady = false;
        }
    }
}
