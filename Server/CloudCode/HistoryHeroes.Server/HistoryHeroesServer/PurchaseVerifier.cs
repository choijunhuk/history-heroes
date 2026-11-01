using System.Globalization;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using HistoryHeroes.Economy;
using Microsoft.Extensions.Logging;
using Unity.Services.CloudCode.Core;
using Unity.Services.CloudCode.Shared;

namespace HistoryHeroes.Server;

/// <summary>
/// 결제 검증과 지급 판정 (Monetization 7-1). 지급 수량 · 계정당 1번 · 첫 구매 2배는 서버 장부로만 정한다 — 게임은 받은 수량을 그대로 넣는다.
/// 저장 방식 (교차 리뷰 2차): 결제 한 건 = 키 하나(p_{토큰 해시}), 판정은 머리(iap_head)의 lock으로 한 줄로 세운다 —
/// "머리 lock 읽기 → 결제 전부 읽기 → 판정 → [머리(그 lock) + 새 결제 키] 한 batch". 그사이 다른 판정이 끼면 batch 전체가 거절되고 처음부터 다시.
/// 머리의 처음 값은 늘 같아서 두 호출이 동시에 lock 없이 만들어도 무해하고, 판정은 머리 내용이 아니라 결제 키들로만 하므로 늦게 다시 만들어져도 잃는 것이 없다.
/// 진짜 내용은 lock 없이 덮어쓰지 않는다 (lock 없이 쓰는 키는 모두 토큰 · 플레이어마다 하나뿐).
/// 모든 호출은 한 함수 안에서 Budget(12초) 안에 끝낸다 (Cloud Code 한도 15초). 토큰 · 비밀값은 기록에 남기지 않는다 (토큰 해시 앞 8자만).
/// </summary>
public sealed class PurchaseVerifier
{
    /// <summary>쓰기 충돌을 이만큼까지 다시 시도한다</summary>
    public const int MaxAttempts = 5;
    /// <summary>구글 구매 토큰은 수백 자다. 이보다 길면 받지 않는다</summary>
    public const int MaxTokenLength = 2048;
    /// <summary>계정을 지운 뒤 결제 기록을 보관하는 햇수 (전자상거래법 — 대금 결제 · 재화 공급 기록 5년)</summary>
    public const int RetentionYears = 5;
    /// <summary>정리 작업 한 번에 살펴볼 보관본 칸 수 (256칸을 돌아가며)</summary>
    public const int ArchiveShardsPerRun = 8;
    public const int ArchiveShards = 256;
    /// <summary>GetPurchases에 얹은 정리 작업 간격 (예약 실행이 없을 때의 대비, 구글 API 한도 안에서)</summary>
    public static readonly TimeSpan JobInterval = TimeSpan.FromMinutes(10);
    /// <summary>예약 실행(RunMaintenance)끼리의 최소 간격 — 같은 때 두 번 돌지 않게</summary>
    public static readonly TimeSpan ScheduledMinInterval = TimeSpan.FromMinutes(1);
    /// <summary>구글은 확정하지 않은 결제를 3일 뒤 환불한다 → 그 뒤로는 확정을 다시 시도하지 않고 토큰도 지운다</summary>
    public static readonly TimeSpan AckWindow = TimeSpan.FromDays(3);
    /// <summary>결제 기록이 끝내 생기지 않은 확정 대기(지급 전에 실패)는 이만큼 뒤 지운다</summary>
    public static readonly TimeSpan OrphanAckAge = TimeSpan.FromHours(1);
    private static readonly TimeSpan VoidedOverlap = TimeSpan.FromHours(1);
    /// <summary>구글이 받는 가장 오래된 startTime은 30일 전</summary>
    private static readonly TimeSpan VoidedMaxLookback = TimeSpan.FromDays(29);

    private readonly IPurchaseStore _store;
    private readonly IGooglePlay _google;
    private readonly ILogger _log;
    private readonly Func<DateTime> _utcNow;
    private readonly TimeSpan _budget;

