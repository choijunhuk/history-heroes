using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace HistoryHeroes.UI
{
    /// <summary>
    /// 모드 안내 (로비, v0.6): 무한의 균열 · 일일 도전. 제목 + 설명 + 버튼 2개 (시작 / 이어하기·연습) + 뒤로.
    /// 버튼을 누르면 바로 전투로 넘어가므로 닫지 않는다 (실패하면 알림만).
    /// </summary>
    public sealed class ModeInfoPanel : MonoBehaviour
    {
        [SerializeField] private GameObject root;
        [SerializeField] private TMP_Text titleLabel;
        [SerializeField] private TMP_Text bodyLabel;
        [SerializeField] private Button primaryButton;
        [SerializeField] private TMP_Text primaryLabel;
        [SerializeField] private Button secondaryButton;
        [SerializeField] private TMP_Text secondaryLabel;
        [SerializeField] private Button backButton;

        private Action _onPrimary;
        private Action _onSecondary;
        private Action _onClosed;

        public bool IsOpen => root.activeSelf;

        private void Awake()
        {
            root.SetActive(false);
            primaryButton.onClick.AddListener(() => _onPrimary?.Invoke());
            secondaryButton.onClick.AddListener(() => _onSecondary?.Invoke());
            backButton.onClick.AddListener(Close);
        }

        /// <param name="secondary">비우면 두 번째 버튼을 숨긴다</param>
        public void Open(string title, string body, string primary, bool primaryEnabled, Action onPrimary, string secondary, bool secondaryEnabled, Action onSecondary, Action onClosed = null)
        {
            titleLabel.text = title;
            bodyLabel.text = body;
            primaryLabel.text = primary;
            primaryButton.interactable = primaryEnabled;
            _onPrimary = onPrimary;
            bool hasSecondary = !string.IsNullOrEmpty(secondary);
            secondaryButton.gameObject.SetActive(hasSecondary);
            secondaryLabel.text = secondary ?? string.Empty;
            secondaryButton.interactable = secondaryEnabled;
            _onSecondary = onSecondary;
            _onClosed = onClosed;
            root.SetActive(true);
        }

        public void Close()
        {
            if (!IsOpen) return;
            root.SetActive(false);
            Action closed = _onClosed;
            _onClosed = null;
            closed?.Invoke();
        }
    }
}
