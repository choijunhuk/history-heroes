using Newtonsoft.Json;

namespace HistoryHeroes.Server;

/// <summary>
/// 계정을 지운 플레이어의 결제 기록 보관본 (전자상거래법 5년, 개인정보 처리방침 3장). 게임 계정 ID 대신 되돌릴 수 없는 해시만 남긴다.
/// 비공개 custom 데이터 iap_archive_{지운 달}/d{일}에 쌓이고, deleteAfter가 지난 달은 통째로 지운다.
/// </summary>
public sealed class ArchivedPlayer
{
    /// <summary>SHA-256("archive:" + UGS 플레이어 ID) — 문의 · 분쟁 때 그 사람이 알려 준 ID로만 다시 찾을 수 있다</summary>
    [JsonProperty("id")] public string Id { get; set; } = "";
    [JsonProperty("deletedUtc")] public string DeletedUtc { get; set; } = "";
    [JsonProperty("deleteAfterUtc")] public string DeleteAfterUtc { get; set; } = "";
    [JsonProperty("purchases")] public List<PurchaseRecord> Purchases { get; set; } = new();
}
