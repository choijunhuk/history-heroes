using Unity.Services.CloudCode.Core;

namespace HistoryHeroes.Server;

/// <summary>구글 플레이 개발자 API (실제는 GooglePlayClient, 테스트는 가짜). 모든 호출은 함수 전체 시간 한도(ct) 안에서</summary>
public interface IGooglePlay
{
    /// <summary>서비스 계정 비밀값이 있고 읽을 수 있나 (구글에는 묻지 않는다)</summary>
    Task<bool> IsConfiguredAsync(IExecutionContext context, CancellationToken ct);

    /// <summary>purchases.products.get</summary>
    Task<GoogleCheck> GetPurchaseAsync(IExecutionContext context, string productId, string purchaseToken, CancellationToken ct);

    /// <summary>
    /// purchases.products.acknowledge. 확정됐거나, 이미 확정 · 소비 · 취소돼 더 할 일이 없으면 true. 실패하면 false (나중에 다시)
    /// </summary>
    Task<bool> AcknowledgeAsync(IExecutionContext context, string productId, string purchaseToken, CancellationToken ct);

    /// <summary>purchases.voidedpurchases.list (인앱 상품만, startTime부터, 몇 쪽까지). 비밀값이 없거나 못 받으면 null</summary>
    Task<List<VoidedPurchase>?> ListVoidedAsync(IExecutionContext context, long startTimeMillis, CancellationToken ct);
}
