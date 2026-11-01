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
/// VerifyPurchase: 장부에 이 토큰이 있으면 already(환불됐으면 voided) → 구글에 구매 확인 (결제됨 · 같은 상품 · 수량 1 · 돈을 낸 결제 · 이 플레이어가 결제) →
/// 색인(토큰 해시 → 플레이어) → 장부를 write lock으로 읽고-더하고-쓰기 (충돌하면 다시 읽고 처음부터) → 구글에 결제 확정(acknowledge).
/// GetPurchases: 환불 정리(10분에 한 번, 게임 전체) → 장부(없으면 빈 장부를 만든다) → 못 한 확정 다시.
/// 모든 호출은 한 함수 안에서 Budget(12초) 안에 끝낸다 (Cloud Code 한도 15초). 토큰 · 비밀값은 기록에 남기지 않는다 (토큰 해시 앞 8자만).
/// </summary>
public sealed class PurchaseVerifier
{
    /// <summary>쓰기 충돌을 이만큼까지 다시 시도한다 (같은 플레이어가 동시에 몇 건을 보내도 충분)</summary>
    public const int MaxAttempts = 5;
    /// <summary>구글 구매 토큰은 수백 자다. 이보다 길면 받지 않는다</summary>
    public const int MaxTokenLength = 2048;
    /// <summary>계정을 지운 뒤 결제 기록을 보관하는 햇수 (전자상거래법 — 대금 결제 · 재화 공급 기록 5년)</summary>
    public const int RetentionYears = 5;
    /// <summary>환불 목록 받기 간격 (구글 API 하루 한도 안에서 넉넉하게)</summary>
    public static readonly TimeSpan JobInterval = TimeSpan.FromMinutes(10);
    /// <summary>구글은 확정하지 않은 결제를 3일 뒤 환불한다 → 그 뒤로는 확정을 다시 시도하지 않고 남긴 토큰도 지운다</summary>
    public static readonly TimeSpan AckWindow = TimeSpan.FromDays(3);
    /// <summary>환불 목록은 지난번 본 시각보다 이만큼 앞부터 다시 본다 (늦게 들어온 칸 대비, 겹쳐도 표시는 한 번)</summary>
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

