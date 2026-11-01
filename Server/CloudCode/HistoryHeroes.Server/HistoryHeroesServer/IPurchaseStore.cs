using Unity.Services.CloudCode.Core;

namespace HistoryHeroes.Server;

/// <summary>
/// 서버 저장소 (실제는 CloudSavePurchaseStore, 테스트는 가짜). 진짜 내용이 든 쓰기는 모두 읽은 판의 lock을 붙이거나, 다른 호출과 겹칠 수 없는 새 키에만 한다.
/// lock 없이 쓰는 건 (1) 늘 같은 처음 값(머리 · 작업 상태) 만들기 (2) 토큰 · 플레이어마다 하나뿐인 키(결제 · 색인 · 확정 대기 · 보관본 · 닫힘 표시) 뿐이다.
/// </summary>
public interface IPurchaseStore
{
    // ---- 플레이어 결제 장부 (protected: 게임은 자기 것을 읽기만)

    /// <summary>머리(결제 판정을 한 줄로 세우는 lock 전용 문서)의 lock. 없으면 null</summary>
    Task<string?> LoadHeadLockAsync(IExecutionContext context, string playerId, CancellationToken ct);

    /// <summary>머리를 처음 값으로 만든다 (lock 없이 — 처음 값은 늘 같아서 동시에 만들어도 무해)</summary>
    Task CreateHeadAsync(IExecutionContext context, string playerId, CancellationToken ct);

    /// <summary>이 플레이어의 결제 전부 (키 p_*)</summary>
    Task<List<PurchaseRecord>> LoadPurchasesAsync(IExecutionContext context, string playerId, CancellationToken ct);

    /// <summary>결제 하나와 그 lock (없으면 null, null)</summary>
    Task<(PurchaseRecord? record, string? writeLock)> LoadPurchaseAsync(IExecutionContext context, string playerId, string tokenHash, CancellationToken ct);

    /// <summary>
    /// 새 결제를 더한다: [머리(읽은 lock) + 새 결제 키]를 한 번의 batch로 (Cloud Save batch는 한 접근 등급 안에서 전부 되거나 전부 안 된다).
    /// 그사이 다른 판정이 머리를 바꿨으면 false (아무것도 안 써짐)
    /// </summary>
    Task<bool> TryAddPurchaseAsync(IExecutionContext context, string playerId, string headLock, PurchaseRecord record, CancellationToken ct);

    /// <summary>결제 하나를 읽은 lock으로 고친다 (확정 · 환불 표시). 충돌이면 false</summary>
    Task<bool> TryUpdatePurchaseAsync(IExecutionContext context, string playerId, PurchaseRecord record, string writeLock, CancellationToken ct);

    /// <summary>이 플레이어의 protected 데이터를 모두 지운다 (계정 닫기 — 보관본을 먼저 쓴 뒤)</summary>
    Task DeletePlayerPurchasesAsync(IExecutionContext context, string playerId, CancellationToken ct);

    // ---- 플레이어 닫힘 표시 (private: 서버만)

    Task<bool> IsClosedAsync(IExecutionContext context, string playerId, CancellationToken ct);

    Task CloseAsync(IExecutionContext context, string playerId, DateTime utcNow, CancellationToken ct);

    // ---- 게임 전체 (private custom: 서버만)

    /// <summary>토큰 색인에서 찾은 것만</summary>
    Task<Dictionary<string, IndexEntry>> FindIndexAsync(IExecutionContext context, IReadOnlyCollection<string> tokenHashes, CancellationToken ct);

    Task SetIndexAsync(IExecutionContext context, string tokenHash, IndexEntry entry, CancellationToken ct);

    Task SetAckAsync(IExecutionContext context, string tokenHash, PendingAck ack, CancellationToken ct);

    Task<Dictionary<string, PendingAck>> FindAcksAsync(IExecutionContext context, IReadOnlyCollection<string> tokenHashes, CancellationToken ct);

    /// <summary>확정 대기 전부 (정리 작업용, 많아야 몇 개)</summary>
    Task<Dictionary<string, PendingAck>> ListAcksAsync(IExecutionContext context, CancellationToken ct);

    Task DeleteAckAsync(IExecutionContext context, string tokenHash, CancellationToken ct);

    /// <summary>작업 상태와 lock (없으면 null, null)</summary>
    Task<(JobState? state, string? writeLock)> LoadJobAsync(IExecutionContext context, CancellationToken ct);

    /// <summary>작업 상태를 처음 값(new JobState())으로 만든다 (lock 없이 — 늘 같은 글)</summary>
    Task CreateJobAsync(IExecutionContext context, CancellationToken ct);

    /// <summary>lock을 붙여 쓴다. 새 lock, 충돌이면 null</summary>
    Task<string?> TrySaveJobAsync(IExecutionContext context, JobState state, string writeLock, CancellationToken ct);

    /// <summary>보관본을 그 플레이어 키에 쓴다 (플레이어마다 다른 키)</summary>
    Task SetArchiveAsync(IExecutionContext context, ArchivedPlayer archive, CancellationToken ct);

    /// <summary>보관본 칸(0~255) 하나의 보관본 전부</summary>
    Task<List<ArchivedPlayer>> LoadArchiveShardAsync(IExecutionContext context, int shard, CancellationToken ct);

    Task DeleteArchiveAsync(IExecutionContext context, string archiveId, CancellationToken ct);
}
