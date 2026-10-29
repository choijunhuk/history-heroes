namespace HistoryHeroes.Heroes
{
    /// <summary>정지 준비와 성공 뒤 재사용 시간을 나눠 이동 반복으로 차단을 늘리지 않는다.</summary>
    public sealed class AthenaShieldState
    {
        private float _progress;
        private float _cooldownUntil;
        private bool _ready;

        public float Progress => _progress;
        public bool IsReady => _ready;
        public float CooldownRemaining(float now) => System.Math.Max(0f, _cooldownUntil - now);

        public void Tick(float delta, bool stopped, float now, float prepareSeconds)
        {
            if (!stopped || prepareSeconds <= 0f)
            {
                CancelPreparation();
                return;
            }

            if (now < _cooldownUntil || _ready) return;
            _progress = System.Math.Min(prepareSeconds, _progress + System.Math.Max(0f, delta));
            _ready = _progress >= prepareSeconds;
        }

        public bool TryBlock(float now, float reuseSeconds)
        {
            if (!_ready || now < _cooldownUntil) return false;
            CancelPreparation();
            _cooldownUntil = now + System.Math.Max(0f, reuseSeconds);
            return true;
        }

        public void CancelPreparation()
        {
            _progress = 0f;
            _ready = false;
        }
    }
}
