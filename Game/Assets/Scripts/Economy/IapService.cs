using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using HistoryHeroes.Records;
using UnityEngine;
using UnityEngine.Purchasing;

namespace HistoryHeroes.Economy
{
    /// <summary>
    /// 실제 결제 (Unity IAP 5 · 안드로이드는 구글 플레이 결제, 에디터는 가짜 상점). 로비가 동의 뒤에 Start()를 부른다.
    /// 순서: 연결 → 상품(가격) 받기 → 끝나지 않은 결제 받기 → 결제가 오면 [영수증 검사 → 장부에 지급 → 저장] → 그다음에 구매 확정.
    /// 확정 전에 앱이 꺼지면 상점이 다음 연결 때 같은 거래를 다시 보내고, 장부가 두 번 지급을 막는다 (Monetization 7장).
    /// 연결이 안 되면 상점 단추만 "연결할 수 없어요"가 되고 게임은 그대로 돈다.
    /// 안드로이드는 영수증 검사 키(GooglePlayTangle)가 없으면 아무것도 팔지 않는다 (닫힌 쪽으로 실패).
    /// 안드로이드 기기는 지급을 **서버가 정한다** (ServerAuthority): 결제가 오면 서버(PurchaseServer.VerifyAsync)가 구글에 묻고 서버 장부로 수량을 정해
    /// 돌려준 그대로 이 기기 장부에 넣고 저장한 뒤 확정한다. 서버가 안 되면 결제를 남겨 두고 다시 묻는다 (확정 안 함).
    /// 로그인하면 서버 장부를 받아 이 기기에 없는 결제를 다시 넣는다 (SyncServer). 에디터 · 가짜 상점은 서버 없이 이 기기 장부로 지급한다 (개발용).
    /// </summary>
    public static class IapService
    {
        public enum Status
        {
            Off,
            Connecting,
            Ready,
            /// <summary>인터넷 없음 · 상점 없음 · 상품을 못 받음 (다음 로비 · 상점을 열 때 다시 시도)</summary>
            Unavailable,
        }

        public static Status State { get; private set; } = Status.Off;
        /// <summary>결제 창을 띄운 상품 (끝날 때까지 다른 구매 단추를 막는다)</summary>
        public static string BuyingId { get; private set; }
        /// <summary>상태 · 가격 · 진행 중 구매가 바뀌었다</summary>
        public static event Action Changed;
        /// <summary>지급하고 저장까지 끝났다 (화면은 "보석 +N")</summary>
        public static event Action<PurchaseEntry> Granted;
        /// <summary>사용자에게 보여 줄 짧은 안내 (실패 · 취소 · 대기)</summary>
        public static event Action<string> Notice;

        public const string UnavailableText = "상점에 연결할 수 없어요";
        public const string NotReadyText = "결제 확인 준비 중이에요. 이번 결제는 확정하지 않아서 3일 안에 자동으로 환불돼요.";
        public const string InvalidReceiptText = "결제를 확인하지 못해서 지급하지 않았어요. 확정하지 않은 결제는 3일 안에 자동으로 환불돼요. 계속되면 문의해 주세요.";
        public const string CheckingText = "결제 확인 중이에요. 확인되면 바로 들어와요 (인터넷 연결을 확인해 주세요).";
        public const string SaveFailedText = "저장하지 못해서 아직 지급하지 않았어요. 저장 공간을 확인해 주세요. 다음에 게임을 열면 다시 받아요.";
        public const string AlreadyClaimedText = "첫 결제 패키지는 계정당 1번만 받을 수 있어요. 이번 결제는 확정하지 않아 환불 대기 중이에요 (3일 안에 자동 환불).";
        public const string UngrantableText = "이 결제를 지급하지 못해 환불 대기 중이에요 (3일 안에 자동 환불). 계속되면 문의해 주세요.";
        public const string PendingText = "결제가 아직 끝나지 않았어요. 결제가 끝나면 들어와요.";
        public const string NotPaidText = "무료 결제(프로모션 코드 · 보상형)는 이 게임에서 지급하지 않아요. 확정하지 않아 3일 안에 자동으로 취소돼요.";
        public const string VoidedText = "환불 · 취소된 결제라서 지급하지 않았어요.";
        /// <summary>막 산 결제를 구글이 아직 모른다고(404) 하면 이 시간 동안은 "확인 중"으로 다시 묻는다 (구글 쪽 반영이 늦을 수 있다)</summary>
        public static readonly TimeSpan JustBoughtWindow = TimeSpan.FromMinutes(10);
        /// <summary>확정(소비) 실패를 이만큼 다시 하면 멈춘다 — 다음 실행에 상점이 다시 보내면 그때 또 (끝없이 돌지 않게)</summary>
        public const int MaxConfirmTries = 6;
        /// <summary>확정이 실패하면 이만큼 뒤에 다시 (구글은 3일 안에 확정하지 않으면 환불한다)</summary>
        private const int ConfirmRetryMilliseconds = 10000;

