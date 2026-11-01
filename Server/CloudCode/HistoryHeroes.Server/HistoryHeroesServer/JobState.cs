using Newtonsoft.Json;

namespace HistoryHeroes.Server;

/// <summary>
/// 게임 전체 정리 작업의 상태 (Cloud Save 비공개 custom 데이터 iap_jobs/state, 게임은 못 읽는다).
/// 환불 목록 받기는 10분에 한 번까지 (구글 API 한도), 보관 기록 지우기는 달 단위.
/// </summary>
public sealed class JobState
{
    /// <summary>마지막으로 작업을 잡은 시각 (밀리초)</summary>
    [JsonProperty("lastRunMs")] public long LastRunMs { get; set; }
    /// <summary>여기까지 본 환불 시각 (밀리초). 다음에는 이보다 1시간 앞부터 다시 본다 (겹쳐 봐도 표시는 한 번)</summary>
    [JsonProperty("voidedCheckpointMs")] public long VoidedCheckpointMs { get; set; }
    /// <summary>이 달(yyyyMM)까지의 보관 기록은 지웠다</summary>
    [JsonProperty("purgedThrough")] public string PurgedThrough { get; set; } = "202610";
}
