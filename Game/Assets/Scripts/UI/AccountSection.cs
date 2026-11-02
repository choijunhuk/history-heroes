using System.Threading.Tasks;
using HistoryHeroes.Account;
using HistoryHeroes.Heroes;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace HistoryHeroes.UI
{
    /// <summary>
    /// 설정 → 계정 (LiveOps 3장 · 13장, 로비 설정에만): 상태 한 줄 · 카카오 계정 연결 · 지금 클라우드에 저장 · 계정 삭제.
    /// 카카오 버튼은 쓸 수 없는 곳(에디터 · 앱 키 없는 빌드)에서는 숨긴다.
    /// 진행 고르기 · 확인 창은 로비의 고르기 창을 빌리고, 로비가 Bind로 AccountService에 끼운다 (설정을 안 열어도 고르기가 뜰 수 있게).
    /// </summary>
    public sealed class AccountSection : MonoBehaviour
    {
        [SerializeField] private TMP_Text statusLabel;
        [SerializeField] private Button kakaoButton;
        [SerializeField] private TMP_Text kakaoLabel;
        [SerializeField] private Button cloudButton;
        [SerializeField] private Button deleteButton;
        [SerializeField] private ChoicePanel choice;

        private void Awake()
        {
            kakaoButton.onClick.AddListener(() => _ = AccountService.LinkAsync(KakaoLogin.Provider));
            cloudButton.onClick.AddListener(() => _ = AccountService.UploadNowAsync());
            deleteButton.onClick.AddListener(ConfirmDelete);
        }

        private void OnEnable()
        {
            AccountService.Changed += Render;
            AccountService.Start(); // 오프라인이었으면 다시 시도
            Render();
        }

        private void OnDisable() => AccountService.Changed -= Render;

        private void Render()
        {
            AccountState state = AccountService.State;
            string message = AccountService.Message;
            statusLabel.text = AccountRules.StatusText(state, AccountService.LinkedName)
                               + (string.IsNullOrEmpty(message) ? string.Empty : $"\n<color=#FFD966>{message}</color>");
            bool linked = state == AccountState.Linked;
            kakaoButton.gameObject.SetActive(KakaoLogin.Provider.Available());
            kakaoButton.interactable = state == AccountState.Guest;
            kakaoLabel.text = linked && AccountService.LinkedName == KakaoLogin.Provider.DisplayName ? "카카오 연결됨" : "카카오 계정 연결";
            cloudButton.interactable = linked;
            deleteButton.interactable = state == AccountState.Guest || linked;
        }

        private void ConfirmDelete()
        {
            choice.Open("계정 삭제",
                "클라우드 저장과 이 기기의 진행을 모두 지워요. 되돌릴 수 없어요\n유료로 산 보석 · 상품이 생긴 뒤라면 그것도 사라져요",
                new[]
                {
                    new ChoicePanel.Option
                    {
                        Text = "모두 지우기",
                        Danger = true,
                        OnChosen = () => _ = AccountService.DeleteAsync(),
                    },
                },
                laterText: "취소");
        }

        // ------------------------------------------------------------------ 로비가 끼우는 고르기 · 확인 창

        /// <summary>
        /// 로비 시작 때 부른다. 고르기 창이 닫히는 모든 길(고르기 · 나중에 · 다른 창이 덮음 · 장면이 바뀜)에서 기다리는 쪽이 답을 받는다
        /// (ChoicePanel이 나중에 콜백을 꼭 부른다) → 계정이 "처리 중"에 갇히지 않는다.
        /// </summary>
        public static void Bind(ChoicePanel choice, HeroRoster roster)
        {
            if (choice == null) return;
            AccountService.ChooseProgress = (title, info, device, cloud) => ChooseProgress(choice, roster, title, info, device, cloud);
            AccountService.Confirm = (title, info, yes) => Confirm(choice, title, info, yes);
        }

        public static void Unbind()
        {
            AccountService.ChooseProgress = null;
            AccountService.Confirm = null;
        }

        /// <summary>두 요약을 보여 주고 한 번만 고르게 한다 (LiveOps 3-2). 진행도가 높은 쪽에 "추천". "나중에" = 그만두기</summary>
        private static Task<AccountService.Choice> ChooseProgress(ChoicePanel choice, HeroRoster roster, string title, string info,
            AccountSummary device, AccountSummary cloud)
        {
            var picked = new TaskCompletionSource<AccountService.Choice>();
            string Name(string id) => roster != null ? roster.Find(id)?.displayName : id;
            bool cloudFirst = AccountSummary.CloudRecommended(device, cloud);
            choice.Open(title, info,
                new[]
                {
                    new ChoicePanel.Option
                    {
                        Text = $"이 기기 진행{(cloudFirst ? "" : " (추천)")}\n<size=62%>{device.Text(Name).Replace(" · ", "\n")}</size>",
                        Primary = !cloudFirst,
                        OnChosen = () => picked.TrySetResult(AccountService.Choice.Device),
                    },
                    new ChoicePanel.Option
                    {
                        Text = $"클라우드 진행{(cloudFirst ? " (추천)" : "")}\n<size=62%>{cloud.Text(Name).Replace(" · ", "\n")}</size>",
                        Primary = cloudFirst,
                        OnChosen = () => picked.TrySetResult(AccountService.Choice.Cloud),
                    },
                },
                () => picked.TrySetResult(AccountService.Choice.Cancel));
            return picked.Task;
        }

        private static Task<bool> Confirm(ChoicePanel choice, string title, string info, string yes)
        {
            var answer = new TaskCompletionSource<bool>();
            choice.Open(title, info,
                new[] { new ChoicePanel.Option { Text = yes, OnChosen = () => answer.TrySetResult(true) } },
                () => answer.TrySetResult(false), "취소");
            return answer.Task;
        }
    }
}
