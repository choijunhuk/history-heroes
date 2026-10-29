using UnityEngine;

namespace HistoryHeroes.Heroes
{
    /// <summary>연속 정지 시간을 기본 공격 한 번의 관통 준비로 바꾼다. 표적 탐색은 호출자가 맡는다.</summary>
    public sealed class SeondeokObservationState
    {
        private float _elapsed;
        private float _required;

        public bool IsReady { get; private set; }
        public float Progress => IsReady ? 1f : _required > 0f ? Mathf.Clamp01(_elapsed / _required) : 0f;

        public void Tick(float deltaTime, bool stopped, HeroData hero)
        {
            if (hero == null || hero.observationSeconds <= 0f || !stopped)
            {
                Reset();
                return;
            }

            _required = hero.observationSeconds;
            if (IsReady) return;
            _elapsed = Mathf.Min(_required, _elapsed + Mathf.Max(0f, deltaTime));
            IsReady = _elapsed >= _required;
        }

        public bool TryConsume()
        {
            if (!IsReady) return false;
            _elapsed = 0f;
            IsReady = false;
            return true;
        }

        public void Reset()
        {
            _elapsed = 0f;
            _required = 0f;
            IsReady = false;
        }
    }
}
