using System.Net;
using System.Security.Cryptography;
using System.Text;
using HistoryHeroes.Server;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace HistoryHeroesServer.Tests;

/// <summary>구글 호출: JWT 서명 · 토큰 캐시 · 상태 코드와 이유 코드 · 확정 · 환불 목록 · 비밀값이 없을 때 (실제 구글 대신 가짜 HTTP)</summary>
public sealed class GooglePlayClientTests
{
    private sealed class StubHandler : HttpMessageHandler
    {
        public readonly List<(HttpMethod method, string url, string body, string? auth)> Requests = new();
        public HttpStatusCode TokenStatus = HttpStatusCode.OK;
        public string TokenBody = "{\"access_token\":\"at-1\",\"expires_in\":3599}";
        public HttpStatusCode ApiStatus = HttpStatusCode.OK;
        public string ApiBody = "{\"purchaseState\":0,\"consumptionState\":0,\"acknowledgementState\":0,\"orderId\":\"GPA.1\",\"productId\":\"gems_t1\",\"quantity\":1,\"obfuscatedExternalAccountId\":\"abc\"}";
        public HttpStatusCode AckStatus = HttpStatusCode.NoContent;
        public readonly Queue<string> VoidedPages = new();

        public int Count(string part) => Requests.Count(r => r.url.Contains(part));

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            string url = request.RequestUri!.AbsoluteUri;
            Requests.Add((request.Method, url, request.Content == null ? "" : await request.Content.ReadAsStringAsync(cancellationToken), request.Headers.Authorization?.ToString()));
            if (request.RequestUri.Host == "oauth2.googleapis.com") return Reply(TokenStatus, TokenBody);
            if (url.Contains("/voidedpurchases")) return Reply(HttpStatusCode.OK, VoidedPages.Count > 0 ? VoidedPages.Dequeue() : "{}");
            if (request.Method == HttpMethod.Post) return Reply(AckStatus, "");
            return Reply(ApiStatus, ApiBody);
        }

