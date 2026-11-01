using System.Net;
using Microsoft.Extensions.DependencyInjection;
using Unity.Services.CloudCode.Apis;
using Unity.Services.CloudCode.Apis.Extensions;
using Unity.Services.CloudCode.Core;
using Unity.Services.CloudCode.Shared;

namespace HistoryHeroes.Server;

/// <summary>Cloud Code가 모듈을 올릴 때 한 번 부른다: 의존성 연결</summary>
public sealed class ModuleSetup : ICloudCodeSetup
{
    /// <summary>구글 호출 하나의 한도 (함수 전체는 PurchaseVerifier의 12초 안 — Cloud Code 한도 15초)</summary>
    private static readonly TimeSpan GoogleTimeout = TimeSpan.FromSeconds(5);

    public void Setup(ICloudCodeConfig config)
    {
        config.AddGameApiClient();
        config.Dependencies.AddSingleton<IPurchaseStore>(sp => new CloudSavePurchaseStore(sp.GetRequiredService<IGameApiClient>()));
        config.Dependencies.AddSingleton<IGooglePlay>(sp =>
        {
            IGameApiClient api = sp.GetRequiredService<IGameApiClient>();
            return new GooglePlayClient((context, ct) => ReadSecret(api, context, ct), new HttpClient { Timeout = GoogleTimeout });
        });
    }

    /// <summary>비밀값 글. 아직 만들지 않았으면 null (그 밖의 오류는 그대로 올린다 → 검증은 retry)</summary>
    private static async Task<string?> ReadSecret(IGameApiClient api, IExecutionContext context, CancellationToken ct)
    {
        try
        {
            return (await api.SecretManager.GetSecret(context, GooglePlayClient.SecretName).WaitAsync(ct))?.Value;
        }
        catch (ApiException e) when (e.Response?.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }
    }
}
