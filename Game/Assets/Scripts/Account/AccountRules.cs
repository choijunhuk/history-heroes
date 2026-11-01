using System;
using System.Text;
using UnityEngine;

namespace HistoryHeroes.Account
{
    public enum AccountState
    {
        /// <summary>서버에 못 닿았다 (인터넷 없음 · 서버 문제). 게임은 이 기기 저장으로 그대로 된다</summary>
        Offline,
        Connecting,
        /// <summary>익명 로그인 (UGS). 진행은 이 기기에만 (LiveOps 3-1)</summary>
        Guest,
        /// <summary>카카오(나중에 구글) 계정이 붙어 있다 → 클라우드에도 올린다</summary>
        Linked,
        /// <summary>연결 · 삭제 · 진행 고르기 도중 (버튼을 막는다)</summary>
        Busy,
    }

    /// <summary>계정 판정과 화면 글 (네트워크 없이 테스트한다). 실제 서버 호출은 AccountService.</summary>
    public static class AccountRules
    {
        /// <summary>자동 클라우드 저장은 이보다 자주 하지 않는다 (앱이 뒤로 갈 때 · 연결 직후 · "지금 저장"은 예외)</summary>
        public const double UploadIntervalSeconds = 60;

        /// <summary>Cloud Save 한 사람 한 접근 등급 한도 (모든 키 합)</summary>
        public const int CloudLimitBytes = 5 * 1024 * 1024;

        /// <summary>
        /// 올릴 차례인가. 게스트는 이 기기에만 저장하므로 올리지 않는다 (LiveOps 3-1). 진행 고르기를 끝내지 않았으면
        /// 이 기기 진행으로 클라우드를 덮으면 안 된다. 바뀐 게 없으면 force여도 올리지 않는다 (서버 쓰기 아끼기).
        /// </summary>
        public static bool ShouldUpload(AccountState state, bool choicePending, bool changed, bool force, double now, double lastUpload)
        {
            if (state != AccountState.Linked || choicePending || !changed) return false;
            return force || now - lastUpload >= UploadIntervalSeconds;
        }

        /// <summary>
        /// 한도 안인가. 저장 글은 클라우드에 JSON 문자열로 들어가서 따옴표 · 줄바꿈마다 \ 가 붙는다 →
        /// 넉넉히 두 배로 잡는다 (지금 저장은 수십 KB라 걸릴 일이 없다).
        /// </summary>
        public static bool FitsCloud(string saveJson, string metaJson)
        {
            long bytes = Encoding.UTF8.GetByteCount(saveJson ?? string.Empty) + Encoding.UTF8.GetByteCount(metaJson ?? string.Empty);
            return bytes * 2 <= CloudLimitBytes;
        }

        /// <summary>
        /// 클라우드가 이 기기가 마지막으로 맞춘 뒤 바뀌었나 (다른 기기가 올렸거나, 이 기기가 아직 맞춘 적이 없다).
        /// 클라우드에 저장이 없으면 바뀐 게 아니다. 올린 번호가 없는 옛 meta는 알 수 없으니 바뀐 것으로 본다 (덮지 않는 쪽이 안전).
        /// </summary>
        public static bool CloudChangedElsewhere(bool cloudHasSave, string cloudUploadId, string lastSyncedUploadId)
        {
            if (!cloudHasSave) return false;
            return string.IsNullOrEmpty(cloudUploadId) || cloudUploadId != lastSyncedUploadId;
        }

        /// <summary>
        /// 이 기기 결제 장부를 믿어도 되나 (계정당 1번 상품 · 첫 구매 2배). 게스트는 늘 된다 (이 기기 장부가 전부).
        /// 연결된 계정은 이번 실행에서 클라우드와 맞췄고(checkedThisRun) 충돌 · 고르기 대기 · 저장 복구 확인이 없을 때만. 그 밖의 상태는 모르니 안 된다.
        /// </summary>
        public static bool SyncSettled(AccountState state, bool checkedThisRun, bool conflict, bool choicePending, bool recheckNeeded)
        {
            if (state == AccountState.Guest) return true;
            return state == AccountState.Linked && checkedThisRun && !conflict && !choicePending && !recheckNeeded;
        }

        [Serializable]
        private sealed class JwtClaims
        {
            public string sub;
        }

