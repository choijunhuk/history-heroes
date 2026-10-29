using System;

namespace HistoryHeroes.Skills
{
    /// <summary>지연 효과가 방이나 영웅이 바뀐 뒤 실행되지 않도록 발동 당시의 세대를 보관한다.</summary>
    public sealed class SkillPulseSequence
    {
        private int _generation;
        private float _next;
        private float _interval;
        public int Remaining { get; private set; }

        public void Start(int count, float interval, float now, int generation)
        {
            Remaining = Math.Max(0, count);
            _interval = Math.Max(0f, interval);
            _next = now;
            _generation = generation;
        }

        public bool TryPulse(float now, int generation, bool alive)
        {
            if (!alive || generation != _generation) Cancel();
            if (Remaining <= 0 || now < _next) return false;
            Remaining--;
            _next += _interval;
            return true;
        }

        public void Cancel() => Remaining = 0;
    }
}