    public PurchaseVerifier(IPurchaseStore store, IGooglePlay google, ILogger log, Func<DateTime>? utcNow = null, TimeSpan? budget = null)
    {
        _store = store;
        _google = google;
        _log = log;
        _utcNow = utcNow ?? (() => DateTime.UtcNow);
        _budget = budget ?? TimeSpan.FromSeconds(12);
    }

    /// <summary>SHA-256 소문자 16진수 (게임의 IapLedger.Hash와 같다). 토큰 키 · 구글 계정 표시(obfuscatedAccountId)에 쓴다</summary>
    public static string Hash(string text) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text))).ToLowerInvariant();

    /// <summary>지운 계정 보관본의 ID — 게임 계정 ID를 되돌릴 수 없게 바꾼 것</summary>
    public static string ArchiveId(string playerId) => Hash("archive:" + playerId);

    private static string Utc(DateTime t) => t.ToUniversalTime().ToString("o", CultureInfo.InvariantCulture);

    // ------------------------------------------------------------------ VerifyPurchase

    public async Task<VerifyResult> VerifyAsync(IExecutionContext context, string? productId, string? purchaseToken)
    {
        string? playerId = context.PlayerId;
        if (string.IsNullOrEmpty(playerId)) return VerifyResult.Of(VerifyResult.Invalid, "no_player");
        IapCatalog.Item? item = string.IsNullOrEmpty(productId) ? null : IapCatalog.Find(productId);
        if (item == null) return VerifyResult.Of(VerifyResult.Invalid, "unknown_product");
        if (string.IsNullOrEmpty(purchaseToken) || purchaseToken.Length > MaxTokenLength) return VerifyResult.Of(VerifyResult.Invalid, "bad_token");

        string tokenHash = Hash(purchaseToken);
        string tag = tokenHash[..8];
        using var budget = new CancellationTokenSource(_budget);
        CancellationToken ct = budget.Token;
        try
        {
            if (await _store.IsClosedAsync(context, playerId, ct)) return VerifyResult.Of(VerifyResult.Invalid, "account_closed");
            string headLock = await HeadLockAsync(context, playerId, ct); // 머리 먼저: 그 뒤에 읽은 결제 목록만 이 lock과 맞는다
            (PurchaseRecord? known, _) = await _store.LoadPurchaseAsync(context, playerId, tokenHash, ct);
            if (known != null) return await AnswerKnownAsync(context, playerId, known, ct);

            // 게임 전체 색인: 이 토큰을 다른 플레이어나 지운 계정이 이미 받았으면 새로 주지 않는다 (장부를 지워도 다시 받을 수 없다)
            Dictionary<string, IndexEntry> index = await _store.FindIndexAsync(context, new[] { tokenHash }, ct);
            index.TryGetValue(tokenHash, out IndexEntry? owner);
            if (owner != null && owner.Player != playerId)
            {
                _log.LogWarning("이미 다른 계정 · 지운 계정에 쓴 토큰 {Tag}", tag);
                return VerifyResult.Of(VerifyResult.Invalid, "token_used");
            }

            GoogleCheck check = await _google.GetPurchaseAsync(context, item.Id, purchaseToken, ct);
            if (check.Result == GoogleCheck.Kind.NotConfigured) return VerifyResult.Of(VerifyResult.NotConfigured);
            if (check.Result != GoogleCheck.Kind.Ok || check.Found == null)
            {
                _log.LogWarning("구글 확인 실패 {Product} {Tag}: {Result} {Reason}", item.Id, tag, check.Result, check.Reason);
                return VerifyResult.Of(check.Result == GoogleCheck.Kind.Invalid ? VerifyResult.Invalid : VerifyResult.Retry, check.Reason);
            }

            GoogleCheck.Purchase found = check.Found;
            // 대기 중(현금 결제 등)은 아직 돈이 들어오지 않았다. 상태 칸이 없으면 결제된 것으로 보지 않는다
            if (found.PurchaseState == 2) return VerifyResult.Of(VerifyResult.Retry, "pending");
            if (found.PurchaseState == null) return VerifyResult.Of(VerifyResult.Retry, "state_missing");
            string? problem = Problem(found, item.Id, playerId);
            if (problem != null)
            {
                _log.LogWarning("결제 거절 {Product} {Tag}: {Problem}", item.Id, tag, problem);
                return VerifyResult.Of(VerifyResult.Invalid, problem);
            }

            // 지급 전에: 색인(토큰 → 이 플레이어, 토큰마다 하나뿐인 키 · 이 플레이어만 통과하므로 늘 같은 내용)과 확정 대기(토큰 원문, 서버 전용)
            if (owner == null) await _store.SetIndexAsync(context, tokenHash, new IndexEntry { Player = playerId }, ct);
            if (!found.Settled)
                await _store.SetAckAsync(context, tokenHash, new PendingAck { Player = playerId, ProductId = item.Id, Token = purchaseToken, Utc = Utc(_utcNow()) }, ct);

            for (int attempt = 1; attempt <= MaxAttempts; attempt++)
            {
                if (attempt > 1) headLock = await HeadLockAsync(context, playerId, ct);
                List<PurchaseRecord> ledger = await _store.LoadPurchasesAsync(context, playerId, ct);
                known = ledger.Find(p => p.TokenHash == tokenHash);
                if (known != null) return await AnswerKnownAsync(context, playerId, known, ct);

                // 계정당 1번 상품: 이 계정 장부에 이미 있으면 지급하지 않는다 (토큰이 달라도, 환불된 것이라도)
                if (item.OncePerAccount && ledger.Exists(p => p.ProductId == item.Id))
                {
                    await _store.DeleteAckAsync(context, tokenHash, ct); // 확정하지 않는다 → 구글이 환불
                    return VerifyResult.Of(VerifyResult.AlreadyClaimed);
                }

                PurchaseRecord record = Decide(ledger, item, tokenHash, found, _utcNow());
                record.Acknowledged = found.Settled;
                if (!await _store.TryAddPurchaseAsync(context, playerId, headLock, record, ct))
                {
                    _log.LogInformation("다른 판정이 먼저 끝남 → 다시 판정 {Tag} ({Attempt})", tag, attempt);
                    continue;
                }

                // 지급이 장부에 남았다 → 구글에 확정. 실패해도 지급은 그대로 (확정 대기에 남아 다음 호출 · 정리 작업이 다시 확정한다)
                if (!record.Acknowledged) record = await AcknowledgeAsync(context, playerId, record, purchaseToken, ct) ?? record;
                _log.LogInformation("지급 {Product} {Tag} 유료 {Paid} 무료 {Free} 첫2배 {First} 테스트 {Test} 확정 {Ack}",
                    item.Id, tag, record.PaidGems, record.FreeGems, record.FirstBonus, record.Test, record.Acknowledged);
                return VerifyResult.Of(VerifyResult.Granted, purchase: record);
            }

            _log.LogWarning("판정 충돌이 {Max}번 이어짐 {Tag}", MaxAttempts, tag);
            return VerifyResult.Of(VerifyResult.Retry, "busy");
        }
        catch (Exception e)
        {
            // 저장소 · 네트워크 오류 · 시간 초과: 지급을 정하지 못했다 (이미 썼다면 다음에 다시 물을 때 already로 답한다)
            _log.LogError("결제 검증 오류 {Tag}: {Error}", tag, Describe(e));
            return VerifyResult.Of(VerifyResult.Retry, e is OperationCanceledException ? "timeout" : "error");
        }
    }

    /// <summary>
    /// 머리의 lock. 없으면 처음 값으로 lock 없이 만들고 다시 읽는다.
    /// ponytail: 두 호출이 동시에 처음 만들면 늦은 쪽이 머리를 처음 값으로 되돌릴 수 있다 — 머리 내용은 판정에 쓰지 않고(판정은 결제 키들로) lock만 바뀌므로,
    /// 그사이 판정 중이던 쪽은 batch가 거절돼 다시 할 뿐 잃는 결제가 없다
    /// </summary>
    private async Task<string> HeadLockAsync(IExecutionContext context, string playerId, CancellationToken ct)
    {
        string? headLock = await _store.LoadHeadLockAsync(context, playerId, ct);
        if (headLock != null) return headLock;
        await _store.CreateHeadAsync(context, playerId, ct);
        return await _store.LoadHeadLockAsync(context, playerId, ct) ?? throw new InvalidOperationException("head_create");
    }

    /// <summary>이미 장부에 있는 토큰: 환불됐으면 voided, 아니면 already (확정을 못 했으면 이참에 다시)</summary>
    private async Task<VerifyResult> AnswerKnownAsync(IExecutionContext context, string playerId, PurchaseRecord known, CancellationToken ct)
    {
        if (known.Voided) return VerifyResult.Of(VerifyResult.Voided, purchase: known);
        if (!known.Acknowledged)
        {
            Dictionary<string, PendingAck> pending = await _store.FindAcksAsync(context, new[] { known.TokenHash }, ct);
            if (pending.TryGetValue(known.TokenHash, out PendingAck? ack))
                known = await AcknowledgeAsync(context, playerId, known, ack.Token, ct) ?? known;
        }

        return VerifyResult.Of(VerifyResult.Already, purchase: known);
    }

    // ------------------------------------------------------------------ GetPurchases

    public async Task<PurchaseList> GetPurchasesAsync(IExecutionContext context)
    {
        string? playerId = context.PlayerId;
        if (string.IsNullOrEmpty(playerId)) return new PurchaseList { Status = VerifyResult.Invalid };
        using var budget = new CancellationTokenSource(_budget);
        CancellationToken ct = budget.Token;
        try
        {
            if (await _store.IsClosedAsync(context, playerId, ct)) return new PurchaseList { Status = PurchaseList.Closed };
            // 예약 실행(RunMaintenance)이 아직 없거나 멈췄을 때를 위한 대비: 10분에 한 번, 시간 절반까지만
            using (var jobBudget = CancellationTokenSource.CreateLinkedTokenSource(ct))
            {
                jobBudget.CancelAfter(_budget / 2);
                await RunJobAsync(context, JobInterval, jobBudget.Token, ct);
            }

            List<PurchaseRecord> ledger = await _store.LoadPurchasesAsync(context, playerId, ct);
            ledger = await RetryOwnAcksAsync(context, playerId, ledger, ct);
            var list = new PurchaseList { Purchases = ledger };
            try
            {
                list.Configured = await _google.IsConfiguredAsync(context, ct);
            }
            catch (Exception e) when (e is not OperationCanceledException)
            {
                // 비밀값을 못 읽으면 "준비 안 됨"으로 답한다 (장부는 그대로 돌려줘야 게임이 맞출 수 있다)
                _log.LogWarning("비밀값 확인 실패: {Error}", Describe(e));
            }

            return list;
        }
        catch (Exception e)
        {
            _log.LogError("장부 읽기 오류: {Error}", Describe(e));
            return new PurchaseList { Status = VerifyResult.Retry };
        }
    }

    // ------------------------------------------------------------------ 구글 확정 (acknowledge)

    /// <summary>구글에 확정하고 결제 키에 "확정됨"을 적은 뒤 확정 대기를 지운다. 못 하면 null (지급은 그대로, 대기가 남아 다음에 다시). 예외를 밖으로 내지 않는다</summary>
    private async Task<PurchaseRecord?> AcknowledgeAsync(IExecutionContext context, string playerId, PurchaseRecord record, string token, CancellationToken ct)
    {
        try
        {
            if (!await _google.AcknowledgeAsync(context, record.ProductId, token, ct))
            {
                _log.LogWarning("구글 확정 실패 → 다음에 다시 {Tag}", record.TokenHash[..8]);
                return null;
            }

            PurchaseRecord? updated = await UpdateAsync(context, playerId, record.TokenHash, r => r.Acknowledged = true, ct);
            if (updated != null) await _store.DeleteAckAsync(context, record.TokenHash, ct); // 표시를 못 했으면 대기를 남겨 다음에 다시 (구글은 "이미 확정"으로 끝난다)
            return updated;
        }
        catch (Exception e)
        {
            _log.LogWarning("구글 확정 오류 → 다음에 다시 {Tag}: {Error}", record.TokenHash[..8], Describe(e));
            return null;
        }
    }

    /// <summary>이 플레이어의 확정 못 한 결제를 다시 확정한다 (3일이 지난 것은 대기만 지운다 — 구글이 이미 환불, 환불 정리가 회수). 돌려주는 값: 지금 장부</summary>
    private async Task<List<PurchaseRecord>> RetryOwnAcksAsync(IExecutionContext context, string playerId, List<PurchaseRecord> ledger, CancellationToken ct)
    {
        List<string> open = ledger.Where(p => !p.Acknowledged && !p.Voided).Select(p => p.TokenHash).ToList();
        if (open.Count == 0) return ledger;
        Dictionary<string, PendingAck> pending = await _store.FindAcksAsync(context, open, ct);
        DateTime now = _utcNow();
        for (int i = 0; i < ledger.Count; i++)
        {
            if (!pending.TryGetValue(ledger[i].TokenHash, out PendingAck? ack)) continue;
            if (now - ParseUtc(ack.Utc) > AckWindow) await _store.DeleteAckAsync(context, ledger[i].TokenHash, ct);
            else if (await AcknowledgeAsync(context, playerId, ledger[i], ack.Token, ct) is { } done) ledger[i] = done;
        }

        return ledger;
    }

    /// <summary>결제 하나를 바꿔 lock으로 쓴다 (충돌하면 다시 읽고). 못 찾거나 계속 충돌하면 null</summary>
    private async Task<PurchaseRecord?> UpdateAsync(IExecutionContext context, string playerId, string tokenHash, Action<PurchaseRecord> change, CancellationToken ct)
    {
        for (int attempt = 0; attempt < MaxAttempts; attempt++)
        {
            (PurchaseRecord? record, string? writeLock) = await _store.LoadPurchaseAsync(context, playerId, tokenHash, ct);
            if (record == null || writeLock == null) return null;
            change(record);
            if (await _store.TryUpdatePurchaseAsync(context, playerId, record, writeLock, ct)) return record;
        }

        return null;
    }

    // ------------------------------------------------------------------ 정리 작업 (RunMaintenance · GetPurchases 대비)

    /// <summary>
    /// 예약 실행(UGS Scheduler → Trigger → RunMaintenance) 전용. 플레이어가 부르면 거절한다 (부른 사람이 없는 서버 호출만).
    /// 하는 일: 환불 목록 반영 · 확정 대기 다시 확정/3일 지난 토큰 지우기 · 기한 지난 보관본 지우기
    /// </summary>
    public async Task<MaintenanceResult> RunMaintenanceAsync(IExecutionContext context)
    {
        if (!string.IsNullOrEmpty(context.PlayerId)) return new MaintenanceResult { Status = "forbidden" };
        using var budget = new CancellationTokenSource(_budget);
        try
        {
            return await RunJobAsync(context, ScheduledMinInterval, budget.Token, budget.Token);
        }
        catch (Exception e)
        {
            _log.LogError("정리 작업 오류: {Error}", Describe(e));
            return new MaintenanceResult { Status = VerifyResult.Retry };
        }
    }

    /// <summary>
    /// 게임 전체 정리 작업. 작업 상태에 lock으로 "잡기"를 써서 동시에 두 번 돌지 않는다 (상태의 처음 값은 늘 같아 lock 없이 만들어도 무해).
    /// jobCt가 끝나면(시간 절반) 조용히 멈추고, 바깥 ct가 끝났으면 밖으로 올린다
    /// </summary>
    internal async Task<MaintenanceResult> RunJobAsync(IExecutionContext context, TimeSpan minInterval, CancellationToken jobCt, CancellationToken ct)
    {
        var result = new MaintenanceResult { Status = "skipped" };
        try
        {
            DateTime now = _utcNow();
            long nowMs = new DateTimeOffset(now).ToUnixTimeMilliseconds();
            (JobState? state, string? writeLock) = await _store.LoadJobAsync(context, jobCt);
            if (state == null || writeLock == null)
            {
                await _store.CreateJobAsync(context, jobCt);
                (state, writeLock) = await _store.LoadJobAsync(context, jobCt);
                if (state == null || writeLock == null) return result;
            }

            if (state.LastRunMs > 0 && nowMs - state.LastRunMs < (long)minInterval.TotalMilliseconds) return result;
            state.LastRunMs = nowMs;
            string? claimed = await _store.TrySaveJobAsync(context, state, writeLock, jobCt);
            if (claimed == null) return result; // 다른 호출이 먼저 잡았다

            result.Status = PurchaseList.Ok;
            result.Voided = await SyncVoidedAsync(context, state, now, jobCt);
            result.AcksDone = await SweepAcksAsync(context, now, jobCt);
            result.ArchivesDeleted = await PurgeArchivesAsync(context, state, now, jobCt);
            await _store.TrySaveJobAsync(context, state, claimed, jobCt);
            _log.LogInformation("정리 작업: 환불 표시 {Voided} · 확정 대기 정리 {Acks} · 보관본 지움 {Archives}", result.Voided, result.AcksDone, result.ArchivesDeleted);
        }
        catch (Exception e) when (!ct.IsCancellationRequested)
        {
            _log.LogWarning("정리 작업 실패 (다음에 다시): {Error}", Describe(e));
            result.Status = VerifyResult.Retry;
        }

        return result;
    }

    /// <summary>
    /// 구글 환불 목록을 받아 색인으로 찾은 플레이어 결제에 voided를 붙인다. 하나라도 못 붙였으면 체크포인트를 그 가장 오래된 것에 둔다
    /// (다음에 그것부터 다시 — 겹쳐 봐도 표시는 한 번). 돌려주는 값: 새로 붙인 수
    /// </summary>
    private async Task<int> SyncVoidedAsync(IExecutionContext context, JobState state, DateTime now, CancellationToken ct)
    {
        long nowMs = new DateTimeOffset(now).ToUnixTimeMilliseconds();
        long start = Math.Max(state.VoidedCheckpointMs - (long)VoidedOverlap.TotalMilliseconds, nowMs - (long)VoidedMaxLookback.TotalMilliseconds);
        List<VoidedPurchase>? voided = await _google.ListVoidedAsync(context, start, ct);
        if (voided == null || voided.Count == 0) return 0; // 준비 안 됨 · 구글 오류 · 없음: 체크포인트 그대로

        var times = new Dictionary<string, long>();
        foreach (VoidedPurchase v in voided)
            if (!string.IsNullOrEmpty(v.PurchaseToken)) times[Hash(v.PurchaseToken)] = v.VoidedTimeMillis;
        Dictionary<string, IndexEntry> owners = times.Count == 0 ? new() : await _store.FindIndexAsync(context, times.Keys.ToList(), ct);
        int marked = 0;
        long? oldestUnapplied = null;
        foreach ((string tokenHash, IndexEntry owner) in owners)
        {
            if (owner.Player == null) continue; // 지운 계정: 보관본은 지운 때의 기록 그대로
            int result = await MarkVoidedAsync(context, owner.Player, tokenHash, now, ct);
            if (result < 0) oldestUnapplied = Math.Min(oldestUnapplied ?? long.MaxValue, times[tokenHash]);
            else marked += result;
        }

        long newest = voided.Max(v => v.VoidedTimeMillis);
        state.VoidedCheckpointMs = oldestUnapplied ?? Math.Max(state.VoidedCheckpointMs, newest);
        if (oldestUnapplied != null) _log.LogWarning("환불 표시를 못 한 결제가 있어 체크포인트를 그 앞에 둠");
        return marked;
    }

    /// <summary>결제 하나에 voided를 붙인다 (확정 대기도 지운다). 1 붙임 · 0 할 일 없음(없거나 이미) · -1 계속 충돌</summary>
    private async Task<int> MarkVoidedAsync(IExecutionContext context, string playerId, string tokenHash, DateTime now, CancellationToken ct)
    {
        for (int attempt = 0; attempt < MaxAttempts; attempt++)
        {
            (PurchaseRecord? record, string? writeLock) = await _store.LoadPurchaseAsync(context, playerId, tokenHash, ct);
            if (record == null || writeLock == null || record.Voided) return 0;
            record.Voided = true;
            record.VoidedUtc = Utc(now);
            if (!await _store.TryUpdatePurchaseAsync(context, playerId, record, writeLock, ct)) continue;
            await _store.DeleteAckAsync(context, tokenHash, ct);
            return 1;
        }

        return -1;
    }

    /// <summary>
    /// 게임 전체 확정 대기를 정리한다: 3일이 지난 것 · 환불됐거나 이미 확정된 것 · 1시간이 지나도 결제 기록이 없는 것은 지우고, 나머지는 다시 확정. 돌려주는 값: 정리한 수
    /// </summary>
    private async Task<int> SweepAcksAsync(IExecutionContext context, DateTime now, CancellationToken ct)
    {
        int done = 0;
        foreach ((string tokenHash, PendingAck ack) in await _store.ListAcksAsync(context, ct))
        {
            TimeSpan age = now - ParseUtc(ack.Utc);
            (PurchaseRecord? record, _) = string.IsNullOrEmpty(ack.Player) ? (null, null) : await _store.LoadPurchaseAsync(context, ack.Player, tokenHash, ct);
            bool drop = age > AckWindow || (record == null && age > OrphanAckAge) || record?.Voided == true || record?.Acknowledged == true;
            if (drop)
            {
                await _store.DeleteAckAsync(context, tokenHash, ct);
                done++;
            }
            else if (record != null && await AcknowledgeAsync(context, ack.Player, record, ack.Token, ct) != null)
            {
                done++;
            }
        }

        return done;
    }

    /// <summary>보관본 칸을 몇 개씩 돌아가며 deleteAfter가 지난 보관본을 지운다. 돌려주는 값: 지운 수</summary>
    private async Task<int> PurgeArchivesAsync(IExecutionContext context, JobState state, DateTime now, CancellationToken ct)
    {
        int deleted = 0;
        for (int i = 0; i < ArchiveShardsPerRun; i++)
        {
            int shard = ((state.PurgeShard % ArchiveShards) + ArchiveShards) % ArchiveShards;
            foreach (ArchivedPlayer archive in await _store.LoadArchiveShardAsync(context, shard, ct))
            {
                if (ParseUtc(archive.DeleteAfterUtc) > now) continue;
                await _store.DeleteArchiveAsync(context, archive.Id, ct);
                deleted++;
            }

            state.PurgeShard = (shard + 1) % ArchiveShards;
        }

        return deleted;
    }

    // ------------------------------------------------------------------ 계정 삭제

    /// <summary>
    /// 계정 삭제 (게임이 Cloud Save · UGS 계정을 지우기 직전에만 부른다 — 설정의 계정 삭제, 카카오 계정으로 바꿀 때 지우는 게스트):
    /// 결제를 보관본(되돌릴 수 없는 ID, 5년 뒤 지움)에 쓰고 → 색인을 "지운 계정"으로 바꾸고(지우지 않는다 — 같은 토큰을 다시 받지 못하게) →
    /// 서버 전용 닫힘 표시를 남긴 뒤 → 장부를 지운다. 닫힌 플레이어는 그 뒤 VerifyPurchase · GetPurchases가 모두 거절된다 (되돌릴 수 없다).
    /// 이미 닫혔으면 다시 보관하지 않고, 덜 지운 장부만 마저 지운다. 실패하면 retry — 게임은 계정 삭제를 멈춘다
    /// </summary>
    public async Task<ArchiveResult> ArchiveAndDeleteAsync(IExecutionContext context)
    {
        string? playerId = context.PlayerId;
        if (string.IsNullOrEmpty(playerId)) return new ArchiveResult { Status = VerifyResult.Invalid };
        using var budget = new CancellationTokenSource(_budget);
        CancellationToken ct = budget.Token;
        try
        {
            if (await _store.IsClosedAsync(context, playerId, ct))
            {
                await _store.DeletePlayerPurchasesAsync(context, playerId, ct);
                return new ArchiveResult { AlreadyArchived = true };
            }

            List<PurchaseRecord> ledger = await _store.LoadPurchasesAsync(context, playerId, ct);
            DateTime now = _utcNow();
            string archiveId = ArchiveId(playerId);
            if (ledger.Count > 0)
            {
                await _store.SetArchiveAsync(context, new ArchivedPlayer
                {
                    Id = archiveId,
                    DeletedUtc = Utc(now),
                    DeleteAfterUtc = Utc(now.AddYears(RetentionYears)),
                    Purchases = ledger,
                }, ct);
                foreach (PurchaseRecord[] batch in ledger.Chunk(10))
                {
                    await Task.WhenAll(batch.Select(p => _store.SetIndexAsync(context, p.TokenHash, new IndexEntry { ArchiveId = archiveId }, ct)));
                    await Task.WhenAll(batch.Select(p => _store.DeleteAckAsync(context, p.TokenHash, ct)));
                }
            }

            await _store.CloseAsync(context, playerId, now, ct);
            await _store.DeletePlayerPurchasesAsync(context, playerId, ct);
            _log.LogInformation("계정 삭제: 결제 {Count}건 보관 · 장부 지움 · 닫음", ledger.Count);
            return new ArchiveResult { Archived = ledger.Count };
        }
        catch (Exception e)
        {
            _log.LogError("결제 기록 보관 오류: {Error}", Describe(e));
            return new ArchiveResult { Status = VerifyResult.Retry };
        }
    }

    // ------------------------------------------------------------------ 판정

    /// <summary>구글이 확인해 준 구매를 이 플레이어 · 이 상품으로 받아도 되나. 문제가 없으면 null</summary>
    internal static string? Problem(GoogleCheck.Purchase found, string productId, string playerId)
    {
        if (found.PurchaseState != 0) return "state_" + (found.PurchaseState?.ToString(CultureInfo.InvariantCulture) ?? "missing");
        if (found.ProductId != null && found.ProductId != productId) return "product_mismatch";
        // 여러 개 사기는 콘솔에서 껐다. 켜져 있으면 1개 값만 주게 되니 지급하지 않는다 (확정 안 함 → 구글이 환불)
        if ((found.Quantity ?? 1) != 1) return "quantity";
        // 돈을 낸 결제만 지급한다: 프로모션 코드(1) · 보상형(2)은 주지 않는다 (테스트 결제 0은 라이선스 테스터라 지급)
        if (found.PurchaseType == 1) return "not_paid_promo";
        if (found.PurchaseType == 2) return "not_paid_rewarded";
        // 다른 플레이어가 결제한 토큰을 들고 와도 받지 못한다 (결제 때 넣은 계정 표시)
        if (found.ObfuscatedAccountId != Hash(playerId)) return "account_mismatch";
        return null;
    }

    /// <summary>지금 장부로 이번 결제에 줄 것 (게임의 IapLedger.Preview와 같은 계산)</summary>
    internal static PurchaseRecord Decide(List<PurchaseRecord> ledger, IapCatalog.Item item, string tokenHash, GoogleCheck.Purchase found, DateTime utcNow)
    {
        bool first = item.FirstDouble && !ledger.Exists(p => p.ProductId == item.Id && p.FirstBonus);
        return new PurchaseRecord
        {
            TokenHash = tokenHash,
            OrderId = found.OrderId,
            ProductId = item.Id,
            Utc = Utc(utcNow),
            PaidGems = item.PaidGems,
            FreeGems = item.FreeGems + (first ? item.PaidGems : 0),
            Tickets = item.Tickets,
            Gold = item.Gold,
            FirstBonus = first,
            Test = found.PurchaseType == 0,
        };
    }

    private static DateTime ParseUtc(string utc) =>
        DateTime.TryParse(utc, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out DateTime t) ? t : DateTime.MinValue;

    /// <summary>기록용 오류 설명: 예외 종류 + HTTP 상태만 (메시지에 주소 · 토큰이 섞일 수 있어서 안 쓴다)</summary>
    internal static string Describe(Exception e) => e switch
    {
        ApiException a => $"{nameof(ApiException)} {a.Type} {(int?)a.Response?.StatusCode}",
        HttpRequestException h => $"{nameof(HttpRequestException)} {(int?)h.StatusCode}",
        _ => e.GetType().Name,
    };
}
