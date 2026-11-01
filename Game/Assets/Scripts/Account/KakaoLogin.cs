using System.Threading.Tasks;
using Unity.Services.Authentication;
using UnityEngine;
#if UNITY_ANDROID && !UNITY_EDITOR
using UnityEngine.Android;
using UnityEngine.Scripting;
#endif

namespace HistoryHeroes.Account
{
    /// <summary>
    /// 카카오 로그인 (안드로이드 전용): 카카오 SDK(Plugins/Android/KakaoBridge.androidlib)로 로그인해서 OpenID Connect id_token을 받고,
    /// UGS의 OIDC 공급자 "oidc-kakao"(발급자 https://kauth.kakao.com)에 넘긴다. 카카오용 공식 Unity SDK가 없어서 자바 다리를 직접 둔다.
    /// 에디터 · 다른 기기 · 앱 키 없이 만든 빌드에서는 쓸 수 없다 (설정에서 버튼을 숨긴다).
    /// </summary>
    public static class KakaoLogin
    {
        /// <summary>UGS 대시보드의 공급자 이름. 바꾸면 이미 연결된 계정이 끊긴다</summary>
        public const string ProviderId = "oidc-kakao";

        /// <summary>폰 로그인이 이보다 오래 돌아오지 않으면 멈춘다 (앱이 다시 만들어지면 결과가 사라질 수 있다)</summary>
        private const int TimeoutMs = 180_000;

        public static readonly LoginProvider Provider = new LoginProvider
        {
            DisplayName = "카카오",
            Available = IsAvailable,
            Login = () => Call("login"),
            Link = token => AuthenticationService.Instance.LinkWithOpenIdConnectAsync(ProviderId, token),
            SignIn = token => AuthenticationService.Instance.SignInWithOpenIdConnectAsync(ProviderId, token),
            LinkedId = info => info?.GetOpenIdConnectId(ProviderId),
            Unlink = UnlinkVerified,
        };

        /// <summary>
        /// 계정 삭제 때 카카오 연결 끊기. 이 기기에 남은 카카오 로그인이 그 게임 계정에 붙은 카카오 계정이면 다시 로그인하지 않고 끊는다.
        /// 아니면 다시 로그인하고, 로그인한 계정(id_token sub)이 붙은 계정과 다르면 끊지 않는다 (다른 사람 · 다른 계정의 연결을 끊지 않게).
        /// </summary>
        private static async Task<LoginProvider.Result> UnlinkVerified(string linkedId)
        {
            LoginProvider.Result stored = await Call("storedUser");
            if (stored.Status == LoginProvider.Status.Ok && AccountRules.SameExternalUser(stored.Token, linkedId)) return await Call("unlink");

            LoginProvider.Result login = await Call("login");
            if (login.Status != LoginProvider.Status.Ok) return login;
            if (!AccountRules.SameExternalUser(AccountRules.JwtSubject(login.Token), linkedId))
            {
                await Call("logout"); // 방금 로그인한 다른 계정의 토큰을 남기지 않는다
                return new LoginProvider.Result(LoginProvider.Status.Error, null, "OtherAccount");
            }

            return await Call("unlink");
        }

#if UNITY_ANDROID && !UNITY_EDITOR
        private const string BridgeClass = "com.choijunhuk.historyheroes.kakao.KakaoBridge";
        private static int _available = -1;

        /// <summary>앱 키는 빌드 때 매니페스트에 들어간다 (AndroidManifestPatch, 저장소 밖 ~/.history-heroes/kakao.properties). 없으면 못 쓴다</summary>
        private static bool IsAvailable()
        {
            if (_available < 0)
            {
                try
                {
                    using var bridge = new AndroidJavaClass(BridgeClass);
                    _available = string.IsNullOrEmpty(bridge.CallStatic<string>("appKey", AndroidApplication.currentContext)) ? 0 : 1;
                }
                catch (System.Exception e)
                {
                    Debug.LogWarning("[Kakao] 다리를 찾지 못함: " + e.Message);
                    _available = 0;
                }
            }

            return _available == 1;
        }

        /// <summary>자바 다리 호출 하나 ("login" · "storedUser" · "unlink" · "logout"). 결과는 자바 스레드에서 오므로 TaskCompletionSource만 채운다</summary>
        private static async Task<LoginProvider.Result> Call(string method)
        {
            var done = new TaskCompletionSource<LoginProvider.Result>(TaskCreationOptions.RunContinuationsAsynchronously);
            try
            {
                using var bridge = new AndroidJavaClass(BridgeClass);
                bridge.CallStatic(method, AndroidApplication.currentActivity, new Callback(done));
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[Kakao] {method} 호출 실패: {e.Message}");
                return new LoginProvider.Result(LoginProvider.Status.Error, null, "Bridge");
            }

            Task finished = await Task.WhenAny(done.Task, Task.Delay(TimeoutMs));
            return finished == done.Task ? done.Task.Result : new LoginProvider.Result(LoginProvider.Status.Error, null, "Timeout");
        }

        /// <summary>KakaoBridge.Callback 구현. 메서드 이름으로 불리므로 IL2CPP가 지우지 않게 Preserve</summary>
        private sealed class Callback : AndroidJavaProxy
        {
            private readonly TaskCompletionSource<LoginProvider.Result> _done;

            public Callback(TaskCompletionSource<LoginProvider.Result> done) : base(BridgeClass + "$Callback") => _done = done;

            [Preserve]
            public void onResult(string status, string idToken, string code)
            {
                LoginProvider.Status s = status == "ok" ? LoginProvider.Status.Ok
                    : status == "cancelled" ? LoginProvider.Status.Cancelled : LoginProvider.Status.Error;
                _done.TrySetResult(new LoginProvider.Result(s, idToken, code));
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => _available = -1;
#else
        private static bool IsAvailable() => false;

        private static Task<LoginProvider.Result> Call(string method) =>
            Task.FromResult(new LoginProvider.Result(LoginProvider.Status.Error, null, "NotSupported"));
#endif
    }
}
