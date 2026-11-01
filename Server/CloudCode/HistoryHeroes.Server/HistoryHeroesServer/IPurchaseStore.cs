using Unity.Services.CloudCode.Core;

namespace HistoryHeroes.Server;

/// <summary>
/// 서버 저장소 (실제는 CloudSavePurchaseStore, 테스트는 가짜). 진짜 내용이 든 쓰기는 모두 읽은 판의 lock을 붙이거나 lock으로 지킨 batch 안에서 한다.
/// lock 없이 쓰는 건 (1) 늘 같은 처음 값(머리 · 작업 문지기) (2) 토큰 · 플레이어마다 하나뿐이고 쓰는 쪽이 한 플레이어뿐인 새 키
/// (3) 한 방향으로만 바뀌는 닫힘 표시뿐이다. 토큰 색인은 늘 읽은 lock으로, 기대한 주인일 때만 바꾼다 (없을 때 만들기는 없음을 확인한 뒤).
/// </summary>
public interface IPurchaseStore
{
    // ---- 플레이어 결제 장부 (protected: 게임은 자기 것을 읽기만)

    /// <summary>머리의 lock과 닫혔는지 (머리가 닫힘 또는 닫힘 표시가 있음 — 한 번에 읽는다). 머리가 없으면 (null, 표시 여부)</summary>
    Task<(string? writeLock, bool closed)> LoadHeadAsync(IExecutionContext context, string playerId, CancellationToken ct);

    /// <summary>머리를 처음 값으로 만든다 (lock 없이 — 처음 값은 늘 같아서 동시에 만들어도 무해. 닫힘은 따로 둔 닫힘 표시가 지킨다)</summary>
    Task CreateHeadAsync(IExecutionContext context, string playerId, CancellationToken ct);

    /// <summary>[머리를 읽은 lock으로 "닫힘" + 닫힘 표시] 한 batch (그 뒤 옛 lock을 든 batch는 모두 거절, 머리가 닫히면 표시도 반드시 있다). 충돌이면 false</summary>
    Task<bool> TryCloseHeadAsync(IExecutionContext context, string playerId, string headLock, DateTime utcNow, CancellationToken ct);

    /// <summary>이 플레이어의 결제 전부 (키 p_*)</summary>
    Task<List<PurchaseRecord>> LoadPurchasesAsync(IExecutionContext context, string playerId, CancellationToken ct);

    /// <summary>결제 하나와 그 lock (없으면 null, null)</summary>
    Task<(PurchaseRecord? record, string? writeLock)> LoadPurchaseAsync(IExecutionContext context, string playerId, string tokenHash, CancellationToken ct);

    /// <summary>
    /// 새 결제를 더한다: [머리(읽은 lock) + 새 결제 키]를 한 번의 batch로 (Cloud Save batch는 한 접근 등급 안에서 전부 되거나 전부 안 된다).
    /// 그사이 다른 판정 · 예약 · 닫기가 머리를 바꿨으면 false (아무것도 안 써짐)
    /// </summary>
    Task<bool> TryAddPurchaseAsync(IExecutionContext context, string playerId, string headLock, PurchaseRecord record, CancellationToken ct);

    /// <summary>결제 하나를 읽은 lock으로 고친다 (확정 · 환불 표시). 충돌이면 false</summary>
    Task<bool> TryUpdatePurchaseAsync(IExecutionContext context, string playerId, PurchaseRecord record, string writeLock, CancellationToken ct);

    /// <summary>결제 키들을 지운다 (머리는 남긴다 — 닫힌 머리가 늘 남아 있게)</summary>
    Task DeletePurchasesAsync(IExecutionContext context, string playerId, IReadOnlyCollection<string> tokenHashes, CancellationToken ct);

    /// <summary>
    /// 토큰 예약: [머리(읽은 lock) + r_{토큰 해시}]를 한 batch로 (protected). 닫히는 중(머리 lock이 바뀜)이면 false — 닫힌 뒤에는 예약이 생기지 않고,
    /// 닫기 전에 생긴 예약은 닫은 뒤 읽는 예약 목록에 모두 있다
    /// </summary>
    Task<bool> TryReserveAsync(IExecutionContext context, string playerId, string headLock, string tokenHash, CancellationToken ct);

    /// <summary>이 플레이어가 예약한 토큰 (r_*): 색인에 이 플레이어를 적을 수 있었던 토큰 전부 (지급 안 된 것 포함)</summary>
    Task<List<string>> ListReservedAsync(IExecutionContext context, string playerId, CancellationToken ct);

    Task DeleteReservedAsync(IExecutionContext context, string playerId, IReadOnlyCollection<string> tokenHashes, CancellationToken ct);

    /// <summary>옛 배포가 private 플레이어 데이터에 남긴 예약 (r_*) — 보관 · 마무리 · 계정 바꾸기 확인이 함께 본다</summary>
    Task<List<string>> ListLegacyReservedAsync(IExecutionContext context, string playerId, CancellationToken ct);

    Task DeleteLegacyReservedAsync(IExecutionContext context, string playerId, IReadOnlyCollection<string> tokenHashes, CancellationToken ct);

    // ---- 닫힘 표시 (protected iap_closed — 옛 배포는 private에): 한 방향, 지우는 함수가 없다

    /// <summary>닫힘 표시가 있나 (지금 표시 + 옛 private 표시)</summary>
    Task<bool> IsClosedAsync(IExecutionContext context, string playerId, CancellationToken ct);

    /// <summary>닫힘 표시만 쓴다 (계정 삭제: 표시 먼저 → 머리 닫기). 머리 만들기는 이 키를 쓰지 않는다</summary>
    Task CloseAsync(IExecutionContext context, string playerId, DateTime utcNow, CancellationToken ct);

    // ---- 게임 전체 (private custom: 서버만)

    /// <summary>색인 여러 칸 (찾은 것만, 읽기 전용 — 환불 목록 맞추기)</summary>
    Task<Dictionary<string, IndexEntry>> FindIndexAsync(IExecutionContext context, IReadOnlyCollection<string> tokenHashes, CancellationToken ct);

    /// <summary>색인 한 칸과 그 lock (없으면 null, null)</summary>
    Task<(IndexEntry? entry, string? writeLock)> LoadIndexAsync(IExecutionContext context, string tokenHash, CancellationToken ct);

    /// <summary>색인 한 칸을 읽은 lock으로 바꾼다 (lock이 null이면 새로 만들기 — 없음을 확인한 뒤에만). 충돌이면 false</summary>
    Task<bool> TrySetIndexAsync(IExecutionContext context, string tokenHash, IndexEntry entry, string? writeLock, CancellationToken ct);

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

    /// <summary>계정 삭제 마무리 할 일 (보관본 ID 키)</summary>
    Task SetClosingAsync(IExecutionContext context, string archiveId, ClosingPlayer item, CancellationToken ct);

    Task<Dictionary<string, ClosingPlayer>> ListClosingAsync(IExecutionContext context, CancellationToken ct);

    Task DeleteClosingAsync(IExecutionContext context, string archiveId, CancellationToken ct);
}
