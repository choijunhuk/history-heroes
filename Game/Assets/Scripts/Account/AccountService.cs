using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using HistoryHeroes.Records;
using Unity.Services.Authentication;
using Unity.Services.CloudSave;
using Unity.Services.CloudSave.Models;
using Unity.Services.Core;
using Unity.Services.Core.Environments;
using UnityEngine;
using SaveData = HistoryHeroes.Records.SaveData;

namespace HistoryHeroes.Account
{
    /// <summary>
    /// 계정 (LiveOps 3장): 게스트(UGS 익명) → 카카오 연결 → 클라우드 저장 → 계정 삭제.
    /// 서버가 없거나 실패해도 게임은 지금처럼 이 기기 저장으로만 돈다 (상태 "오프라인"). 모든 서버 호출은 async로 메인 스레드를 막지 않는다.
    /// 클라우드 키: "save" = 마지막으로 파일에 저장된 글 그대로 (해시 포함 → 받을 때 손댄 글을 알아챈다), "meta" = AccountSummary (+ 올린 번호).
    /// 올리기 전에 클라우드를 읽어, 이 기기가 마지막으로 맞춘 뒤 다른 기기가 올렸으면 덮지 않고 고르게 한다.
    /// 쓰기는 읽은 판의 write lock을 붙여서만 한다 → 그사이 다른 기기가 쓰면 서버가 거절하고(충돌) 다시 고르게 한다 (서버 기준 합치기는 아직 없음).
    /// </summary>
    public static class AccountService
    {
        public const string EnvironmentName = "production";
        public const string SaveKey = "save";
        public const string MetaKey = "meta";

        /// <summary>
        /// 이미 있는 카카오 계정으로 바꾸기 ① 게스트 정리 중 (값 = 그 게스트의 UGS 플레이어 ID). 정리 도중 꺼지면 다음 실행이 그 게스트 세션으로
        /// 닫기 · 보관 · 지우기를 마저 한다 (서버에서 한 번 닫은 게스트는 다시 열지 않는다). 게스트를 지운 뒤에만 ②로 넘어간다
        /// </summary>
        private const string SwitchPrepKey = "hh.account.switchPrep";
        /// <summary>
        /// 이미 있는 카카오 계정으로 바꾸기 ② 그 계정으로 들어가 진행을 고르는 중 (값 = 그 계정의 UGS 플레이어 ID, 로그인 전이면 ""). 그 계정에서 무엇이든 받기 전에 남긴다.
        /// 고르기가 끝나기 전에 꺼지면 다음 실행은 **그 계정 세션 그대로** 고르기를 다시 연다 — 새 게스트를 만들지 않는다 (그 계정 진행이 게스트로 새지 않게)
        /// </summary>
        private const string SwitchTargetKey = "hh.account.switchTarget";
        /// <summary>
        /// 계정 삭제 중 (값 = 지우는 플레이어 ID). 서버 단계(카카오 연결 끊기 · 결제 보관 · 클라우드 지우기 · 계정 지우기) 전에 남긴다 — 도중에 꺼지면
        /// 다음 실행은 저장을 읽기 전에 이 기기 진행을 지우고(SaveSystem.WipeOnNextLoad), 로그인 전에 서버 단계를 마저 한 뒤에만 새 게스트를 만든다
        /// </summary>
        private const string DeleteIntentKey = "hh.account.deleteIntent";
        /// <summary>+ UGS 플레이어 ID: 이 기기가 마지막으로 올리거나 받은 클라우드 저장의 올린 번호</summary>
        private const string SyncedKeyPrefix = "hh.account.synced.";

        /// <summary>연결 가능한 외부 로그인. 구글 플레이 게임즈는 여기에 하나 더 넣는다</summary>
        public static readonly LoginProvider[] Providers = { KakaoLogin.Provider };

        public enum Choice
        {
            Cancel,
            Device,
            Cloud,
        }

        public static AccountState State { get; private set; } = AccountState.Offline;
        /// <summary>방금 한 일의 결과 · 오류 (설정 화면 두 번째 줄). 없으면 null</summary>
        public static string Message { get; private set; }
        /// <summary>붙어 있는 외부 계정 이름 ("카카오")</summary>
        public static string LinkedName { get; private set; }
        public static event Action Changed;

        /// <summary>진행 고르기 화면 (제목, 안내, 이 기기, 클라우드). 로비가 끼운다 (AccountSection.Bind). 없으면 고르기를 미룬다</summary>
        public static Func<string, string, AccountSummary, AccountSummary, Task<Choice>> ChooseProgress;
        /// <summary>예/아니오 확인 (제목, 안내, 예 글자). 로비가 끼운다</summary>
        public static Func<string, string, string, Task<bool>> Confirm;

        /// <summary>클라우드가 다른 곳에서 바뀌어 올리기를 멈췄다 → 로비가 한가할 때 ResolveConflictAsync를 부른다</summary>
        public static bool ConflictPending { get; private set; }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private static bool _sandbox;

        /// <summary>QA 전용: 에디터 게스트를 연결된 계정처럼 다뤄 올리기 · 받기 · 삭제를 진짜 서버로 시험한다 (QaTools.AccountSandbox)</summary>
        public static bool SandboxLinked => _sandbox;

        public static void SetSandbox(bool on)
        {
            _sandbox = on;
            if (State == AccountState.Guest && on) LinkedName = "시험";
            else if (State == AccountState.Linked && !on && LinkedName == "시험") LinkedName = null;
            else return;
            Set(LinkedName != null ? AccountState.Linked : AccountState.Guest, null);
            if (on) _cloudChanged = true;
        }
#else
        private static bool SandboxLinked => false;
#endif

        private static bool _starting;
        private static bool _cloudChanged;
        private static bool _uploading;
        /// <summary>고르기에서 "나중에"를 골랐다: 이번 실행에서는 자동으로 올리지 않는다 ("지금 클라우드에 저장"을 누르면 다시 고른다)</summary>
        private static bool _conflictDeferred;
        /// <summary>이번 실행에서 클라우드와 맞췄다 (올리기 성공 · 클라우드 진행 받기). 결제의 계정당 1번 · 첫 구매 2배가 이걸 본다</summary>
        private static bool _cloudChecked;
        private static double _lastUpload = double.NegativeInfinity;

        /// <summary>
        /// 결제가 이 기기 장부의 "계정당 1번 · 첫 구매 2배"를 믿어도 되나 (LiveOps 3-2 · Monetization 7장).
        /// 게스트는 이 기기 장부가 전부라 늘 된다. 연결된 계정은 이번 실행에서 클라우드와 맞췄고 고를 일이 남지 않았을 때만.
        /// 오프라인 · 연결 중 · 처리 중은 알 수 없으니 안 된다.
        /// </summary>
        public static bool SyncSettled => AccountRules.SyncSettled(State, _cloudChecked, ConflictPending || _conflictDeferred, ChoicePending, SaveSystem.CloudRecheckNeeded);

        public static string PlayerId
        {
            get
            {
                if (UnityServices.State != ServicesInitializationState.Initialized) return null;
                IAuthenticationService auth = AuthenticationService.Instance;
                return auth.IsSignedIn ? auth.PlayerId : null;
            }
        }

        /// <summary>바꾸려는 카카오 계정의 진행 고르기가 남았다 (②). 그동안 올리기 · 결제를 멈춘다</summary>
        private static bool ChoicePending => PlayerPrefs.HasKey(SwitchTargetKey);