        private static StoreController _store;
        /// <summary>지급은 끝났는데 확정이 아직인 거래 (확정이 실패하면 잠시 뒤 · 다시 연결될 때 · 상점을 열 때 또 확정한다)</summary>
        private static readonly Dictionary<string, PendingOrder> Unconfirmed = new Dictionary<string, PendingOrder>();
        /// <summary>이번 실행에서 이미 안내한 거래 (지급 못 한 결제가 다시 올 때마다 같은 안내를 띄우지 않게)</summary>
        private static readonly HashSet<string> Noticed = new HashSet<string>();
        /// <summary>확인 창에서 보여 준 "받는 것" (지급이 달라지면 알린다)</summary>
        private static PurchaseEntry _shown;
        private static bool _retryScheduled;
        /// <summary>Play를 다시 시작하면 바뀐다 (지난 Play에서 걸어 둔 재시도가 새 Play에 끼어들지 않게)</summary>
        private static int _session;

        /// <summary>서버 답을 기다리는 결제 (거래 번호 → 결제). 다시 묻기: 잠시 뒤(점점 길게) · 로그인될 때 · 상점을 열 때 · 상점이 다시 보낼 때</summary>
        private static readonly Dictionary<string, PendingOrder> Waiting = new Dictionary<string, PendingOrder>();
        /// <summary>지금 서버에 묻는 중인 거래 (같은 결제를 동시에 두 번 묻지 않게)</summary>
        private static readonly HashSet<string> Asking = new HashSet<string>();
        /// <summary>다시 묻기를 걸어 둔 거래 + 몇 번째인지</summary>
        private static readonly Dictionary<string, int> AskCount = new Dictionary<string, int>();
        private static readonly HashSet<string> AskScheduled = new HashSet<string>();
        /// <summary>이번 실행에서 처음 서버에 물은 시각 (막 산 결제의 404를 잠시 기다리려고)</summary>
        private static readonly Dictionary<string, DateTime> FirstAsked = new Dictionary<string, DateTime>();
        /// <summary>확정이 실패한 횟수 (MaxConfirmTries까지)</summary>
        private static readonly Dictionary<string, int> ConfirmTries = new Dictionary<string, int>();

        /// <summary>
        /// 서버에 묻기 직전의 주인: 로그인한 플레이어 + 메모리 저장의 세대(SaveSystem.Generation). 답이 왔을 때 둘 중 하나라도 바뀌었으면
        /// (계정 전환 · 클라우드 진행 받기 · 계정 삭제) 그 답을 지금 진행에 넣지도, 결제를 확정하지도 않는다 — 결제는 남겨 두고 지금 주인으로 다시 묻는다
        /// </summary>
        public readonly struct Owner
        {
            public readonly string Player;
            public readonly int Generation;

            public Owner(string player, int generation)
            {
                Player = player;
                Generation = generation;
            }

            public static Owner Now => new Owner(Account.AccountService.PlayerId, SaveSystem.Generation);

            public bool Same(Owner other) => Player == other.Player && Generation == other.Generation;
        }
        /// <summary>이번 실행에서 서버 장부와 맞춘 UGS 플레이어 (계정이 바뀌면 다시 맞춘다)</summary>
        private static string _serverPlayer;
        /// <summary>마지막으로 받은 서버 장부에서: 서버에 구글 서비스 계정이 있다</summary>
        private static bool _serverConfigured;
        private static bool _serverSyncing;

        /// <summary>지급을 서버가 정한다: 안드로이드 기기 (에디터 · 가짜 상점은 아님)</summary>
        public static bool ServerAuthority
        {
            get
            {
#if UNITY_ANDROID && !UNITY_EDITOR
                return true;
#else
                return false;
#endif
            }
        }

        /// <summary>이번 실행에서 지금 로그인한 플레이어의 서버 장부를 받아 이 기기 장부에 맞췄다</summary>
        public static bool ServerSynced => _serverPlayer != null && _serverPlayer == Account.AccountService.PlayerId;

        /// <summary>
        /// 이 기기 장부의 계정당 1번 · 첫 구매 2배를 믿고 팔아도 되나 (상점 카드 · 확인 창 · Buy).
        /// 기기: 이번 실행에서 서버 장부와 맞춘 뒤 (지급은 어차피 서버가 정하고, 맞춘 장부라 표시도 서버와 같다).
        /// 에디터: 지금처럼 클라우드 동기화 (AccountService.SyncSettled)
        /// </summary>
        public static bool LedgerSettled => ServerAuthority ? ServerSynced : Account.AccountService.SyncSettled;

