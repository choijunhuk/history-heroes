using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Unity.Services.CloudCode.Core;

namespace HistoryHeroes.Server;

/// <summary>
/// 구글 플레이 개발자 API v3: purchases.products.get · acknowledge, purchases.voidedpurchases.list. 인증은 서비스 계정 JWT(RS256) → OAuth 액세스 토큰.
/// 서비스 계정 JSON은 UGS Secret Manager의 GOOGLE_PLAY_SERVICE_ACCOUNT에서 읽는다 (없으면 NotConfigured).
/// 한 작업자(모듈 인스턴스) 안에서 읽은 서비스 계정은 10분, 액세스 토큰은 만료 60초 전까지 다시 쓴다 (싱글턴).
/// 키 · 토큰 · JWT는 어디에도 기록하지 않는다. 오류는 HTTP 상태 + 구글이 준 짧은 이유 코드만 돌려준다.
/// </summary>
public sealed class GooglePlayClient : IGooglePlay
{
    public const string SecretName = "GOOGLE_PLAY_SERVICE_ACCOUNT";
    /// <summary>게임의 안드로이드 패키지 이름과 같아야 한다 (EditMode 시험 PackageName_MatchesAndroidIdentifier가 본다)</summary>
    public const string PackageName = "com.choijunhuk.historyheroes";
    private const string Scope = "https://www.googleapis.com/auth/androidpublisher";
    /// <summary>비밀값의 token_uri 대신 늘 이 주소 (서명한 JWT를 다른 곳에 보내지 않게)</summary>
    private const string TokenUrl = "https://oauth2.googleapis.com/token";
    private const string ApiBase = "https://androidpublisher.googleapis.com/androidpublisher/v3/applications/";
    /// <summary>환불 목록은 한 번에 1000건씩, 이만큼의 쪽까지 (10분마다 보니 넘칠 일이 없다)</summary>
    public const int MaxVoidedPages = 3;
    private static readonly TimeSpan AccountCacheTime = TimeSpan.FromMinutes(10);
    private static readonly TimeSpan TokenMargin = TimeSpan.FromSeconds(60);

    private readonly Func<IExecutionContext, CancellationToken, Task<string?>> _readSecret;
    private readonly HttpClient _http;
    private readonly Func<DateTime> _utcNow;
    private readonly SemaphoreSlim _tokenLock = new(1, 1);

    private ServiceAccount? _account;
    private DateTime _accountReadUtc;
    private string? _accessToken;
    private string? _accessTokenFor;
    private DateTime _accessTokenExpiresUtc;

    /// <param name="readSecret">비밀값 글 (없으면 null)</param>
    public GooglePlayClient(Func<IExecutionContext, CancellationToken, Task<string?>> readSecret, HttpClient http, Func<DateTime>? utcNow = null)
    {
        _readSecret = readSecret;
        _http = http;
        _utcNow = utcNow ?? (() => DateTime.UtcNow);
    }

    /// <summary>서비스 계정 JSON 중 쓰는 칸</summary>
    internal sealed class ServiceAccount
    {
        [JsonProperty("client_email")] public string? ClientEmail { get; set; }
        [JsonProperty("private_key")] public string? PrivateKey { get; set; }
        [JsonProperty("private_key_id")] public string? PrivateKeyId { get; set; }
    }

    private sealed class VoidedPage
    {
        [JsonProperty("voidedPurchases")] public List<VoidedPurchase>? VoidedPurchases { get; set; }
        [JsonProperty("tokenPagination")] public JObject? TokenPagination { get; set; }
    }

    public async Task<bool> IsConfiguredAsync(IExecutionContext context, CancellationToken ct) => await AccountAsync(context, ct) != null;

