using System;
using System.Text;
using HistoryHeroes.Records;
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
        /// <summary>이미 있는 카카오 계정으로 바꾸기가 끝나기 전에 앱이 꺼졌을 때 다음 실행이 할 일 (AccountService ① 게스트 정리 · ② 진행 고르기 표시)</summary>
        public enum SwitchStep
        {
            /// <summary>표시 없음</summary>
            Nothing,
            /// <summary>표시만 지운다 (가리키는 세션이 이미 없다 — 지운 게스트, 또는 그 계정에 들어가기 전에 꺼짐)</summary>
            Clear,
            /// <summary>① 그 게스트 세션 그대로: 닫기 · 보관 · 지우기를 마저 한다 (닫은 게스트는 다시 열지 않는다)</summary>
            FinishGuestCleanup,
            /// <summary>② 그 계정 세션 그대로: 진행 고르기를 다시 연다 (새 게스트를 만들지 않는다)</summary>
            ResumeTarget,
            /// <summary>건드리지 않고 기다린다 (로그인 전 · 그 계정에 들어간 뒤인데 그 계정 세션이 없음 — 새 게스트 · 다른 플레이어로 이어 가면 그 계정 진행이 샌다)</summary>
            Hold,
        }

        /// <summary>
        /// ① prepGuest: 정리하던 게스트 ID, current: 지금 로그인한 플레이어 (로그인 전이면 null),
        /// anonymous: 지금 플레이어에 연결된 외부 계정이 하나도 없나 (모르면 null). 지우는 것은 표시의 그 게스트이고 익명일 때만 —
        /// 연결된 계정이면 표시만 지우고 아무것도 지우지 않는다, 확인하지 못하면 기다린다
        /// </summary>
        public static SwitchStep DecideSwitchPrep(string prepGuest, string current, bool? anonymous)
        {
            if (prepGuest == null) return SwitchStep.Nothing;
            if (string.IsNullOrEmpty(current)) return SwitchStep.Hold;
            if (current != prepGuest) return SwitchStep.Clear;
            if (anonymous == null) return SwitchStep.Hold;
            return anonymous.Value ? SwitchStep.FinishGuestCleanup : SwitchStep.Clear;
        }

        /// <summary>UGS 플레이어 정보로 본 익명 게스트: 외부 계정(카카오 · 구글 …)도 사용자 이름도 없다</summary>
        public static bool IsAnonymous(int identityCount, string username) => identityCount == 0 && string.IsNullOrEmpty(username);

        /// <summary>
        /// ② target: 바꾸려는 계정 ID ("" = 그 계정 로그인 직전에 남긴 표시, null = 표시 없음), current: 이 기기 세션의 플레이어 (세션이 없으면 null).
        /// "" + 세션 있음 = 그 계정 (게스트는 이미 지워져 세션을 남기지 않는다)
        /// </summary>
        public static SwitchStep DecideSwitchTarget(string target, string current)
        {
            if (target == null) return SwitchStep.Nothing;
            if (string.IsNullOrEmpty(current)) return target.Length == 0 ? SwitchStep.Clear : SwitchStep.Hold;
            // 그 계정에 들어간 뒤인데 다른 플레이어 세션: 이 기기 저장에 그 계정 진행이 있을 수 있다 → 그 플레이어로 이어 가지 않는다
            return target.Length == 0 || current == target ? SwitchStep.ResumeTarget : SwitchStep.Hold;
        }

        /// <summary>
        /// 바꾸려는 계정에 결제 기록이 하나라도 있나 (있으면 그 계정 진행만 고를 수 있다): 클라우드 진행의 장부(회수된 것 포함) 또는 서버 장부.
        /// 서버 장부를 못 받았거나 "ok"가 아니면 모르는 것 → 있는 것으로 본다
        /// </summary>
        public static bool TargetHasPurchases(SaveData cloud, Economy.PurchaseServer.Ledger server)
        {
            if (cloud != null && HasPurchases(cloud)) return true;
            if (server == null || (server.status != null && server.status != "ok")) return true;
            return server.purchases != null && server.purchases.Count > 0;
        }

        /// <summary>계정 삭제가 끝나기 전에 앱이 꺼졌을 때 다음 실행이 할 일 (이 기기 진행은 저장을 읽기 전에 이미 지웠다 — SaveSystem.WipeOnNextLoad)</summary>
        public enum DeletionStep
        {
            /// <summary>삭제 표시 없음</summary>
            Nothing,
            /// <summary>그 계정이 아직 있다 (세션으로 들어왔다) → 서버 단계를 마저 (결제 보관 · 클라우드 지우기 · 계정 지우기)</summary>
            FinishRemote,
            /// <summary>서버 쪽은 끝났다 (세션이 지워짐 · 서버가 세션을 거절 = 지운 계정 · 다른 플레이어) → 표시만 지우고 새 게스트로</summary>
            Done,
        }

        /// <summary>intent: 지우던 플레이어 ID, sessionExists: 이 기기에 세션이 남았나, current: 그 세션으로 들어온 플레이어, sessionRejected: 서버가 세션을 거절했나</summary>
        public static DeletionStep DecideDeletionResume(string intent, bool sessionExists, string current, bool sessionRejected)
        {
            if (intent == null) return DeletionStep.Nothing;
            if (!sessionExists || sessionRejected) return DeletionStep.Done;
            return current == intent ? DeletionStep.FinishRemote : DeletionStep.Done;
        }

        /// <summary>이 기기 장부에 결제가 하나라도 있나 (있으면 이미 다른 진행이 있는 카카오 계정으로 바꾸지 않는다 — LiveOps 3-2)</summary>
        public static bool HasPurchases(SaveData d) => d?.purchases?.ledger != null && d.purchases.ledger.Count > 0;

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