        public static void Start()
        {
            if (ServerAuthority)
            {
                Account.AccountService.Changed -= OnAccountChanged;
                Account.AccountService.Changed += OnAccountChanged;
                SyncServer();
                AskWaitingAgain();
            }

            if (State == Status.Connecting) return;
            if (State == Status.Ready)
            {
                RetryConfirm();
                return;
            }
            Connect();
        }

        private static async void Connect()
        {
            Set(Status.Connecting);
            try
            {
                if (_store == null)
                {
                    // StoreController()는 부를 때마다 새 껍데기지만 이벤트는 공용 서비스에 붙는다 → 한 번만 만들고 한 번만 붙인다
                    _store = UnityIAPServices.StoreController();
                    Wire(_store, true);
                }

                await _store.Connect();
            }
            catch (Exception e)
            {
                Debug.LogWarning("[IAP] 연결 실패: " + e.Message);
                Set(Status.Unavailable);
            }
        }

        private static void Wire(StoreController store, bool on)
        {
            if (on)
            {
                store.OnStoreConnected += OnConnected;
                store.OnStoreDisconnected += OnDisconnected;
                store.OnProductsFetched += OnProductsFetched;
                store.OnProductsFetchFailed += OnProductsFetchFailed;
                store.OnPurchasesFetched += OnPurchasesFetched;
                store.OnPurchasesFetchFailed += OnPurchasesFetchFailed;
                store.OnPurchasePending += OnPurchasePending;
                store.OnPurchaseConfirmed += OnPurchaseConfirmed;
                store.OnPurchaseFailed += OnPurchaseFailed;
                store.OnPurchaseDeferred += OnPurchaseDeferred;
                return;
            }

            store.OnStoreConnected -= OnConnected;
            store.OnStoreDisconnected -= OnDisconnected;
            store.OnProductsFetched -= OnProductsFetched;
            store.OnProductsFetchFailed -= OnProductsFetchFailed;
            store.OnPurchasesFetched -= OnPurchasesFetched;
            store.OnPurchasesFetchFailed -= OnPurchasesFetchFailed;
            store.OnPurchasePending -= OnPurchasePending;
            store.OnPurchaseConfirmed -= OnPurchaseConfirmed;
            store.OnPurchaseFailed -= OnPurchaseFailed;
            store.OnPurchaseDeferred -= OnPurchaseDeferred;
        }

        /// <summary>상점이 준 가격 글 ("₩1,200", 가짜 상점은 "$0.01"). 못 받았으면 null → 단추에 UnavailableText</summary>
        public static string Price(string productId)
        {
            if (State != Status.Ready) return null;
            Product p = _store.GetProductById(productId);
            return p != null && p.availableToPurchase ? p.metadata.localizedPriceString : null;
        }

        /// <summary>
        /// 결제를 검사할 준비가 됐다 (에디터 · 가짜 상점은 늘 true). 안드로이드는 GooglePlayTangle이 있고, 서버 장부를 받았다면 서버에 구글 서비스 계정이 있을 때만.
        /// 아니면 팔지 않는다 ("결제 준비 중"). 서버 장부를 아직 못 받았으면 LedgerSettled가 막는다 ("계정 확인 중")
        /// </summary>
        public static bool ValidationReady
        {
            get
            {
#if UNITY_ANDROID && !UNITY_EDITOR
                return GooglePublicKey() != null && (!ServerSynced || _serverConfigured);
#else
                return true;
#endif
            }
        }

        /// <summary>
        /// 결제 창을 연다 (플레이어가 확인 창에서 [구매]를 눌렀을 때만). 결과는 Granted · Notice로 온다.
        /// shown: 확인 창에서 보여 준 받는 것 (그사이 첫 구매 2배가 다른 결제에 쓰였으면 지급 때 알린다)
        /// </summary>
        public static bool Buy(string productId, PurchaseEntry shown = null)
        {
            IapCatalog.Item item = IapCatalog.Find(productId);
            if (BuyingId != null || item == null || Price(productId) == null || !ValidationReady) return false;
            if (!IapLedger.Sellable(SaveSystem.Data, item, LedgerSettled)) return false; // 계정당 1번 상품을 두 번 결제하지 않게 (장부를 맞추기 전에는 안 판다)
            // 이 결제를 지금 플레이어에 묶는다: 서버는 구글이 돌려준 계정 표시가 부른 플레이어와 같을 때만 지급한다 (남의 토큰으로 받기 막기)
            if (ServerAuthority) _store.GooglePlayStoreExtendedService?.SetObfuscatedAccountId(IapLedger.Hash(Account.AccountService.PlayerId));
            BuyingId = productId;
            _shown = shown;
            Changed?.Invoke();
            _store.PurchaseProduct(productId);
            return true;
        }

