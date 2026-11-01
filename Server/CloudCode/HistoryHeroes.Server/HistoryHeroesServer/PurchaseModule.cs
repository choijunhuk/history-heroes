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

    /// <summary>이 플레이어의 서버 장부 (게임이 로그인 뒤 이 기기 장부와 맞춘다 — 환불된 것은 회수) + 결제 검증 준비 여부. 예약 실행의 대비로 10분에 한 번 정리 작업도 여기서</summary>
    [CloudCodeFunction("GetPurchases")]
    public Task<PurchaseList> GetPurchases(IExecutionContext context) => _verifier.GetPurchasesAsync(context);

    /// <summary>계정 삭제 직전에만: 이 플레이어를 닫고(되돌릴 수 없음) 결제를 보관본(5년 뒤 지움)으로 옮긴 뒤 장부를 지운다</summary>
    [CloudCodeFunction("ArchiveAndDeletePurchases")]
    public Task<ArchiveResult> ArchiveAndDeletePurchases(IExecutionContext context) => _verifier.ArchiveAndDeleteAsync(context);

    /// <summary>게스트가 이미 있는 카카오 계정으로 바꾸기 직전: 게스트를 닫고 결제 이전권을 받는다</summary>
    [CloudCodeFunction("PrepareTransfer")]
    public Task<TransferResult> PrepareTransfer(IExecutionContext context) => _verifier.PrepareTransferAsync(context);

    /// <summary>바꾼 계정으로 들어간 뒤: 이전권으로 게스트 결제를 이 계정으로 옮긴다</summary>
    [CloudCodeFunction("ClaimTransfer")]
    public Task<TransferResult> ClaimTransfer(IExecutionContext context, string ticket) => _verifier.ClaimTransferAsync(context, ticket);

    /// <summary>정리 작업 (UGS Scheduler → Trigger가 10분마다 부른다, 플레이어 호출은 거절): 환불 반영 · 확정 대기 정리 · 기한 지난 보관본 지우기</summary>
    [CloudCodeFunction("RunMaintenance")]
    public Task<MaintenanceResult> RunMaintenance(IExecutionContext context) => _verifier.RunMaintenanceAsync(context);
}
