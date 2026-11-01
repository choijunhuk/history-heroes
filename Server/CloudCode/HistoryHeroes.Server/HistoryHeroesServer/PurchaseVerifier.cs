using System.Security.Cryptography;
using System.Text;
using HistoryHeroes.Economy;
using Microsoft.Extensions.Logging;
using Unity.Services.CloudCode.Core;

namespace HistoryHeroes.Server;

/// <summary>
/// 결제 검증과 지급 판정 (Monetization 7-1). 지급 수량 · 계정당 1번 · 첫 구매 2배는 서버 장부로만 정한다 — 게임은 받은 수량을 그대로 넣는다.
/// 순서: 장부에 이 토큰이 있으면 already → 구글에 구매 확인 (결제됨 · 같은 상품 · 수량 1 · 이 플레이어가 결제) →
/// 장부를 write lock으로 읽고-더하고-쓰기 (충돌하면 다시 읽고 처음부터 판정).
/// 토큰 · 비밀값은 기록에 남기지 않는다 (토큰 해시 앞 8자만).
/// </summary>
public sealed class PurchaseVerifier
{
    /// <summary>쓰기 충돌을 이만큼까지 다시 시도한다 (같은 플레이어가 동시에 몇 건을 보내도 충분)</summary>
    public const int MaxAttempts = 5;
    /// <summary>구글 구매 토큰은 수백 자다. 이보다 길면 받지 않는다</summary>
    public const int MaxTokenLength = 2048;

    private readonly IPurchaseStore _store;
    private readonly IGooglePlay _google;
    private readonly ILogger _log;
    private readonly Func<DateTime> _utcNow;

    public PurchaseVerifier(IPurchaseStore store, IGooglePlay google, ILogger log, Func<DateTime>? utcNow = null)
    {
        _store = store;
        _google = google;
        _log = log;
        _utcNow = utcNow ?? (() => DateTime.UtcNow);
    }

