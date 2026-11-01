using Newtonsoft.Json;

namespace HistoryHeroes.Server;

/// <summary>
/// 게스트 → 이미 있는 카카오 계정으로 바꿀 때의 결제 이전권 (Cloud Save private custom iap_transfer/{SHA-256("transfer:"+이전권)} — 서버만).
/// 게스트가 PrepareTransfer로 만들고(그때 게스트를 닫고 결제 목록을 고정), 새 계정이 ClaimTransfer로 한 번 가져간다.
/// 처음 가져간 플레이어에 묶이고(다른 플레이어는 거절), 기한이 지나도 아무도 안 가져갔으면 정리 작업이 게스트 결제를 보관본으로 옮긴다
/// </summary>
public sealed class TransferTicket
{
    [JsonProperty("source")] public string Source { get; set; } = "";
    [JsonProperty("hashes")] public List<string> Hashes { get; set; } = new();
    [JsonProperty("expiresUtc")] public string ExpiresUtc { get; set; } = "";
    /// <summary>가져간 플레이어 (처음 가져갈 때 묶인다)</summary>
    [JsonProperty("target", NullValueHandling = NullValueHandling.Ignore)] public string? Target { get; set; }
    [JsonProperty("claimedUtc", NullValueHandling = NullValueHandling.Ignore)] public string? ClaimedUtc { get; set; }
}
