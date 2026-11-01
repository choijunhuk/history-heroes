using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.Services.CloudCode;
using UnityEngine;

namespace HistoryHeroes.Economy
{
    /// <summary>
    /// 서버 결제 검증 모듈 부르기 (UGS Cloud Code C# 모듈 HistoryHeroesServer — 코드는 Server/CloudCode, Monetization 7-1).
    /// 서버가 구글에 구매를 묻고, 서버 장부(Cloud Save protected)로 지급 수량 · 계정당 1번 · 첫 구매 2배를 정한다. 게임은 받은 수량을 그대로 넣는다.
    /// 로그인한 UGS 플레이어가 있어야 부를 수 있다 (AccountService.PlayerId). 네트워크 · 서버 오류는 예외로 올라온다 → 부르는 쪽이 "다시 묻기"로 다룬다.
    /// 답은 JsonUtility로 읽는다 — 칸 이름이 서버 PurchaseRecord · VerifyResult · PurchaseList의 JSON 이름과 글자까지 같아야 한다.
    /// </summary>
    public static class PurchaseServer
    {
        public const string Module = "HistoryHeroesServer";

        // VerifyPurchase 답의 status (서버 VerifyResult와 같다)
        public const string Granted = "granted";
        public const string Already = "already";
        public const string AlreadyClaimed = "already_claimed";
        public const string Invalid = "invalid";
        /// <summary>지급했던 결제가 환불 · 취소됐다 → 지급하지 않고, 이 기기에 있으면 회수</summary>
        public const string Voided = "voided";
        public const string NotConfigured = "not_configured";
        /// <summary>이 계정의 서버 장부 칸이 거의 찼다 → 지급 · 확정 안 함 (구글이 환불), 문의 안내</summary>
        public const string Limit = "limit";
        public const string Retry = "retry";
        /// <summary>GetPurchases의 status: 계정 삭제로 서버가 닫은 플레이어 (그 뒤 결제 함수는 모두 거절)</summary>
        public const string Closed = "closed";

        /// <summary>서버 장부의 결제 한 건 = 지급할 것</summary>
        [Serializable]
        public sealed class Purchase
        {
            /// <summary>SHA-256(구매 토큰). 서버는 토큰 원문을 갖지 않는다</summary>
            public string tokenHash;
            public string orderId;
            public string productId;
            public string utc;
            public int paidGems;
            public int freeGems;
            public int tickets;
            public int gold;
            public bool firstBonus;
            /// <summary>라이선스 테스터 결제 (지급은 같다)</summary>
            public bool test;
            /// <summary>구글 환불 · 취소 · 차지백 → 회수 (LiveOps 3-4)</summary>
            public bool voided;
        }

        [Serializable]
        public sealed class Verdict
        {
            public string status;
            public string reason;
            public Purchase purchase;

            /// <summary>JsonUtility는 null 칸도 빈 객체로 만든다 → 토큰 해시로 진짜 지급 내역인지 본다</summary>
            public bool HasPurchase => purchase != null && !string.IsNullOrEmpty(purchase.tokenHash) && !string.IsNullOrEmpty(purchase.productId);
        }

        [Serializable]
        public sealed class Ledger
        {
            /// <summary>"ok"가 아니면(서버 오류 · 시간 초과) 장부를 믿지 말고 나중에 다시</summary>
            public string status;
            /// <summary>서버에 구글 서비스 계정이 들어 있다 (아니면 기기에서는 팔지 않는다)</summary>
            public bool configured;
            public List<Purchase> purchases = new List<Purchase>();
        }

        public static async Task<Verdict> VerifyAsync(string productId, string purchaseToken)
        {
            string json = await CloudCodeService.Instance.CallModuleEndpointAsync(Module, "VerifyPurchase",
                new Dictionary<string, object> { ["productId"] = productId, ["purchaseToken"] = purchaseToken });
            return string.IsNullOrEmpty(json) ? null : JsonUtility.FromJson<Verdict>(json);
        }

        public static async Task<Ledger> GetPurchasesAsync()
        {
            string json = await CloudCodeService.Instance.CallModuleEndpointAsync(Module, "GetPurchases", new Dictionary<string, object>());
            return string.IsNullOrEmpty(json) ? null : JsonUtility.FromJson<Ledger>(json);
        }

        /// <summary>CloseForSwitch: 결제 · 예약이 없을 때만 게스트를 닫았다</summary>
        public const string HasPurchases = "has_purchases";

        /// <summary>
        /// 결제 안 한 게스트를 이미 있는 카카오 계정으로 바꾸기 전 (게스트로 부른다): 서버가 결제 · 예약이 하나도 없을 때만 게스트를 닫는다 (진행 중이던 결제와 겹쳐도 서버가 한 줄로 판정).
        /// "ok" 닫음(한 방향 — 다시 여는 길은 없다, 끊겼으면 다시 불러 보관 · 삭제를 마저 한다) · "has_purchases" 결제가 있어 바꾸지 않음(아무것도 쓰지 않았다) · 그 밖은 실패
        /// </summary>
        public static async Task<string> CloseForSwitchAsync() => await StatusOf("CloseForSwitch");

        private static async Task<string> StatusOf(string function)
        {
            string json = await CloudCodeService.Instance.CallModuleEndpointAsync(Module, function, new Dictionary<string, object>());
            return string.IsNullOrEmpty(json) ? null : JsonUtility.FromJson<StatusOnly>(json)?.status;
        }

        [Serializable]
        private sealed class StatusOnly
        {
            public string status;
        }

        /// <summary>
        /// 계정 삭제 직전 (AccountService.DeleteAsync): 서버 결제 장부를 보관본(되돌릴 수 없는 ID, 5년 뒤 지움)으로 옮기고 장부 · 색인을 지운다.
        /// 서버는 그 뒤 이 플레이어를 닫는다 (되돌릴 수 없음 — 결제 · 장부 받기 모두 거절). 그래서 계정을 지우는 길에서만 부른다 (설정의 계정 삭제, 결제 안 한 게스트를 이미 있는 카카오 계정으로 바꿀 때 지우는 게스트).
        /// 돌려주는 값: "ok"면 끝 (이미 닫혔어도 ok). 그 밖은 실패 — 계정 삭제를 멈추고 다시 하게 한다 (장부가 지운 계정 밑에 남지 않게)
        /// </summary>
        public static async Task<string> ArchiveAndDeleteAsync()
        {
            string json = await CloudCodeService.Instance.CallModuleEndpointAsync(Module, "ArchiveAndDeletePurchases", new Dictionary<string, object>());
            return string.IsNullOrEmpty(json) ? null : JsonUtility.FromJson<StatusOnly>(json)?.status;
        }
    }
}