        private static void Set(Status state)
        {
            State = state;
            Changed?.Invoke();
        }

        private static void Say(string text) => Notice?.Invoke(text);

        private static void EndBuying(string productId)
        {
            if (BuyingId == null || (productId != null && BuyingId != productId)) return;
            BuyingId = null;
            Changed?.Invoke();
        }

        // ------------------------------------------------------------------ 연결 · 상품

        private static void OnConnected()
        {
            var defs = new List<ProductDefinition>();
            // 첫 결제 패키지도 상점에는 소모품으로 둔다 (계정당 1번은 장부가 지킨다) — 소모품은 확정할 때 소비돼 다시 살 수 있다
            foreach (IapCatalog.Item item in IapCatalog.All) defs.Add(new ProductDefinition(item.Id, ProductType.Consumable));
            _store.FetchProducts(defs);
            RetryConfirm();
        }

        private static void OnDisconnected(StoreConnectionFailureDescription failure)
        {
            Debug.LogWarning($"[IAP] 상점 연결 끊김: {failure.message}");
            EndBuying(null);
            Set(Status.Unavailable);
        }

        private static void OnProductsFetched(List<Product> products)
        {
            Set(Status.Ready);
            // 상품을 받은 뒤에 불러야 소모품으로 알아본다 (먼저 부르면 종류를 몰라 소비 대신 승인만 → 다시 못 산다)
            _store.FetchPurchases();
        }

        private static void OnProductsFetchFailed(ProductFetchFailed failure)
        {
            Debug.LogWarning($"[IAP] 상품 {failure.FailedFetchProducts.Count}개를 못 받음: {failure.FailureReason}");
            if (_store.GetProducts().Count == 0) Set(Status.Unavailable);
            else Changed?.Invoke();
        }

        private static void OnPurchasesFetched(Orders orders) =>
            Debug.Log($"[IAP] 끝나지 않은 결제 {orders.PendingOrders.Count} · 대기 {orders.DeferredOrders.Count}");

        private static void OnPurchasesFetchFailed(PurchasesFetchFailureDescription failure) =>
            Debug.LogWarning($"[IAP] 끝나지 않은 결제를 못 받음: {failure.message}");

        // ------------------------------------------------------------------ 결제

        /// <summary>새 결제와 지난번에 끝나지 않은 결제가 모두 여기로 온다</summary>
        private static void OnPurchasePending(PendingOrder order)
        {
            string productId = ProductOf(order);
            string transactionId = order.Info.TransactionID;
            EndBuying(productId);
            string problem = ReceiptProblem(order);
            if (ServerAuthority && problem == null)
            {
                // 서버가 정한 지급을 이미 넣고 저장했는데 확정 전에 꺼졌다 (또는 다른 기기에서 받아 장부를 맞췄다) → 확정만
                if (IapLedger.Find(SaveSystem.Data, transactionId) != null) Confirm(order);
                else AskServer(order);
                return;
            }

            PurchaseEntry granted = Process(productId, transactionId, problem, () => Confirm(order), out string notice);
            if (granted != null) OnGranted(granted);
            SayOnce(transactionId, notice);
        }

        private static void OnGranted(PurchaseEntry granted)
        {
            Granted?.Invoke(granted);
            PurchaseEntry shown = _shown;
            if (shown == null || shown.productId != granted.productId) return;
            _shown = null;
            // 보여 준 것보다 적게 받았을 때만 알린다 (동기화 전이라 2배를 약속하지 않았는데 받았으면 그냥 더 받은 것)
            if (granted.paidGems + granted.freeGems < shown.paidGems + shown.freeGems)
                Say($"첫 구매 2배는 먼저 들어온 같은 상품 결제에 쓰였어요. 이번 결제로 {IapLedger.Text(granted)}을 받았어요.");
        }

        /// <summary>같은 거래의 같은 안내는 이번 실행에서 한 번만 (결제가 다시 올 때마다 띄우지 않게)</summary>
        private static void SayOnce(string transactionId, string notice)
        {
            if (notice != null && (string.IsNullOrEmpty(transactionId) || Noticed.Add(transactionId + "\n" + notice))) Say(notice);
        }

        // ------------------------------------------------------------------ 서버 검증 (기기)