        private static HttpResponseMessage Reply(HttpStatusCode status, string body) => new(status) { Content = new StringContent(body) };
    }

    private static readonly RSA Key = RSA.Create(2048);

    private static string SecretJson() => JsonConvert.SerializeObject(new
    {
        type = "service_account",
        client_email = "verifier@history-heroes.iam.gserviceaccount.com",
        private_key_id = "kid-1",
        private_key = Key.ExportPkcs8PrivateKeyPem(),
    });

    private int _secretReads;
    private DateTime _now;

    [SetUp]
    public void ResetClock() => _now = Make.Now;

    private (GooglePlayClient client, StubHandler http) Client(string? secret)
    {
        var http = new StubHandler();
        _secretReads = 0;
        return (new GooglePlayClient((_, _) =>
        {
            _secretReads++;
            return Task.FromResult(secret);
        }, new HttpClient(http), () => _now), http);
    }

    private static readonly FakeContext Ctx = new();

    [Test]
    public async Task Ok_ParsesPurchase_CallsRightUrlWithBearer()
    {
        (GooglePlayClient client, StubHandler http) = Client(SecretJson());
        GoogleCheck r = await client.GetPurchaseAsync(Ctx, "gems_t1", "tok/+=", default);
        Assert.That(r.Result, Is.EqualTo(GoogleCheck.Kind.Ok));
        GoogleCheck.Purchase f = r.Found!;
        Assert.That((f.PurchaseState, f.ConsumptionState, f.AcknowledgementState, f.OrderId, f.ProductId, f.Quantity, f.ObfuscatedAccountId, f.PurchaseType, f.Settled),
            Is.EqualTo(((int?)0, (int?)0, (int?)0, "GPA.1", "gems_t1", (int?)1, "abc", (int?)null, false)));

        Assert.That(http.Requests[0].url, Is.EqualTo("https://oauth2.googleapis.com/token"));
        Assert.That(http.Requests[0].body, Does.Contain("grant_type=urn%3Aietf%3Aparams%3Aoauth%3Agrant-type%3Ajwt-bearer"));
        Assert.That(http.Requests[1].url, Is.EqualTo(
            "https://androidpublisher.googleapis.com/androidpublisher/v3/applications/com.choijunhuk.historyheroes/purchases/products/gems_t1/tokens/tok%2F%2B%3D"));
        Assert.That(http.Requests[1].auth, Is.EqualTo("Bearer at-1"));
    }

    [Test]
    public async Task MissingPurchaseState_IsNull()
    {
        (GooglePlayClient client, StubHandler http) = Client(SecretJson());
        http.ApiBody = "{\"orderId\":\"GPA.1\"}";
        Assert.That((await client.GetPurchaseAsync(Ctx, "gems_t1", "t", default)).Found!.PurchaseState, Is.Null);
    }

    [TestCase(HttpStatusCode.BadRequest, GoogleCheck.Kind.Invalid)]
    [TestCase(HttpStatusCode.NotFound, GoogleCheck.Kind.Invalid)]
    [TestCase(HttpStatusCode.Gone, GoogleCheck.Kind.Invalid)]
    [TestCase(HttpStatusCode.Unauthorized, GoogleCheck.Kind.Retry)]
    [TestCase(HttpStatusCode.Forbidden, GoogleCheck.Kind.Retry)]
    [TestCase(HttpStatusCode.TooManyRequests, GoogleCheck.Kind.Retry)]
    [TestCase(HttpStatusCode.ServiceUnavailable, GoogleCheck.Kind.Retry)]
    public async Task ApiStatus_MapsToKind_WithGoogleReasonCode(HttpStatusCode status, GoogleCheck.Kind kind)
    {
        (GooglePlayClient client, StubHandler http) = Client(SecretJson());
        http.ApiStatus = status;
        http.ApiBody = "{\"error\":{\"code\":" + (int)status + ",\"message\":\"long text with the token tok\",\"errors\":[{\"reason\":\"purchaseTokenNotFound\"}],\"status\":\"NOT_FOUND\"}}";
        GoogleCheck r = await client.GetPurchaseAsync(Ctx, "gems_t1", "tok", default);
        Assert.That((r.Result, r.Reason), Is.EqualTo((kind, "google_" + (int)status + ":purchaseTokenNotFound")));
    }

    [Test]
    public async Task TokenRejected_RetryWithStatusAndErrorCode_DoesNotCallApi()
    {
        (GooglePlayClient client, StubHandler http) = Client(SecretJson());
        http.TokenStatus = HttpStatusCode.BadRequest;
        http.TokenBody = "{\"error\":\"invalid_grant\",\"error_description\":\"Invalid JWT Signature.\"}";
        GoogleCheck r = await client.GetPurchaseAsync(Ctx, "gems_t1", "tok", default);
        Assert.That((r.Result, r.Reason), Is.EqualTo((GoogleCheck.Kind.Retry, "google_auth_400:invalid_grant")));
        Assert.That(http.Requests, Has.Count.EqualTo(1));
    }

    [Test]
    public async Task AccessToken_CachedUntilOneMinuteBeforeExpiry_ForgottenOn401()
    {
        (GooglePlayClient client, StubHandler http) = Client(SecretJson());
        await client.GetPurchaseAsync(Ctx, "gems_t1", "a", default);
        await client.GetPurchaseAsync(Ctx, "gems_t1", "b", default);
        Assert.That(http.Count("oauth2"), Is.EqualTo(1), "두 번째는 받아 둔 토큰");

        _now = Make.Now.AddSeconds(3599 - 59);
        await client.GetPurchaseAsync(Ctx, "gems_t1", "c", default);
        Assert.That(http.Count("oauth2"), Is.EqualTo(2), "만료 60초 전부터는 새로 받는다");

        http.ApiStatus = HttpStatusCode.Unauthorized;
        await client.GetPurchaseAsync(Ctx, "gems_t1", "d", default);
        http.ApiStatus = HttpStatusCode.OK;
        await client.GetPurchaseAsync(Ctx, "gems_t1", "e", default);
        Assert.That(http.Count("oauth2"), Is.EqualTo(3), "401이면 토큰을 버리고 새로");
    }

    [Test]
    public async Task ServiceAccount_CachedTenMinutes_NotConfiguredNotCached()
    {
        (GooglePlayClient client, _) = Client(SecretJson());
        await client.IsConfiguredAsync(Ctx, default);
        await client.IsConfiguredAsync(Ctx, default);
        Assert.That(_secretReads, Is.EqualTo(1));
        _now = Make.Now.AddMinutes(11);
        await client.IsConfiguredAsync(Ctx, default);
        Assert.That(_secretReads, Is.EqualTo(2));

        (GooglePlayClient empty, _) = Client(null);
        await empty.IsConfiguredAsync(Ctx, default);
        await empty.IsConfiguredAsync(Ctx, default);
        Assert.That(_secretReads, Is.EqualTo(2), "비밀값을 넣으면 바로 보이게 '없음'은 기억하지 않는다");
    }

    [Test]
    public async Task Acknowledge_Ok_PostsToAcknowledge()
    {
        (GooglePlayClient client, StubHandler http) = Client(SecretJson());
        Assert.That(await client.AcknowledgeAsync(Ctx, "gems_t1", "tok", default), Is.True);
        (HttpMethod method, string url, _, string? auth) = http.Requests.Last();
        Assert.That((method, url, auth), Is.EqualTo((HttpMethod.Post,
            "https://androidpublisher.googleapis.com/androidpublisher/v3/applications/com.choijunhuk.historyheroes/purchases/products/gems_t1/tokens/tok:acknowledge", "Bearer at-1")));
    }

    [TestCase("{\"purchaseState\":0,\"acknowledgementState\":1}", true)]
    [TestCase("{\"purchaseState\":0,\"consumptionState\":1}", true)]
    [TestCase("{\"purchaseState\":1}", true)]
    [TestCase("{\"purchaseState\":0,\"acknowledgementState\":0,\"consumptionState\":0}", false)]
    public async Task Acknowledge_Rejected_LooksAtPurchase_AlreadyDoneCountsAsDone(string current, bool done)
    {
        (GooglePlayClient client, StubHandler http) = Client(SecretJson());
        http.AckStatus = HttpStatusCode.BadRequest;
        http.ApiBody = current;
        Assert.That(await client.AcknowledgeAsync(Ctx, "gems_t1", "tok", default), Is.EqualTo(done));
    }

    [Test]
    public async Task VoidedList_FollowsPages_ParsesMillisStrings()
    {
        (GooglePlayClient client, StubHandler http) = Client(SecretJson());
        http.VoidedPages.Enqueue("{\"tokenPagination\":{\"nextPageToken\":\"p2\"},\"voidedPurchases\":[{\"purchaseToken\":\"a\",\"orderId\":\"GPA.a\",\"voidedTimeMillis\":\"1761958800000\"}]}");
        http.VoidedPages.Enqueue("{\"voidedPurchases\":[{\"purchaseToken\":\"b\",\"orderId\":\"GPA.b\",\"voidedTimeMillis\":\"1761958900000\"}]}");
        List<VoidedPurchase>? list = await client.ListVoidedAsync(Ctx, 1761900000000, default);
        Assert.That(list!.Select(v => (v.PurchaseToken, v.OrderId, v.VoidedTimeMillis)),
            Is.EqualTo(new[] { ("a", "GPA.a", 1761958800000L), ("b", "GPA.b", 1761958900000L) }));
        string[] urls = http.Requests.Where(r => r.url.Contains("voided")).Select(r => r.url).ToArray();
        Assert.That(urls[0], Does.Contain("/purchases/voidedpurchases?type=0&maxResults=1000&startTime=1761900000000").And.Not.Contain("token="));
        Assert.That(urls[1], Does.EndWith("&token=p2"));
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("not json")]
    [TestCase("{\"client_email\":\"a@b\",\"private_key\":\"not a pem key\"}")]
    [TestCase("{\"client_email\":\"a@b\"}")]
    public async Task MissingOrBrokenSecret_NotConfigured_NoNetwork(string? secret)
    {
        (GooglePlayClient client, StubHandler http) = Client(secret);
        Assert.That(await client.IsConfiguredAsync(Ctx, default), Is.False);
        Assert.That((await client.GetPurchaseAsync(Ctx, "gems_t1", "tok", default)).Result, Is.EqualTo(GoogleCheck.Kind.NotConfigured));
        Assert.That(await client.AcknowledgeAsync(Ctx, "gems_t1", "tok", default), Is.False);
        Assert.That(await client.ListVoidedAsync(Ctx, 0, default), Is.Null);
        Assert.That(http.Requests, Is.Empty);
    }

    [Test]
    public async Task ValidSecret_Configured()
    {
        Assert.That(await Client(SecretJson()).client.IsConfiguredAsync(Ctx, default), Is.True);
    }

    [Test]
    public void Assertion_IsRs256SignedJwt_ForAndroidPublisherScope()
    {
        var account = JsonConvert.DeserializeObject<GooglePlayClient.ServiceAccount>(SecretJson())!;
        var now = new DateTimeOffset(2026, 11, 1, 0, 0, 0, TimeSpan.Zero);
        string[] parts = GooglePlayClient.BuildAssertion(account, now).Split('.');
        Assert.That(parts, Has.Length.EqualTo(3));

        JObject header = JObject.Parse(Decode(parts[0]));
        Assert.That(((string)header["alg"]!, (string)header["typ"]!, (string)header["kid"]!), Is.EqualTo(("RS256", "JWT", "kid-1")));
        JObject claims = JObject.Parse(Decode(parts[1]));
        Assert.That((string)claims["iss"]!, Is.EqualTo("verifier@history-heroes.iam.gserviceaccount.com"));
        Assert.That((string)claims["scope"]!, Is.EqualTo("https://www.googleapis.com/auth/androidpublisher"));
        Assert.That((string)claims["aud"]!, Is.EqualTo("https://oauth2.googleapis.com/token"));
        Assert.That((long)claims["exp"]! - (long)claims["iat"]!, Is.EqualTo(3600));
        Assert.That((long)claims["iat"]!, Is.EqualTo(now.ToUnixTimeSeconds()));

        bool ok = Key.VerifyData(Encoding.ASCII.GetBytes(parts[0] + "." + parts[1]), Bytes(parts[2]), HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        Assert.That(ok, Is.True, "서비스 계정 공개 키로 서명이 맞아야 한다");
    }

    [Test]
    public void ErrorReason_ShortCodeOnly()
    {
        Assert.That(GooglePlayClient.ErrorReason("{\"error\":{\"message\":\"secret stuff\",\"status\":\"PERMISSION_DENIED\"}}"), Is.EqualTo(":PERMISSION_DENIED"));
        Assert.That(GooglePlayClient.ErrorReason("not json"), Is.Empty);
        Assert.That(GooglePlayClient.ErrorReason("{}"), Is.Empty);
    }

    private static string Decode(string part) => Encoding.UTF8.GetString(Bytes(part));

    private static byte[] Bytes(string part)
    {
        string b64 = part.Replace('-', '+').Replace('_', '/');
        return Convert.FromBase64String(b64.PadRight(b64.Length + (4 - b64.Length % 4) % 4, '='));
    }
}
