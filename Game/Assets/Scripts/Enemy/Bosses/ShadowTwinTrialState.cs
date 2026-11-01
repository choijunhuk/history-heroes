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

        /// <summary>
        /// 실제로 깎을 피해: 형상 몫까지. 다른 형상이 쓰러졌고 이 피해가 몫을 넘으면 본체 남은 체력 전부
        /// (형상 몫 합과 본체 체력은 float 오차로 조금 다를 수 있다 — 마지막 한 방이 그 차이까지 지운다)
        /// </summary>
        public static float FinalDamage(float incoming, float remaining, bool otherDown, float bodyCurrent)
        {
            if (incoming <= 0f || remaining <= 0f) return 0f;
            if (otherDown && incoming >= remaining) return bodyCurrent;
            return Math.Min(incoming, remaining);
        }

        public float Apply(bool second, float dealt)
        {
            float actual = Math.Min(Math.Max(0f, dealt), Remaining(second));
            if (second) _second -= actual;
            else _first -= actual;
            return actual;
        }
    }
}
