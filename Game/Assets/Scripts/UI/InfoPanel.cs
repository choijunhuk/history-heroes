using HistoryHeroes.Records;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace HistoryHeroes.UI
{
    /// <summary>
    /// 정보 (LiveOps 13장 · Systems L 법·정책): 이용약관 · 개인정보 처리방침 · 크레딧 · 정보(버전·사용자 ID).
    /// 글은 저장소의 Docs/Legal · Docs/Credits.md를 장면 빌더가 Resources/Legal로 복사한 것 (웹 페이지와 같은 글).
    /// 확률 보기는 상점의 뽑기 버튼 옆에 있다.
    /// </summary>
    public sealed class InfoPanel : MonoBehaviour
    {
        [SerializeField] private GameObject root;
        [SerializeField] private TMP_Text titleLabel;
        [SerializeField] private TMP_Text bodyLabel;
        [SerializeField] private ScrollRect scroll;
        [SerializeField] private Button termsButton;
        [SerializeField] private Button privacyButton;
        [SerializeField] private Button creditsButton;
        [SerializeField] private Button aboutButton;
        [SerializeField] private Button backButton;

        public bool IsOpen => root.activeSelf;

        private void Awake()
        {
            root.SetActive(false);
            termsButton.onClick.AddListener(() => ShowText("이용약관", "Legal/terms"));
            privacyButton.onClick.AddListener(() => ShowText("개인정보 처리방침", "Legal/privacy"));
            creditsButton.onClick.AddListener(() => ShowText("크레딧", "Legal/credits"));
            aboutButton.onClick.AddListener(ShowAbout);
            backButton.onClick.AddListener(() => root.SetActive(false));
        }

        public void Open()
        {
            root.SetActive(true);
            ShowAbout();
        }

        private void ShowText(string title, string resource)
        {
            var asset = Resources.Load<TextAsset>(resource);
            Show(title, asset != null ? asset.text : "(글을 찾지 못했어요)");
        }

        private void ShowAbout()
        {
            SaveData d = SaveSystem.Data;
            string body = $"<b>History Heroes (역사 영웅)</b>\n버전 {Application.version}\n\n" +
                          $"사용자 ID  <b>{d.userId}</b>\n<size=80%>문의할 때 이 번호를 알려 주세요 (이름·연락처와 연결되지 않은 무작위 번호)</size>\n\n" +
                          "진행은 이 기기 안에만 저장돼요. 저장이 깨지면 백업(최근 판 3개)에서 되살려요.\n\n" +
                          "<size=80%>장비 상자 확률은 상점의 \"확률 보기\"에 있어요. 화면의 확률과 실제 추첨은 같은 표를 써요.</size>";
            Show("정보", body);
        }

        private void Show(string title, string body)
        {
            titleLabel.text = title;
            bodyLabel.text = body;
            Canvas.ForceUpdateCanvases();
            if (scroll != null) scroll.verticalNormalizedPosition = 1f;
        }
    }
}
