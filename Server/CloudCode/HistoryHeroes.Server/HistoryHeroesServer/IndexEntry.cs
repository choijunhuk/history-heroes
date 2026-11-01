using Newtonsoft.Json;

namespace HistoryHeroes.Server;

/// <summary>
/// 게임 전체 토큰 색인 한 칸 (Cloud Save private custom iap_index_{해시 앞 2자}/{토큰 해시}, 게임은 못 읽는다). 지우지 않는다.
/// 지급 전에 쓰고, 지급 전마다 읽는다: 다른 플레이어 · 지운 계정의 토큰이면 새로 지급하지 않는다. 환불 목록의 토큰으로 장부를 찾을 때도 쓴다.
/// 계정을 지우면 플레이어 ID를 지우고 보관본 ID(되돌릴 수 없는 해시)만 남긴다.
/// </summary>
public sealed class IndexEntry
{
    /// <summary>이 토큰을 받은 플레이어 (계정을 지웠으면 null)</summary>
    [JsonProperty("p", NullValueHandling = NullValueHandling.Ignore)] public string? Player { get; set; }
    /// <summary>계정을 지웠으면 그 보관본 ID</summary>
    [JsonProperty("a", NullValueHandling = NullValueHandling.Ignore)] public string? ArchiveId { get; set; }
}
