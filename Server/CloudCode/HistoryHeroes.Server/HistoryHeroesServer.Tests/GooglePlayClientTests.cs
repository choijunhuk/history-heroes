using System.Net;
using System.Security.Cryptography;
using System.Text;
using HistoryHeroes.Server;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace HistoryHeroesServer.Tests;

/// <summary>구글 호출: 서비스 계정 JWT 서명, 상태 코드 → 판정, 비밀값이 없을 때 (실제 구글 대신 가짜 HTTP)</summary>
public sealed class GooglePlayClientTests
{
    private sealed class StubHandler : HttpMessageHandler
    {
        public readonly List<HttpRequestMessage> Requests = new();
        public readonly List<string> Bodies = new();
        public HttpStatusCode TokenStatus = HttpStatusCode.OK;
        public HttpStatusCode ApiStatus = HttpStatusCode.OK;
        public string ApiBody = "{\"purchaseState\":0,\"orderId\":\"GPA.1\",\"productId\":\"gems_t1\",\"quantity\":1,\"obfuscatedExternalAccountId\":\"abc\"}";

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(request);
            Bodies.Add(request.Content == null ? "" : await request.Content.ReadAsStringAsync(cancellationToken));
            if (request.RequestUri!.Host == "oauth2.googleapis.com")
                return new HttpResponseMessage(TokenStatus) { Content = new StringContent("{\"access_token\":\"at-1\",\"expires_in\":3599}") };
            return new HttpResponseMessage(ApiStatus) { Content = new StringContent(ApiBody) };
        }
    }

    private static readonly RSA Key = RSA.Create(2048);

    private static string SecretJson() => JsonConvert.SerializeObject(new
    {
        type = "service_account",
        client_email = "verifier@history-heroes.iam.gserviceaccount.com",
        private_key_id = "kid-1",
        private_key = Key.ExportPkcs8PrivateKeyPem(),
    });

    private static (GooglePlayClient client, StubHandler http) Client(string? secret)
    {
        var http = new StubHandler();
        return (new GooglePlayClient(_ => Task.FromResult(secret), new HttpClient(http)), http);
    }

    [Test]
    public async Task Ok_ParsesPurchase_CallsRightUrlWithBearer()
    {
        (GooglePlayClient client, StubHandler http) = Client(SecretJson());
        GoogleCheck r = await client.GetPurchaseAsync(new FakeContext(), "gems_t1", "tok/+=");
        Assert.That(r.Result, Is.EqualTo(GoogleCheck.Kind.Ok));
        Assert.That((r.Found!.PurchaseState, r.Found.OrderId, r.Found.ProductId, r.Found.Quantity, r.Found.ObfuscatedAccountId, r.Found.PurchaseType),
            Is.EqualTo((0, "GPA.1", "gems_t1", (int?)1, "abc", (int?)null)));

        Assert.That(http.Requests[0].RequestUri!.ToString(), Is.EqualTo("https://oauth2.googleapis.com/token"));
        Assert.That(http.Bodies[0], Does.Contain("grant_type=urn%3Aietf%3Aparams%3Aoauth%3Agrant-type%3Ajwt-bearer"));
        HttpRequestMessage api = http.Requests[1];
        Assert.That(api.RequestUri!.AbsoluteUri, Is.EqualTo(
            "https://androidpublisher.googleapis.com/androidpublisher/v3/applications/com.choijunhuk.historyheroes/purchases/products/gems_t1/tokens/tok%2F%2B%3D"));
        Assert.That(api.Headers.Authorization!.ToString(), Is.EqualTo("Bearer at-1"));
    }

    [TestCase(HttpStatusCode.BadRequest, GoogleCheck.Kind.Invalid)]
    [TestCase(HttpStatusCode.NotFound, GoogleCheck.Kind.Invalid)]
    [TestCase(HttpStatusCode.Gone, GoogleCheck.Kind.Invalid)]
    [TestCase(HttpStatusCode.Unauthorized, GoogleCheck.Kind.Retry)]
    [TestCase(HttpStatusCode.Forbidden, GoogleCheck.Kind.Retry)]
    [TestCase(HttpStatusCode.TooManyRequests, GoogleCheck.Kind.Retry)]
    [TestCase(HttpStatusCode.ServiceUnavailable, GoogleCheck.Kind.Retry)]
    public async Task ApiStatus_MapsToKind(HttpStatusCode status, GoogleCheck.Kind kind)
    {
        (GooglePlayClient client, StubHandler http) = Client(SecretJson());
        http.ApiStatus = status;
        GoogleCheck r = await client.GetPurchaseAsync(new FakeContext(), "gems_t1", "tok");
        Assert.That((r.Result, r.Reason), Is.EqualTo((kind, "google_" + (int)status)));
    }

    [Test]
    public async Task TokenRejected_Retry_DoesNotCallApi()
    {
        (GooglePlayClient client, StubHandler http) = Client(SecretJson());
        http.TokenStatus = HttpStatusCode.BadRequest;
        GoogleCheck r = await client.GetPurchaseAsync(new FakeContext(), "gems_t1", "tok");
        Assert.That((r.Result, r.Reason), Is.EqualTo((GoogleCheck.Kind.Retry, "google_auth")));
        Assert.That(http.Requests, Has.Count.EqualTo(1));
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("not json")]
    [TestCase("{\"client_email\":\"a@b\",\"private_key\":\"not a pem key\"}")]
    [TestCase("{\"client_email\":\"a@b\"}")]
    public async Task MissingOrBrokenSecret_NotConfigured_NoNetwork(string? secret)
    {
        (GooglePlayClient client, StubHandler http) = Client(secret);
        Assert.That(await client.IsConfiguredAsync(new FakeContext()), Is.False);
        Assert.That((await client.GetPurchaseAsync(new FakeContext(), "gems_t1", "tok")).Result, Is.EqualTo(GoogleCheck.Kind.NotConfigured));
        Assert.That(http.Requests, Is.Empty);
    }

    [Test]
    public async Task ValidSecret_Configured()
    {
        Assert.That(await Client(SecretJson()).client.IsConfiguredAsync(new FakeContext()), Is.True);
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

    private static string Decode(string part) => Encoding.UTF8.GetString(Bytes(part));

    private static byte[] Bytes(string part)
    {
        string b64 = part.Replace('-', '+').Replace('_', '/');
        return Convert.FromBase64String(b64.PadRight(b64.Length + (4 - b64.Length % 4) % 4, '='));
    }
}
