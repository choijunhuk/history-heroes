using Unity.Services.CloudCode.Core;

namespace HistoryHeroes.Server;

/// <summary>
/// 서버 저장소 (실제는 CloudSavePurchaseStore, 테스트는 가짜). 플레이어 장부 + 게임 전체 자료(토큰 → 플레이어 색인 · 작업 상태 · 지운 계정 보관본).
/// 플레이어 ID를 따로 받는다 — 환불 정리는 부른 사람이 아닌 다른 플레이어의 장부도 고친다.
/// </summary>
public interface IPurchaseStore
{
    /// <summary>장부와 그 판의 write lock. 키가 아직 없으면 빈 장부 + null</summary>
    Task<(List<PurchaseRecord> purchases, string? writeLock)> LoadAsync(IExecutionContext context, string playerId, CancellationToken ct);

    /// <summary>읽은 판의 lock을 붙여 쓴다. 그사이 다른 호출이 썼으면 false (충돌). lock이 null이면 조건 없이 쓴다 (빈 장부 처음 만들기에만 쓴다)</summary>
    Task<bool> TrySaveAsync(IExecutionContext context, string playerId, List<PurchaseRecord> purchases, string? writeLock, CancellationToken ct);

    /// <summary>장부 키를 지운다 (계정 삭제 — 보관본을 먼저 쓴 뒤)</summary>
    Task DeleteAsync(IExecutionContext context, string playerId, CancellationToken ct);

    /// <summary>토큰 해시 → 플레이어 색인 (환불 목록의 토큰으로 장부를 찾으려고). 지급 전에 쓴다</summary>
    Task SetIndexAsync(IExecutionContext context, string tokenHash, string playerId, CancellationToken ct);

    /// <summary>색인에서 찾은 것만 (토큰 해시 → 플레이어)</summary>
    Task<Dictionary<string, string>> FindIndexAsync(IExecutionContext context, IReadOnlyCollection<string> tokenHashes, CancellationToken ct);

    Task DeleteIndexAsync(IExecutionContext context, IReadOnlyCollection<string> tokenHashes, CancellationToken ct);

    /// <summary>작업 상태와 lock (없으면 처음 값 + null)</summary>
    Task<(JobState state, string? writeLock)> LoadJobAsync(IExecutionContext context, CancellationToken ct);

    /// <summary>lock을 붙여 쓴다 (null이면 조건 없이). 새 lock, 충돌이면 null</summary>
    Task<string?> TrySaveJobAsync(IExecutionContext context, JobState state, string? writeLock, CancellationToken ct);

    /// <summary>지운 계정 보관본을 그 날 칸에 더한다 (충돌하면 다시 읽고 더한다)</summary>
    Task AppendArchiveAsync(IExecutionContext context, DateTime utcNow, ArchivedPlayer archive, CancellationToken ct);

    /// <summary>그 달(yyyyMM)에 쌓인 보관본을 통째로 지운다</summary>
    Task DeleteArchiveMonthAsync(IExecutionContext context, string month, CancellationToken ct);
}
