using Newtonsoft.Json;

namespace HistoryHeroes.Server;

/// <summary>
/// 게임 전체 정리 작업의 상태 (Cloud Save private custom iap_jobs/state, 게임은 못 읽는다).
/// 처음 값은 늘 같다(new JobState()) → 두 호출이 동시에 처음 만들어도 같은 글이라 무해하고, 그 뒤 고치기는 늘 읽은 판의 lock으로만 한다.
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
