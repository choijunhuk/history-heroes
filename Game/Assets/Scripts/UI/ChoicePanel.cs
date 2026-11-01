using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace HistoryHeroes.UI
{
    /// <summary>
    /// 여럿 중 하나 고르기 (로비). v0.6: 5구간 보스 최초 격파 "전설 장비 1개 선택" (Equipment 8장).
    /// 고르기 전에는 닫을 수 없다 → 나중에 로비에 다시 들어오면 또 뜬다 (보상을 놓치지 않게, 닫기 = 나중에).
    /// </summary>
    public sealed class ChoicePanel : MonoBehaviour
    {
        public struct Option
        {
            public string Text;
            public Action OnChosen;
        }

        [SerializeField] private GameObject root;
        [SerializeField] private TMP_Text titleLabel;
        [SerializeField] private TMP_Text infoLabel;
        [SerializeField] private ButtonList list;
        [SerializeField] private Button laterButton;

        private static readonly Color Row = new Color(0.32f, 0.24f, 0.1f);

        /// <summary>"나중에"를 눌렀을 때 (고르기를 기다리는 쪽이 그만뒀다고 알아야 할 때)</summary>
        private Action _onLater;

        public bool IsOpen => root.activeSelf;

        private void Awake()
        {
            root.SetActive(false);
            laterButton.onClick.AddListener(() =>
            {
                root.SetActive(false);
                Later();
            });
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

        /// <summary>다른 고르기가 떠 있었으면 그 고르기는 "나중에"로 끝내고 새로 연다</summary>
        public void Open(string title, string info, IReadOnlyList<Option> options, Action onLater = null)
        {
            Later();
            _onLater = onLater;
            titleLabel.text = title;
            infoLabel.text = info;
            var rows = new List<ButtonList.Row>();
            foreach (Option o in options)
            {
                Option option = o;
                rows.Add(new ButtonList.Row(o.Text, Row, () =>
                {
                    root.SetActive(false);
                    _onLater = null;
                    option.OnChosen?.Invoke();
                }));
            }

            list.Show(rows, scrollToTop: true);
            root.SetActive(true);
        }
    }
}
