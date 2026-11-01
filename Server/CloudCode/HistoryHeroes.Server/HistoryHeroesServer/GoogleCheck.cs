using Newtonsoft.Json;

namespace HistoryHeroes.Server;

/// <summary>구글에 구매를 물은 결과 (전송 · 인증까지만. 결제 상태 · 상품 · 계정 판단은 PurchaseVerifier)</summary>
public sealed class GoogleCheck
{
    public enum Kind
    {
        Ok,
        /// <summary>비밀값 GOOGLE_PLAY_SERVICE_ACCOUNT가 없거나 읽을 수 없다</summary>
        NotConfigured,
        /// <summary>구글이 이 토큰을 모른다 (가짜 · 다른 앱 · 다른 상품) — 다시 물어도 같다</summary>
        Invalid,
        /// <summary>네트워크 · 구글 오류 · 권한이 아직 퍼지지 않음 — 나중에 다시</summary>
        Retry,
    }

    public Kind Result { get; init; }
    public string? Reason { get; init; }
    public Purchase? Found { get; init; }

    public static GoogleCheck Of(Kind result, string? reason = null) => new() { Result = result, Reason = reason };

    /// <summary>purchases.products.get의 ProductPurchase 중 쓰는 칸 (https://developers.google.com/android-publisher/api-ref/rest/v3/purchases.products)</summary>
    public sealed class Purchase
    {
        /// <summary>0 결제됨 · 1 취소 · 2 대기 중. 칸이 없으면 결제된 것으로 보지 않는다</summary>
        [JsonProperty("purchaseState")] public int? PurchaseState { get; set; }
        /// <summary>0 아직 소비 안 함 · 1 소비됨 (게임이 확정했다)</summary>
        [JsonProperty("consumptionState")] public int? ConsumptionState { get; set; }
        /// <summary>0 아직 확정 안 함 · 1 확정됨</summary>
        [JsonProperty("acknowledgementState")] public int? AcknowledgementState { get; set; }
        /// <summary>0 테스트(라이선스 테스터) · 1 프로모션 코드 · 2 보상형. 보통 결제는 칸이 없다</summary>
        [JsonProperty("purchaseType")] public int? PurchaseType { get; set; }
        [JsonProperty("orderId")] public string? OrderId { get; set; }
        /// <summary>없을 수도 있다 (그때는 주소에 넣은 상품으로 구글이 이미 맞춰 봤다)</summary>
        [JsonProperty("productId")] public string? ProductId { get; set; }
        /// <summary>없으면 1</summary>
        [JsonProperty("quantity")] public int? Quantity { get; set; }
        /// <summary>결제할 때 게임이 넣은 계정 표시 = SHA-256(UGS 플레이어 ID)</summary>
        [JsonProperty("obfuscatedExternalAccountId")] public string? ObfuscatedAccountId { get; set; }

        /// <summary>구글 쪽에서 이미 확정 · 소비가 끝났다 (서버가 따로 확정할 필요 없음)</summary>
        public bool Settled => AcknowledgementState == 1 || ConsumptionState == 1;
    }
}