        private static void ClearPending()
        {
            PlayerPrefs.DeleteKey(SwitchTargetKey);
            PlayerPrefs.Save(); // 안드로이드는 Save 전에 꺼지면 지운 게 되살아난다
            SyncSwitching();
        }

        private static void ClearPrep()
        {
            PlayerPrefs.DeleteKey(SwitchPrepKey);
            PlayerPrefs.Save();
            SyncSwitching();
        }

        /// <summary>바꾸기 표시(① · ②)가 남아 있는 동안 결제를 멈춘다 (새 결제 · 서버 묻기 · 서버 장부 맞추기). 다 지우면 그때의 계정으로 다시 묻고 맞춘다</summary>
        private static void SyncSwitching() => Economy.IapService.SetSwitching(PlayerPrefs.HasKey(SwitchPrepKey) || PlayerPrefs.HasKey(SwitchTargetKey));

        private static string SyncedKey => SyncedKeyPrefix + AuthenticationService.Instance.PlayerId;

        /// <summary>이 기기가 마지막으로 맞춘 클라우드 올린 번호 (저장을 못 읽고 새로 시작했으면 RefreshLinked가 지워 둔다)</summary>
        private static string SyncedUploadId => PlayerPrefs.GetString(SyncedKey, string.Empty);

        private static void SetSynced(string uploadId)
        {
            PlayerPrefs.SetString(SyncedKey, uploadId ?? string.Empty);
            PlayerPrefs.Save();
        }

        private static bool InLobby => UnityEngine.SceneManagement.SceneManager.GetActiveScene().name == Common.GameSession.LobbyScene;

        private static void Set(AccountState state, string message)
        {
            State = state;
            Message = message;
            Changed?.Invoke();
        }

        // ------------------------------------------------------------------ 시작

        /// <summary>동의 뒤 로비에서 부른다. 이미 로그인했으면 아무것도 안 한다. 실패하면 "오프라인"으로 두고 다음 로비 · 설정에서 다시 시도한다</summary>
        public static async void Start()
        {
            if (_starting || State == AccountState.Guest || State == AccountState.Linked || State == AccountState.Busy) return;
            // 동의 전에는 서버에 닿지 않는다 (설정 화면의 계정 칸이 장면을 열 때 한 번 켜졌다 꺼지면서 부를 수 있다)
            if (Progress.Consent.NeedsConsent(SaveSystem.Data) || SaveSystem.WipePending) return;
            _starting = true;
            Set(AccountState.Connecting, null);
            try
            {
                Lifecycle.Ensure();
                SaveSystem.Saved -= OnSaved;
                SaveSystem.Saved += OnSaved;
                if (UnityServices.State != ServicesInitializationState.Initialized)
                    await UnityServices.InitializeAsync(new InitializationOptions().SetEnvironmentName(EnvironmentName));

                IAuthenticationService auth = AuthenticationService.Instance;
                if (PlayerPrefs.HasKey(DeleteIntentKey) && !await ResumeDeletionAsync(auth))
                {
                    Set(AccountState.Offline, "계정 삭제를 마무리하지 못했어요. 인터넷을 확인해 주세요 (다음에 이어서 지워요)");
                    return;
                }

                SyncSwitching(); // 바꾸기가 끝나지 않았으면 로그인되자마자 결제를 묻지 않게
                if (auth.IsSignedIn && auth.IsExpired) auth.SignOut();
                if (ChoicePending && !auth.IsSignedIn && !auth.SessionTokenExists)
                {
                    // ② 표시인데 이 기기에 세션이 없다: 그 계정 로그인 전에 꺼졌으면("") 받은 것이 없으니 지우고 평소대로.
                    // 그 계정으로 들어간 뒤인데 세션이 없으면 새 게스트를 만들지 않는다 (그 계정 진행이 이 기기에 있을 수 있다)
                    if (AccountRules.DecideSwitchTarget(PlayerPrefs.GetString(SwitchTargetKey, string.Empty), null) == AccountRules.SwitchStep.Hold)
                    {
                        Debug.LogWarning("[Account] 바꾸려던 계정의 세션이 없음 → 새 게스트를 만들지 않음");
                        Set(AccountState.Offline, SwitchLostText);
                        return;
                    }

                    ClearPending();
                }

                // 저장된 세션이 있으면 그 플레이어(게스트든 카카오든)로 돌아온다
                if (!auth.IsSignedIn) await auth.SignInAnonymouslyAsync();
                if (ChoicePending && !ResumeSwitchTarget(auth))
                {
                    Debug.LogWarning("[Account] 바꾸려던 계정이 아닌 세션 → 그 플레이어로 이어 가지 않음");
                    auth.SignOut(); // 세션 정보는 남긴다 (지우지 않는다)
                    Set(AccountState.Offline, SwitchLostText);
                    return;
                }

                if (!ChoicePending && PlayerPrefs.HasKey(SwitchPrepKey)) await ResumeSwitchPrepAsync(auth);
                await RefreshLinked();
                // ② 그 계정 세션 그대로: 로비가 한가할 때 진행 고르기를 다시 연다 (그동안 올리기 · 결제는 멈춤)
                if (ChoicePending && State == AccountState.Linked) ConflictPending = true;
                Debug.Log($"[Account] 로그인: {State} ({auth.PlayerId})");
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Account] 서버에 연결하지 못함 → 오프라인: " + e.Message);
                Set(AccountState.Offline, null);
            }
            finally
            {
                _starting = false;
            }
        }

        private static async Task RefreshLinked()
        {
            IAuthenticationService auth = AuthenticationService.Instance;
            PlayerInfo info;
            try
            {
                info = await auth.GetPlayerInfoAsync();
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Account] 플레이어 정보를 못 받음 (로그인 응답 정보로): " + e.Message);
                info = auth.PlayerInfo;
            }

            LinkedName = null;
            foreach (LoginProvider p in Providers)
            {
                if (p.IsLinked(info)) LinkedName = p.DisplayName;
            }

            if (LinkedName == null && SandboxLinked) LinkedName = "시험";
            _cloudChecked = false;
            // 최근 저장을 잃었다(못 읽음 · 오래된 백업): 맞춘 기록을 지워 두면 다음 올리기가 덮지 않고 클라우드 복구를 권한다 (다시 켜도 그대로)
            if (LinkedName != null && SaveSystem.CloudRecheckNeeded)
            {
                PlayerPrefs.DeleteKey(SyncedKey);
                PlayerPrefs.Save();
            }

            Set(LinkedName != null ? AccountState.Linked : AccountState.Guest, null);
            if (State != AccountState.Linked) return;

