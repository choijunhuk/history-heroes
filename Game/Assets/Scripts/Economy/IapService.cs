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

        public static void Start()
        {
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

        /// <summary>영수증을 검사할 수 있다 (에디터 · 가짜 상점은 늘 true, 안드로이드는 GooglePlayTangle이 있을 때만). 아니면 팔지 않는다</summary>
        public static bool ValidationReady
        {
            get
            {
#if UNITY_ANDROID && !UNITY_EDITOR
                return GooglePublicKey() != null;
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
            if (!IapLedger.Available(SaveSystem.Data, item)) return false; // 계정당 1번 상품을 두 번 결제하지 않게
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
            PurchaseEntry granted = Process(productId, transactionId, ReceiptProblem(order), () => Confirm(order), out string notice);
            if (granted != null)
            {
                Granted?.Invoke(granted);
                PurchaseEntry shown = _shown;
                if (shown != null && shown.productId == granted.productId)
                {
                    _shown = null;
                    if (shown.paidGems != granted.paidGems || shown.freeGems != granted.freeGems)
                        Say($"첫 구매 2배는 먼저 들어온 같은 상품 결제에 쓰였어요. 이번 결제로 {IapLedger.Text(granted)}을 받았어요.");
                }
            }

            if (notice != null && (string.IsNullOrEmpty(transactionId) || Noticed.Add(transactionId))) Say(notice);
        }

        /// <summary>
        /// 결제 하나의 처리 순서 (상점 없이 테스트한다). 영수증 문제가 있으면 지급도 확정도 안 한다 → 지급 · 저장 → 저장이 끝난 뒤에만 confirm.
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
            if (saved == SaveSystem.CommitResult.SaveFailed)
                notice = "저장하지 못해서 아직 지급하지 않았어요. 저장 공간을 확인해 주세요. 다음에 게임을 열면 다시 받아요.";
            else if (result == IapLedger.Result.AlreadyClaimed)
                notice = "첫 결제 패키지는 계정당 1번만 받을 수 있어요. 이번 결제는 확정하지 않아 환불 대기 중이에요 (3일 안에 자동 환불).";
            else
                notice = "이 결제를 지급하지 못해 환불 대기 중이에요 (3일 안에 자동 환불). 계속되면 문의해 주세요.";
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
            if (order is FailedOrder failed && failed.FailureReason != PurchaseFailureReason.DuplicateTransaction)
            {
                // 지급은 끝났다. 확정만 다시 하면 된다
                Debug.LogWarning($"[IAP] {ProductOf(order)} 확정 실패 (다시 시도): {failed.FailureReason} {failed.Details}");
                RetryConfirmSoon();
                return;
            }
            Unconfirmed.Remove(id);
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
            _session++;
        }
    }
}
