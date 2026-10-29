using System;

namespace HistoryHeroes.Heroes
{
    /// <summary>실제로 시작한 정지 기본 공격만 세고, 방을 옮겨도 재사용 시각은 유지한다.</summary>
    public sealed class BuddhaBreathState
    {
        private float _cooldownUntil;

        public int Count { get; private set; }
        public float CooldownRemaining(float now) => Math.Max(0f, _cooldownUntil - now);

        public bool RecordAttack(bool stopped, int requiredAttacks, float now, float cooldown)
        {
            if (!stopped || requiredAttacks <= 0 || now < _cooldownUntil) return false;
            if (++Count < requiredAttacks) return false;
            Count = 0;
            _cooldownUntil = now + Math.Max(0f, cooldown);
            return true;
        }

        public void ResetPreparation() => Count = 0;
    }
}