    /// <summary>SHA-256 소문자 16진수 (게임의 IapLedger.Hash와 같다). 토큰 장부 키 · 구글 계정 표시(obfuscatedAccountId)에 쓴다</summary>
    public static string Hash(string text) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text))).ToLowerInvariant();

    public async Task<VerifyResult> VerifyAsync(IExecutionContext context, string? productId, string? purchaseToken)
    {
        string? playerId = context.PlayerId;
        if (string.IsNullOrEmpty(playerId)) return VerifyResult.Of(VerifyResult.Invalid, "no_player");
        IapCatalog.Item? item = string.IsNullOrEmpty(productId) ? null : IapCatalog.Find(productId);
        if (item == null) return VerifyResult.Of(VerifyResult.Invalid, "unknown_product");
        if (string.IsNullOrEmpty(purchaseToken) || purchaseToken.Length > MaxTokenLength) return VerifyResult.Of(VerifyResult.Invalid, "bad_token");

        string tokenHash = Hash(purchaseToken);
        string tag = tokenHash[..8];
        try
        {
            (List<PurchaseRecord> ledger, string? writeLock) = await _store.LoadAsync(context);
            PurchaseRecord? known = ledger.Find(p => p.TokenHash == tokenHash);
            if (known != null) return VerifyResult.Of(VerifyResult.Already, purchase: known);

            GoogleCheck check = await _google.GetPurchaseAsync(context, item.Id, purchaseToken);
            if (check.Result == GoogleCheck.Kind.NotConfigured) return VerifyResult.Of(VerifyResult.NotConfigured);
            if (check.Result != GoogleCheck.Kind.Ok || check.Found == null)
            {
                _log.LogWarning("구글 확인 실패 {Product} {Tag}: {Result} {Reason}", item.Id, tag, check.Result, check.Reason);
                return VerifyResult.Of(check.Result == GoogleCheck.Kind.Invalid ? VerifyResult.Invalid : VerifyResult.Retry, check.Reason);
            }

            GoogleCheck.Purchase found = check.Found;
            // 대기 중(현금 결제 등)은 아직 돈이 들어오지 않았다: 지급하지 않고 끝나면 다시 묻게 한다
            if (found.PurchaseState == 2) return VerifyResult.Of(VerifyResult.Retry, "pending");
            string? problem = Problem(found, item.Id, playerId);
            if (problem != null)
            {
                _log.LogWarning("결제 거절 {Product} {Tag}: {Problem}", item.Id, tag, problem);
                return VerifyResult.Of(VerifyResult.Invalid, problem);
            }

            for (int attempt = 1; attempt <= MaxAttempts; attempt++)
            {
                if (attempt > 1)
                {
                    (ledger, writeLock) = await _store.LoadAsync(context);
                    known = ledger.Find(p => p.TokenHash == tokenHash);
                    if (known != null) return VerifyResult.Of(VerifyResult.Already, purchase: known);
                }

                // 계정당 1번 상품: 이 계정 장부에 이미 있으면 지급하지 않는다 (토큰이 달라도)
                if (item.OncePerAccount && ledger.Exists(p => p.ProductId == item.Id))
                    return VerifyResult.Of(VerifyResult.AlreadyClaimed);

                PurchaseRecord record = Decide(ledger, item, tokenHash, found, _utcNow());
                var next = new List<PurchaseRecord>(ledger) { record };
                if (!await _store.TrySaveAsync(context, next, writeLock))
                {
                    _log.LogInformation("장부 쓰기 충돌 → 다시 판정 {Tag} ({Attempt})", tag, attempt);
                    continue;
                }

                if (writeLock == null)
                {
                    // ponytail: 처음 만들기는 lock 없이 쓴다 (Cloud Save에 "없을 때만 만들기"가 없음). 같은 플레이어의 첫 결제 두 건이 같은 순간에 오면
                    // 서로 덮을 수 있어 바로 다시 읽어 내 기록이 남았는지 본다 — 그 뒤에 늦게 덮이는 아주 짧은 틈은 남는다. 완전히 막으려면 플레이어별 잠금 서비스
                    (List<PurchaseRecord> after, _) = await _store.LoadAsync(context);
                    if (!after.Exists(p => p.TokenHash == tokenHash))
                    {
                        _log.LogInformation("처음 만든 장부가 덮임 → 다시 판정 {Tag}", tag);
                        continue;
                    }
                }

                _log.LogInformation("지급 {Product} {Tag} 유료 {Paid} 무료 {Free} 첫2배 {First} 테스트 {Test}",
                    item.Id, tag, record.PaidGems, record.FreeGems, record.FirstBonus, record.Test);
                return VerifyResult.Of(VerifyResult.Granted, purchase: record);
            }

            _log.LogWarning("장부 쓰기 충돌이 {Max}번 이어짐 {Tag}", MaxAttempts, tag);
            return VerifyResult.Of(VerifyResult.Retry, "busy");
        }
        catch (Exception e)
        {
            // 저장소 · 네트워크 오류: 지급을 정하지 못했다 (이미 썼다면 다음에 다시 물을 때 already로 답한다)
            _log.LogError("결제 검증 오류 {Tag}: {Error}", tag, e.GetType().Name + ": " + e.Message);
            return VerifyResult.Of(VerifyResult.Retry, "error");
        }
    }

    public async Task<PurchaseList> GetPurchasesAsync(IExecutionContext context)
    {
        var list = new PurchaseList();
        if (string.IsNullOrEmpty(context.PlayerId)) return list;
        (list.Purchases, _) = await _store.LoadAsync(context);
        try
        {
            list.Configured = await _google.IsConfiguredAsync(context);
        }
        catch (Exception e)
        {
            // 비밀값을 못 읽으면 "준비 안 됨"으로 답한다 (장부는 그대로 돌려줘야 게임이 맞출 수 있다)
            _log.LogWarning("비밀값 확인 실패: {Error}", e.GetType().Name + ": " + e.Message);
        }

        return list;
    }

    /// <summary>구글이 확인해 준 구매를 이 플레이어 · 이 상품으로 받아도 되나. 문제가 없으면 null</summary>
    internal static string? Problem(GoogleCheck.Purchase found, string productId, string playerId)
    {
        if (found.PurchaseState != 0) return "state_" + found.PurchaseState;
        if (found.ProductId != null && found.ProductId != productId) return "product_mismatch";
        // 여러 개 사기는 콘솔에서 껐다. 켜져 있으면 1개 값만 주게 되니 지급하지 않는다 (확정 안 함 → 구글이 환불)
        if ((found.Quantity ?? 1) != 1) return "quantity";
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
            Utc = utcNow.ToUniversalTime().ToString("o"),
            PaidGems = item.PaidGems,
            FreeGems = item.FreeGems + (first ? item.PaidGems : 0),
            Tickets = item.Tickets,
            Gold = item.Gold,
            FirstBonus = first,
            Test = found.PurchaseType == 0,
        };
    }
}