            // 지난번 실행에서 못 올렸을 수 있다 (60초 · 클라우드 확인 규칙은 그대로)
            _cloudChanged = true;
        }

        // ------------------------------------------------------------------ 연결

        /// <summary>
        /// 외부 계정 연결 (설정 → 카카오 계정 연결). 그 계정이 이미 다른 플레이어에 붙어 있으면(AccountAlreadyLinked)
        /// 그 계정으로 들어가서 클라우드 진행을 읽고, 한 번만 고르게 한다 (LiveOps 3-2).
        /// </summary>
        public static async Task LinkAsync(LoginProvider provider)
        {
            if (State != AccountState.Guest) return;
            if (PlayerPrefs.HasKey(SwitchPrepKey))
            {
                // ① 지난 계정 바꾸기의 게스트 정리가 남았다: 새 연결 전에 마무리한다 (그대로 연결하면 다음 실행의 정리가 연결된 계정을 게스트로 알고 지울 수 있다)
                Set(AccountState.Busy, "지난 계정 바꾸기를 마무리하는 중…");
                await ResumeSwitchPrepAsync(AuthenticationService.Instance);
                if (PlayerPrefs.HasKey(SwitchPrepKey))
                {
                    Set(AccountState.Guest, SwitchRetryText);
                    return;
                }
            }

            Set(AccountState.Busy, $"{provider.DisplayName} 로그인 중…");
            LoginProvider.Result login = await provider.Login();
            if (login.Status == LoginProvider.Status.Cancelled)
            {
                Set(AccountState.Guest, null); // 사용자가 그만뒀다: 조용히
                return;
            }

            if (login.Status == LoginProvider.Status.Error)
            {
                Debug.LogWarning($"[Account] {provider.DisplayName} 로그인 실패: {login.Code}");
                Set(AccountState.Guest, AccountRules.LoginErrorText(provider.DisplayName, login.Code));
                return;
            }

            bool alreadyLinked = false;
            try
            {
                await provider.Link(login.Token);
            }
            catch (RequestFailedException e) when (e.ErrorCode == AccountRules.AccountAlreadyLinked)
            {
                alreadyLinked = true;
            }
            catch (RequestFailedException e)
            {
                Debug.LogWarning($"[Account] 연결 실패 {e.ErrorCode}: {e.Message}");
                Set(AccountState.Guest, AccountRules.ErrorText(e.ErrorCode, e.Message));
                return;
            }
            catch (Exception e)
            {
                // 처리 중 상태로 멈춰 버튼이 안 눌리는 일이 없게 어떤 오류든 게스트로 돌린다
                Debug.LogWarning("[Account] 연결 실패: " + e);
                Set(AccountState.Guest, "문제가 생겼어요. 잠시 뒤에 다시 해 주세요");
                return;
            }

            if (!alreadyLinked)
            {
                // 게스트 플레이어의 클라우드는 비어 있다 → 그대로 올린다 (비어 있지 않으면 올리기가 멈추고 고르게 한다)
                LinkedName = provider.DisplayName;
                Set(AccountState.Linked, null);
                _cloudChanged = true;
                bool uploaded = await UploadAsync(true);
                Set(AccountState.Linked, uploaded
                    ? $"{provider.DisplayName} 계정을 연결했어요. 이제 클라우드에도 저장돼요"
                    : $"{provider.DisplayName} 계정을 연결했어요. 클라우드 저장은 다음에 다시 해요");
                return;
            }

            await SwitchToExisting(provider, login.Token);
        }

        private static async Task SwitchToExisting(LoginProvider provider, string token)
        {
            IAuthenticationService auth = AuthenticationService.Instance;
            // 결제한 게스트는 바꾸지 않는다 (이 기기 장부 — 서버 장부는 CloseForSwitch가 같은 판정 안에서 다시 본다)
            if (AccountRules.HasPurchases(SaveSystem.Data))
            {
                Set(AccountState.Guest, PaidGuestSwitchText);
                return;
            }

            // 지우는 것은 연결이 하나도 없는 익명 게스트뿐 (확인하지 못하면 바꾸지 않는다)
            if (await IsAnonymousAsync() != true)
            {
                Set(AccountState.Guest, SwitchRetryText);
                return;
            }

            // ① 게스트 정리 중 표시를 먼저 (게스트 세션은 그대로) → 바꾸기가 끝날 때까지 결제는 멈춘다
            PlayerPrefs.SetString(SwitchPrepKey, auth.PlayerId ?? string.Empty);
            PlayerPrefs.Save();
            SyncSwitching();
            try
            {
                // 결제 기록을 다른 계정으로 옮기는 길은 두지 않는다 → 결제 · 예약이 없는 게스트만 서버에서 닫고(CloseForSwitch, 한 방향),
                // 빈 장부를 보관한 뒤 UGS 계정을 지우고 들어간다 — 게스트로 로그인된 지금만 지울 수 있다
                string blocked = await PrepareGuestForSwitchAsync();
                if (blocked != null)
                {
                    // 결제가 있으면 서버는 아무것도 쓰지 않았다 → 게스트 그대로. 그 밖의 실패는 게스트가 이미 닫혔을 수 있다 — 다시 열지 않고
                    // 표시를 남겨 둔다 (다시 하면 이어서, 아니면 다음 실행에서 지우기를 마저 하고 새 게스트로. 그동안 결제는 멈춤)
                    if (blocked == PaidGuestSwitchText) ClearPrep();
                    Set(AccountState.Guest, blocked);
                    return;
                }

                // 게스트를 지웠다 → ②로 (그 계정 로그인 전이라 ""), ① 표시는 같은 저장에서 지운다
                PlayerPrefs.SetString(SwitchTargetKey, string.Empty);
                PlayerPrefs.DeleteKey(SwitchPrepKey);
                PlayerPrefs.Save();
                await provider.SignIn(token);
                // 그 계정에서 무엇이든(클라우드 진행 · 결제 장부) 받기 전에 대상을 남긴다
                PlayerPrefs.SetString(SwitchTargetKey, auth.PlayerId);
                PlayerPrefs.Save();
                LinkedName = provider.DisplayName;
                await ChooseSwitchAsync(provider.DisplayName, resumed: false);
            }
            catch (Exception e)
            {
                // 여기 오는 것은 그 계정 로그인 실패뿐 (고르기는 안에서 처리한다) → 받은 것이 없다
                Debug.LogWarning("[Account] 계정 전환 실패: " + e.Message);
                await BackToGuest(e is RequestFailedException r ? AccountRules.ErrorText(r.ErrorCode, r.Message) : "문제가 생겼어요. 잠시 뒤에 다시 해 주세요");
            }
            finally
            {
                SyncSwitching(); // 바꾸기가 끝났으면(표시 없음) 그때의 계정으로 결제를 다시 묻고 장부를 맞춘다
            }
        }

        /// <summary>
        /// ② 바꾸려는 카카오 계정으로 들어온 뒤의 진행 고르기 (방금 들어옴, 또는 resumed = 지난 실행이 고르기 전에 꺼져 그 세션으로 다시 엶).
        /// 그 계정에 결제 기록이 하나라도 있으면(서버 장부 · 클라우드 장부, 모르면 있는 것으로) **그 계정 진행만** 고를 수 있다 — 이 기기 진행에 그 계정 결제를 합치는 길이 없다.
        /// 결제가 없으면 두 쪽 다 고를 수 있고 어느 쪽도 결제를 옮기지 않는다 (게스트에도 결제가 없다 — ①에서 확인).
        /// 그 계정 진행을 받는 것은 SaveSystem.Replace 한 번 (되거나 안 되거나). 받은 뒤 / 이 기기 진행을 올리기 시작한 뒤에는 게스트로 돌아가지 않는다.
        /// 그만두기 · 실패: 방금 들어왔으면 새 게스트로 (받은 것이 없다), 다시 연 것이면 그 계정에 머물고 다음에 다시 고른다
        /// </summary>
        private static async Task ChooseSwitchAsync(string name, bool resumed)
        {
            bool committed = false;
            bool touched = false; // 이 기기 저장을 그 계정 진행으로 바꾸려 했다 → 결과와 상관없이 게스트로 돌아가지 않는다 (그 계정 표시 · 세션 유지)
            try
            {
                Set(AccountState.Busy, "클라우드 진행을 확인하는 중…");
                CloudCopy cloud = await LoadCloud(true);
                if (!cloud.HasSave)
                {
                    // 그 계정에 클라우드 진행이 없다 → 이 기기 진행이 그 계정 진행이 된다 (그 계정 결제는 서버 장부 맞추기가 넣는다 — 이제 같은 계정)
                    committed = true;
                    await FinishWithDevice(name, cloud);
                    return;
                }

                if (!cloud.Readable)
                {
                    await StopSwitchAsync(cloud.UnreadableText, resumed);
                    return;
                }

                bool paid = AccountRules.TargetHasPurchases(cloud.Data, await TargetLedgerAsync());
                Set(AccountState.Busy, "어느 진행을 쓸지 골라 주세요");
                Choice choice;
                if (paid)
                {
                    bool yes = Confirm != null && await Confirm($"이 {name} 계정에는 결제 기록이 있어요",
                        $"결제 기록이 있는 계정은 그 계정의 진행으로만 이어서 할 수 있어요. 이 기기 진행은 사라져요 (이 기기 백업에는 남아요).", $"{name} 계정 진행 쓰기");
                    choice = yes ? Choice.Cloud : Choice.Cancel;
                }
                else
                {
                    choice = await Ask($"이 {name} 계정에 저장된 진행이 있어요", "고른 진행으로 이어서 해요. 고르지 않은 쪽 진행은 사라져요", cloud);
                }

                if (choice == Choice.Cancel)
                {
                    await StopSwitchAsync("연결을 그만뒀어요. 이 기기 진행 그대로예요", resumed);
                    return;
                }

                if (choice == Choice.Device)
                {
                    // 결제가 없는 계정일 때만 여기 온다 → 옮길 결제가 없다
                    committed = true;
                    await FinishWithDevice(name, cloud);
                    return;
                }

                touched = true;
                if (!ApplyCloud(cloud, mergeDevicePurchases: false))
                {
                    // 교체가 실패했다 (메모리 · 디스크 모두 되돌림 — SaveStore.RollBack). 그래도 게스트로 돌아가지 않고 그 계정에 머문다:
                    // 되돌리기까지 실패해 그 계정 진행의 임시 파일이 남았어도, 다음 실행은 그 계정 세션으로 열려 다른 플레이어의 진행이 되지 않는다
                    await StopSwitchAsync("저장하지 못했어요. 기기 저장 공간을 확인하고 다시 해 주세요", resumed: true);
                    return;
                }

                committed = true;
                ClearPending();
                LinkedName = name;
                Set(AccountState.Linked, $"{name} 계정의 진행을 불러왔어요");
                Common.GameSession.ToLobby(); // 바뀐 저장으로 로비를 다시 그린다
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Account] 계정 전환 고르기 실패: " + e.Message);
                string text = e is RequestFailedException r ? AccountRules.ErrorText(r.ErrorCode, r.Message) : "문제가 생겼어요. 잠시 뒤에 다시 해 주세요";
                if (!committed)
                {
                    await StopSwitchAsync(text, resumed || touched);
                    return;
                }

                // 이미 그 계정 진행을 받았거나 이 기기 진행을 그 계정으로 올리기 시작했다 → 그 계정에 머문다
                ClearPending();
                LinkedName = name;
                Set(AccountState.Linked, text);
            }
        }

        /// <summary>고르기를 그만둔다: 방금 들어왔으면 표시를 지우고 새 게스트로 (받은 것이 없다), 다시 연 것이면 그 계정에 머물고 다음에 다시 고른다 (올리기 · 결제는 멈춘 채)</summary>
        private static async Task StopSwitchAsync(string message, bool resumed)
        {
            if (!resumed)
            {
                await BackToGuest(message);
                return;
            }

            _conflictDeferred = true;
            Set(AccountState.Linked, message + ". 고를 때까지 클라우드 저장과 결제를 멈춰요. [지금 클라우드에 저장]을 누르면 다시 골라요");
        }

        /// <summary>바꾸려는 계정(지금 로그인)의 서버 결제 장부. 못 받으면 null (= 결제가 있는 것으로 본다)</summary>
        private static async Task<Economy.PurchaseServer.Ledger> TargetLedgerAsync()
        {
            try
            {
                return await Economy.PurchaseServer.GetPurchasesAsync();
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Account] 바꾸려는 계정의 결제 장부를 못 받음 (결제가 있는 것으로): " + e.Message);
                return null;
            }
        }

        /// <summary>
        /// 사용자가 "이 기기 진행"을 골랐다 (또는 클라우드가 비었다): 사용자가 본 클라우드 판(seen)의 write lock으로 덮는다.
        /// 그 뒤 다른 기기가 썼으면 서버가 거절하고 고르기가 다시 뜬다 (본 적 없는 진행을 말없이 덮지 않게).
        /// </summary>
        private static async Task FinishWithDevice(string name, CloudCopy seen)
        {
            ClearPending();
            LinkedName = name;
            Set(AccountState.Linked, null);
            _cloudChanged = true;
            bool uploaded = await UploadAsync(true, seen);
            Set(AccountState.Linked, uploaded ? $"{name} 계정에 이 기기 진행을 올렸어요"
                : ConflictPending ? "그사이 클라우드가 바뀌었어요. 다시 골라 주세요"
                : $"{name} 계정을 연결했어요. 클라우드 저장은 다음에 다시 해요");
        }

        /// <summary>계정 전환을 그만둔다: 외부 계정에서 나와 새 게스트로 (이 기기 저장은 손대지 않았다)</summary>
        private static async Task BackToGuest(string message)
        {
            IAuthenticationService auth = AuthenticationService.Instance;
            try
            {
                ClearPending(); // 표시를 먼저 지운다 (그 계정에서 받은 것이 없다) — 세션을 지운 뒤 표시가 남으면 다음 실행이 막힌다
                if (auth.IsSignedIn) auth.SignOut(true);
                else if (auth.SessionTokenExists) auth.ClearSessionToken();
                LinkedName = null;
                await auth.SignInAnonymouslyAsync();
                Set(AccountState.Guest, message);
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Account] 게스트로 돌아가지 못함: " + e.Message);
                ClearPending();
                Set(AccountState.Offline, message);
            }
        }

        public const string PaidGuestSwitchText = "이 기기에서 결제한 기록이 있어서 이미 다른 진행이 있는 카카오 계정으로는 바꿀 수 없어요. 바꾸려면 문의해 주세요 (choijunhuk2007@gmail.com, 사용자 ID 함께).";
        public const string SwitchRetryText = "서버에 연결하지 못해 계정을 바꾸지 않았어요. 잠시 뒤에 다시 해 주세요";
        public const string SwitchLostText = "계정 바꾸기를 마치지 못했어요. 문의해 주세요 (choijunhuk2007@gmail.com)";

        /// <summary>
        /// 이미 있는 카카오 계정으로 바꾸기 전 (게스트로 로그인된 채): 서버가 결제 · 예약이 없을 때만 게스트를 닫고(한 방향 — 이미 닫혔으면 그대로 ok),
        /// 빈 장부를 보관한 뒤 게스트 UGS 계정을 지운다. 돌려주는 값: 바꾸지 못하는 이유 (null이면 게스트를 지웠고 들어가도 된다)
        /// </summary>
        private static async Task<string> PrepareGuestForSwitchAsync()
        {
            try
            {
                // 서버가 같은 판정 안에서: 결제 · 예약이 하나도 없을 때만 게스트를 닫는다 (진행 중이던 결제가 끼어들면 has_purchases — 아무것도 쓰지 않는다)
                string closed = await Economy.PurchaseServer.CloseForSwitchAsync();
                if (closed == Economy.PurchaseServer.HasPurchases) return PaidGuestSwitchText;
                if (closed != "ok") return SwitchRetryText;
                string archived = await Economy.PurchaseServer.ArchiveAndDeleteAsync();
                if (archived != "ok") return SwitchRetryText;
                await AuthenticationService.Instance.DeleteAccountAsync(); // 끝나면 로그아웃 + 세션도 지운다
                return null;
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Account] 게스트를 정리하지 못해 계정을 바꾸지 않음: " + e.Message);
                return SwitchRetryText;
            }
        }

        /// <summary>
        /// ① 지난 실행이 게스트 정리 도중 꺼졌다: 그 게스트 세션이면 닫기 · 보관 · 지우기를 마저 하고 새 게스트로 (닫은 게스트는 다시 열지 않는다 —
        /// 아직 닫히지 않았고 결제가 있으면 서버가 아무것도 쓰지 않아 게스트 그대로). 그 세션이 이미 없으면(지워짐) 표시만 지운다. 서버에 못 닿으면 표시를 남겨 다음에 다시
        /// </summary>
        private static async Task ResumeSwitchPrepAsync(IAuthenticationService auth)
        {
            try
            {
                string prep = PlayerPrefs.GetString(SwitchPrepKey, string.Empty);
                // 지우기 전에: 표시의 그 게스트이고, 연결된 계정이 하나도 없는 익명 게스트인지 (연결됐으면 표시만 지우고 아무것도 지우지 않는다)
                bool? anonymous = prep.Length > 0 && prep == auth.PlayerId ? await IsAnonymousAsync() : null;
                AccountRules.SwitchStep step = AccountRules.DecideSwitchPrep(prep, auth.PlayerId, anonymous);
                Debug.Log($"[Account] 게스트 정리 이어 받기: {step}");
                if (step == AccountRules.SwitchStep.Hold) return;
                if (step == AccountRules.SwitchStep.FinishGuestCleanup && await PrepareGuestForSwitchAsync() == SwitchRetryText) return;
                ClearPrep();
                if (!auth.IsSignedIn) await auth.SignInAnonymouslyAsync(); // 지웠으면 새 게스트로 (② 표시는 없다)
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Account] 게스트 정리 마무리 실패 (다음에 다시): " + e.Message);
            }
            finally
            {
                SyncSwitching();
            }
        }

        /// <summary>지금 플레이어에 외부 계정 · 사용자 이름이 하나도 없나 (서버에서 받은 정보로). 못 받으면 null</summary>
        private static async Task<bool?> IsAnonymousAsync()
        {
            try
            {
                PlayerInfo info = await AuthenticationService.Instance.GetPlayerInfoAsync();
                if (info == null) return null;
                return AccountRules.IsAnonymous(info.Identities?.Count ?? 0, info.Username);
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Account] 플레이어 정보를 못 받아 익명 게스트인지 모름: " + e.Message);
                return null;
            }
        }

        /// <summary>
        /// ② 지난 실행이 바꾸려는 계정의 진행 고르기 전에 꺼졌다 (로그인된 뒤): 그 계정 세션이면 그대로 두고 대상을 기록 (true).
        /// 그 계정이 아닌 세션이면 false — 그 플레이어로 이어 가지 않는다 (이 기기 저장에 그 계정 진행이 있을 수 있다)
        /// </summary>
        private static bool ResumeSwitchTarget(IAuthenticationService auth)
        {
            string target = PlayerPrefs.GetString(SwitchTargetKey, string.Empty);
            AccountRules.SwitchStep step = AccountRules.DecideSwitchTarget(target, auth.PlayerId);
            Debug.Log($"[Account] 계정 바꾸기 이어 받기: {step}");
            if (step != AccountRules.SwitchStep.ResumeTarget) return false;
            if (target.Length == 0)
            {
                PlayerPrefs.SetString(SwitchTargetKey, auth.PlayerId); // 그 계정 로그인 직후 꺼졌다 (게스트는 지워져 남은 세션은 그 계정뿐)
                PlayerPrefs.Save();
            }

            return true;
        }

        // ------------------------------------------------------------------ 클라우드 읽기 · 고르기

        /// <summary>읽은 클라우드 한 판: 값 + 키마다 write lock (없는 키는 null → 쓰면 새로 만든다)</summary>
        private sealed class CloudCopy
        {
            public bool HasSave;
            public string SaveLock;
            public string MetaLock;
            public SaveStore.ParseResult Parsed;
            public SaveData Data;
            public string Problem;
            public AccountSummary Meta;

            public bool Readable => Parsed == SaveStore.ParseResult.Ok || Parsed == SaveStore.ParseResult.Tampered;

            public string UnreadableText => Parsed == SaveStore.ParseResult.NewerSchema
                ? "클라우드 진행이 더 새 버전에서 저장됐어요. 게임을 업데이트한 뒤 다시 해 주세요"
                : "클라우드 진행을 읽지 못했어요. 문의해 주세요";
        }

        /// <summary>클라우드 읽기 (값 + write lock). parse면 저장 글을 파일 읽기와 같은 검사로 읽는다 (고르기 · 받기 때만)</summary>
        private static async Task<CloudCopy> LoadCloud(bool parse)
        {
            Dictionary<string, Item> items = await CloudSaveService.Instance.Data.Player.LoadAsync(new HashSet<string> { SaveKey, MetaKey });
            var copy = new CloudCopy();
            if (items.TryGetValue(MetaKey, out Item meta))
            {
                copy.MetaLock = meta.WriteLock;
                copy.Meta = JsonUtility.FromJson<AccountSummary>(meta.Value.GetAs<string>());
            }

            if (!items.TryGetValue(SaveKey, out Item save)) return copy;
            copy.HasSave = true;
            copy.SaveLock = save.WriteLock;
            if (!parse) return copy;
            copy.Parsed = SaveStore.Parse(save.Value.GetAs<string>(), out copy.Data, out copy.Problem);
            if (!copy.Readable) Debug.LogWarning($"[Account] 클라우드 저장을 못 읽음 ({copy.Parsed}): {copy.Problem}");
            else if (copy.Parsed == SaveStore.ParseResult.Tampered) Debug.LogWarning("[Account] 클라우드 저장 해시 불일치 (표시만 남기고 쓴다)");
            return copy;
        }

        private static Task<Choice> Ask(string title, string info, CloudCopy cloud)
        {
            SaveSystem.SaveIfDirty();
            if (ChooseProgress == null) return Task.FromResult(Choice.Cancel);
            return ChooseProgress(title, info, AccountSummary.From(SaveSystem.Data), AccountSummary.From(cloud.Data));
        }

        /// <summary>클라우드 진행으로 바꾼다: 이 기기 진행은 백업(.run1 · .bak)으로 남는다 (해시 · 구조 옮기기는 Parse가 이미 했다).
        /// 이 기기에서만 한 결제(보석 · 장부 · 첫 구매 2배 · 패키지)는 클라우드 진행에 옮겨 담는다 — 진짜 돈으로 산 것은 사라지면 안 된다</summary>
        private static bool ApplyCloud(CloudCopy cloud, bool mergeDevicePurchases = true)
        {
            // 계정 바꾸기에서는 합치지 않는다 (게스트에 결제가 없다 — 다른 계정 사이에 결제를 옮기는 길을 두지 않는다)
            if (mergeDevicePurchases) Economy.IapLedger.MergeInto(cloud.Data, SaveSystem.Data);
            if (!SaveSystem.Replace(cloud.Data)) return false;
            SetSynced(cloud.Meta?.uploadId);
            SaveSystem.CloudRechecked();
            _cloudChecked = true;
            // 이 기기 결제를 합쳤으면 클라우드보다 앞서 있다 → 받은 판의 lock으로 곧 올린다 (meta 번호가 같으니 자동 올리기가 통과한다)
            _cloudChanged = true;
            return true;
        }

        private static bool KeepCloudPurchases(CloudCopy cloud) => KeepPurchases(cloud?.Data);

        /// <summary>
        /// "이 기기 진행"을 골라도 다른 쪽에만 있는 결제는 이 기기 저장에 옮겨 담고, 다른 쪽에서 회수한 결제는 여기서도 회수한 뒤 **올리기 전에 저장**한다
        /// (올리기는 디스크 파일을 그대로 보낸다 — 메모리만 바꾸면 회수 전 글이 올라간다). 바뀐 게 없으면 저장하지 않는다. 저장 실패면 false
        /// </summary>
        public static bool KeepPurchases(SaveData other)
        {
            // 합치기 + 저장을 한 번에: 저장을 못 하면 합친 것까지 통째로 되돌린다 (메모리에 반쯤 합친 장부가 남지 않게)
            return other == null || SaveSystem.Commit(() => Economy.IapLedger.MergeInto(SaveSystem.Data, other) > 0) != SaveSystem.CommitResult.SaveFailed;
        }

        /// <summary>
        /// 연결된 계정인데 클라우드가 다른 기기에서 바뀌었거나 이 기기 저장을 못 읽어 새로 시작했다 → 덮지 않고 한 번 고르게 한다.
        /// 로비가 다른 창이 없을 때 부른다. 실패하면 다음 자동 올리기(60초)가 다시 확인한다.
        /// </summary>
        public static async Task ResolveConflictAsync()
        {
            if (!ConflictPending || State != AccountState.Linked) return;
            ConflictPending = false;
            if (ChoicePending)
            {
                // ② 바꾸려던 계정의 진행 고르기가 남았다 → 계정 바꾸기 규칙으로 다시 고른다 (그 계정에 결제가 있으면 그 계정 진행만)
                _conflictDeferred = false;
                await ChooseSwitchAsync(LinkedName ?? Providers[0].DisplayName, resumed: true);
                return;
            }

            Set(AccountState.Busy, "클라우드 진행을 확인하는 중…");
            try
            {
                CloudCopy cloud = await LoadCloud(true);
                if (!cloud.HasSave)
                {
                    Set(AccountState.Linked, null);
                    _cloudChanged = true;
                    await UploadAsync(true, cloud);
                    return;
                }

                if (!cloud.Readable)
                {
                    _conflictDeferred = true;
                    Set(AccountState.Linked, cloud.UnreadableText);
                    return;
                }

                bool lost = SaveSystem.CloudRecheckNeeded;
                Choice choice = await Ask(lost ? "이 기기 저장을 읽지 못했어요" : "다른 기기에서 저장한 진행이 있어요",
                    lost ? "클라우드에 저장된 진행을 불러올 수 있어요" : "고른 진행으로 이어서 해요. 고르지 않은 쪽 진행은 사라져요", cloud);
                if (choice == Choice.Cancel)
                {
                    _conflictDeferred = true;
                    Set(AccountState.Linked, "고를 때까지 클라우드 저장을 멈췄어요. [지금 클라우드에 저장]을 누르면 다시 골라요");
                    return;
                }

                if (choice == Choice.Device)
                {
                    if (!KeepCloudPurchases(cloud))
                    {
                        _conflictDeferred = true;
                        Set(AccountState.Linked, "저장하지 못했어요. 기기 저장 공간을 확인하고 다시 해 주세요");
                        return;
                    }
                    Set(AccountState.Linked, null);
                    _cloudChanged = true;
                    bool ok = await UploadAsync(true, cloud);
                    Set(AccountState.Linked, ok ? "이 기기 진행을 클라우드에 올렸어요"
                        : ConflictPending ? "그사이 클라우드가 바뀌었어요. 다시 골라 주세요"
                        : "클라우드에 저장하지 못했어요. 인터넷을 확인해 주세요");
                    return;
                }

                if (!ApplyCloud(cloud))
                {
                    _conflictDeferred = true;
                    Set(AccountState.Linked, "저장하지 못했어요. 기기 저장 공간을 확인하고 다시 해 주세요");
                    return;
                }

                Set(AccountState.Linked, "클라우드 진행을 불러왔어요");
                Common.GameSession.ToLobby();
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Account] 클라우드 확인 실패: " + e.Message);
                _cloudChanged = true;
                Set(AccountState.Linked, e is RequestFailedException r ? AccountRules.ErrorText(r.ErrorCode, r.Message) : "클라우드를 확인하지 못했어요");
            }
        }

        // ------------------------------------------------------------------ 클라우드 올리기

        private static void OnSaved() => _cloudChanged = true;

        /// <summary>설정 → 지금 클라우드에 저장. 클라우드가 다른 곳에서 바뀌었으면 고르기를 다시 연다</summary>
        public static async Task UploadNowAsync()
        {
            if (ChoicePending && State == AccountState.Linked)
            {
                ConflictPending = true; // 계정 바꾸기 고르기가 남았다 → 그것부터
                await ResolveConflictAsync();
                return;
            }

            _conflictDeferred = false;
            _cloudChanged = true;
            bool ok = await UploadAsync(true);
            if (ConflictPending)
            {
                await ResolveConflictAsync();
                return;
            }

            Set(State, ok ? "클라우드에 저장했어요" : "클라우드에 저장하지 못했어요. 인터넷을 확인해 주세요");
        }

        /// <summary>
        /// 연결된 계정만, 로비에서만 올린다 (게스트는 이 기기에만, 전투 중에는 안 올린다). force가 아니면 60초에 한 번까지.
        /// 올리는 글은 마지막으로 파일에 저장된 글 그대로다 (판 도중 메모리 상태를 올리지 않는다).
        /// seen이 없으면 먼저 클라우드를 읽어 이 기기가 마지막으로 맞춘 뒤 바뀌었는지 보고(meta 올린 번호), 바뀌었으면 올리지 않고 ConflictPending.
        /// seen이 있으면(고르기에서 "이 기기"를 골랐다) 번호 확인 없이 그 판을 덮는다.
        /// 어느 쪽이든 읽은 판의 write lock을 붙여 쓴다 → 그사이 다른 기기가 쓰면 서버가 거절(충돌) → ConflictPending.
        /// 키가 없어 처음 만들 때는 lock이 없어 동시에 만든 기기를 못 막으므로, 쓴 뒤 다시 읽어 내 번호인지 확인한다.
        /// 더 새 게임의 저장을 읽은 기기는 빈 저장을 들고 있으니 절대 올리지 않는다.
        /// </summary>
        private static async Task<bool> UploadAsync(bool force, CloudCopy seen = null)
        {
            if (_uploading || SaveSystem.UnsupportedNewerSave || SaveSystem.WipePending || !InLobby) return false;
            if (_conflictDeferred && seen == null) return false;
            double now = Time.realtimeSinceStartupAsDouble;
            if (!AccountRules.ShouldUpload(State, ChoicePending, _cloudChanged || SaveSystem.IsDirty, force, now, _lastUpload)) return false;
            if (!SaveSystem.SaveIfDirty() || !ReadUploadFile(out string json, out SaveData saved)) return false;

            _uploading = true;
            _cloudChanged = false;
            _lastUpload = now;
            bool conflict = false;
            try
            {
                CloudCopy target = seen;
                if (target == null)
                {
                    target = await LoadCloud(false);
                    conflict = AccountRules.CloudChangedElsewhere(target.HasSave, target.Meta?.uploadId, SyncedUploadId);
                }

                if (!conflict)
                {
                    AccountSummary meta = AccountSummary.From(saved);
                    meta.uploadId = Guid.NewGuid().ToString("N");
                    meta.playerId = AuthenticationService.Instance.PlayerId;
                    string metaJson = JsonUtility.ToJson(meta);
                    if (!AccountRules.FitsCloud(json, metaJson))
                    {
                        Debug.LogWarning($"[Account] 저장이 클라우드 한도를 넘어 올리지 않음 ({json.Length:N0}자)");
                        return false;
                    }

                    await CloudSaveService.Instance.Data.Player.SaveAsync(new Dictionary<string, SaveItem>
                    {
                        [SaveKey] = new SaveItem(json, target.SaveLock),
                        [MetaKey] = new SaveItem(metaJson, target.MetaLock),
                    });
                    if (target.SaveLock == null || target.MetaLock == null)
                    {
                        // 처음 만들기: Cloud Save에는 "없을 때만 만들기"가 없다 (SaveItem 주석: 새로 만들 때는 lock을 비운다).
                        // 쓴 뒤 바로 한 번, 3초 뒤 한 번 더 읽어 둘 다 내 번호일 때만 맞춘 것으로 친다.
                        // ponytail: 그래도 남는 아주 짧은 동시 생성 틈은 다음 업로드의 번호 검사(CloudChangedElsewhere)가 잡아 고르기 창을 연다 — 이 기기 저장은 지우지 않는다. 완전히 막으려면 서버(Cloud Code) 생성 함수
                        conflict = (await LoadCloud(false)).Meta?.uploadId != meta.uploadId;
                        if (!conflict)
                        {
                            await Task.Delay(3000);
                            conflict = (await LoadCloud(false)).Meta?.uploadId != meta.uploadId;
                        }
                    }

                    if (!conflict)
                    {
                        SetSynced(meta.uploadId);
                        SaveSystem.CloudRechecked();
                        bool first = !_cloudChecked;
                        _cloudChecked = true;
                        if (first) Changed?.Invoke(); // 결제 화면: 계정당 1번 상품 · 첫 구매 2배가 열린다
                        return true;
                    }
                }
            }
            catch (CloudSaveConflictException e)
            {
                Debug.Log("[Account] 쓰는 사이 클라우드가 바뀜 (write lock 충돌): " + e.Message);
                conflict = true;
            }
            catch (Exception e)
            {
                _cloudChanged = true;
                Debug.LogWarning("[Account] 클라우드 저장 실패: " + e.Message);
                return false;
            }
            finally
            {
                _uploading = false;
            }

            Debug.Log("[Account] 클라우드가 이 기기가 맞춘 뒤 바뀜 (다른 기기 · 저장 복구) → 덮지 않고 고르기를 기다림");
            _cloudChanged = true;
            _cloudChecked = false;
            ConflictPending = true;
            Changed?.Invoke();
            return false;
        }

        /// <summary>
        /// 올릴 글: 본 저장 파일 글 + 그 글을 읽은 데이터 (요약용). 파일이 없거나 못 읽으면 false.
        /// 파일에 옛 모양(구매 토큰 원문 거래 번호)이 남아 있으면 — 읽을 때 메모리에서만 바뀐 것 — 지금 메모리(바꾼 모양)로 먼저 저장하고 다시 읽는다.
        /// 그래서 옮기기를 거치지 않은 파일은 올라가지 않는다
        /// </summary>
        public static bool ReadUploadFile(out string json, out SaveData saved)
        {
            if (!ReadSavedFile(out json, out saved)) return false;
            if (!saved.purchaseIdsMigrated) return true; // 읽을 때 바꾼 거래 번호가 하나도 없다 (파싱한 칸을 하나하나 본 결과)
            Debug.Log("[Account] 저장 파일에 옛 거래 번호가 남아 먼저 다시 저장한 뒤 올림");
            return SaveSystem.Save() && ReadSavedFile(out json, out saved) && !saved.purchaseIdsMigrated;
        }

        private static bool ReadSavedFile(out string json, out SaveData saved)
        {
            json = null;
            saved = null;
            try
            {
                string path = SaveSystem.Store.MainPath;
                if (!File.Exists(path)) return false;
                json = File.ReadAllText(path, Encoding.UTF8);
                SaveStore.ParseResult parsed = SaveStore.Parse(json, out saved, out string problem);
                if (parsed == SaveStore.ParseResult.Ok || parsed == SaveStore.ParseResult.Tampered) return true;
                Debug.LogWarning($"[Account] 저장 파일을 올릴 수 없음 ({parsed}): {problem}");
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Account] 저장 파일을 읽지 못함: " + e.Message);
            }

            return false;
        }

        // ------------------------------------------------------------------ 계정 삭제

        /// <summary>
        /// 계정 삭제 (LiveOps 3-3, 바로 삭제): 외부 계정 연결 끊기 → 서버 결제 장부 보관 · 지우기 → 클라우드 저장 지우기 → UGS 계정 지우기 → 이 기기 진행 지우기 → 로비 (동의부터 새로).
        /// UGS 계정 삭제는 Cloud Save를 지우지 않으므로 먼저 지운다. 서버 단계가 실패하면 이 기기 진행은 그대로 둔다.
        /// 외부 연결을 끊지 못하면(취소 · 다른 계정 · 실패) 건너뛰고 지울지 묻는다 — 앱 안 삭제는 언제나 끝까지 갈 수 있어야 한다 (구글 정책).
        /// </summary>
        public static async Task DeleteAsync()
        {
            AccountState before = State;
            if (before != AccountState.Guest && before != AccountState.Linked)
            {
                Set(before, "인터넷에 연결된 뒤에 지울 수 있어요");
                return;
            }

            Set(AccountState.Busy, "계정을 지우는 중…");
            ConflictPending = false;
            while (_uploading) await Task.Yield(); // 올리는 중이면 끝나길 기다린다 (지운 뒤에 키가 다시 생기지 않게)
            string syncedKey = SyncedKey;
            try
            {
                IAuthenticationService auth = AuthenticationService.Instance;
                // 서버 단계 전에 삭제를 남긴다: 이 뒤 어디서 꺼져도 다음 실행은 이 기기 진행을 읽기 전에 지우고 삭제를 마저 한다
                PlayerPrefs.SetString(DeleteIntentKey, auth.PlayerId ?? string.Empty);
                PlayerPrefs.Save();
                SaveSystem.WipeOnNextLoad(true);
                PlayerInfo info = auth.PlayerInfo;
                try
                {
                    info = await auth.GetPlayerInfoAsync();
                }
                catch (Exception e)
                {
                    Debug.LogWarning("[Account] 플레이어 정보를 못 받음: " + e.Message);
                }

                foreach (LoginProvider p in Providers)
                {
                    string linkedId = p.LinkedId(info);
                    if (string.IsNullOrEmpty(linkedId)) continue;
                    LoginProvider.Result r = p.Available()
                        ? await p.Unlink(linkedId)
                        : new LoginProvider.Result(LoginProvider.Status.Error, null, "NotSupported");
                    if (r.Status == LoginProvider.Status.Ok) continue;

                    Debug.LogWarning($"[Account] {p.DisplayName} 연결 끊기 못 함: {r.Status} {r.Code}");
                    bool skip = Confirm != null && await Confirm($"{p.DisplayName} 연결을 끊지 못했어요",
                        "건너뛰어도 게임 계정과 진행은 모두 지워져요\n카카오톡 설정 → 연결된 서비스 관리에서 끊을 수 있어요",
                        $"{p.DisplayName} 연결 끊기를 건너뛰고 삭제");
                    if (!skip)
                    {
                        AbortDeletion();
                        Set(before, "계정 삭제를 그만뒀어요");
                        return;
                    }
                }

                Set(AccountState.Busy, "계정을 지우는 중…");
                // 서버 결제 장부를 먼저 보관본(되돌릴 수 없는 ID, 5년 뒤 지움)으로 옮기고 지운다 — 게임은 보호된 장부를 지울 수 없어서 서버가 한다 (개인정보 처리방침 3장)
                string archived = await Economy.PurchaseServer.ArchiveAndDeleteAsync();
                if (archived != "ok") throw new InvalidOperationException("결제 기록 보관 실패: " + (archived ?? "빈 답"));
                await CloudSaveService.Instance.Data.Player.DeleteAllAsync();
                if (ChoicePending) ClearPending(); // 세션이 지워진 뒤 계정 바꾸기 표시가 남으면 다음 실행이 막힌다 (이 기기 진행은 아래에서 지운다)
                await auth.DeleteAccountAsync(); // 끝나면 로그아웃 + 세션도 지운다
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Account] 계정 삭제 실패: " + e.Message);
                AbortDeletion(); // 계정이 남았다 → 이 기기 진행도 그대로 (다시 지우면 이어서 된다)
                Set(before, e is RequestFailedException r ? AccountRules.ErrorText(r.ErrorCode, r.Message) : "지우지 못했어요. 잠시 뒤에 다시 해 주세요");
                return;
            }

            PlayerPrefs.DeleteKey(syncedKey);
            ClearPending();
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            _sandbox = false;
#endif
            LinkedName = null;
            _cloudChanged = false;
            _conflictDeferred = false;
            _cloudChecked = false;
            // 지우기 표시를 먼저 남기고 지운다. 실패해도 옛 진행은 메모리에 남지 않고, 로비는 막히며 다음 실행에서 이어서 지운다
            if (!SaveSystem.ResetToNewGame()) Debug.LogError("[Account] 계정은 지웠지만 이 기기 진행을 다 지우지 못함 (다음 실행에서 이어서): " + SaveSystem.LastError);
            else
            {
                PlayerPrefs.DeleteKey(DeleteIntentKey); // 서버 · 이 기기 모두 지웠다 (못 지웠으면 다음 실행이 지운 뒤 표시를 지운다)
                PlayerPrefs.Save();
            }
            Set(AccountState.Connecting, null); // 로비가 동의를 다시 받은 뒤 Start()가 새 게스트를 만든다
            Common.GameSession.ToLobby();
        }

        private static void AbortDeletion()
        {
            PlayerPrefs.DeleteKey(DeleteIntentKey);
            PlayerPrefs.Save();
            SaveSystem.WipeOnNextLoad(false);
        }

        /// <summary>
        /// 지난 실행이 계정 삭제 도중 꺼졌다 (이 기기 진행은 저장을 읽을 때 이미 지웠다). 로그인 전에: 남은 세션이 그 계정이면 서버 단계를 마저 하고
        /// (카카오 연결 끊기는 건너뛴다 — 카카오톡 설정에서 끊을 수 있다), 세션이 없거나 서버가 거절하면(지운 계정) 표시만 지운다.
        /// 서버에 못 닿으면 false — 표시를 남기고 새 게스트를 만들지 않는다 (다음에 다시)
        /// </summary>
        private static async Task<bool> ResumeDeletionAsync(IAuthenticationService auth)
        {
            string target = PlayerPrefs.GetString(DeleteIntentKey, string.Empty);
            bool session = auth.IsSignedIn || auth.SessionTokenExists;
            bool rejected = false;
            if (session && !auth.IsSignedIn)
            {
                try
                {
                    await auth.SignInAnonymouslyAsync(); // 남은 세션 그대로 (새 게스트가 아니다)
                }
                catch (RequestFailedException e) when (e.ErrorCode == AuthenticationErrorCodes.InvalidSessionToken)
                {
                    rejected = true; // 지운 계정의 세션 (SDK가 지웠다)
                }
            }

            AccountRules.DeletionStep step = AccountRules.DecideDeletionResume(target, session, auth.IsSignedIn ? auth.PlayerId : null, rejected);
            Debug.Log($"[Account] 계정 삭제 이어 받기: {step}");
            if (step == AccountRules.DeletionStep.FinishRemote)
            {
                if (await Economy.PurchaseServer.ArchiveAndDeleteAsync() != "ok") return false;
                await CloudSaveService.Instance.Data.Player.DeleteAllAsync();
                await auth.DeleteAccountAsync();
            }

            PlayerPrefs.DeleteKey(SyncedKeyPrefix + target);
            PlayerPrefs.DeleteKey(SwitchPrepKey);
            PlayerPrefs.DeleteKey(SwitchTargetKey);
            PlayerPrefs.DeleteKey(DeleteIntentKey);
            PlayerPrefs.Save();
            SyncSwitching();
            return true;
        }

        // ------------------------------------------------------------------ 수명

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            State = AccountState.Offline;
            Message = null;
            LinkedName = null;
            Changed = null;
            ChooseProgress = null;
            Confirm = null;
            ConflictPending = false;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            _sandbox = false;
#endif
            _starting = false;
            _cloudChanged = false;
            _uploading = false;
            _conflictDeferred = false;
            _cloudChecked = false;
            _lastUpload = double.NegativeInfinity;
        }

        /// <summary>자동 클라우드 저장: 로비에서만 몇 초마다 확인 (전투 중 끊김 방지) + 로비에서 앱이 뒤로 가거나 꺼질 때</summary>
        private sealed class Lifecycle : MonoBehaviour
        {
            private static Lifecycle _instance;
            private float _next;

            public static void Ensure()
            {
                if (_instance != null || !Application.isPlaying) return;
                var go = new GameObject("AccountLifecycle") { hideFlags = HideFlags.HideInHierarchy };
                DontDestroyOnLoad(go);
                _instance = go.AddComponent<Lifecycle>();
            }

            private void Update()
            {
                if (Time.unscaledTime < _next) return;
                _next = Time.unscaledTime + 5f;
                _ = UploadAsync(false);
            }

            private void OnApplicationPause(bool paused)
            {
                if (paused) _ = UploadAsync(true);
            }

            private void OnApplicationQuit() => _ = UploadAsync(true);

            [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
            private static void ResetInstance() => _instance = null;
        }
    }
}