    public async Task<GoogleCheck> GetPurchaseAsync(IExecutionContext context, string productId, string purchaseToken, CancellationToken ct)
    {
        ServiceAccount? account = await AccountAsync(context, ct);
        if (account == null) return GoogleCheck.Of(GoogleCheck.Kind.NotConfigured);
        try
        {
            (string? accessToken, string authProblem) = await AccessTokenAsync(account, ct);
            if (accessToken == null) return GoogleCheck.Of(GoogleCheck.Kind.Retry, authProblem);

            using HttpResponseMessage response = await SendAsync(HttpMethod.Get, PurchaseUrl(productId, purchaseToken), accessToken, null, ct);
            string body = await response.Content.ReadAsStringAsync(ct);
            if (response.IsSuccessStatusCode)
            {
                var found = JsonConvert.DeserializeObject<GoogleCheck.Purchase>(body);
                return found == null ? GoogleCheck.Of(GoogleCheck.Kind.Retry, "google_empty") : new GoogleCheck { Result = GoogleCheck.Kind.Ok, Found = found };
            }

            if (response.StatusCode == HttpStatusCode.Unauthorized) _accessToken = null; // 토큰이 일찍 끝났다: 다음엔 새로
            return GoogleCheck.Of(KindOf(response.StatusCode), "google_" + (int)response.StatusCode + ErrorReason(body));
        }
        catch (Exception e) when (IsTransient(e, ct))
        {
            return GoogleCheck.Of(GoogleCheck.Kind.Retry, "google_" + e.GetType().Name);
        }
    }

    public async Task<bool> AcknowledgeAsync(IExecutionContext context, string productId, string purchaseToken, CancellationToken ct)
    {
        ServiceAccount? account = await AccountAsync(context, ct);
        if (account == null) return false;
        try
        {
            (string? accessToken, _) = await AccessTokenAsync(account, ct);
            if (accessToken == null) return false;
            using (HttpResponseMessage response = await SendAsync(HttpMethod.Post, PurchaseUrl(productId, purchaseToken) + ":acknowledge", accessToken,
                       new StringContent("{}", Encoding.UTF8, "application/json"), ct))
            {
                if (response.IsSuccessStatusCode) return true;
            }

            // 확정이 거절됐다: 이미 확정 · 소비(게임이 먼저 확정)됐거나 취소된 결제면 더 할 일이 없다 → 끝난 것으로 (끝없이 다시 하지 않게)
            GoogleCheck now = await GetPurchaseAsync(context, productId, purchaseToken, ct);
            return now.Result == GoogleCheck.Kind.Ok && now.Found != null && (now.Found.Settled || now.Found.PurchaseState == 1);
        }
        catch (Exception e) when (IsTransient(e, ct))
        {
            return false;
        }
    }

    public async Task<List<VoidedPurchase>?> ListVoidedAsync(IExecutionContext context, long startTimeMillis, CancellationToken ct)
    {
        ServiceAccount? account = await AccountAsync(context, ct);
        if (account == null) return null;
        try
        {
            (string? accessToken, _) = await AccessTokenAsync(account, ct);
            if (accessToken == null) return null;
            var all = new List<VoidedPurchase>();
            string? page = null;
            // ponytail: 쪽 수 한도를 넘으면 나머지는 다음 차례 (구글은 환불 시각 순으로 준다) — 10분에 3000건 넘는 환불이 생기면 쪽 수를 늘린다
            for (int i = 0; i < MaxVoidedPages; i++)
            {
                string url = ApiBase + PackageName + "/purchases/voidedpurchases?type=0&maxResults=1000&startTime="
                             + startTimeMillis.ToString(CultureInfo.InvariantCulture) + (page != null ? "&token=" + Uri.EscapeDataString(page) : "");
                using HttpResponseMessage response = await SendAsync(HttpMethod.Get, url, accessToken, null, ct);
                if (!response.IsSuccessStatusCode) return null;
                var body = JsonConvert.DeserializeObject<VoidedPage>(await response.Content.ReadAsStringAsync(ct));
                if (body?.VoidedPurchases != null) all.AddRange(body.VoidedPurchases);
                page = (string?)body?.TokenPagination?["nextPageToken"];
                if (string.IsNullOrEmpty(page)) break;
            }

            return all;
        }
        catch (Exception e) when (IsTransient(e, ct))
        {
            return null;
        }
    }

    /// <summary>
    /// 400 · 404 · 410: 구글이 이 토큰 · 상품 · 앱 조합을 모른다 (가짜 토큰 · 다른 앱 · 다른 상품) → 다시 물어도 같다.
    /// (막 산 결제의 404는 구글 쪽 반영이 늦은 것일 수 있어 게임이 산 뒤 10분 동안은 "확인 중"으로 다시 묻는다 — IapService.JustBoughtWindow)
    /// 401 · 403: 서비스 계정 권한 문제 (Play Console 초대 뒤 권한이 퍼지는 데 시간이 걸린다) → 결제를 버리지 않고 나중에 다시.
    /// 그 밖(429 · 5xx): 잠시 뒤 다시.
    /// </summary>
    internal static GoogleCheck.Kind KindOf(HttpStatusCode status) => (int)status switch
    {
        400 or 404 or 410 => GoogleCheck.Kind.Invalid,
        _ => GoogleCheck.Kind.Retry,
    };

