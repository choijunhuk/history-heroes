using Microsoft.Extensions.Logging;
using Unity.Services.CloudCode.Core;

namespace HistoryHeroes.Server;

/// <summary>
/// 게임이 부르는 결제 함수 (모듈 HistoryHeroesServer). 로그인한 플레이어만 부를 수 있고, 늘 부른 플레이어 자기 장부만 다룬다.
/// 게임 쪽: Game/Assets/Scripts/Economy/PurchaseServer.cs
/// </summary>
public sealed class PurchaseModule
{
    private readonly PurchaseVerifier _verifier;

    public PurchaseModule(IPurchaseStore store, IGooglePlay google, ILogger<PurchaseModule> logger)
    {
        _verifier = new PurchaseVerifier(store, google, logger);
    }

    /// <summary>구글 결제 하나를 확인하고 서버 장부로 지급을 정한다 (VerifyResult 상태 참고)</summary>
    [CloudCodeFunction("VerifyPurchase")]
    public Task<VerifyResult> VerifyPurchase(IExecutionContext context, string productId, string purchaseToken) =>
        _verifier.VerifyAsync(context, productId, purchaseToken);

    /// <summary>이 플레이어의 서버 장부 (게임이 로그인 뒤 이 기기 장부와 맞춘다 — 환불된 것은 회수) + 결제 검증 준비 여부. 10분에 한 번 게임 전체 환불 정리도 여기서</summary>
    [CloudCodeFunction("GetPurchases")]
    public Task<PurchaseList> GetPurchases(IExecutionContext context) => _verifier.GetPurchasesAsync(context);

    /// <summary>계정 삭제 직전: 이 플레이어 결제 장부를 보관본(5년 뒤 지움)으로 옮기고 장부 · 색인을 지운다</summary>
    [CloudCodeFunction("ArchiveAndDeletePurchases")]
    public Task<ArchiveResult> ArchiveAndDeletePurchases(IExecutionContext context) => _verifier.ArchiveAndDeleteAsync(context);
}