        /// <summary>서버에 이 결제를 묻고 답대로 처리한다. 로그인 전 · 오프라인 · 서버 오류면 결제를 남겨 두고 잠시 뒤 다시 묻는다</summary>
        private static async void AskServer(PendingOrder order)
        {
            string transactionId = order.Info.TransactionID;
            string productId = ProductOf(order);
            if (string.IsNullOrEmpty(transactionId) || !Asking.Add(transactionId)) return;
            Waiting[transactionId] = order;
            if (!FirstAsked.ContainsKey(transactionId)) FirstAsked[transactionId] = DateTime.UtcNow;
            int session = _session;
            Owner askedAs = Owner.Now;
            PurchaseServer.Verdict verdict = null;
            try
            {
                if (Account.AccountService.PlayerId == null) Account.AccountService.Start(); // 로그인되면 OnAccountChanged가 다시 묻는다
                else verdict = await PurchaseServer.VerifyAsync(productId, transactionId);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[IAP] 서버 결제 확인 실패 ({productId}, 다시 시도): {e.Message}");
            }

            if (session != _session) return;
            Asking.Remove(transactionId);
            if (verdict != null && verdict.status != PurchaseServer.Granted && verdict.status != PurchaseServer.Already)
                Debug.LogWarning($"[IAP] 서버 판정 {productId}: {verdict.status} {verdict.reason}");
            bool justBought = DateTime.UtcNow - FirstAsked[transactionId] < JustBoughtWindow;
            PurchaseEntry granted = ApplyVerdict(verdict, transactionId, askedAs, justBought, () => Confirm(order), out string notice, out bool retry);
            if (retry) AskAgainSoon(transactionId);
            else
            {
                Waiting.Remove(transactionId);
                AskCount.Remove(transactionId);
                FirstAsked.Remove(transactionId);
            }

            if (granted != null) OnGranted(granted);
            SayOnce(transactionId, notice);
        }

        /// <summary>
        /// 서버 답 하나의 처리 (상점 없이 테스트한다). askedAs: 물을 때의 주인 — 지금과 다르면 아무것도 하지 않고 retry (다른 진행에 넣지 않는다).
        /// granted · already → 서버가 준 수량 그대로 장부에 넣고 저장한 뒤에만 confirm (이미 이 기기 장부에 있으면 확정만).
        /// voided → 지급 · 확정 안 함, 이 기기에 있으면 회수. already_claimed · invalid · not_configured → 지급도 확정도 안 함 (구글이 3일 안에 환불).
        /// 막 산 결제(justBought)의 구글 404는 반영이 늦은 것일 수 있어 retry. retry · 답 없음(오프라인 · 오류) · 저장 실패 → 지급 · 확정 없이 retry=true.
        /// 돌려주는 값: 이번에 새로 지급한 내역 (없으면 null)
        /// </summary>
        public static PurchaseEntry ApplyVerdict(PurchaseServer.Verdict verdict, string transactionId, Owner askedAs, bool justBought, Action confirm,
            out string notice, out bool retry)
        {
            notice = null;
            retry = false;
            if (!askedAs.Same(Owner.Now))
            {
                Debug.LogWarning("[IAP] 서버에 묻는 사이 계정 · 저장이 바뀜 → 이 답은 쓰지 않고 다시 묻는다");
                retry = true;
                return null;
            }

            switch (verdict?.status)
            {
                case PurchaseServer.Granted:
                case PurchaseServer.Already:
                    if (!verdict.HasPurchase) break;
                    IapLedger.Result result = GrantServerAndSave(verdict.purchase, transactionId, out PurchaseEntry entry, out SaveSystem.CommitResult saved);
                    if (result == IapLedger.Result.AlreadyGranted)
                    {
                        confirm();
                        return null;
                    }

                    if (saved == SaveSystem.CommitResult.Saved)
                    {
                        confirm(); // 저장이 끝난 뒤에만 확정한다
                        return entry;
                    }

                    Debug.LogWarning($"[IAP] 서버가 정한 지급을 넣지 못함: {result} / {saved}");
                    notice = saved == SaveSystem.CommitResult.SaveFailed ? SaveFailedText : UngrantableText;
                    retry = saved == SaveSystem.CommitResult.SaveFailed; // 다시 물으면 서버가 already로 같은 지급을 준다
                    return null;
                case PurchaseServer.Voided:
                    if (verdict.HasPurchase)
                        SaveSystem.Commit(() => IapLedger.ApplyServer(SaveSystem.Data, verdict.purchase, transactionId, out _) == IapLedger.Result.Invalid);
                    notice = VoidedText + (Wallet.InDebt(SaveSystem.Data) ? " " + Wallet.DebtText : string.Empty);
                    return null;
                case PurchaseServer.AlreadyClaimed:
                    notice = AlreadyClaimedText;
                    return null;
                case PurchaseServer.Invalid:
                    string reason = verdict.reason ?? string.Empty;
                    if (justBought && reason.StartsWith("google_404", StringComparison.Ordinal)) break; // 구글 반영 대기 → 다시 묻기
                    notice = reason.StartsWith("not_paid", StringComparison.Ordinal) ? NotPaidText : InvalidReceiptText;
                    return null;
                case PurchaseServer.NotConfigured:
                    notice = NotReadyText;
                    return null;
            }

            retry = true;
            notice = verdict?.reason == "pending" ? PendingText : CheckingText;
            return null;
        }

