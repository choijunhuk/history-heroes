using System;
using System.Globalization;
using HistoryHeroes.Records;

namespace HistoryHeroes.Account
{
    /// <summary>
    /// 진행 요약 한 줄 (LiveOps 3-2 고르기 화면): 구간 · 고른 영웅과 레벨 · 보석 · 저장 시각.
    /// 클라우드의 "meta" 키에도 이 모양 그대로 올린다 (대시보드 · 문의 때 저장 전체를 열지 않고 본다).
    /// </summary>
    [Serializable]
    public sealed class AccountSummary
    {
        public int chapter;
        public string heroId;
        public int heroLevel;
        public int gems;
        public int accountLevel;
        public long savedAtUnixSeconds;
        /// <summary>사람이 읽는 저장 시각 (UTC, ISO 8601)</summary>
        public string savedAtUtc;
        public int schemaVersion;
        /// <summary>올릴 때마다 새로 만드는 번호. 기기는 마지막으로 맞춘 번호를 기억해, 다른 기기가 올렸는지 안다</summary>
        public string uploadId;
        /// <summary>문의 · 삭제 요청을 서버 데이터와 맞추려고 (게임 안 사용자 ID · UGS 플레이어 ID)</summary>
        public string userId;
        public string playerId;

        public static AccountSummary From(SaveData d)
        {
            int level = 1;
            foreach (HeroProgress h in d.heroProgress)
            {
                if (h != null && h.heroId == d.selectedHero) level = h.level;
            }

            return new AccountSummary
            {
                chapter = d.unlockedChapter,
                heroId = d.selectedHero,
                heroLevel = level,
                gems = Economy.Wallet.Gems(d),
                accountLevel = d.accountLevel,
                savedAtUnixSeconds = d.savedAtUnixSeconds,
                savedAtUtc = DateTimeOffset.FromUnixTimeSeconds(Math.Max(0, d.savedAtUnixSeconds)).UtcDateTime
                    .ToString("o", CultureInfo.InvariantCulture),
                schemaVersion = d.schemaVersion,
                userId = d.userId,
            };
        }

        /// <summary>"3구간 · 칭기즈 칸 Lv14 · 보석 820 · 11-02 21:10" (시각은 이 기기 시간대)</summary>
        public string Text(Func<string, string> heroName)
        {
            string name = heroName?.Invoke(heroId);
            if (string.IsNullOrEmpty(name)) name = heroId;
            string when = savedAtUnixSeconds > 0
                ? DateTimeOffset.FromUnixTimeSeconds(savedAtUnixSeconds).ToLocalTime().ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture)
                : "저장 시각 모름";
            return $"{chapter}구간 · {name} Lv{heroLevel} · 보석 {Economy.Wallet.Format(gems)} · {when}";
        }

        /// <summary>진행도가 높은 쪽을 추천한다: 열린 구간 → 계정 레벨 → 나중에 저장한 쪽. 같으면 이 기기</summary>
        public static bool CloudRecommended(AccountSummary device, AccountSummary cloud)
        {
            if (cloud.chapter != device.chapter) return cloud.chapter > device.chapter;
            if (cloud.accountLevel != device.accountLevel) return cloud.accountLevel > device.accountLevel;
            return cloud.savedAtUnixSeconds > device.savedAtUnixSeconds;
        }
    }
}
