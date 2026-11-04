using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace HistoryHeroes.UI
{
    /// <summary>저장된 뽑기 결과만 보여 주는 개봉 화면. 재화나 추첨 상태를 소유하지 않는다.</summary>
    public sealed class GachaReveal : MonoBehaviour
    {
        public readonly struct Entry
        {
            public readonly string Title;
            public readonly string Detail;
            public readonly Sprite Icon;
            public readonly Color Accent;

            public Entry(string title, string detail, Sprite icon, Color accent)
            {
                Title = title;
                Detail = detail;
                Icon = icon;
                Accent = accent;
            }
        }

        private readonly List<CanvasGroup> _cards = new List<CanvasGroup>(10);
        private GameObject _panel;
        private Image _closedChest;
        private Image _openChest;
        private Image _glow;
        private Image _rays;
        private TMP_Text _title;
        private TMP_Text _buttonText;
        private RectTransform _content;
        private ScrollRect _scroll;
        private GridLayoutGroup _grid;
        private Action _onClosed;
        private float _started;
        private bool _reduceFlashes;
        private bool _highlight;
        private bool _open;
        private bool _results;

        public bool IsOpen => _open;
        public bool ResultsVisible => _results;
        public int VisibleCardCount => _cards.Count;

        public void Initialize(TMP_Text template)
        {
            if (_panel != null) return;
            RectTransform panel = Rect("GachaReveal", transform);
            panel.anchorMin = Vector2.zero;
            panel.anchorMax = Vector2.one;
            panel.offsetMin = panel.offsetMax = Vector2.zero;
            _panel = panel.gameObject;
            var shade = panel.gameObject.AddComponent<Image>();
            shade.color = new Color(0.035f, 0.055f, 0.13f, 0.97f);
            shade.raycastTarget = true;

            _rays = Picture("Rays", panel, "rays", new Vector2(660f, 660f), new Vector2(0f, 100f));
            _glow = Picture("Glow", panel, "glow", new Vector2(670f, 670f), new Vector2(0f, 100f));
            _closedChest = Picture("ChestClosed", panel, "chest_premium", new Vector2(570f, 570f), new Vector2(0f, 100f));
            _openChest = Picture("ChestOpen", panel, "chest_open", new Vector2(640f, 640f), new Vector2(0f, 100f));
            Image heading = Picture("TitleRibbon", panel, "ribbon", new Vector2(1000f, 110f), Vector2.zero);
            heading.color = new Color(0.82f, 0.22f, 0.18f, 1f);
            heading.preserveAspect = false;
            heading.rectTransform.anchorMin = heading.rectTransform.anchorMax = new Vector2(0.5f, 1f);
            heading.rectTransform.anchoredPosition = new Vector2(0f, -105f);
            _title = Label("Title", panel, template, 55, Vector2.zero, new Vector2(1000f, 110f));
            _title.rectTransform.anchorMin = _title.rectTransform.anchorMax = new Vector2(0.5f, 1f);
            _title.rectTransform.anchoredPosition = new Vector2(0f, -105f);

            RectTransform viewport = Rect("ResultsViewport", panel);
            viewport.anchorMin = Vector2.zero;
            viewport.anchorMax = Vector2.one;
            viewport.offsetMin = new Vector2(65f, 230f);
            viewport.offsetMax = new Vector2(-65f, -220f);
            var viewportImage = viewport.gameObject.AddComponent<Image>();
            viewportImage.color = new Color(0.04f, 0.09f, 0.21f, 0.55f);
            viewport.gameObject.AddComponent<RectMask2D>();
            _content = Rect("Results", viewport);
            _content.anchorMin = new Vector2(0f, 1f);
            _content.anchorMax = Vector2.one;
            _content.pivot = new Vector2(0.5f, 1f);
            _content.anchoredPosition = Vector2.zero;
            _content.sizeDelta = new Vector2(0f, 1180f);
            _grid = _content.gameObject.AddComponent<GridLayoutGroup>();
            _grid.cellSize = new Vector2(451f, 218f);
            _grid.spacing = new Vector2(14f, 18f);
            _grid.padding = new RectOffset(8, 8, 8, 8);
            _grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            _grid.constraintCount = 2;
            _grid.childAlignment = TextAnchor.MiddleCenter;
            _scroll = viewport.gameObject.AddComponent<ScrollRect>();
            _scroll.viewport = viewport;
            _scroll.content = _content;
            _scroll.horizontal = false;
            _scroll.vertical = true;
            _scroll.movementType = ScrollRect.MovementType.Clamped;
            _scroll.scrollSensitivity = 40f;

            RectTransform buttonRect = Rect("Continue", panel);
            Place(buttonRect, new Vector2(700f, 108f), Vector2.zero);
            buttonRect.anchorMin = buttonRect.anchorMax = new Vector2(0.5f, 0f);
            buttonRect.anchoredPosition = new Vector2(0f, 130f);
            Image buttonImage = buttonRect.gameObject.AddComponent<Image>();
            buttonImage.sprite = Resources.Load<Sprite>("UI/Gacha/slot");
            buttonImage.type = Image.Type.Sliced;
            buttonImage.pixelsPerUnitMultiplier = 3f;
            buttonImage.color = new Color(0.14f, 0.62f, 0.37f);
            var button = buttonRect.gameObject.AddComponent<Button>();
            button.onClick.AddListener(OnContinue);
            _buttonText = Label("Text", buttonRect, template, 40, Vector2.zero, buttonRect.sizeDelta);
            _buttonText.raycastTarget = false;
            _panel.SetActive(false);
        }

        public void Show(string title, Sprite closedArt, Sprite openedArt, IReadOnlyList<Entry> entries,
            int spotlightIndex, bool highlight, bool reduceFlashes, Action onClosed)
        {
            if (_panel == null || entries == null || entries.Count == 0)
            {
                onClosed?.Invoke();
                return;
            }

            Hide();
            _onClosed = onClosed;
            _open = true;
            _results = false;
            _reduceFlashes = reduceFlashes;
            _highlight = highlight;
            _started = Time.unscaledTime;
            _title.text = title;
            _buttonText.text = "바로 결과 보기";
            _closedChest.sprite = closedArt != null ? closedArt : Resources.Load<Sprite>("UI/Gacha/chest_premium");
            _openChest.sprite = openedArt != null ? openedArt : Resources.Load<Sprite>("UI/Gacha/chest_open");
            if (_openChest.sprite == null) _openChest.sprite = _closedChest.sprite;
            _closedChest.enabled = _closedChest.sprite != null;
            _openChest.enabled = _openChest.sprite != null;
            _closedChest.rectTransform.anchoredPosition = new Vector2(0f, 100f);
            _closedChest.rectTransform.localScale = Vector3.one * 0.86f;
            _openChest.rectTransform.localScale = Vector3.one * 0.75f;
            _closedChest.gameObject.SetActive(true);
            _openChest.gameObject.SetActive(false);
            _glow.gameObject.SetActive(_glow.sprite != null);
            _rays.gameObject.SetActive(_rays.sprite != null && !reduceFlashes);
            _glow.color = new Color(1f, 0.71f, 0.23f, reduceFlashes ? 0.13f : 0.29f);
            _rays.color = new Color(1f, 0.83f, 0.43f, 0.33f);
            _scroll.gameObject.SetActive(false);
            foreach (CanvasGroup card in _cards)
            {
                if (card == null) continue;
                card.gameObject.SetActive(false);
                if (Application.isPlaying) Destroy(card.gameObject);
                else DestroyImmediate(card.gameObject);
            }
            _cards.Clear();
            _grid.constraintCount = entries.Count == 1 ? 1 : 2;
            _grid.cellSize = entries.Count == 1 ? new Vector2(800f, 640f) : new Vector2(451f, 218f);
            _content.sizeDelta = new Vector2(0f, Mathf.CeilToInt(entries.Count / 2f) * (entries.Count == 1 ? 640f : 236f) + 16f);
            foreach (Entry entry in entries) AddCard(entry);
            if (spotlightIndex >= 0 && spotlightIndex < _cards.Count)
            {
                CanvasGroup spotlight = _cards[spotlightIndex];
                _cards.RemoveAt(spotlightIndex);
                _cards.Add(spotlight); // 결과 위치는 유지하고 가장 좋은 카드만 마지막에 공개
            }
            _panel.transform.SetAsLastSibling();
            _panel.SetActive(true);
            Canvas.ForceUpdateCanvases();
            _content.sizeDelta = new Vector2(0f, Mathf.Max(_content.sizeDelta.y, _scroll.viewport.rect.height));
            Audio.AudioManager.Play("reward", 0.85f);
        }

        private void AddCard(Entry entry)
        {
            RectTransform card = Rect("ResultCard", _content);
            var background = card.gameObject.AddComponent<Image>();
            background.sprite = Resources.Load<Sprite>("UI/Gacha/slot");
            background.type = Image.Type.Sliced;
            background.pixelsPerUnitMultiplier = 3f;
            background.color = new Color(0.12f, 0.18f, 0.32f, 1f);
            background.raycastTarget = false;
            var outline = card.gameObject.AddComponent<Outline>();
            outline.effectColor = entry.Accent;
            outline.effectDistance = new Vector2(3f, -3f);
            bool single = _grid.constraintCount == 1;
            Vector2 iconPosition = single ? new Vector2(0f, 115f) : new Vector2(-152f, 0f);
            if (single)
            {
                Image itemGlow = Picture("ItemGlow", card, "glow", new Vector2(400f, 400f), iconPosition);
                itemGlow.color = new Color(entry.Accent.r, entry.Accent.g, entry.Accent.b, _reduceFlashes ? 0.1f : 0.25f);
            }
            var icon = Picture("Icon", card, null, Vector2.one * (single ? 300f : 135f), iconPosition);
            icon.sprite = entry.Icon;
            icon.gameObject.SetActive(entry.Icon != null);
            float textWidth = single ? 740f : 285f;
            TMP_Text label = Label("Name", card, _title, single ? 44 : 34,
                single ? new Vector2(0f, -90f) : new Vector2(62f, 35f), new Vector2(textWidth, 92f));
            label.alignment = single ? TextAlignmentOptions.Center : TextAlignmentOptions.MidlineLeft;
            label.text = entry.Title;
            TMP_Text detail = Label("Detail", card, _title, single ? 34 : 30,
                single ? new Vector2(0f, -190f) : new Vector2(62f, -55f), new Vector2(textWidth, 110f));
            detail.alignment = single ? TextAlignmentOptions.Center : TextAlignmentOptions.MidlineLeft;
            detail.color = entry.Accent;
            detail.text = entry.Detail;
            var group = card.gameObject.AddComponent<CanvasGroup>();
            group.alpha = 0f;
            card.localScale = Vector3.one * 0.8f;
            _cards.Add(group);
        }

        private void Update()
        {
            if (!_open || _results) return;
            float elapsed = Time.unscaledTime - _started;
            if (elapsed < 0.55f)
            {
                float shake = _reduceFlashes ? 2f : 8f;
                _closedChest.rectTransform.anchoredPosition = new Vector2(Mathf.Sin(elapsed * 33f) * shake, 100f);
                _closedChest.rectTransform.localScale = Vector3.one * (0.86f + elapsed * 0.3f);
                return;
            }

            if (_closedChest.gameObject.activeSelf)
            {
                _closedChest.gameObject.SetActive(false);
                _openChest.gameObject.SetActive(true);
                _openChest.rectTransform.localScale = Vector3.one * 0.75f;
                Audio.AudioManager.Play(_highlight ? "legendary" : "card", _reduceFlashes ? 0.55f : 0.8f);
            }

            float open = Mathf.Clamp01((elapsed - 0.55f) / 0.45f);
            _openChest.rectTransform.localScale = Vector3.one * Mathf.Lerp(0.75f, 1.08f, PanelIntro.OutBack(open));
            if (elapsed >= 1.18f) RevealResults();
        }

        private void LateUpdate()
        {
            if (!_open || !_results) return;
            float elapsed = Time.unscaledTime - _started;
            for (int i = 0; i < _cards.Count; i++)
            {
                if (_cards[i] == null) continue;
                float k = Mathf.Clamp01((elapsed - i * 0.09f) / 0.25f);
                _cards[i].alpha = k;
                _cards[i].transform.localScale = Vector3.one * Mathf.Lerp(0.8f, 1f, PanelIntro.OutBack(k));
            }
            if (_cards.Count == 0 || _cards[_cards.Count - 1].alpha >= 1f) _buttonText.text = "확인";
        }

        public void Skip()
        {
            if (!_open || _results) return;
            RevealResults();
            FinishCards();
        }

        private void FinishCards()
        {
            _started = Time.unscaledTime - _cards.Count * 0.09f - 0.25f;
            foreach (CanvasGroup card in _cards)
            {
                if (card == null) continue;
                card.alpha = 1f;
                card.transform.localScale = Vector3.one;
            }
            _buttonText.text = "확인";
        }

        private void RevealResults()
        {
            _results = true;
            _title.text += " · 획득";
            _buttonText.text = "결과 모두 보기";
            _closedChest.gameObject.SetActive(false);
            _openChest.gameObject.SetActive(false);
            _glow.gameObject.SetActive(false);
            _rays.gameObject.SetActive(false);
            _scroll.gameObject.SetActive(true);
            _scroll.verticalNormalizedPosition = 1f;
            _started = Time.unscaledTime;
            Audio.AudioManager.Play("card", 0.75f);
        }

        private void OnContinue()
        {
            if (_results && _cards.Count > 0 && _cards[_cards.Count - 1].alpha < 1f) FinishCards();
            else if (_results) Hide();
            else Skip();
        }

        public void Hide()
        {
            if (!_open) return;
            _open = false;
            _results = false;
            if (_panel != null) _panel.SetActive(false);
            Action closed = _onClosed;
            _onClosed = null;
            closed?.Invoke();
        }

        private void OnDisable() => Hide();

        private static RectTransform Rect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            return rect;
        }

        private static void Place(RectTransform rect, Vector2 size, Vector2 position)
        {
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = size;
            rect.anchoredPosition = position;
        }

        private static Image Picture(string name, Transform parent, string key, Vector2 size, Vector2 position)
        {
            RectTransform rect = Rect(name, parent);
            Place(rect, size, position);
            var image = rect.gameObject.AddComponent<Image>();
            image.preserveAspect = true;
            image.raycastTarget = false;
            if (key != null) image.sprite = Resources.Load<Sprite>("UI/Gacha/" + key);
            return image;
        }

        private static TMP_Text Label(string name, Transform parent, TMP_Text template, int size, Vector2 position, Vector2 area)
        {
            RectTransform rect = Rect(name, parent);
            Place(rect, area, position);
            var text = rect.gameObject.AddComponent<TextMeshProUGUI>();
            if (template != null)
            {
                text.font = template.font;
                text.fontSharedMaterial = template.fontSharedMaterial;
                text.spriteAsset = template.spriteAsset;
            }
            text.fontSize = size;
            text.enableAutoSizing = true;
            text.fontSizeMin = 27f;
            text.fontSizeMax = size;
            text.alignment = TextAlignmentOptions.Center;
            text.color = Color.white;
            text.raycastTarget = false;
            return text;
        }
    }
}
