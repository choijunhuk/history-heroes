using Newtonsoft.Json;

namespace HistoryHeroes.Server;

/// <summary>
/// 게임 전체 정리 작업의 상태 (Cloud Save private custom iap_jobs/state, 게임은 못 읽는다).
/// 상태는 늘 같은 내용인 문지기(iap_jobs/gate)의 lock으로 지킨 batch 안에서만 쓴다 — 문지기만 lock 없이 만들 수 있어, 늦게 다시 만들어져도 상태는 덮이지 않는다.
/// </summary>
public sealed class JobState
{
    /// <summary>마지막으로 작업을 잡은 시각 (밀리초)</summary>
    [JsonProperty("lastRunMs")] public long LastRunMs { get; set; }
    /// <summary>여기까지 본 환불 시각 (밀리초). 다음에는 이보다 1시간 앞부터 다시 본다 (겹쳐 봐도 표시는 한 번)</summary>
    [JsonProperty("voidedCheckpointMs")] public long VoidedCheckpointMs { get; set; }
    /// <summary>다음에 기한을 살펴볼 보관본 칸 번호 (0~255를 돌아가며)</summary>
    [JsonProperty("purgeShard")] public int PurgeShard { get; set; }
}
