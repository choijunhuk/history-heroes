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

        public bool IsOpen => root.activeSelf;

        private void Awake()
        {
            root.SetActive(false);
            laterButton.onClick.AddListener(() => root.SetActive(false));
        }

        public void Open(string title, string info, IReadOnlyList<Option> options)
        {
            titleLabel.text = title;
            infoLabel.text = info;
            var rows = new List<ButtonList.Row>();
            foreach (Option o in options)
            {
                Option option = o;
                rows.Add(new ButtonList.Row(o.Text, Row, () =>
                {
                    root.SetActive(false);
                    option.OnChosen?.Invoke();
                }));
            }

            list.Show(rows, scrollToTop: true);
            root.SetActive(true);
        }
    }
}
