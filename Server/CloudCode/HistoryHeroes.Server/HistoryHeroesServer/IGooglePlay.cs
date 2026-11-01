using Unity.Services.CloudCode.Core;

namespace HistoryHeroes.Server;

/// <summary>구글 플레이 개발자 API (실제는 GooglePlayClient, 테스트는 가짜)</summary>
public interface IGooglePlay
{
    /// <summary>서비스 계정 비밀값이 있고 읽을 수 있나 (구글에는 묻지 않는다)</summary>
    Task<bool> IsConfiguredAsync(IExecutionContext context);

    /// <summary>purchases.products.get</summary>
    Task<GoogleCheck> GetPurchaseAsync(IExecutionContext context, string productId, string purchaseToken);
}
