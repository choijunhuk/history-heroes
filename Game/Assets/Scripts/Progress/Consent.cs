using System;
using System.Globalization;
using HistoryHeroes.Records;

namespace HistoryHeroes.Progress
{
    /// <summary>
    /// 첫 실행 동의 (LiveOps 19장): 이용약관 · 개인정보 처리방침 · 만 14세 이상 확인.
    /// 약관 글(Docs/Legal)을 고쳐서 다시 동의받아야 하면 아래 버전 숫자를 올린다 — 동의한 버전이 낮은 저장은 다시 묻는다.
    /// 판정은 저장이나 화면을 바꾸지 않는다 (Accept만 기록을 쓴다).
    /// </summary>
    public static class Consent
    {
        public const int TermsVersion = 1;
        /// <summary>2 (2026-11-01): 계정(UGS) · 카카오 로그인 · 클라우드 저장 · 계정 삭제를 처리방침에 넣었다</summary>
        public const int PrivacyVersion = 2;

        /// <summary>동의 화면에 보이는 등급 줄. IARC 등급이 나오면 여기만 고친다.</summary>
        public const string RatingText = "이용 등급: 등급 분류 전 (테스트판)";

        /// <summary>동의가 필요한가: 기록이 없거나, 만 14세 확인이 없거나, 약관·개인정보 버전이 지금보다 낮을 때</summary>
        public static bool NeedsConsent(SaveData save)
        {
            ConsentRecord c = save?.consent;
            return c == null || !c.age14 || string.IsNullOrEmpty(c.agreedAtUtc)
                   || c.termsVersion < TermsVersion || c.privacyVersion < PrivacyVersion;
        }

        /// <summary>세 항목 모두 동의했을 때 지금 버전과 시각(UTC)을 적는다. 이미 동의가 유효해도 새로 적는다.</summary>
        public static void Accept(SaveData save, DateTime utcNow)
        {
            save.consent = new ConsentRecord
            {
                termsVersion = TermsVersion,
                privacyVersion = PrivacyVersion,
                agreedAtUtc = utcNow.ToUniversalTime().ToString("o", CultureInfo.InvariantCulture),
                age14 = true,
            };
        }

        /// <summary>정보 화면 한 줄. 동의 전이거나 시각을 못 읽으면 "약관 동의: 아직 안 했어요"</summary>
        public static string AgreedDateText(SaveData save)
        {
            string raw = save?.consent?.agreedAtUtc;
            if (!string.IsNullOrEmpty(raw)
                && DateTime.TryParse(raw, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out DateTime t))
                return "약관 동의: " + t.ToLocalTime().ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            return "약관 동의: 아직 안 했어요";
        }
    }
}
