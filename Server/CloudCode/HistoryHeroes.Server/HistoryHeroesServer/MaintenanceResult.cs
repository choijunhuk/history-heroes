using Newtonsoft.Json;

namespace HistoryHeroes.Server;

/// <summary>RunMaintenance의 답 (트리거 기록용): ok · skipped(다른 실행이 방금 함) · forbidden(플레이어가 부름) · retry</summary>
public sealed class MaintenanceResult
{
    [JsonProperty("status")] public string Status { get; set; } = PurchaseList.Ok;
    [JsonProperty("voided")] public int Voided { get; set; }
    [JsonProperty("acks")] public int AcksDone { get; set; }
    [JsonProperty("archivesDeleted")] public int ArchivesDeleted { get; set; }
}
