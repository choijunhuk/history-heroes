using System;
using System.Collections;
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

        // ---- 창이 열릴 때 줄이 위에서부터 차례로 나타난다 (0.045초 간격, 최대 8줄 — UI-Research 5장). 다시 그릴 때는 안 한다
        private const int IntroRows = 8;
        private const float IntroGap = 0.045f, IntroTime = 0.2f;
        private bool _introDone;

        private void OnEnable()
        {
            _introDone = false;
            if (Count > 0) Intro();
        }

        private void OnDisable()
        {
            _introDone = false;
            foreach (Button b in _rows) Settle(b);
        }

        private void Intro()
        {
            _introDone = true;
            StopAllCoroutines();
            StartCoroutine(IntroRoutine());
        }

        private IEnumerator IntroRoutine()
        {
            int n = Mathf.Min(Count, IntroRows, _rows.Count);
            float total = IntroGap * n + IntroTime;
            for (float t = 0f; t < total; t += Time.unscaledDeltaTime)
            {
                for (int i = 0; i < n; i++)
                {
                    float k = Mathf.Clamp01((t - IntroGap * i) / IntroTime);
                    CanvasGroup g = Group(_rows[i]);
                    g.alpha = k;
                    _rows[i].transform.localScale = Vector3.one * Mathf.LerpUnclamped(0.94f, 1f, PanelIntro.OutBack(k));
                }

                yield return null;
            }

            for (int i = 0; i < n; i++) Settle(_rows[i]);
        }

        private static CanvasGroup Group(Button b) => b.TryGetComponent(out CanvasGroup g) ? g : b.gameObject.AddComponent<CanvasGroup>();

        private static void Settle(Button b)
        {
            if (b == null) return;
            b.transform.localScale = Vector3.one;
            if (b.TryGetComponent(out CanvasGroup g)) g.alpha = 1f;
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
                if (image != null) image.color = UiTone.Lift(rows[i].Background); // 옛 어두운 줄 색도 밝은 카드로
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
            if (!_introDone && isActiveAndEnabled) Intro();
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
