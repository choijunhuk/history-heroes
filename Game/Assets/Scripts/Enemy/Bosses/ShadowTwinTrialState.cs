using System;

namespace HistoryHeroes.Enemy
{
    /// <summary>스사노오 시련의 두 시험 형상은 각자 체력을 잃고, 둘 다 쓰러져야 끝난다.</summary>
    public sealed class ShadowTwinTrialState
    {
        private float _first;
        private float _second;

        public float First => _first;
        public float Second => _second;
        public float Total => _first + _second;
        public bool FirstAlive => _first > 0f;
        public bool SecondAlive => _second > 0f;
        public bool Complete => !FirstAlive && !SecondAlive;

        public void Reset(float totalHp)
        {
            _first = Math.Max(0f, totalHp) * 0.5f;
            _second = Math.Max(0f, totalHp) - _first;
        }

        public float Remaining(bool second) => second ? _second : _first;

        public float Apply(bool second, float dealt)
        {
            float actual = Math.Min(Math.Max(0f, dealt), Remaining(second));
            if (second) _second -= actual;
            else _first -= actual;
            return actual;
        }
    }
}
