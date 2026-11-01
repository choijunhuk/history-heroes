using UnityEngine;

namespace HistoryHeroes.Heroes
{
    /// <summary>깃발의 설치·수명·영역 판정. 장면이나 물리 없이 같은 규칙을 검증한다.</summary>
    public sealed class JeanneBannerState
    {
        private float _deployTime;

        public bool IsPlanted { get; private set; }
        public Vector2 Center { get; private set; }
        public float TimeLeft { get; private set; }

        /// <returns>깃발 안에 멈춰 있어 보정을 받을 수 있으면 true.</returns>
        public bool Tick(float deltaTime, bool stopped, Vector2 position, HeroData hero, HeroSignature signature)
        {
            if (hero == null || hero.bannerDeploySeconds <= 0f || hero.bannerRadius <= 0f || signature.BannerDuration <= 0f)
            {
                Reset();
                return false;
            }

            float dt = Mathf.Max(0f, deltaTime);
            if (IsPlanted)
            {
                TimeLeft = Mathf.Max(0f, TimeLeft - dt);
                if (TimeLeft <= 0f)
                {
                    Reset();
                    return false;
                }

                return stopped && (position - Center).sqrMagnitude <= hero.bannerRadius * hero.bannerRadius;
            }

            if (!stopped)
            {
                _deployTime = 0f;
                return false;
            }

            _deployTime += dt;
            if (_deployTime < hero.bannerDeploySeconds) return false;
            _deployTime = 0f;
            Center = position;
            TimeLeft = signature.BannerDuration;
            IsPlanted = true;
            return true;
        }

        public void Reset()
        {
            _deployTime = 0f;
            IsPlanted = false;
            Center = Vector2.zero;
            TimeLeft = 0f;
        }
    }
}
