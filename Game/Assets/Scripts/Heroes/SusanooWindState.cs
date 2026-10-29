namespace HistoryHeroes.Heroes
{
    /// <summary>실제로 나간 베기만 세어 이동 뒤의 첫 베기를 다시 넓게 만든다.</summary>
    public sealed class SusanooWindState
    {
        public readonly struct SlashShape
        {
            public readonly float Arc;
            public readonly float Coefficient;

            public SlashShape(float arc, float coefficient)
            {
                Arc = arc;
                Coefficient = coefficient;
            }
        }

        public int Count { get; private set; }

        public SlashShape RecordSlash(float firstArc, float firstCoefficient, float followArc, float followCoefficient)
        {
            Count++;
            return Count == 1 ? new SlashShape(firstArc, firstCoefficient) : new SlashShape(followArc, followCoefficient);
        }

        public void Moved() => Count = 0;
    }
}