    /// <summary>SHA-256 소문자 16진수 (게임의 IapLedger.Hash와 같다). 토큰 장부 키 · 구글 계정 표시(obfuscatedAccountId)에 쓴다</summary>
    public static string Hash(string text) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text))).ToLowerInvariant();

    /// <summary>지운 계정 보관본의 ID — 게임 계정 ID를 되돌릴 수 없게 바꾼 것</summary>
    public static string ArchiveId(string playerId) => Hash("archive:" + playerId);

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
            (List<PurchaseRecord> ledger, string writeLock) = await LoadOrCreateAsync(context, playerId, ct);
            PurchaseRecord? known = ledger.Find(p => p.TokenHash == tokenHash);
            if (known != null) return await AnswerKnownAsync(context, playerId, known, ct);

            GoogleCheck check = await _google.GetPurchaseAsync(context, item.Id, purchaseToken, ct);
            if (check.Result == GoogleCheck.Kind.NotConfigured) return VerifyResult.Of(VerifyResult.NotConfigured);
            if (check.Result != GoogleCheck.Kind.Ok || check.Found == null)
            {
                _log.LogWarning("구글 확인 실패 {Product} {Tag}: {Result} {Reason}", item.Id, tag, check.Result, check.Reason);
                return VerifyResult.Of(check.Result == GoogleCheck.Kind.Invalid ? VerifyResult.Invalid : VerifyResult.Retry, check.Reason);
            }

            GoogleCheck.Purchase found = check.Found;
            // 대기 중(현금 결제 등)은 아직 돈이 들어오지 않았다: 지급하지 않고 끝나면 다시 묻게 한다. 상태 칸이 없으면 결제된 것으로 보지 않는다
            if (found.PurchaseState == 2) return VerifyResult.Of(VerifyResult.Retry, "pending");
            if (found.PurchaseState == null) return VerifyResult.Of(VerifyResult.Retry, "state_missing");
            string? problem = Problem(found, item.Id, playerId);
            if (problem != null)
            {
                _log.LogWarning("결제 거절 {Product} {Tag}: {Problem}", item.Id, tag, problem);
                return VerifyResult.Of(VerifyResult.Invalid, problem);
            }

            // 색인을 지급보다 먼저: 지급 기록이 있는데 색인이 없으면 환불 목록의 토큰으로 이 장부를 찾지 못한다
            await _store.SetIndexAsync(context, tokenHash, playerId, ct);

            for (int attempt = 1; attempt <= MaxAttempts; attempt++)
            {
                if (attempt > 1)
                {
                    (ledger, writeLock) = await LoadOrCreateAsync(context, playerId, ct);
                    known = ledger.Find(p => p.TokenHash == tokenHash);
                    if (known != null) return await AnswerKnownAsync(context, playerId, known, ct);
                }

                // 계정당 1번 상품: 이 계정 장부에 이미 있으면 지급하지 않는다 (토큰이 달라도, 환불된 것이라도)
                if (item.OncePerAccount && ledger.Exists(p => p.ProductId == item.Id))
                    return VerifyResult.Of(VerifyResult.AlreadyClaimed);

                PurchaseRecord record = Decide(ledger, item, tokenHash, found, _utcNow());
                record.Acknowledged = found.Settled;
                record.AckToken = found.Settled ? null : purchaseToken; // 확정이 끝나면 바로 지운다
                var next = new List<PurchaseRecord>(ledger) { record };
                // 지급은 언제나 읽은 판의 lock을 붙여 쓴다 (lock 없이 쓰는 건 빈 장부 만들기뿐)
                if (!await _store.TrySaveAsync(context, playerId, next, writeLock, ct))
                {
                    _log.LogInformation("장부 쓰기 충돌 → 다시 판정 {Tag} ({Attempt})", tag, attempt);
                    continue;
                }

                // 지급이 장부에 남았다 → 구글에 확정. 실패해도 지급은 그대로 (다음 GetPurchases · VerifyPurchase가 다시 확정한다)
                if (!record.Acknowledged) record = await AcknowledgeAsync(context, playerId, record, ct) ?? record;
                _log.LogInformation("지급 {Product} {Tag} 유료 {Paid} 무료 {Free} 첫2배 {First} 테스트 {Test} 확정 {Ack}",
                    item.Id, tag, record.PaidGems, record.FreeGems, record.FirstBonus, record.Test, record.Acknowledged);
                return VerifyResult.Of(VerifyResult.Granted, purchase: record.ForClient());
            }

            _log.LogWarning("장부 쓰기 충돌이 {Max}번 이어짐 {Tag}", MaxAttempts, tag);
            return VerifyResult.Of(VerifyResult.Retry, "busy");
        }
        catch (Exception e)
        {
            // 저장소 · 네트워크 오류 · 시간 초과: 지급을 정하지 못했다 (이미 썼다면 다음에 다시 물을 때 already로 답한다)
            _log.LogError("결제 검증 오류 {Tag}: {Error}", tag, Describe(e));
            return VerifyResult.Of(VerifyResult.Retry, e is OperationCanceledException ? "timeout" : "error");
        }
    }

    /// <summary>이미 장부에 있는 토큰: 환불됐으면 voided, 아니면 already (확정을 못 했으면 이참에 다시)</summary>
    private async Task<VerifyResult> AnswerKnownAsync(IExecutionContext context, string playerId, PurchaseRecord known, CancellationToken ct)
    {
        if (known.Voided) return VerifyResult.Of(VerifyResult.Voided, purchase: known.ForClient());
        if (!known.Acknowledged && known.AckToken != null) known = await AcknowledgeAsync(context, playerId, known, ct) ?? known;
        return VerifyResult.Of(VerifyResult.Already, purchase: known.ForClient());
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
            await RunJobIfDueAsync(context, ct); // 환불 정리 먼저 (이 플레이어 장부에도 voided가 붙을 수 있다)
            (List<PurchaseRecord> ledger, string writeLock) = await LoadOrCreateAsync(context, playerId, ct);
            ledger = await RetryAcksAsync(context, playerId, ledger, writeLock, ct);
            var list = new PurchaseList { Purchases = ledger.ConvertAll(p => p.ForClient()) };
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

    /// <summary>
    /// 장부와 lock. 없으면 빈 장부를 lock 없이 만들고 다시 읽어 lock을 얻는다 — 결제를 lock 없이 쓰는 일은 없다.
    /// ponytail: 두 호출이 동시에 "없음"을 보고 둘 다 빈 장부를 쓰면, 늦게 쓴 빈 장부가 그사이 다른 호출이 lock으로 더한 결제를 덮을 수 있다.
    /// 그러려면 늦은 쪽의 "읽기 → 빈 장부 쓰기" 한 번이 다른 쪽의 "빈 장부 쓰기 → 다시 읽기 → 구글 확인 → 색인 → 지급 쓰기" 전체보다 길어야 한다.
    /// 게다가 게임은 GetPurchases(로그인 직후)가 장부를 만든 뒤에만 팔아서 결제 때는 장부가 이미 있다 → 사실상 일어나지 않는다. 완전히 막으려면 플레이어별 잠금 서비스
    /// </summary>
    private async Task<(List<PurchaseRecord> ledger, string writeLock)> LoadOrCreateAsync(IExecutionContext context, string playerId, CancellationToken ct)
    {
        (List<PurchaseRecord> ledger, string? writeLock) = await _store.LoadAsync(context, playerId, ct);
        if (writeLock != null) return (ledger, writeLock);
        await _store.TrySaveAsync(context, playerId, new List<PurchaseRecord>(), null, ct);
        (ledger, writeLock) = await _store.LoadAsync(context, playerId, ct);
        return writeLock != null ? (ledger, writeLock) : throw new InvalidOperationException("ledger_create");
    }

    // ------------------------------------------------------------------ 구글 확정 (acknowledge)

    /// <summary>구글에 확정하고 장부에 "확정됨"을 적는다. 못 하면 null (지급은 그대로, 다음에 다시). 예외를 밖으로 내지 않는다</summary>
    private async Task<PurchaseRecord?> AcknowledgeAsync(IExecutionContext context, string playerId, PurchaseRecord record, CancellationToken ct)
    {
        try
        {
            if (record.AckToken == null || !await _google.AcknowledgeAsync(context, record.ProductId, record.AckToken, ct))
            {
                _log.LogWarning("구글 확정 실패 → 다음에 다시 {Tag}", record.TokenHash[..8]);
                return null;
            }

            return await UpdateAsync(context, playerId, record.TokenHash, r =>
            {
                r.Acknowledged = true;
                r.AckToken = null;
            }, ct);
        }
        catch (Exception e)
        {
            _log.LogWarning("구글 확정 오류 → 다음에 다시 {Tag}: {Error}", record.TokenHash[..8], Describe(e));
            return null;
        }
    }

    /// <summary>
    /// 확정을 못 한 결제를 다시 확정한다 (GetPurchases). 3일이 지난 것은 구글이 이미 환불했으니 남긴 토큰만 지운다 (환불 정리가 회수한다).
    /// 바꾼 게 있으면 한 번에 쓴다 (충돌이면 그대로 두고 다음에). 돌려주는 값: 지금 장부
    /// </summary>
    private async Task<List<PurchaseRecord>> RetryAcksAsync(IExecutionContext context, string playerId, List<PurchaseRecord> ledger, string writeLock, CancellationToken ct)
    {
        DateTime now = _utcNow();
        bool changed = false;
        foreach (PurchaseRecord p in ledger)
        {
            if (p.Acknowledged || p.AckToken == null || p.Voided) continue;
            try
            {
                if (now - ParseUtc(p.Utc) > AckWindow) p.AckToken = null;
                else if (await _google.AcknowledgeAsync(context, p.ProductId, p.AckToken, ct))
                {
                    p.Acknowledged = true;
                    p.AckToken = null;
                }
                else continue;
                changed = true;
            }
            catch (Exception e) when (e is not OperationCanceledException)
            {
                _log.LogWarning("구글 확정 다시 실패 {Tag}: {Error}", p.TokenHash[..8], Describe(e));
            }
        }

        if (changed && !await _store.TrySaveAsync(context, playerId, ledger, writeLock, ct))
            (ledger, _) = await _store.LoadAsync(context, playerId, ct); // 그사이 바뀌었다: 확정 표시는 다음에 다시 (구글 확정은 이미 됐다)
        return ledger;
    }

    /// <summary>장부의 한 결제를 바꿔 lock으로 쓴다 (충돌하면 다시 읽고). 못 찾거나 계속 충돌하면 null</summary>
    private async Task<PurchaseRecord?> UpdateAsync(IExecutionContext context, string playerId, string tokenHash, Action<PurchaseRecord> change, CancellationToken ct)
    {
        for (int attempt = 0; attempt < MaxAttempts; attempt++)
        {
            (List<PurchaseRecord> ledger, string? writeLock) = await _store.LoadAsync(context, playerId, ct);
            PurchaseRecord? record = ledger.Find(p => p.TokenHash == tokenHash);
            if (record == null || writeLock == null) return null;
            change(record);
            if (await _store.TrySaveAsync(context, playerId, ledger, writeLock, ct)) return record;
        }

        return null;
    }

    // ------------------------------------------------------------------ 환불 정리 · 보관본 지우기 (게임 전체, GetPurchases 때 10분에 한 번)

    /// <summary>
    /// 게임 전체 정리 작업: 구글 환불 목록(voidedpurchases)을 받아 색인으로 플레이어를 찾아 장부에 voided를 붙이고, 5년이 지난 보관본을 지운다.
    /// 작업 상태에 lock으로 "잡기"를 써서 동시에 두 번 돌지 않게 한다. 오류는 삼킨다 (다음 차례에 다시).
    /// ponytail: 예약 실행(UGS Scheduler)이 아니라 플레이어 호출에 얹었다 — 아무도 접속하지 않으면 정리도 멈춘다. 예약을 에디터에서 배포할 수 없어서 (Scheduler 패키지 필요)
    /// </summary>
    internal async Task RunJobIfDueAsync(IExecutionContext context, CancellationToken ct)
    {
        // 작업은 함수 시간의 절반까지만 쓴다 (나머지는 부른 플레이어 장부 몫)
        using var jobBudget = CancellationTokenSource.CreateLinkedTokenSource(ct);
        jobBudget.CancelAfter(_budget / 2);
        CancellationToken jt = jobBudget.Token;
        try
        {
            DateTime now = _utcNow();
            long nowMs = new DateTimeOffset(now).ToUnixTimeMilliseconds();
            (JobState state, string? writeLock) = await _store.LoadJobAsync(context, jt);
            if (writeLock != null && nowMs - state.LastRunMs < (long)JobInterval.TotalMilliseconds) return;
            state.LastRunMs = nowMs;
            string? claimed = await _store.TrySaveJobAsync(context, state, writeLock, jt);
            if (claimed == null) return; // 다른 호출이 먼저 잡았다

            await SyncVoidedAsync(context, state, now, jt);
            await PurgeArchivesAsync(context, state, now, jt);
            await _store.TrySaveJobAsync(context, state, claimed, jt);
        }
        catch (Exception e) when (!ct.IsCancellationRequested)
        {
            _log.LogWarning("정리 작업 실패 (다음에 다시): {Error}", Describe(e));
        }
    }

    private async Task SyncVoidedAsync(IExecutionContext context, JobState state, DateTime now, CancellationToken ct)
    {
        long nowMs = new DateTimeOffset(now).ToUnixTimeMilliseconds();
        long start = Math.Max(state.VoidedCheckpointMs - (long)VoidedOverlap.TotalMilliseconds, nowMs - (long)VoidedMaxLookback.TotalMilliseconds);
        List<VoidedPurchase>? voided = await _google.ListVoidedAsync(context, start, ct);
        if (voided == null) return; // 준비 안 됨 · 구글 오류: 체크포인트 그대로 (다음에 같은 구간부터)

        var hashes = new Dictionary<string, string?>();
        foreach (VoidedPurchase v in voided)
            if (!string.IsNullOrEmpty(v.PurchaseToken)) hashes[Hash(v.PurchaseToken)] = v.OrderId;
        Dictionary<string, string> owners = hashes.Count == 0 ? new() : await _store.FindIndexAsync(context, hashes.Keys.ToList(), ct);
        int marked = 0;
        foreach (IGrouping<string, KeyValuePair<string, string>> player in owners.GroupBy(o => o.Value))
        {
            var mine = new HashSet<string>(player.Select(o => o.Key));
            var orders = new HashSet<string>(mine.Select(h => hashes[h]).Where(o => !string.IsNullOrEmpty(o))!);
            marked += await MarkVoidedAsync(context, player.Key, mine, orders, now, ct);
        }

        if (voided.Count > 0) state.VoidedCheckpointMs = Math.Max(state.VoidedCheckpointMs, voided.Max(v => v.VoidedTimeMillis));
        _log.LogInformation("환불 목록 {Count}건 · 우리 결제 {Known}건 · 새로 표시 {Marked}건", voided.Count, owners.Count, marked);
    }

    /// <summary>그 플레이어 장부에서 토큰 해시(또는 주문 번호)가 맞는 결제에 voided를 붙인다. 붙인 수</summary>
    private async Task<int> MarkVoidedAsync(IExecutionContext context, string playerId, HashSet<string> tokenHashes, HashSet<string> orderIds, DateTime now, CancellationToken ct)
    {
        for (int attempt = 0; attempt < MaxAttempts; attempt++)
        {
            (List<PurchaseRecord> ledger, string? writeLock) = await _store.LoadAsync(context, playerId, ct);
            int count = 0;
            foreach (PurchaseRecord p in ledger)
            {
                if (p.Voided || !(tokenHashes.Contains(p.TokenHash) || (p.OrderId != null && orderIds.Contains(p.OrderId)))) continue;
                p.Voided = true;
                p.VoidedUtc = now.ToString("o", CultureInfo.InvariantCulture);
                p.AckToken = null;
                count++;
            }

            if (count == 0 || writeLock == null) return 0;
            if (await _store.TrySaveAsync(context, playerId, ledger, writeLock, ct)) return count;
        }

        return 0;
    }

    /// <summary>보관 기한(지운 달의 마지막 날 + 5년)이 지난 달의 보관본을 지운다 (한 번에 두 달까지)</summary>
    private async Task PurgeArchivesAsync(IExecutionContext context, JobState state, DateTime now, CancellationToken ct)
    {
        for (int i = 0; i < 2; i++)
        {
            DateTime next = DateTime.ParseExact(state.PurgedThrough, "yyyyMM", CultureInfo.InvariantCulture).AddMonths(1);
            if (next.AddMonths(1).AddYears(RetentionYears) > now) break;
            string month = next.ToString("yyyyMM", CultureInfo.InvariantCulture);
            await _store.DeleteArchiveMonthAsync(context, month, ct);
            _log.LogInformation("보관 기한이 지난 결제 기록을 지움: {Month}", month);
            state.PurgedThrough = month;
        }
    }

    // ------------------------------------------------------------------ 계정 삭제

    /// <summary>
    /// 계정 삭제 (게임의 계정 삭제가 Cloud Save · UGS 계정을 지우기 전에 부른다): 장부를 보관본(되돌릴 수 없는 ID, 5년 뒤 지움)에 옮기고,
    /// 색인과 장부를 지운다. 장부가 없으면 할 일 없음. 실패하면 retry — 게임은 계정 삭제를 멈춘다
    /// </summary>
    public async Task<ArchiveResult> ArchiveAndDeleteAsync(IExecutionContext context)
    {
        string? playerId = context.PlayerId;
        if (string.IsNullOrEmpty(playerId)) return new ArchiveResult { Status = VerifyResult.Invalid };
        using var budget = new CancellationTokenSource(_budget);
        CancellationToken ct = budget.Token;
        try
        {
            (List<PurchaseRecord> ledger, string? writeLock) = await _store.LoadAsync(context, playerId, ct);
            if (writeLock == null) return new ArchiveResult();
            if (ledger.Count > 0)
            {
                DateTime now = _utcNow();
                await _store.AppendArchiveAsync(context, now, new ArchivedPlayer
                {
                    Id = ArchiveId(playerId),
                    DeletedUtc = now.ToString("o", CultureInfo.InvariantCulture),
                    DeleteAfterUtc = now.AddYears(RetentionYears).ToString("o", CultureInfo.InvariantCulture),
                    Purchases = ledger.ConvertAll(p => p.ForClient()),
                }, ct);
                await _store.DeleteIndexAsync(context, ledger.ConvertAll(p => p.TokenHash), ct);
            }

            await _store.DeleteAsync(context, playerId, ct);
            _log.LogInformation("계정 삭제: 결제 {Count}건 보관 · 장부 지움", ledger.Count);
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
        // 다른 플레이어가 결제한 토큰을 들고 와도 받지 못한다 (장부가 플레이어별이라 계정 표시로 막는다)
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
            Utc = utcNow.ToUniversalTime().ToString("o", CultureInfo.InvariantCulture),
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
