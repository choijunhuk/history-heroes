namespace HistoryHeroes.Heroes
{
    /// <summary>실제 기본 공격 횟수만 센다. 추가 베기와 연쇄 피해는 이 상태를 건드리지 않는다.</summary>
    public sealed class ThorThunderState
    {
        public int Count { get; private set; }

        public bool RecordAttack(int every)
        {
            if (every <= 0) return false;
            Count++;
            if (Count < every) return false;
            Count = 0;
            return true;
        }

        public void Moved() => Count = 0;
    }
}
