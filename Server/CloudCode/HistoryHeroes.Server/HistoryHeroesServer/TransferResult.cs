using Newtonsoft.Json;

namespace HistoryHeroes.Server;

/// <summary>PrepareTransfer · ClaimTransfer의 답. status: ok · none(옮길 결제 없음) · invalid(+reason) · retry</summary>
public sealed class TransferResult
{
    public const string None = "none";

    [JsonProperty("status")] public string Status { get; set; } = PurchaseList.Ok;
    [JsonProperty("reason", NullValueHandling = NullValueHandling.Ignore)] public string? Reason { get; set; }
    /// <summary>PrepareTransfer: 새 계정이 가져갈 이전권 (게임은 기기에만 잠깐 둔다)</summary>
    [JsonProperty("ticket", NullValueHandling = NullValueHandling.Ignore)] public string? Ticket { get; set; }
    /// <summary>ClaimTransfer: 이번에 옮긴 결제 수 (다시 불러 이미 옮겼으면 0)</summary>
    [JsonProperty("moved")] public int Moved { get; set; }

    public static TransferResult Of(string status, string? reason = null) => new() { Status = status, Reason = reason };
}
