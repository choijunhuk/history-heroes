using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;
using Unity.Services.CloudCode.Core;

namespace HistoryHeroes.Server;

/// <summary>
/// 구글 플레이 개발자 API v3 (purchases.products.get). 인증은 서비스 계정 JWT(RS256) → OAuth 액세스 토큰.
/// 서비스 계정 JSON은 UGS Secret Manager의 GOOGLE_PLAY_SERVICE_ACCOUNT에서 읽는다 (없으면 NotConfigured).
/// 키 · 토큰 · JWT는 어디에도 기록하지 않는다.
/// </summary>
public sealed class GooglePlayClient : IGooglePlay
{
    public const string SecretName = "GOOGLE_PLAY_SERVICE_ACCOUNT";
    public const string PackageName = "com.choijunhuk.historyheroes";
    private const string Scope = "https://www.googleapis.com/auth/androidpublisher";
    /// <summary>비밀값의 token_uri 대신 늘 이 주소 (서명한 JWT를 다른 곳에 보내지 않게)</summary>
    private const string TokenUrl = "https://oauth2.googleapis.com/token";
    private const string ApiBase = "https://androidpublisher.googleapis.com/androidpublisher/v3/applications/";

    private readonly Func<IExecutionContext, Task<string?>> _readSecret;
    private readonly HttpClient _http;

    /// <param name="readSecret">비밀값 글 (없으면 null)</param>
    public GooglePlayClient(Func<IExecutionContext, Task<string?>> readSecret, HttpClient http)
    {
        _readSecret = readSecret;
        _http = http;
    }

    /// <summary>서비스 계정 JSON 중 쓰는 칸</summary>
    internal sealed class ServiceAccount
    {
        [JsonProperty("client_email")] public string? ClientEmail { get; set; }
        [JsonProperty("private_key")] public string? PrivateKey { get; set; }
        [JsonProperty("private_key_id")] public string? PrivateKeyId { get; set; }
    }

    public async Task<bool> IsConfiguredAsync(IExecutionContext context) => await ReadAccount(context) != null;

    public async Task<GoogleCheck> GetPurchaseAsync(IExecutionContext context, string productId, string purchaseToken)
    {
        ServiceAccount? account = await ReadAccount(context);
        if (account == null) return GoogleCheck.Of(GoogleCheck.Kind.NotConfigured);

        try
        {
            string? accessToken = await FetchAccessToken(account);
            if (accessToken == null) return GoogleCheck.Of(GoogleCheck.Kind.Retry, "google_auth");

            string url = ApiBase + PackageName + "/purchases/products/" + Uri.EscapeDataString(productId) + "/tokens/" + Uri.EscapeDataString(purchaseToken);
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            using HttpResponseMessage response = await _http.SendAsync(request);
            if (response.IsSuccessStatusCode)
            {
                var found = JsonConvert.DeserializeObject<GoogleCheck.Purchase>(await response.Content.ReadAsStringAsync());
                return found == null ? GoogleCheck.Of(GoogleCheck.Kind.Retry, "google_empty") : new GoogleCheck { Result = GoogleCheck.Kind.Ok, Found = found };
            }

            return GoogleCheck.Of(KindOf(response.StatusCode), "google_" + (int)response.StatusCode);
        }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException or JsonException)
        {
            return GoogleCheck.Of(GoogleCheck.Kind.Retry, "google_" + e.GetType().Name);
        }
    }

    /// <summary>
    /// 400 · 404 · 410: 구글이 이 토큰 · 상품 · 앱 조합을 모른다 (가짜 토큰 · 다른 앱 · 다른 상품) → 다시 물어도 같다.
    /// 401 · 403: 서비스 계정 권한 문제 (Play Console 초대 뒤 권한이 퍼지는 데 시간이 걸린다) → 결제를 버리지 않고 나중에 다시.
    /// 그 밖(429 · 5xx): 잠시 뒤 다시.
    /// </summary>
    internal static GoogleCheck.Kind KindOf(HttpStatusCode status) => (int)status switch
    {
        400 or 404 or 410 => GoogleCheck.Kind.Invalid,
        _ => GoogleCheck.Kind.Retry,
    };

    private async Task<ServiceAccount?> ReadAccount(IExecutionContext context)
    {
        string? json = await _readSecret(context);
        if (string.IsNullOrWhiteSpace(json)) return null;
        try
        {
            var account = JsonConvert.DeserializeObject<ServiceAccount>(json);
            if (string.IsNullOrEmpty(account?.ClientEmail) || string.IsNullOrEmpty(account.PrivateKey)) return null;
            using RSA rsa = RSA.Create();
            rsa.ImportFromPem(account.PrivateKey); // 키가 깨졌으면 여기서 실패 → 준비 안 됨
            return account;
        }
        catch (Exception e) when (e is JsonException or ArgumentException or CryptographicException)
        {
            return null;
        }
    }

    private async Task<string?> FetchAccessToken(ServiceAccount account)
    {
        using var body = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "urn:ietf:params:oauth:grant-type:jwt-bearer",
            ["assertion"] = BuildAssertion(account, DateTimeOffset.UtcNow),
        });
        using HttpResponseMessage response = await _http.PostAsync(TokenUrl, body);
        if (!response.IsSuccessStatusCode) return null;
        var token = JsonConvert.DeserializeAnonymousType(await response.Content.ReadAsStringAsync(), new { access_token = "" });
        return string.IsNullOrEmpty(token?.access_token) ? null : token.access_token;
    }

    /// <summary>서비스 계정 JWT (RS256, 1시간). https://developers.google.com/identity/protocols/oauth2/service-account#jwt-auth</summary>
    internal static string BuildAssertion(ServiceAccount account, DateTimeOffset now)
    {
        string header = Base64Url(JsonConvert.SerializeObject(new { alg = "RS256", typ = "JWT", kid = account.PrivateKeyId },
            new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore }));
        string claims = Base64Url(JsonConvert.SerializeObject(new
        {
            iss = account.ClientEmail,
            scope = Scope,
            aud = TokenUrl,
            iat = now.ToUnixTimeSeconds(),
            exp = now.AddHours(1).ToUnixTimeSeconds(),
        }));
        using RSA rsa = RSA.Create();
        rsa.ImportFromPem(account.PrivateKey);
        byte[] signature = rsa.SignData(Encoding.ASCII.GetBytes(header + "." + claims), HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        return header + "." + claims + "." + Base64Url(signature);
    }

    private static string Base64Url(string text) => Base64Url(Encoding.UTF8.GetBytes(text));

    private static string Base64Url(byte[] bytes) => Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
