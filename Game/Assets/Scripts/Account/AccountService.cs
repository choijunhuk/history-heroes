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
    /// 올리기 전에 meta만 읽어, 이 기기가 마지막으로 맞춘 뒤 다른 기기가 올렸으면 덮지 않고 고르게 한다 (서버 기준 합치기는 아직 없음).
    /// </summary>
    public static class AccountService
    {
        public const string EnvironmentName = "production";
        public const string SaveKey = "save";
        public const string MetaKey = "meta";

        /// <summary>진행 고르기를 끝내기 전에 앱이 꺼졌다는 표시 (다음 실행에서 카카오 계정에 이어 붙어 클라우드를 덮지 않게)</summary>
        private const string PendingChoiceKey = "hh.account.pendingChoice";
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
        private static double _lastUpload = double.NegativeInfinity;

        public static string PlayerId
        {
            get
            {
                if (UnityServices.State != ServicesInitializationState.Initialized) return null;
                IAuthenticationService auth = AuthenticationService.Instance;
                return auth.IsSignedIn ? auth.PlayerId : null;
            }
        }

        private static bool ChoicePending => PlayerPrefs.GetInt(PendingChoiceKey, 0) == 1;

        private static void ClearPending()
        {
            PlayerPrefs.DeleteKey(PendingChoiceKey);
            PlayerPrefs.Save(); // 안드로이드는 Save 전에 꺼지면 지운 게 되살아난다
        }

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
                if (ChoicePending)
                {
                    // 진행 고르기 도중 꺼졌다: 그 카카오 계정에 그대로 이어 붙으면 이 기기 진행이 클라우드를 덮는다 → 게스트로 돌아간다
                    if (auth.IsSignedIn) auth.SignOut(true);
                    else if (auth.SessionTokenExists) auth.ClearSessionToken();
                    ClearPending();
                }

                if (auth.IsSignedIn && auth.IsExpired) auth.SignOut();
                // 저장된 세션이 있으면 그 플레이어(게스트든 카카오든)로 돌아온다
                if (!auth.IsSignedIn) await auth.SignInAnonymouslyAsync();
                await RefreshLinked();
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
            Set(LinkedName != null ? AccountState.Linked : AccountState.Guest, null);
            if (State != AccountState.Linked) return;

            // 저장을 못 읽어 새로 시작했다: 맞춘 기록을 지워 두면 다음 올리기가 덮지 않고 클라우드 복구를 권한다 (다시 켜도 그대로)
            if (SaveSystem.LostSaveOnLoad)
            {
                PlayerPrefs.DeleteKey(SyncedKey);
                PlayerPrefs.Save();
            }

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
            PlayerPrefs.SetInt(PendingChoiceKey, 1);
            PlayerPrefs.Save();
            try
            {
                // 지금 게스트 플레이어는 클라우드에 올린 게 없다 (게스트는 이 기기에만 저장). 다른 계정으로 들어가면 되찾을 수 없으니 지우고 들어간다
                try
                {
                    await auth.DeleteAccountAsync();
                }
                catch (Exception e)
                {
                    Debug.LogWarning("[Account] 게스트 플레이어를 지우지 못함 (로그아웃만): " + e.Message);
                    auth.SignOut(true);
                }

                await provider.SignIn(token);
                CloudCopy cloud = await LoadCloud();
                if (cloud == null)
                {
                    // 연결된 계정이지만 클라우드 저장이 없다 → 이 기기 진행을 올린다
                    await FinishWithDevice(provider);
                    return;
                }

                if (!cloud.Readable)
                {
                    await BackToGuest(cloud.UnreadableText);
                    return;
                }

                Set(AccountState.Busy, "어느 진행을 쓸지 골라 주세요");
                Choice choice = await Ask($"이 {provider.DisplayName} 계정에 저장된 진행이 있어요", "고른 진행으로 이어서 해요. 고르지 않은 쪽 진행은 사라져요", cloud);
                if (choice == Choice.Cancel)
                {
                    await BackToGuest("연결을 그만뒀어요. 이 기기 진행 그대로예요");
                    return;
                }

                if (choice == Choice.Device)
                {
                    await FinishWithDevice(provider);
                    return;
                }

                if (!ApplyCloud(cloud))
                {
                    await BackToGuest("저장하지 못했어요. 기기 저장 공간을 확인하고 다시 해 주세요");
                    return;
                }

                ClearPending();
                LinkedName = provider.DisplayName;
                Set(AccountState.Linked, $"{provider.DisplayName} 계정의 진행을 불러왔어요");
                Common.GameSession.ToLobby(); // 바뀐 저장으로 로비를 다시 그린다
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Account] 계정 전환 실패: " + e.Message);
                await BackToGuest(e is RequestFailedException r ? AccountRules.ErrorText(r.ErrorCode, r.Message) : "문제가 생겼어요. 잠시 뒤에 다시 해 주세요");
            }
        }

        /// <summary>사용자가 "이 기기 진행"을 골랐다 (또는 클라우드가 비었다): 클라우드를 확인 없이 덮는다</summary>
        private static async Task FinishWithDevice(LoginProvider provider)
        {
            ClearPending();
            LinkedName = provider.DisplayName;
            Set(AccountState.Linked, null);
            _cloudChanged = true;
            bool uploaded = await UploadAsync(true, overwrite: true);
            Set(AccountState.Linked, uploaded
                ? $"{provider.DisplayName} 계정에 이 기기 진행을 올렸어요"
                : $"{provider.DisplayName} 계정을 연결했어요. 클라우드 저장은 다음에 다시 해요");
        }

        /// <summary>계정 전환을 그만둔다: 외부 계정에서 나와 새 게스트로 (이 기기 저장은 손대지 않았다)</summary>
        private static async Task BackToGuest(string message)
        {
            IAuthenticationService auth = AuthenticationService.Instance;
            try
            {
                if (auth.IsSignedIn) auth.SignOut(true);
                else if (auth.SessionTokenExists) auth.ClearSessionToken();
                ClearPending();
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

        // ------------------------------------------------------------------ 클라우드 읽기 · 고르기

        private sealed class CloudCopy
        {
            public SaveStore.ParseResult Parsed;
            public SaveData Data;
            public string Problem;
            public AccountSummary Meta;

            public bool Readable => Parsed == SaveStore.ParseResult.Ok || Parsed == SaveStore.ParseResult.Tampered;

            public string UnreadableText => Parsed == SaveStore.ParseResult.NewerSchema
                ? "클라우드 진행이 더 새 버전에서 저장됐어요. 게임을 업데이트한 뒤 다시 해 주세요"
                : "클라우드 진행을 읽지 못했어요. 문의해 주세요";
        }

        private static async Task<AccountSummary> LoadCloudMeta()
        {
            Dictionary<string, Item> items = await CloudSaveService.Instance.Data.Player.LoadAsync(new HashSet<string> { MetaKey });
            return items.TryGetValue(MetaKey, out Item meta) ? JsonUtility.FromJson<AccountSummary>(meta.Value.GetAs<string>()) : null;
        }

        /// <summary>클라우드 저장 받기 (파일 읽기와 같은 검사). 저장이 없으면 null</summary>
        private static async Task<CloudCopy> LoadCloud()
        {
            Dictionary<string, Item> items = await CloudSaveService.Instance.Data.Player.LoadAsync(new HashSet<string> { SaveKey, MetaKey });
            if (!items.TryGetValue(SaveKey, out Item save)) return null;
            var copy = new CloudCopy();
            copy.Parsed = SaveStore.Parse(save.Value.GetAs<string>(), out copy.Data, out copy.Problem);
            if (items.TryGetValue(MetaKey, out Item meta)) copy.Meta = JsonUtility.FromJson<AccountSummary>(meta.Value.GetAs<string>());
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

        /// <summary>클라우드 진행으로 바꾼다: 이 기기 진행은 백업(.run1 · .bak)으로 남는다 (해시 · 구조 옮기기는 Parse가 이미 했다)</summary>
        private static bool ApplyCloud(CloudCopy cloud)
        {
            if (!SaveSystem.Replace(cloud.Data)) return false;
            SetSynced(cloud.Meta?.uploadId);
            _cloudChanged = false;
            return true;
        }

        /// <summary>
        /// 연결된 계정인데 클라우드가 다른 기기에서 바뀌었거나 이 기기 저장을 못 읽어 새로 시작했다 → 덮지 않고 한 번 고르게 한다.
        /// 로비가 다른 창이 없을 때 부른다. 실패하면 다음 자동 올리기(60초)가 다시 확인한다.
        /// </summary>
        public static async Task ResolveConflictAsync()
        {
            if (!ConflictPending || State != AccountState.Linked) return;
            ConflictPending = false;
            Set(AccountState.Busy, "클라우드 진행을 확인하는 중…");
            try
            {
                CloudCopy cloud = await LoadCloud();
                if (cloud == null)
                {
                    Set(AccountState.Linked, null);
                    _cloudChanged = true;
                    await UploadAsync(true);
                    return;
                }

                if (!cloud.Readable)
                {
                    _conflictDeferred = true;
                    Set(AccountState.Linked, cloud.UnreadableText);
                    return;
                }

                bool lost = SaveSystem.LostSaveOnLoad;
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
                    Set(AccountState.Linked, null);
                    _cloudChanged = true;
                    bool ok = await UploadAsync(true, overwrite: true);
                    Set(AccountState.Linked, ok ? "이 기기 진행을 클라우드에 올렸어요" : "클라우드에 저장하지 못했어요. 인터넷을 확인해 주세요");
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
        /// overwrite가 아니면 먼저 meta만 읽어 이 기기가 마지막으로 맞춘 뒤 바뀌었는지 보고, 바뀌었으면 올리지 않고 ConflictPending.
        /// 더 새 게임의 저장을 읽은 기기는 빈 저장을 들고 있으니 절대 올리지 않는다.
        /// </summary>
        private static async Task<bool> UploadAsync(bool force, bool overwrite = false)
        {
            if (_uploading || SaveSystem.UnsupportedNewerSave || !InLobby) return false;
            if (_conflictDeferred && !overwrite) return false;
            double now = Time.realtimeSinceStartupAsDouble;
            if (!AccountRules.ShouldUpload(State, ChoicePending, _cloudChanged || SaveSystem.IsDirty, force, now, _lastUpload)) return false;
            if (!SaveSystem.SaveIfDirty() || !ReadSavedFile(out string json, out SaveData saved)) return false;

            _uploading = true;
            _cloudChanged = false;
            _lastUpload = now;
            bool conflict = false;
            try
            {
                if (!overwrite)
                {
                    AccountSummary cloudMeta = await LoadCloudMeta();
                    conflict = AccountRules.CloudChangedElsewhere(cloudMeta != null, cloudMeta?.uploadId, SyncedUploadId);
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

                    await CloudSaveService.Instance.Data.Player.SaveAsync(new Dictionary<string, object> { [SaveKey] = json, [MetaKey] = metaJson });
                    SetSynced(meta.uploadId);
                    return true;
                }
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
            ConflictPending = true;
            return false;
        }

        /// <summary>본 저장 파일 글 + 그 글을 읽은 데이터 (요약용). 파일이 없거나 못 읽으면 false</summary>
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
        /// 계정 삭제 (LiveOps 3-3, 바로 삭제): 외부 계정 연결 끊기 → 클라우드 저장 지우기 → UGS 계정 지우기 → 이 기기 진행 지우기 → 로비 (동의부터 새로).
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
                        Set(before, "계정 삭제를 그만뒀어요");
                        return;
                    }
                }

                Set(AccountState.Busy, "계정을 지우는 중…");
                await CloudSaveService.Instance.Data.Player.DeleteAllAsync();
                await auth.DeleteAccountAsync(); // 끝나면 로그아웃 + 세션도 지운다
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Account] 계정 삭제 실패: " + e.Message);
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
            if (!SaveSystem.ResetToNewGame()) Debug.LogError("[Account] 계정은 지웠지만 이 기기 진행을 지우지 못함: " + SaveSystem.LastError);
            Set(AccountState.Connecting, null); // 로비가 동의를 다시 받은 뒤 Start()가 새 게스트를 만든다
            Common.GameSession.ToLobby();
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
