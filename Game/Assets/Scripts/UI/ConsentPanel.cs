using System;
using HistoryHeroes.Economy;
using HistoryHeroes.Records;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace HistoryHeroes.UI
{
    /// <summary>
    /// 첫 실행 동의 창 (LiveOps 19장): 전체 동의 · (필수) 이용약관 · (필수) 개인정보 · (필수) 만 14세 이상.
    /// 셋 다 체크해야 [시작하기]가 켜진다. 약관 글은 [보기]로 읽는다 (Resources/Legal, InfoPanel과 같은 글).
    /// 광고성 알림 동의는 묻지 않는다 — 원격 광고 푸시가 생길 때 추가한다.
    /// </summary>
    public sealed class ConsentPanel : MonoBehaviour
    {
        [SerializeField] private GameObject root;
        [SerializeField] private TMP_Text ratingLabel;
        [SerializeField] private TMP_Text noticeLabel;
        [SerializeField] private Button allButton;
        [SerializeField] private Button termsButton;
        [SerializeField] private Button privacyButton;
        [SerializeField] private Button ageButton;
        [Tooltip("각 항목의 체크 표시 (켜짐 = 체크)")]
        [SerializeField] private GameObject allMark;
        [SerializeField] private GameObject termsMark;
        [SerializeField] private GameObject privacyMark;
        [SerializeField] private GameObject ageMark;
        [SerializeField] private Button termsView;
        [SerializeField] private Button privacyView;
        [SerializeField] private Button startButton;
        [Header("약관 글 보기")]
        [SerializeField] private GameObject viewer;
        [SerializeField] private TMP_Text viewerTitle;
        [SerializeField] private TMP_Text viewerBody;
        [SerializeField] private ScrollRect viewerScroll;
        [SerializeField] private Button viewerBack;

        private bool _terms, _privacy, _age;
        private Action _onAccepted;

        public bool IsOpen => root != null && root.activeSelf;

        private void Awake()
        {
            if (root != null) root.SetActive(false);
            if (viewer != null) viewer.SetActive(false);
            if (ratingLabel != null) ratingLabel.text = Progress.Consent.RatingText;
            allButton.onClick.AddListener(() => SetAll(!(_terms && _privacy && _age)));
            termsButton.onClick.AddListener(() => { _terms = !_terms; Refresh(); });
            privacyButton.onClick.AddListener(() => { _privacy = !_privacy; Refresh(); });
            ageButton.onClick.AddListener(() => { _age = !_age; Refresh(); });
            termsView.onClick.AddListener(() => ShowText("이용약관", "Legal/terms"));
            privacyView.onClick.AddListener(() => ShowText("개인정보 처리방침", "Legal/privacy"));
            viewerBack.onClick.AddListener(() => viewer.SetActive(false));
            startButton.onClick.AddListener(Accept);
        }

        /// <summary>동의 창을 연다. 동의가 저장되면 onAccepted를 부른다 (닫기 · 건너뛰기는 없다).</summary>
        public void Open(Action onAccepted)
        {
            _onAccepted = onAccepted;
            _terms = _privacy = _age = false;
            if (noticeLabel != null) noticeLabel.text = string.Empty;
            viewer.SetActive(false);
            root.SetActive(true);
            root.transform.parent.SetAsLastSibling();
            Refresh();
        }

        private void SetAll(bool on)
        {
            _terms = _privacy = _age = on;
            Refresh();
        }

        private void Refresh()
        {
            termsMark.SetActive(_terms);
            privacyMark.SetActive(_privacy);
            ageMark.SetActive(_age);
            allMark.SetActive(_terms && _privacy && _age);
            bool ready = _terms && _privacy && _age;
            startButton.interactable = ready;
            // 못 누르는 상태가 눈에 띄도록 흐리게 (단추 색만으로는 구분이 약하다)
            // ??는 에디터의 "가짜 null"(빠진 컴포넌트 자리표시)을 못 걸러서 에디터에서 MissingComponentException이 났다 → == null로 본다
            CanvasGroup fade = startButton.GetComponent<CanvasGroup>();
            if (fade == null) fade = startButton.gameObject.AddComponent<CanvasGroup>();
            fade.alpha = ready ? 1f : 0.45f;
        }

        private void Accept()
        {
            if (!(_terms && _privacy && _age)) return;
            SaveSystem.CommitResult r = SaveSystem.Commit(() =>
            {
                Progress.Consent.Accept(SaveSystem.Data, Clock.Now().UtcDateTime);
                return true;
            });
            if (r != SaveSystem.CommitResult.Saved)
            {
                if (noticeLabel != null) noticeLabel.text = SaveSystem.SaveFailedText;
                return;
            }

            root.SetActive(false);
            _onAccepted?.Invoke();
        }

        private void ShowText(string title, string resource)
        {
            var asset = Resources.Load<TextAsset>(resource);
            viewerTitle.text = title;
            viewerBody.text = asset != null ? asset.text : "(글을 찾지 못했어요)";
            viewer.SetActive(true);
            Canvas.ForceUpdateCanvases();
            if (viewerScroll != null) viewerScroll.verticalNormalizedPosition = 1f;
        }
    }
}
