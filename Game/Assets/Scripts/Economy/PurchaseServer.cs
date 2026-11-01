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
        public const string NotConfigured = "not_configured";
        public const string Retry = "retry";

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
    }
}
