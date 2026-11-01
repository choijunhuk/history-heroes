using Newtonsoft.Json;

namespace HistoryHeroes.Server;

/// <summary>VerifyPurchase의 답. status가 granted · already일 때만 purchase가 있다 (게임은 그 수량을 그대로 지급한다)</summary>
public sealed class VerifyResult
{
    public const string Granted = "granted";
    /// <summary>이 토큰은 이미 지급했다 (같은 결제를 다시 보냄) → 게임은 장부에 없으면 기록대로 넣고 확정</summary>
    public const string Already = "already";
    /// <summary>계정당 1번 상품을 또 결제했다 → 지급 · 확정 안 함 (구글이 3일 안에 환불)</summary>
    public const string AlreadyClaimed = "already_claimed";
    public const string Invalid = "invalid";
    /// <summary>이 토큰은 지급했지만 그 뒤 환불 · 취소됐다 → 다시 주지 않는다 (게임은 장부에 있으면 회수)</summary>
    public const string Voided = "voided";
    /// <summary>서버에 구글 서비스 계정 비밀값이 아직 없다 → 지급 · 확정 안 함</summary>
    public const string NotConfigured = "not_configured";
    /// <summary>구글 · 저장소가 잠시 안 됨 → 게임은 결제를 남겨 두고 나중에 다시 묻는다</summary>
    public const string Retry = "retry";

    [JsonProperty("status")] public string Status { get; set; } = Retry;
    /// <summary>invalid · retry의 짧은 이유 (기록 · 문의용, 화면에는 안 보인다)</summary>
    [JsonProperty("reason")] public string? Reason { get; set; }
    [JsonProperty("purchase")] public PurchaseRecord? Purchase { get; set; }

    public static VerifyResult Of(string status, string? reason = null, PurchaseRecord? purchase = null) =>
        new() { Status = status, Reason = reason, Purchase = purchase };
}
