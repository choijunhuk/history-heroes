using Newtonsoft.Json;

namespace HistoryHeroes.Server;

/// <summary>
/// CloseForSwitch의 답. status: ok(닫혔다 — 한 방향) · has_purchases(결제 · 예약이 있어 바꾸지 않음, 아무것도 쓰지 않았다) · retry
/// </summary>
public sealed class SwitchResult
{
    public const string HasPurchases = "has_purchases";

    [JsonProperty("status")] public string Status { get; set; } = PurchaseList.Ok;

    public static SwitchResult Of(string status) => new() { Status = status };
}