        /// <summary>서버가 정한 지급을 장부에 넣고 바로 저장한다. 저장이 실패하면 통째로 되돌린다</summary>
        public static IapLedger.Result GrantServerAndSave(PurchaseServer.Purchase purchase, string transactionId, out PurchaseEntry entry, out SaveSystem.CommitResult saved)
        {
            IapLedger.Result result = IapLedger.Result.Invalid;
            PurchaseEntry granted = null;
            saved = SaveSystem.Commit(() =>
            {
                result = IapLedger.ApplyServer(SaveSystem.Data, purchase, transactionId, out granted);
                return result == IapLedger.Result.Granted;
            });
            entry = granted;
            return result;
        }

        /// <summary>다시 묻기 간격 (초): 5 · 10 · 20 · 40 · 80 · 160 · 그 뒤로 300</summary>
        public static int AskRetrySeconds(int attempt) => Math.Min(300, 5 << Math.Clamp(attempt - 1, 0, 6));

        private static async void AskAgainSoon(string transactionId)
        {
            if (!AskScheduled.Add(transactionId)) return;
            int attempt = AskCount.TryGetValue(transactionId, out int n) ? n + 1 : 1;
            AskCount[transactionId] = attempt;
            int session = _session;
            await Task.Delay(AskRetrySeconds(attempt) * 1000);
            if (session != _session) return;
            AskScheduled.Remove(transactionId);
            if (Waiting.TryGetValue(transactionId, out PendingOrder order)) AskServer(order);
        }

        /// <summary>답을 못 받은 결제를 지금 다시 묻는다 (로그인됐을 때 · 상점을 열 때)</summary>
        private static void AskWaitingAgain()
        {
            if (Account.AccountService.PlayerId == null) return;
            foreach (PendingOrder order in new List<PendingOrder>(Waiting.Values)) AskServer(order);
        }

        private static void OnAccountChanged()
        {
            if (Account.AccountService.PlayerId == null) return;
            if (!ServerSynced) SyncServer();
            AskWaitingAgain();
        }

        /// <summary>
        /// 로그인한 플레이어의 서버 장부를 받아 이 기기에 없는 결제를 넣는다 (LiveOps 3-4 — 저장을 잃었거나 같은 계정 다른 기기에서 산 것).
        /// 끝나면 ServerSynced → 상점이 팔기 시작한다. 실패하면 다음 로비 · 상점 열기 · 계정 바뀜에서 다시
        /// </summary>
        private static async void SyncServer()
        {
            string player = Account.AccountService.PlayerId;
            if (player == null || _serverSyncing || ServerSynced) return;
            _serverSyncing = true;
            int session = _session;
            try
            {
                PurchaseServer.Ledger ledger = await PurchaseServer.GetPurchasesAsync();
                if (session != _session || ledger == null || player != Account.AccountService.PlayerId) return;
                if (ledger.status != null && ledger.status != "ok")
                {
                    Debug.LogWarning($"[IAP] 서버 장부를 받지 못함 ({ledger.status}, 다음에 다시)");
                    return;
                }

                IapLedger.ServerMerge merged = default;
                SaveSystem.CommitResult saved = SaveSystem.Commit(() =>
                {
                    int before = SaveSystem.Data.purchases.ledger.Count;
                    merged = IapLedger.MergeServer(SaveSystem.Data, ledger.purchases);
                    return merged.Added > 0 || merged.Voided > 0 || SaveSystem.Data.purchases.ledger.Count != before; // 회수됨 표시만 더해도 저장
                });
                if (saved == SaveSystem.CommitResult.SaveFailed)
                {
                    Debug.LogWarning("[IAP] 서버 장부를 맞췄지만 저장하지 못함 (다음에 다시)");
                    return;
                }

                _serverConfigured = ledger.configured;
                _serverPlayer = player;
                Debug.Log($"[IAP] 서버 장부 {ledger.purchases?.Count ?? 0}건 · 새로 넣음 {merged.Added} · 회수 {merged.Voided} · 검증 준비 {ledger.configured}");
                if (merged.Added > 0) Say($"서버에 기록된 결제 {merged.Added}건을 이 기기에 다시 넣었어요.");
                if (merged.Voided > 0)
                    Say($"환불 · 취소된 결제 {merged.Voided}건으로 받았던 보석 {Wallet.Format(merged.GemsTaken)}개(와 골드 · 소환권)를 회수했어요."
                        + (Wallet.InDebt(SaveSystem.Data) ? " " + Wallet.DebtText : string.Empty));
                Changed?.Invoke();
            }
            catch (Exception e)
            {
                Debug.LogWarning("[IAP] 서버 장부를 받지 못함 (다음에 다시): " + e.Message);
            }
            finally
            {
                if (session == _session) _serverSyncing = false;
            }
        }

