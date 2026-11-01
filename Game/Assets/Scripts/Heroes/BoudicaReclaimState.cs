using UnityEngine;

namespace HistoryHeroes.Heroes
{
    /// <summary>정지 준비와 재사용을 따로 센다. 표적이 없는 동안은 준비를 쓰지 않는다.</summary>
    public sealed class BoudicaReclaimState
    {
        private float _elapsed;
        private float _required;

        public bool IsReady { get; private set; }
        public float CooldownRemaining { get; private set; }
        public float Progress => IsReady ? 1f : _required > 0f ? Mathf.Clamp01(_elapsed / _required) : 0f;

        public void Tick(float deltaTime, bool stopped, HeroData hero)
        {
            if (hero == null || hero.reclaimStopSeconds <= 0f)
            {
                Reset();
                return;
            }

            float dt = Mathf.Max(0f, deltaTime);
            float cooling = Mathf.Min(dt, CooldownRemaining);
            CooldownRemaining -= cooling;
            dt -= cooling;
            _required = hero.reclaimStopSeconds;
            if (!stopped || CooldownRemaining > 0f)
            {
                _elapsed = 0f;
                IsReady = false;
                return;
            }

            if (IsReady) return;
            _elapsed = Mathf.Min(_required, _elapsed + dt);
            IsReady = _elapsed >= _required;
        }

        public bool TryConsume(HeroData hero)
        {
            if (!IsReady || hero == null || hero.reclaimStopSeconds <= 0f) return false;
            _elapsed = 0f;
            IsReady = false;
            CooldownRemaining = Mathf.Max(0f, hero.reclaimCooldown);
            return true;
        }

        public static float KnockbackDistance(HeroData hero, HeroSignature signature, bool isBoss)
        {
            if (hero == null || hero.reclaimStopSeconds <= 0f) return 0f;
            return Mathf.Max(0f, signature.ReclaimKnockback) * (isBoss ? Mathf.Clamp01(hero.reclaimBossScale) : 1f);
        }

        public void Reset()
        {
            _elapsed = 0f;
            _required = 0f;
            IsReady = false;
            CooldownRemaining = 0f;
        }
    }
}
