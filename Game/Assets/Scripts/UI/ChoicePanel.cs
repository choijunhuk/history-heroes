using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace HistoryHeroes.UI
{
    /// <summary>
    /// 여럿 중 하나 고르기 (로비). 두 가지 모양:
    /// · 팝업 (누를 수 있는 고르기가 1~2개): 로비를 어둡게 덮고 가운데 작은 카드 + 아래 왼쪽 · 오른쪽 단추.
    ///   2026-11-02 개발자: "두 개면 가운데에 두 개 띄우고 왼쪽 오른쪽 선택 — 박스 안에 위로 정렬하면 너무 별로".
    ///   1개면 [나중에/취소 | 고르기], 2개면 [첫째 | 둘째] + 아래 작은 "나중에". 안내 줄(누를 수 없는 줄)은 카드 본문 글로.
    /// · 목록 (3개 이상, 5구간 보스 전설 장비 8장): 화면 가득 줄 목록 + 아래 큰 [나중에].
    /// 고르기 전에는 닫을 수 없다 → 나중에 로비에 다시 들어오면 또 뜬다 (보상을 놓치지 않게, 닫기 = 나중에).
    /// </summary>
    public sealed class ChoicePanel : MonoBehaviour
    {
        public struct Option
        {
            public string Text;
            /// <summary>없으면 누를 수 없는 안내 줄 (구매 확인의 청약철회 · 미성년자 고지)</summary>
            public Action OnChosen;
            /// <summary>팝업에서 초록 강조 (아무도 없으면 맨 오른쪽 단추가 강조)</summary>
            public bool Primary;
            /// <summary>되돌릴 수 없는 고르기 (빨강): 계정 삭제 · 기록 포기</summary>
            public bool Danger;
        }

        [SerializeField] private GameObject root;
        [Tooltip("목록 모양 바탕 (그라데이션 · 무늬, 안전 영역 밖까지)")]
        [SerializeField] private GameObject screenBackdrop;
        [Tooltip("팝업 모양 바탕 (뒤 화면을 어둡게만)")]
        [SerializeField] private GameObject dim;
        [Header("목록 모양")]
        [SerializeField] private GameObject listMode;
        [SerializeField] private TMP_Text titleLabel;
        [SerializeField] private TMP_Text infoLabel;
        [SerializeField] private ButtonList list;
        [SerializeField] private Button laterButton;
        [Header("팝업 모양")]
        [SerializeField] private GameObject dialogMode;
        [SerializeField] private RectTransform dialogCard;
        [SerializeField] private TMP_Text dialogTitle;
        [SerializeField] private TMP_Text dialogInfo;
        [SerializeField] private TMP_Text dialogBody;
        [SerializeField] private LayoutElement buttonRow;
        [SerializeField] private Button leftButton;
        [SerializeField] private Button rightButton;
        [SerializeField] private Button dialogLater;

        private static readonly Color Row = UiTone.Lift(new Color(0.32f, 0.24f, 0.1f));
        private static readonly Color InfoRow = UiTone.Lift(new Color(0.1f, 0.1f, 0.12f));
        private static readonly Color DangerTint = new Color(0.82f, 0.3f, 0.28f); // 장면 빌더 ButtonDangerTone과 같은 빨강
        private const float MinButtonHeight = 150f;

        /// <summary>"나중에"를 눌렀을 때 (고르기를 기다리는 쪽이 그만뒀다고 알아야 할 때)</summary>
        private Action _onLater;
        private Action _left, _right;
        private Color _secondary, _primary;

        public bool IsOpen => root.activeSelf;

        private TMP_Text _laterLabel, _dialogLaterLabel, _leftLabel, _rightLabel;
        private string _laterDefault;

        private void Awake()
        {
            _laterLabel = laterButton.GetComponentInChildren<TMP_Text>(true);
            _laterDefault = _laterLabel != null ? _laterLabel.text : null;
            _dialogLaterLabel = dialogLater.GetComponentInChildren<TMP_Text>(true);
            _leftLabel = leftButton.GetComponentInChildren<TMP_Text>(true);
            _rightLabel = rightButton.GetComponentInChildren<TMP_Text>(true);
            _secondary = leftButton.image.color;
            _primary = rightButton.image.color;
            root.SetActive(false);
            laterButton.onClick.AddListener(CloseLater);
            dialogLater.onClick.AddListener(CloseLater);
            leftButton.onClick.AddListener(() => _left?.Invoke());
            rightButton.onClick.AddListener(() => _right?.Invoke());
        }

        private void CloseLater()
        {
            root.SetActive(false);
            Later();
        }

        /// <summary>기다리는 쪽에 "고르지 않았다"를 한 번만 알린다</summary>
        private void Later()
        {
            Action later = _onLater;
            _onLater = null;
            later?.Invoke();
        }

        /// <summary>장면이 바뀌어 창이 사라져도 기다리는 쪽이 답을 받는다 (계정 고르기가 "처리 중"에 갇히지 않게)</summary>
        private void OnDestroy() => Later();

        /// <summary>다른 고르기가 떠 있었으면 그 고르기는 "나중에"로 끝내고 새로 연다. laterText: 닫기 단추 글 (기본 "나중에")</summary>
        public void Open(string title, string info, IReadOnlyList<Option> options, Action onLater = null, string laterText = null)
        {
            Later();
            _onLater = onLater;
            string later = laterText ?? _laterDefault;

            var actions = new List<Option>();
            var notes = new List<string>();
            foreach (Option o in options)
            {
                if (o.OnChosen != null) actions.Add(o);
                else notes.Add(o.Text);
            }

            bool dialog = actions.Count >= 1 && actions.Count <= 2;
            listMode.SetActive(!dialog);
            dialogMode.SetActive(dialog);
            screenBackdrop.SetActive(!dialog);
            dim.SetActive(dialog);
            if (dialog) ShowDialog(title, info, notes, actions, later);
            else ShowList(title, info, options, later);

            transform.SetAsLastSibling(); // 상점처럼 열 때 맨 위로 오는 창 위에도 덮이게
            root.SetActive(true);
            if (dialog) StartCoroutine(PopIn());
        }

        private Action Choose(Option option) => () =>
        {
            root.SetActive(false);
            _onLater = null;
            option.OnChosen?.Invoke();
        };

        private void ShowList(string title, string info, IReadOnlyList<Option> options, string later)
        {
            titleLabel.text = title;
            infoLabel.text = info;
            if (_laterLabel != null) _laterLabel.text = later;
            var rows = new List<ButtonList.Row>();
            foreach (Option o in options)
            {
                if (o.OnChosen == null) rows.Add(new ButtonList.Row(o.Text, InfoRow, null, false));
                else rows.Add(new ButtonList.Row(o.Text, Row, Choose(o)));
            }

            list.Show(rows, scrollToTop: true);
        }

        private void ShowDialog(string title, string info, List<string> notes, List<Option> actions, string later)
        {
            dialogTitle.text = title;
            dialogInfo.text = info;
            dialogInfo.gameObject.SetActive(!string.IsNullOrEmpty(info));
            dialogBody.text = string.Join("\n", notes);
            dialogBody.gameObject.SetActive(notes.Count > 0);
            dialogBody.alignment = notes.Count == 1 ? TextAlignmentOptions.Center : TextAlignmentOptions.TopLeft;

            if (actions.Count == 1)
            {
                // [나중에/취소 | 고르기]
                Option only = actions[0];
                _left = CloseLater;
                _leftLabel.text = later;
                leftButton.image.color = _secondary;
                _right = Choose(only);
                _rightLabel.text = only.Text;
                rightButton.image.color = only.Danger ? DangerTint : _primary;
                dialogLater.gameObject.SetActive(false);
            }
            else
            {
                // [첫째 | 둘째] + 아래 작은 나중에. 강조가 정해지지 않았으면 오른쪽이 강조
                Option a = actions[0], b = actions[1];
                bool anyPrimary = a.Primary || b.Primary;
                _left = Choose(a);
                _leftLabel.text = a.Text;
                leftButton.image.color = Tint(a, a.Primary);
                _right = Choose(b);
                _rightLabel.text = b.Text;
                rightButton.image.color = Tint(b, anyPrimary ? b.Primary : true);
                if (_dialogLaterLabel != null) _dialogLaterLabel.text = later;
                dialogLater.gameObject.SetActive(true);
            }

            // 단추 높이 = 두 글 중 긴 쪽 (계정 진행 고르기는 요약 여러 줄)
            float width = (dialogCard.rect.width - 120f) * 0.5f - 24f;
            float tallest = Mathf.Max(Preferred(_leftLabel, width), Preferred(_rightLabel, width));
            buttonRow.preferredHeight = Mathf.Max(MinButtonHeight, tallest + 48f);
        }

        private Color Tint(Option o, bool primary) => o.Danger ? DangerTint : primary ? _primary : _secondary;

        private static float Preferred(TMP_Text label, float width)
        {
            float keep = label.fontSize;
            label.fontSize = label.enableAutoSizing ? label.fontSizeMax : keep;
            float h = label.GetPreferredValues(label.text, width, 0f).y;
            label.fontSize = keep;
            return h;
        }

        /// <summary>살짝 커지며 나타난다 (0.15초, 일시정지에도 움직이게 unscaled)</summary>
        private IEnumerator PopIn()
        {
            const float duration = 0.15f;
            for (float t = 0f; t < duration; t += Time.unscaledDeltaTime)
            {
                float k = t / duration;
                dialogCard.localScale = Vector3.one * Mathf.Lerp(0.88f, 1f, 1f - (1f - k) * (1f - k));
                yield return null;
            }

            dialogCard.localScale = Vector3.one;
        }
    }
}
