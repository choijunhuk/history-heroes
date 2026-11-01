using Unity.Services.CloudCode.Core;

namespace HistoryHeroes.Server;

/// <summary>플레이어별 서버 장부 (실제는 CloudSavePurchaseStore, 테스트는 가짜)</summary>
public interface IPurchaseStore
{
    /// <summary>장부와 그 판의 write lock. 키가 아직 없으면 빈 장부 + null</summary>
    Task<(List<PurchaseRecord> purchases, string? writeLock)> LoadAsync(IExecutionContext context);

    /// <summary>읽은 판의 lock을 붙여 쓴다. 그사이 다른 호출이 썼으면 false (충돌). lock이 null이면 조건 없이 쓴다 (처음 만들기)</summary>
    Task<bool> TrySaveAsync(IExecutionContext context, List<PurchaseRecord> purchases, string? writeLock);
}
