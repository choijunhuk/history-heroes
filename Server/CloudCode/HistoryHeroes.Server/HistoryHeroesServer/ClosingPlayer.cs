using Newtonsoft.Json;

namespace HistoryHeroes.Server;

/// <summary>
/// 계정 삭제 뒤 마무리할 일 (Cloud Save private custom iap_closing/{보관본 ID}, 서버만). 보관을 시작할 때 먼저 쓰고,
/// 닫은 뒤 ClosingGrace(1시간 — 어떤 진행 중 호출도 끝났을 시간)가 지나면 정리 작업이 이 플레이어가 예약한 토큰(r_*)의 색인 칸을 마저 익명으로 바꾸고
/// 보관을 다시 확인(같은 결과)한 뒤 예약 목록과 이 칸을 지운다 — 시간 초과로 끝난 호출이 늦게 남긴 색인 칸도 여기서 정리된다
/// </summary>
public sealed class ClosingPlayer
{
    [JsonProperty("p")] public string Player { get; set; } = "";
    [JsonProperty("utc")] public string Utc { get; set; } = "";
}
