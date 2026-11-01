using Unity.Services.CloudCode.Core;

namespace HistoryHeroes.Server;

/// <summary>
/// 서버 저장소 (실제는 CloudSavePurchaseStore, 테스트는 가짜). 진짜 내용이 든 쓰기는 모두 읽은 판의 lock을 붙이거나 lock으로 지킨 batch 안에서,
/// 또는 다른 호출과 겹칠 수 없는 새 키에만 한다. lock 없이 쓰는 건 (1) 늘 같은 처음 값(머리 · 작업 문지기) (2) 토큰 · 플레이어 · 이전권마다 하나뿐인 키,
/// (3) 한 방향으로만 바뀌는 닫힘 표시뿐이다.
/// </summary>
public interface IPurchaseStore
{
    // ---- 플레이어 결제 장부 (protected: 게임은 자기 것을 읽기만)

    /// <summary>머리의 lock과 닫혔는지. 없으면 (null, false)</summary>
    Task<(string? writeLock, bool closed)> LoadHeadAsync(IExecutionContext context, string playerId, CancellationToken ct);

    /// <summary>머리를 처음 값으로 만든다 (lock 없이 — 처음 값은 늘 같아서 동시에 만들어도 무해. 닫힘은 따로 둔 닫힘 표시가 지킨다)</summary>
    Task CreateHeadAsync(IExecutionContext context, string playerId, CancellationToken ct);

    /// <summary>머리를 읽은 lock으로 "닫힘"으로 바꾼다 (그 뒤 옛 lock을 든 결제 batch는 모두 거절). 충돌이면 false</summary>
    Task<bool> TryCloseHeadAsync(IExecutionContext context, string playerId, string headLock, CancellationToken ct);

    /// <summary>이 플레이어의 결제 전부 (키 p_*)</summary>
    Task<List<PurchaseRecord>> LoadPurchasesAsync(IExecutionContext context, string playerId, CancellationToken ct);

    /// <summary>결제 하나와 그 lock (없으면 null, null)</summary>
    Task<(PurchaseRecord? record, string? writeLock)> LoadPurchaseAsync(IExecutionContext context, string playerId, string tokenHash, CancellationToken ct);

    /// <summary>
    /// 새 결제(들)를 더한다: [머리(읽은 lock) + 새 결제 키들]을 한 번의 batch로 (Cloud Save batch는 한 접근 등급 안에서 전부 되거나 전부 안 된다, 한 번에 20칸까지 → 결제 19개).
    /// 그사이 다른 판정 · 닫기가 머리를 바꿨으면 false (아무것도 안 써짐)
    /// </summary>
    Task<bool> TryAddPurchasesAsync(IExecutionContext context, string playerId, string headLock, IReadOnlyList<PurchaseRecord> records, CancellationToken ct);

    /// <summary>결제 하나를 읽은 lock으로 고친다 (확정 · 환불 표시). 충돌이면 false</summary>
    Task<bool> TryUpdatePurchaseAsync(IExecutionContext context, string playerId, PurchaseRecord record, string writeLock, CancellationToken ct);

    /// <summary>결제 키들을 지운다 (머리는 남긴다 — 닫힌 머리가 늘 남아 있게)</summary>
    Task DeletePurchasesAsync(IExecutionContext context, string playerId, IReadOnlyCollection<string> tokenHashes, CancellationToken ct);

    // ---- 플레이어 서버 전용 (private): 닫힘 표시 · 이 플레이어가 색인에 적은 토큰

    Task<bool> IsClosedAsync(IExecutionContext context, string playerId, CancellationToken ct);

    /// <summary>닫힘 표시 (한 방향: 쓰면 다시 열리지 않는다)</summary>
    Task CloseAsync(IExecutionContext context, string playerId, DateTime utcNow, CancellationToken ct);

    /// <summary>이 플레이어가 게임 전체 색인에 적은 토큰 (지급 전에 적는다 — 지급이 안 된 것도 계정을 지울 때 익명으로 바꾸려고)</summary>
    Task AddReservedAsync(IExecutionContext context, string playerId, string tokenHash, CancellationToken ct);

    Task<List<string>> ListReservedAsync(IExecutionContext context, string playerId, CancellationToken ct);

    Task DeleteReservedAsync(IExecutionContext context, string playerId, IReadOnlyCollection<string> tokenHashes, CancellationToken ct);

    // ---- 게임 전체 (private custom: 서버만)

    Task<Dictionary<string, IndexEntry>> FindIndexAsync(IExecutionContext context, IReadOnlyCollection<string> tokenHashes, CancellationToken ct);

    Task SetIndexAsync(IExecutionContext context, string tokenHash, IndexEntry entry, CancellationToken ct);

    Task SetAckAsync(IExecutionContext context, string tokenHash, PendingAck ack, CancellationToken ct);

    Task<Dictionary<string, PendingAck>> FindAcksAsync(IExecutionContext context, IReadOnlyCollection<string> tokenHashes, CancellationToken ct);

    Task<Dictionary<string, PendingAck>> ListAcksAsync(IExecutionContext context, CancellationToken ct);

    Task DeleteAckAsync(IExecutionContext context, string tokenHash, CancellationToken ct);

    /// <summary>작업 문지기(늘 같은 내용, lock만 쓴다)의 lock. 없으면 null</summary>
    Task<string?> LoadJobGateAsync(IExecutionContext context, CancellationToken ct);

    /// <summary>작업 문지기를 만든다 (lock 없이 — 늘 같은 내용)</summary>
    Task CreateJobGateAsync(IExecutionContext context, CancellationToken ct);

    /// <summary>작업 상태 (없으면 null). 상태는 문지기 lock으로 지킨 batch 안에서만 쓰여 lock 없이 덮이는 일이 없다</summary>
    Task<JobState?> LoadJobStateAsync(IExecutionContext context, CancellationToken ct);

    /// <summary>[문지기(읽은 lock) + 상태]를 한 batch로. 새 문지기 lock, 충돌이면 null</summary>
    Task<string?> TrySaveJobAsync(IExecutionContext context, string gateLock, JobState state, CancellationToken ct);

    /// <summary>보관본과 lock (없으면 null, null)</summary>
    Task<(ArchivedPlayer? archive, string? writeLock)> LoadArchiveAsync(IExecutionContext context, string archiveId, CancellationToken ct);

    /// <summary>보관본을 쓴다 (lock이 null이면 새로 — 플레이어마다 다른 키). 충돌이면 false</summary>
    Task<bool> TrySetArchiveAsync(IExecutionContext context, ArchivedPlayer archive, string? writeLock, CancellationToken ct);

    Task<List<ArchivedPlayer>> LoadArchiveShardAsync(IExecutionContext context, int shard, CancellationToken ct);

    Task DeleteArchiveAsync(IExecutionContext context, string archiveId, CancellationToken ct);

    /// <summary>이전권과 lock (없으면 null, null)</summary>
    Task<(TransferTicket? ticket, string? writeLock)> LoadTransferAsync(IExecutionContext context, string ticketHash, CancellationToken ct);

    /// <summary>이전권을 쓴다 (lock이 null이면 새로 — 이전권마다 무작위 키). 새 lock, 충돌이면 null</summary>
    Task<string?> TrySetTransferAsync(IExecutionContext context, string ticketHash, TransferTicket ticket, string? writeLock, CancellationToken ct);

    Task<Dictionary<string, TransferTicket>> ListTransfersAsync(IExecutionContext context, CancellationToken ct);

    Task DeleteTransferAsync(IExecutionContext context, string ticketHash, CancellationToken ct);
}