        /// <summary>
        /// 결제 하나의 처리 순서 — 이 기기 장부로 지급을 정하는 길 (에디터 · 가짜 상점, 기기는 영수증 키가 없을 때 거절만). 상점 없이 테스트한다. 영수증 문제가 있으면 지급도 확정도 안 한다 → 지급 · 저장 → 저장이 끝난 뒤에만 confirm.
        /// 이미 지급한 거래는 확정만 한다. 지급하지 못하면(저장 실패 · 이미 받은 패키지 · 모르는 상품) 확정하지 않는다 (구글이 3일 안에 환불).
        /// 돌려주는 값: 이번에 새로 지급한 내역 (없으면 null). notice: 사용자에게 보여 줄 안내 (없으면 null)
        /// </summary>
        public static PurchaseEntry Process(string productId, string transactionId, string receiptProblem, Action confirm, out string notice)
        {
            notice = null;
            if (receiptProblem != null)
            {
                notice = receiptProblem;
                return null;
            }

            IapLedger.Result result = GrantAndSave(productId, transactionId, out PurchaseEntry entry, out SaveSystem.CommitResult saved);
            if (result == IapLedger.Result.AlreadyGranted)
            {
                confirm();
                return null;
            }

            if (saved == SaveSystem.CommitResult.Saved)
            {
                confirm(); // 저장이 끝난 뒤에만 확정한다
                return entry;
            }

            Debug.LogWarning($"[IAP] {productId} 지급 안 함: {result} / {saved}");
            if (saved == SaveSystem.CommitResult.SaveFailed) notice = SaveFailedText;
            else if (result == IapLedger.Result.AlreadyClaimed) notice = AlreadyClaimedText;
            else notice = UngrantableText;
            return null;
        }

        /// <summary>장부에 지급하고 바로 저장한다. 저장이 실패하면 지급까지 되돌린다 (결제가 올 때와 QA 재전송 시험이 같은 길)</summary>
        public static IapLedger.Result GrantAndSave(string productId, string transactionId, out PurchaseEntry entry, out SaveSystem.CommitResult saved)
        {
            IapLedger.Result result = IapLedger.Result.Invalid;
            PurchaseEntry granted = null;
            saved = SaveSystem.Commit(() =>
            {
                result = IapLedger.Apply(SaveSystem.Data, productId, transactionId, DateTime.UtcNow, out granted);
                return result == IapLedger.Result.Granted;
            });
            entry = granted;
            return result;
        }

        private static void Confirm(PendingOrder order)
        {
            Unconfirmed[order.Info.TransactionID] = order;
            _store.ConfirmPurchase(order);
        }

        private static void RetryConfirm()
        {
            if (_store == null || _store.GetConnectionState() != ConnectionState.Connected) return;
            foreach (PendingOrder order in new List<PendingOrder>(Unconfirmed.Values)) _store.ConfirmPurchase(order);
        }

        /// <summary>확정이 실패했다: 상점에 머물러도 잠시 뒤 다시 확정한다 (한 번에 하나만 걸어 둔다)</summary>
        private static async void RetryConfirmSoon()
        {
            if (_retryScheduled) return;
            _retryScheduled = true;
            int session = _session;
            await Task.Delay(ConfirmRetryMilliseconds);
            if (session != _session) return;
            _retryScheduled = false;
            RetryConfirm();
        }

        private static void OnPurchaseConfirmed(Order order)
        {
            string id = order.Info.TransactionID ?? string.Empty;
            if (order is FailedOrder failed && failed.FailureReason != PurchaseFailureReason.DuplicateTransaction
                && failed.FailureReason != PurchaseFailureReason.PurchaseMissing) // 이미 소비된 결제 → 끝난 것
            {
                // 지급은 끝났다. 확정만 다시 하면 된다 (서버가 이미 구글에 확정했어도 소비는 따로 해야 다시 살 수 있다)
                int tries = (ConfirmTries.TryGetValue(id, out int n) ? n : 0) + 1;
                ConfirmTries[id] = tries;
                if (tries < MaxConfirmTries)
                {
                    Debug.LogWarning($"[IAP] {ProductOf(order)} 확정 실패 (다시 시도 {tries}): {failed.FailureReason}");
                    RetryConfirmSoon();
                    return;
                }

                Debug.LogWarning($"[IAP] {ProductOf(order)} 확정을 {tries}번 실패해 멈춤 — 다음 실행에 상점이 다시 보내면 또 확정한다");
            }

            Unconfirmed.Remove(id);
            ConfirmTries.Remove(id);
        }

