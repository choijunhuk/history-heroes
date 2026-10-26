namespace HistoryHeroes.Common
{
    /// <summary>
    /// 히트스톱(잠깐 멈춤)과 슬로모션 (조사 A9, UX 6장 손맛 규칙). 매 타격엔 하지 않는다 — 큰 순간에만.
    /// GamePause 안에서 돌아서 레벨업 창·결과 화면의 멈춤이 언제나 이긴다.
    /// </summary>
    public static class HitStop
    {
        private static readonly object FreezeKey = new object();
        private static readonly object SlowKey = new object();

        /// <summary>엘리트 처치 0.08초</summary>
        public const float EliteKill = 0.08f;
        /// <summary>보스 단계 전환 0.1초</summary>
        public const float BossPhase = 0.1f;
        /// <summary>보스 처치 0.2초 멈춤 + 0.5초 동안 0.3배</summary>
        public const float BossKill = 0.2f;
        public const float BossKillSlowSeconds = 0.5f;
        public const float BossKillSlowScale = 0.3f;

        public static void Freeze(float realSeconds) => GamePause.RequestSlow(FreezeKey, 0f, realSeconds);

        /// <summary>지금부터 realSeconds 동안 scale배. Freeze와 겹치면 더 느린 쪽(0)이 먼저 이긴다</summary>
        public static void Slow(float scale, float realSeconds) => GamePause.RequestSlow(SlowKey, scale, realSeconds);

        public static void OnBossKilled()
        {
            Freeze(BossKill);
            Slow(BossKillSlowScale, BossKill + BossKillSlowSeconds); // 멈춤이 끝난 뒤 0.5초가 남게
        }
    }
}