    private static string PurchaseUrl(string productId, string purchaseToken) =>
        ApiBase + PackageName + "/purchases/products/" + Uri.EscapeDataString(productId) + "/tokens/" + Uri.EscapeDataString(purchaseToken);

    private Task<HttpResponseMessage> SendAsync(HttpMethod method, string url, string accessToken, HttpContent? content, CancellationToken ct)
    {
        var request = new HttpRequestMessage(method, url) { Content = content };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return _http.SendAsync(request, ct);
    }

    /// <summary>네트워크 · 응답 해석 실패 (시간 한도가 다 된 취소는 밖으로 올린다)</summary>
    private static bool IsTransient(Exception e, CancellationToken ct) =>
        e is HttpRequestException or JsonException || (e is OperationCanceledException && !ct.IsCancellationRequested);

    /// <summary>구글 오류 본문의 짧은 이유 코드 (":purchaseTokenNotFound", ":invalid_grant"). 메시지 전문은 쓰지 않는다</summary>
    internal static string ErrorReason(string body)
    {
        try
        {
            JToken? error = JObject.Parse(body)["error"];
            string? reason = error is JValue plain ? (string?)plain : (string?)error?["errors"]?[0]?["reason"] ?? (string?)error?["status"];
            if (string.IsNullOrEmpty(reason)) return string.Empty;
            var clean = new string(reason.Where(c => char.IsAsciiLetterOrDigit(c) || c is '_' or '.' or '-').Take(48).ToArray());
            return ":" + clean;
        }
        catch (JsonException)
        {
            return string.Empty;
        }
    }

    /// <summary>서비스 계정 (10분 동안 다시 쓴다. 없거나 깨졌으면 null — 이건 기억하지 않아 비밀값을 넣으면 바로 보인다)</summary>
    private async Task<ServiceAccount?> AccountAsync(IExecutionContext context, CancellationToken ct)
    {
        DateTime now = _utcNow();
        ServiceAccount? cached = _account;
        if (cached != null && now - _accountReadUtc < AccountCacheTime) return cached;
        ServiceAccount? account = Parse(await _readSecret(context, ct));
        _account = account;
        _accountReadUtc = now;
        return account;
    }

    private static ServiceAccount? Parse(string? json)
    {
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

    /// <summary>액세스 토큰 (만료 60초 전까지 다시 쓴다). 못 받으면 (null, "google_auth_{상태}:{오류 코드}")</summary>
    private async Task<(string? token, string problem)> AccessTokenAsync(ServiceAccount account, CancellationToken ct)
    {
        string owner = account.ClientEmail + "|" + account.PrivateKeyId;
        if (Fresh(owner) is { } cached) return (cached, string.Empty);
        await _tokenLock.WaitAsync(ct);
        try
        {
            if (Fresh(owner) is { } again) return (again, string.Empty);
            DateTime now = _utcNow();
            using var body = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = "urn:ietf:params:oauth:grant-type:jwt-bearer",
                ["assertion"] = BuildAssertion(account, new DateTimeOffset(now)),
            });
            using HttpResponseMessage response = await _http.PostAsync(TokenUrl, body, ct);
            string text = await response.Content.ReadAsStringAsync(ct);
            if (!response.IsSuccessStatusCode) return (null, "google_auth_" + (int)response.StatusCode + ErrorReason(text));
            var token = JsonConvert.DeserializeAnonymousType(text, new { access_token = "", expires_in = 0 });
            if (string.IsNullOrEmpty(token?.access_token)) return (null, "google_auth_empty");
            _accessToken = token.access_token;
            _accessTokenFor = owner;
            _accessTokenExpiresUtc = now + TimeSpan.FromSeconds(Math.Max(0, token.expires_in)) - TokenMargin;
            return (_accessToken, string.Empty);
        }
        finally
        {
            _tokenLock.Release();
        }
    }

    private string? Fresh(string owner) => _accessToken != null && _accessTokenFor == owner && _utcNow() < _accessTokenExpiresUtc ? _accessToken : null;

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