        /// <summary>id_token(JWT)의 sub (서명 검사는 UGS가 한다. 여기서는 어느 계정인지 비교만). 못 읽으면 null</summary>
        public static string JwtSubject(string jwt)
        {
            try
            {
                string[] parts = jwt.Split('.');
                string b64 = parts[1].Replace('-', '+').Replace('_', '/');
                b64 = b64.PadRight(b64.Length + (4 - b64.Length % 4) % 4, '=');
                return JsonUtility.FromJson<JwtClaims>(Encoding.UTF8.GetString(Convert.FromBase64String(b64)))?.sub;
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>
        /// 같은 외부 계정인가. UGS가 OIDC 외부 ID를 sub 그대로 두는지 확인하지 못해서 접두어가 붙은 모양("…|sub")도 같다고 본다.
        /// 둘 중 하나라도 모르면 다르다고 본다 (끊지 않는 쪽).
        /// </summary>
        public static bool SameExternalUser(string id, string linkedId)
        {
            if (string.IsNullOrEmpty(id) || string.IsNullOrEmpty(linkedId)) return false;
            return linkedId == id || linkedId.EndsWith("|" + id, StringComparison.Ordinal) || linkedId.EndsWith(":" + id, StringComparison.Ordinal);
        }

        /// <summary>설정 → 계정 첫 줄</summary>
        public static string StatusText(AccountState state, string linkedName)
        {
            switch (state)
            {
                case AccountState.Connecting: return "계정: 연결 중…";
                case AccountState.Guest: return "계정: 게스트 · 진행은 이 기기에만 저장돼요";
                case AccountState.Linked: return $"계정: {linkedName} 연결됨 · 클라우드에도 저장돼요";
                case AccountState.Busy: return "계정: 처리 중…";
                default: return "계정: 오프라인 · 진행은 이 기기에 저장돼요";
            }
        }

        // UGS 오류 번호 (Unity.Services.Core.CommonErrorCodes · Authentication.AuthenticationErrorCodes와 같은 값.
        // 저쪽은 const가 아니라 readonly라 switch에 못 쓰고, 테스트가 UGS 어셈블리 없이 돌게 숫자로 둔다)
        public const int TransportError = 1, Timeout = 2, ServiceUnavailable = 3, TooManyRequests = 50, InvalidToken = 51, TokenExpired = 52;
        public const int AccountAlreadyLinked = 10003, AccountLinkLimitExceeded = 10004, InvalidProvider = 10008, BannedUser = 10009, EnvironmentMismatch = 10010;

        /// <summary>
        /// 서버 오류 → 쉬운 말. 토큰 검사 실패(aud · iss · 서명 · 만료)는 원인을 짧게 붙인다 —
        /// 첫 폰 시험에서 "aud"가 보이면 UGS 대시보드의 공급자 설정(클라이언트 ID)을 확인한다.
        /// </summary>
        public static string ErrorText(int code, string message)
        {
            string tag = TokenProblem(message);
            if (tag != null) return $"로그인 정보를 확인하지 못했어요 (코드 {code} · {tag})";
            switch (code)
            {
                case TransportError:
                case Timeout:
                    return "인터넷 연결을 확인해 주세요";
                case ServiceUnavailable:
                case TooManyRequests:
                    return "서버가 바빠요. 잠시 뒤에 다시 해 주세요";
                case InvalidToken:
                case TokenExpired:
                    return $"로그인이 만료됐어요. 다시 해 주세요 (코드 {code})";
                case AccountLinkLimitExceeded:
                    return $"이 계정에는 더 연결할 수 없어요 (코드 {code})";
                case InvalidProvider:
                    return $"로그인 설정이 맞지 않아요 (코드 {code})";
                case BannedUser:
                    return "이용이 제한된 계정이에요. 문의해 주세요";
                case EnvironmentMismatch:
                    return $"서버 환경이 맞지 않아요 (코드 {code})";
                default:
                    return $"문제가 생겼어요. 잠시 뒤에 다시 해 주세요 (코드 {code})";
            }
        }

        /// <summary>UGS의 id_token 검사 오류 문구에서 원인 한 단어 (UGS "Common errors" 문서의 문구)</summary>
        private static string TokenProblem(string message)
        {
            if (string.IsNullOrEmpty(message)) return null;
            string m = message.ToLowerInvariant();
            if (m.Contains("audience")) return "aud";
            if (m.Contains("issuer")) return "iss";
            if (m.Contains("signature")) return "sig";
            if (m.Contains("expired") && m.Contains("token")) return "exp";
            if (m.Contains("not valid yet")) return "nbf";
            if (m.Contains("malformed")) return "jwt";
            return null;
        }

        /// <summary>폰 SDK 로그인 실패 → 쉬운 말 + 짧은 원인 ("KOE009" = 키 해시 미등록, "NoIdToken" = OpenID Connect 꺼짐)</summary>
        public static string LoginErrorText(string providerName, string code)
        {
            switch (code)
            {
                case "NotSupported": return $"{providerName} 로그인은 폰에서만 돼요";
                case "Timeout": return $"{providerName} 로그인이 너무 오래 걸려서 멈췄어요. 다시 해 주세요";
                case "OtherAccount": return $"이 게임 계정에 연결된 것과 다른 {providerName} 계정으로 로그인했어요";
                default: return $"{providerName} 로그인에 실패했어요 (코드 {(string.IsNullOrEmpty(code) ? "?" : code)})";
            }
        }
    }
}
