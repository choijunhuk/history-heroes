namespace HistoryHeroes.Heroes
{
    /// <summary>발사 묶음만 세어 멀티샷이나 갈라진 화살이 다음 교차 사격을 앞당기지 않게 한다.</summary>
    public sealed class TomoeCrossfireState
    {
        public int Count { get; private set; }

        public bool RecordAttack(bool stopped, bool primary, HeroData hero)
        {
            if (!stopped || hero == null || hero.crossShotEvery <= 0)
            {
                Reset();
                return false;
            }
            if (!primary) return false;
            Count++;
            if (Count < hero.crossShotEvery) return false;
            Count = 0;
            return true;
        }

        public void Reset() => Count = 0;
    }
}