        private static void OnPurchaseFailed(FailedOrder failed)
        {
            EndBuying(ProductOf(failed));
            if (failed.FailureReason != PurchaseFailureReason.UserCancelled)
                Debug.LogWarning($"[IAP] {ProductOf(failed)} 결제 실패: {failed.FailureReason} {failed.Details}");
            Say(FailureText(failed.FailureReason));
        }

        private static void OnPurchaseDeferred(DeferredOrder order)
        {
            EndBuying(ProductOf(order));
            Say("결제가 아직 끝나지 않았어요. 결제가 끝나면 게임을 다시 열 때 들어와요.");
        }

        public static string FailureText(PurchaseFailureReason reason)
        {
            switch (reason)
            {
                case PurchaseFailureReason.UserCancelled: return "결제를 취소했어요.";
                case PurchaseFailureReason.PaymentDeclined: return "결제가 거절됐어요. 결제 수단을 확인해 주세요.";
                case PurchaseFailureReason.ExistingPurchasePending: return "이전 결제를 처리하는 중이에요. 잠시 뒤 상점을 다시 열어 주세요.";
                case PurchaseFailureReason.ProductUnavailable: return "지금은 살 수 없는 상품이에요.";
                case PurchaseFailureReason.PurchasingUnavailable:
                case PurchaseFailureReason.StoreNotConnected: return UnavailableText + ". 인터넷을 확인해 주세요.";
                default: return "결제하지 못했어요. 잠시 뒤 다시 해 주세요.";
            }
        }

        private static string ProductOf(Order order)
        {
            IReadOnlyList<CartItem> items = order?.CartOrdered?.Items();
            return items != null && items.Count > 0 ? items[0].Product?.definition?.id : null;
        }

        // ------------------------------------------------------------------ 영수증 검사

        /// <summary>
        /// 안드로이드: 구글 서명을 앱의 라이선스 키로 검사한다. 키(GooglePlayTangle)는 플레이 콘솔 키로
        /// Services → In-App Purchasing → Receipt Validation Obfuscator가 만든다 — 만들기 전에도 컴파일되게 이름으로 찾는다 (link.xml이 지운 코드에서 빼 둔다).
        /// 키가 없으면 지급하지 않는다 (닫힌 쪽). 에디터 · 가짜 상점은 검사하지 않는다 (검사기가 에디터에서는 빈 껍데기).
        /// 돌려주는 값: 문제 안내 (없으면 null)
        /// </summary>
        private static string ReceiptProblem(PendingOrder order)
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            byte[] key = GooglePublicKey();
            if (key == null)
            {
                if (!_loggedNoKey) Debug.LogError("[IAP] 영수증 검사 키(GooglePlayTangle)가 없어 결제를 지급 · 확정하지 않음 — 출시 점검표 3장 5번");
                _loggedNoKey = true;
                return NotReadyText;
            }

            try
            {
                new UnityEngine.Purchasing.Security.CrossPlatformValidator(key, Application.identifier).Validate(order.Info.Receipt);
                return null;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[IAP] 영수증 검사 실패 ({ProductOf(order)}): {e.GetType().Name}");
                return InvalidReceiptText;
            }
#else
            return null;
#endif
        }

#if UNITY_ANDROID && !UNITY_EDITOR
        private static bool _loggedNoKey;
        private static bool _keySearched;
        private static byte[] _googleKey;

        private static byte[] GooglePublicKey()
        {
            if (_keySearched) return _googleKey;
            _keySearched = true;
            foreach (System.Reflection.Assembly asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type tangle = asm.GetType("UnityEngine.Purchasing.Security.GooglePlayTangle", false);
                if (tangle == null) continue;
                // 만드는 도구 판에 따라 IsPopulated가 필드이기도 속성이기도 하다
                object populated = tangle.GetField("IsPopulated")?.GetValue(null) ?? tangle.GetProperty("IsPopulated")?.GetValue(null);
                if (populated is bool ok && ok) _googleKey = tangle.GetMethod("Data")?.Invoke(null, null) as byte[];
                break;
            }

            return _googleKey;
        }
#endif

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            // Play 사이에 도메인을 다시 읽지 않는다: 지난 Play의 상점에서 손을 떼고 새로 시작한다 (결제 패키지도 같은 때 자기 서비스를 새로 만든다)
            if (_store != null) Wire(_store, false);
            _store = null;
            State = Status.Off;
            BuyingId = null;
            Changed = null;
            Granted = null;
            Notice = null;
            Unconfirmed.Clear();
            Noticed.Clear();
            _shown = null;
            _retryScheduled = false;
            Waiting.Clear();
            Asking.Clear();
            AskCount.Clear();
            AskScheduled.Clear();
            FirstAsked.Clear();
            ConfirmTries.Clear();
            _serverPlayer = null;
            _serverConfigured = false;
            _serverSyncing = false;
            _session++;
        }
    }
}
