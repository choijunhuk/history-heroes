using Newtonsoft.Json;

namespace HistoryHeroes.Server;

/// <summary>
/// 구글 확정(acknowledge)을 아직 못 한 결제의 토큰 원문 (Cloud Save private custom iap_ack/{토큰 해시} — 서버만 읽고 쓴다).
/// 지급 전에 쓰고, 확정이 끝나면 지운다. 정리 작업(RunMaintenance)이 3일이 지난 것은 확정 없이 지운다 (구글이 이미 환불).
/// </summary>
public sealed class PendingAck
{
    [JsonProperty("p")] public string Player { get; set; } = "";
    [JsonProperty("product")] public string ProductId { get; set; } = "";
    [JsonProperty("token")] public string Token { get; set; } = "";
    [JsonProperty("utc")] public string Utc { get; set; } = "";
}
