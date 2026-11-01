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
    /// <summary>구글 호출 하나의 한도. Cloud Code 함수 전체 한도가 15초라 토큰 받기 + 구매 확인 + 장부 쓰기가 들어가게 짧게 둔다</summary>
    private static readonly TimeSpan GoogleTimeout = TimeSpan.FromSeconds(5);

    public void Setup(ICloudCodeConfig config)
    {
        config.AddGameApiClient();
        config.Dependencies.AddSingleton<IPurchaseStore>(sp => new CloudSavePurchaseStore(sp.GetRequiredService<IGameApiClient>()));
        config.Dependencies.AddSingleton<IGooglePlay>(sp =>
        {
            IGameApiClient api = sp.GetRequiredService<IGameApiClient>();
            return new GooglePlayClient(context => ReadSecret(api, context), new HttpClient { Timeout = GoogleTimeout });
        });
    }

    /// <summary>비밀값 글. 아직 만들지 않았으면 null (그 밖의 오류는 그대로 올린다 → 검증은 retry)</summary>
    private static async Task<string?> ReadSecret(IGameApiClient api, IExecutionContext context)
    {
        try
        {
            return (await api.SecretManager.GetSecret(context, GooglePlayClient.SecretName))?.Value;
        }
        catch (ApiException e) when (e.Response?.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }
    }
}
