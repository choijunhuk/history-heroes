using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace HistoryHeroes.UI
{
    /// <summary>
    /// 위아래로 미는 버튼 목록 (장비 가방 200칸, 교환소 목록 등). 줄 모양(template)은 장면 빌더가 만들어 두고,
    /// 여기서 필요한 만큼 복제해 다시 쓴다. 줄마다 글자·바탕색·누를 때 할 일을 받는다.
    /// </summary>
    public sealed class ButtonList : MonoBehaviour
    {
        [SerializeField] private Button template;
        [SerializeField] private RectTransform content;
        [SerializeField] private ScrollRect scroll;

        public struct Row
        {
            public string Text;
            public Color Background;
            public Action OnClick;
            public bool Interactable;
            public float Progress;

            public Row(string text, Color background, Action onClick, bool interactable = true, float progress = -1f)
            {
                Text = text;
                Background = background;
                OnClick = onClick;
                Interactable = interactable;
                Progress = progress;
            }
        }

        private readonly List<Button> _rows = new List<Button>();
        private readonly List<Action> _actions = new List<Action>();

        public int Count { get; private set; }

        private void Awake()
        {
            if (template != null) template.gameObject.SetActive(false);
        }

        public void Show(IReadOnlyList<Row> rows, bool scrollToTop = false)
        {
            Count = rows.Count;
            for (int i = 0; i < rows.Count; i++)
            {
                Button b = Get(i);
                b.gameObject.SetActive(true);
                b.interactable = rows[i].Interactable;
                b.GetComponentInChildren<TMP_Text>(true).text = rows[i].Text;
                Image image = b.GetComponent<Image>();
                if (image != null) image.color = rows[i].Background;
                Transform track = b.transform.Find("ProgressTrack");
                if (track != null)
                {
                    track.gameObject.SetActive(rows[i].Progress >= 0f);
                    if (rows[i].Progress >= 0f)
                    {
                        RectTransform fill = (RectTransform)track.Find("ProgressFill");
                        fill.anchorMax = new Vector2(Mathf.Clamp01(rows[i].Progress), 1f);
                    }
                }
                _actions[i] = rows[i].OnClick;
            }

            for (int i = rows.Count; i < _rows.Count; i++) _rows[i].gameObject.SetActive(false);
            if (scrollToTop && scroll != null) scroll.verticalNormalizedPosition = 1f;
        }

        private Button Get(int index)
        {
            while (_rows.Count <= index)
            {
                Button b = Instantiate(template, content);
                b.name = "Row" + _rows.Count;
                int captured = _rows.Count;
                b.onClick.AddListener(() =>
                {
                    if (captured < _actions.Count) _actions[captured]?.Invoke();
                });
                _rows.Add(b);
                _actions.Add(null);
            }

            return _rows[index];
        }
    }
}
