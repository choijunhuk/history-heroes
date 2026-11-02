using System;
using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace HistoryHeroes.UI
{
    /// <summary>
    /// 보상 획득 연출 (개발자: "보상을 받았을 때 이펙트가 없는 게 아쉽다").
    /// 어두운 바탕 + "획득!" 띠 + 보상 카드가 하나씩 통통 튀어나오고 뒤에서 빛줄기가 돈다.
    /// 누르면 골드·보석은 위쪽 지갑 칩으로 날아가고 칩이 통통 뛴다. 나타나는 동안 누르면 바로 끝 상태로.
    /// 전부 실제 시간(unscaled)을 써서 일시정지 중에도 움직인다.
    /// 보상 글 형식: "&lt;sprite name=gold&gt;골드 +3,000 · &lt;sprite name=gem&gt;보석 +80"
    /// </summary>
    public sealed class RewardPopup : MonoBehaviour
    {
        [SerializeField] private GameObject root;
        [SerializeField] private Image dim;
        [SerializeField] private TMP_Text titleLabel;
        [SerializeField] private RectTransform ribbon;
        [SerializeField] private Image rays;
        [SerializeField] private Image glow;
        [SerializeField] private RectTransform cardContainer;
        [SerializeField] private GameObject cardTemplate;
        [SerializeField] private TMP_Text hintLabel;
        [SerializeField] private Button tapButton;
        [SerializeField] private RectTransform flyLayer;
        [SerializeField] private Sprite sparkleSprite;

        private const float PopTime = 0.28f;
        private const float PopGap = 0.1f;
        private const float FlyTime = 0.6f;
        private const float MaxRowWidth = 1000f;
        private const float CardGap = 24f;
        private const float AutoCloseSeconds = 30f; // 아무도 안 눌러도 끝난다

        private enum Phase { Idle, Intro, Wait, Outro }

        private struct Entry
        {
            public string text;
            public string title;
            public Action closed;
        }

        private sealed class Card
        {
            public RectTransform rect;
            public RectTransform icon;
            public string sprite;
            public Vector3 baseScale;
            public bool popped;
        }

        private static readonly Regex SpriteTag = new Regex("<sprite name=\"?(\\w+)\"?>", RegexOptions.Compiled);
        private static RewardPopup _instance;

        private readonly Queue<Entry> _queue = new Queue<Entry>();
        private readonly List<Card> _cards = new List<Card>();
        private bool _running;
        private Phase _phase;
        private bool _skip;
        private bool _tapped;
        private bool _reduce;
        private CanvasGroup _group;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => _instance = null;

        /// <summary>"출석 3일째  보상글" 같은 줄에서 앞 이름표를 떼고 보상 글만 남긴다 (두 칸 띄어쓰기 기준)</summary>
        public static string Tail(string line)
        {
            if (string.IsNullOrEmpty(line)) return line;
            int i = line.IndexOf("  ", StringComparison.Ordinal);
            return i >= 0 ? line.Substring(i + 2).Trim() : line;
        }

        /// <summary>보상 연출을 보여 준다. 이미 하나 보이는 중이면 줄 세웠다가 닫힌 뒤에 보여 준다. 연출 장면이 없으면 바로 onClosed</summary>
        public static void Show(string rewardText, string title = "획득!", Action onClosed = null)
        {
            if (_instance == null || string.IsNullOrWhiteSpace(rewardText))
            {
                onClosed?.Invoke();
                return;
            }

            _instance.Enqueue(new Entry { text = rewardText, title = title, closed = onClosed });
        }

        private void Awake()
        {
            _instance = this;
            if (root != null)
            {
                _group = root.GetComponent<CanvasGroup>();
                if (_group == null) _group = root.AddComponent<CanvasGroup>();
                root.SetActive(false);
            }

            if (cardTemplate != null) cardTemplate.SetActive(false);
            if (tapButton != null) tapButton.onClick.AddListener(OnTap);
        }

        private void OnDestroy()
        {
            if (_instance == this) _instance = null;
        }

        private void Enqueue(Entry e)
        {
            _queue.Enqueue(e);
            if (!isActiveAndEnabled || root == null || cardTemplate == null) FinishUnshown(); // 켜져 있지 않으면 막히지 않게 바로 닫힘
            else if (!_running) StartCoroutine(Run());
        }

        private void FinishUnshown()
        {
            while (_queue.Count > 0) _queue.Dequeue().closed?.Invoke();
        }

        private void OnDisable()
        {
            if (!_running) return;
            // 도중에 꺼지면 정리하고 남은 것도 닫힘 처리
            StopAllCoroutines();
            _running = false;
            ForceHide();
            CloseCurrent(); // 도중에 꺼진 연출도 닫힘을 알린다
            FinishUnshown();
        }

        private void OnTap()
        {
            if (_phase == Phase.Intro) _skip = true;
            else if (_phase == Phase.Wait) _tapped = true;
        }

        private Action _currentClosed;
        private bool _failed;

        private IEnumerator Run()
        {
            _running = true;
            while (_queue.Count > 0)
            {
                Entry e = _queue.Dequeue();
                _currentClosed = e.closed;
                _failed = false;
                yield return Guarded(Play(e));
                if (_failed) ForceHide();
                CloseCurrent();
            }

            _running = false;
        }

        /// <summary>
        /// 연출을 한 단계씩 돌린다. 도중에 오류가 나면 그 연출만 멈추고 _failed (Run이 덮개 · 누름 칸을 치운다) —
        /// 전체 화면 누름 칸이 남아 로비를 막는 일이 없게 (리뷰 2026-11-02)
        /// </summary>
        private IEnumerator Guarded(IEnumerator routine)
        {
            var stack = new Stack<IEnumerator>();
            stack.Push(routine);
            while (stack.Count > 0)
            {
                IEnumerator top = stack.Peek();
                bool more;
                try
                {
                    more = top.MoveNext();
                }
                catch (Exception ex)
                {
                    Debug.LogException(ex);
                    _failed = true;
                    yield break;
                }

                if (!more)
                {
                    stack.Pop();
                    continue;
                }

                if (top.Current is IEnumerator nested)
                {
                    stack.Push(nested);
                    continue;
                }

                yield return top.Current;
            }
        }

        private void ForceHide()
        {
            _phase = Phase.Idle;
            try
            {
                Cleanup();
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
            }

            if (root != null) root.SetActive(false);
        }

        /// <summary>지금 연출을 기다리는 쪽에 한 번만 "닫힘"을 알린다</summary>
        private void CloseCurrent()
        {
            Action closed = _currentClosed;
            _currentClosed = null;
            try
            {
                closed?.Invoke();
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
            }
        }

        private void Update()
        {
            if (_phase == Phase.Idle) return;
            if (rays != null) rays.rectTransform.Rotate(0f, 0f, -(_reduce ? 8f : 25f) * Time.unscaledDeltaTime);
            if (hintLabel != null && _phase == Phase.Wait)
            {
                Color c = hintLabel.color;
                c.a = 0.55f + 0.45f * (0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 4f));
                hintLabel.color = c;
            }
        }

        private IEnumerator Play(Entry e)
        {
            _reduce = Records.SaveSystem.Data != null && Records.SaveSystem.Data.settings.reduceFlashes;
            _skip = false;
            _tapped = false;
            if (titleLabel != null) titleLabel.text = e.title;
            BuildCards(e.text);

            root.SetActive(true);
            _group.alpha = 0f;
            _phase = Phase.Intro;
            if (hintLabel != null) hintLabel.gameObject.SetActive(false);
            if (ribbon != null) ribbon.localScale = Vector3.zero;
            SetGlow(0.6f, 1f);
            foreach (Card c in _cards) c.rect.localScale = Vector3.zero;

            float total = 0.15f + PopTime + PopGap * Mathf.Max(0, _cards.Count - 1) + 0.1f;
            float t = 0f;
            bool flashed = false;
            while (t < total && !_skip)
            {
                t += Time.unscaledDeltaTime;
                _group.alpha = Mathf.Clamp01(t / 0.15f);
                if (ribbon != null) ribbon.localScale = Vector3.one * Mathf.Max(0f, OutBack(Mathf.Clamp01(t / 0.3f)));
                for (int i = 0; i < _cards.Count; i++)
                {
                    float local = (t - 0.15f - i * PopGap) / PopTime;
                    if (local <= 0f) continue;
                    Card c = _cards[i];
                    c.rect.localScale = c.baseScale * Mathf.Max(0f, OutBack(Mathf.Clamp01(local)));
                    if (c.popped) continue;
                    c.popped = true;
                    Audio.AudioManager.Play("gem", 0.7f, 0.1f);
                    if (!flashed && !_reduce)
                    {
                        flashed = true;
                        StartCoroutine(FlashGlow());
                    }

                    StartCoroutine(Sparkle(c.rect));
                }

                yield return null;
            }

            // 건너뛰기든 끝났든 끝 상태로 맞춘다
            _group.alpha = 1f;
            if (ribbon != null) ribbon.localScale = Vector3.one;
            foreach (Card c in _cards) c.rect.localScale = c.baseScale;

            _phase = Phase.Wait;
            if (hintLabel != null)
            {
                hintLabel.gameObject.SetActive(true);
                hintLabel.text = "눌러서 받기";
            }

            // 건너뛴 그 누름이 바로 받기로 이어지지 않게 한 프레임 쉰다
            yield return null;
            _tapped = false;
            float waited = 0f;
            while (!_tapped && waited < AutoCloseSeconds)
            {
                waited += Time.unscaledDeltaTime;
                yield return null;
            }

            _phase = Phase.Outro;
            if (hintLabel != null) hintLabel.gameObject.SetActive(false);
            yield return Outro();
            Cleanup();
            _phase = Phase.Idle;
            root.SetActive(false);
        }

        private IEnumerator Outro()
        {
            int flying = 0;
            foreach (Card c in _cards)
            {
                RectTransform target = TargetFor(c.sprite);
                if (target == null) continue;
                int copies = UnityEngine.Random.Range(5, 9);
                for (int i = 0; i < copies; i++)
                {
                    flying++;
                    StartCoroutine(Fly(c, target, i * 0.05f, () => flying--));
                }
            }

            // 카드·바탕은 서서히 사라지고, 날아가는 복사본은 따로 그린다
            float fade = flying > 0 ? 0.35f : 0.2f;
            float t = 0f;
            while (t < fade)
            {
                t += Time.unscaledDeltaTime;
                _group.alpha = 1f - Mathf.Clamp01(t / fade);
                yield return null;
            }

            _group.alpha = 0f;
            float guard = 0f;
            while (flying > 0 && guard < FlyTime + 2f)
            {
                guard += Time.unscaledDeltaTime;
                yield return null;
            }
        }

        private static RectTransform TargetFor(string sprite)
        {
            if (sprite == "gold") return WalletChip.Target("gold");
            if (sprite == "gem") return WalletChip.Target("gem");
            return null;
        }

        private IEnumerator Fly(Card card, RectTransform target, float delay, Action done)
        {
            GameObject go = Instantiate(card.icon.gameObject, flyLayer);
            go.SetActive(true);
            var rt = (RectTransform)go.transform;
            var label = go.GetComponent<TMP_Text>();
            if (label != null) label.fontSize = 70f;
            rt.sizeDelta = new Vector2(110f, 110f);
            Vector3 start = card.icon.position + new Vector3(UnityEngine.Random.Range(-40f, 40f), UnityEngine.Random.Range(-40f, 40f), 0f);
            rt.position = start;
            rt.localScale = Vector3.zero;
            // 휘어진 길: 시작과 끝의 가운데에서 옆으로 벗어난 점을 지난다
            Vector3 end = target.position;
            Vector3 side = Vector3.Cross(end - start, Vector3.forward).normalized;
            Vector3 control = (start + end) * 0.5f + side * UnityEngine.Random.Range(-250f, 250f);

            float wait = 0f;
            while (wait < delay)
            {
                wait += Time.unscaledDeltaTime;
                yield return null;
            }

            rt.localScale = Vector3.one;
            float t = 0f;
            while (t < 1f)
            {
                t += Time.unscaledDeltaTime / FlyTime;
                float k = Mathf.Clamp01(t);
                k *= k; // 처음엔 천천히, 끝에 빠르게
                if (target != null) end = target.position;
                rt.position = (1f - k) * (1f - k) * start + 2f * (1f - k) * k * control + k * k * end;
                rt.localScale = Vector3.one * Mathf.Lerp(1f, 0.5f, k);
                yield return null;
            }

            Destroy(go);
            Audio.AudioManager.Play("coin", 0.5f, 0.15f);
            if (target != null)
            {
                var chip = target.GetComponent<WalletChip>();
                if (chip != null) chip.Bump();
            }

            done?.Invoke();
        }

        private void BuildCards(string text)
        {
            Cleanup();
            string[] parts = text.Split(new[] { " · " }, StringSplitOptions.RemoveEmptyEntries);
            var items = new List<KeyValuePair<string, string>>(); // 그림 이름(없으면 null), 글
            foreach (string raw in parts)
            {
                string part = raw.Trim();
                if (part.Length == 0) continue;
                Match m = SpriteTag.Match(part);
                string label = (m.Success ? SpriteTag.Replace(part, string.Empty, 1) : part).Trim();
                items.Add(new KeyValuePair<string, string>(m.Success ? m.Groups[1].Value : null, label));
            }

            int n = items.Count;
            if (n == 0) return;
            Vector2 size = ((RectTransform)cardTemplate.transform).sizeDelta;
            int rows = Mathf.CeilToInt(n / 6f);
            int cols = Mathf.CeilToInt(n / (float)rows);
            float scale = n > 8 ? 0.8f : 1f;
            float rowWidth = cols * size.x + (cols - 1) * CardGap;
            scale = Mathf.Min(scale, MaxRowWidth / rowWidth);
            float w = size.x * scale;
            float h = size.y * scale;
            float gap = CardGap * scale;
            float gridH = rows * h + (rows - 1) * gap;

            for (int i = 0; i < n; i++)
            {
                int row = i / cols;
                int inRow = Mathf.Min(cols, n - row * cols);
                int col = i - row * cols;
                float rowW = inRow * w + (inRow - 1) * gap;
                GameObject go = Instantiate(cardTemplate, cardContainer);
                go.SetActive(true);
                var rt = (RectTransform)go.transform;
                rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.pivot = new Vector2(0.5f, 0.5f);
                rt.anchoredPosition = new Vector2(-rowW * 0.5f + w * 0.5f + col * (w + gap), gridH * 0.5f - h * 0.5f - row * (h + gap));
                var card = new Card { rect = rt, baseScale = Vector3.one * scale, sprite = items[i].Key };

                Transform icon = rt.Find("Icon");
                Transform amount = rt.Find("Amount");
                TMP_Text amountLabel = amount != null ? amount.GetComponent<TMP_Text>() : null;
                if (amountLabel != null) amountLabel.text = items[i].Value;
                card.icon = icon != null ? (RectTransform)icon : rt;
                if (items[i].Key != null)
                {
                    TMP_Text iconLabel = icon != null ? icon.GetComponent<TMP_Text>() : null;
                    if (iconLabel != null) iconLabel.text = $"<sprite name={items[i].Key}>";
                }
                else if (icon != null)
                {
                    // 그림이 없으면 글자만 카드 전체에
                    icon.gameObject.SetActive(false);
                    card.icon = rt;
                    if (amountLabel != null)
                    {
                        RectTransform ar = amountLabel.rectTransform;
                        ar.anchorMin = Vector2.zero;
                        ar.anchorMax = Vector2.one;
                        ar.offsetMin = new Vector2(12f, 12f);
                        ar.offsetMax = new Vector2(-12f, -12f);
                        amountLabel.fontSize = 40f;
                        amountLabel.alignment = TextAlignmentOptions.Center;
                    }
                }

                _cards.Add(card);
            }

            // 빛줄기·빛무리는 카드 묶음 크기의 3.5배쯤, 카드 묶음 가운데에
            float span = Mathf.Max(Mathf.Min(rowWidth * scale, MaxRowWidth), gridH);
            float big = Mathf.Clamp(span * 1.7f, 900f, 1400f); // 화면을 다 덮으면 산만하다
            if (rays != null)
            {
                rays.rectTransform.sizeDelta = new Vector2(big, big);
                rays.rectTransform.localRotation = Quaternion.identity;
                rays.rectTransform.position = cardContainer.position;
            }

            if (glow != null)
            {
                glow.rectTransform.sizeDelta = new Vector2(big * 0.6f, big * 0.6f);
                glow.rectTransform.position = cardContainer.position;
            }
        }

        private void Cleanup()
        {
            foreach (Card c in _cards) if (c.rect != null) Destroy(c.rect.gameObject);
            _cards.Clear();
            if (flyLayer != null)
                for (int i = flyLayer.childCount - 1; i >= 0; i--) Destroy(flyLayer.GetChild(i).gameObject);
        }

        private void SetGlow(float alpha, float scale)
        {
            if (glow == null) return;
            Color c = glow.color;
            c.a = alpha;
            glow.color = c;
            glow.rectTransform.localScale = Vector3.one * scale;
        }

        // 하얗게 번쩍: 빛무리를 확 키웠다가 줄인다 (번쩍임 줄이기 설정이면 안 부른다)
        private IEnumerator FlashGlow()
        {
            float t = 0f;
            while (t < 0.3f)
            {
                t += Time.unscaledDeltaTime;
                float k = Mathf.Clamp01(t / 0.3f);
                SetGlow(Mathf.Lerp(1f, 0.6f, k), Mathf.Lerp(1.4f, 1f, k));
                yield return null;
            }

            SetGlow(0.6f, 1f);
        }

        // 카드에서 반짝이가 튀어 나간다
        private IEnumerator Sparkle(RectTransform at)
        {
            if (sparkleSprite == null || flyLayer == null) yield break;
            int count = _reduce ? 5 : 10;
            var rts = new RectTransform[count];
            var imgs = new Image[count];
            var dirs = new Vector2[count];
            var spins = new float[count];
            for (int i = 0; i < count; i++)
            {
                var go = new GameObject("Sparkle", typeof(RectTransform));
                var rt = (RectTransform)go.transform;
                rt.SetParent(flyLayer, false);
                rt.sizeDelta = new Vector2(44f, 44f);
                rt.position = at.position;
                var img = go.AddComponent<Image>();
                img.sprite = sparkleSprite;
                img.raycastTarget = false;
                img.color = new Color(1f, 0.95f, 0.65f, 1f);
                float a = UnityEngine.Random.value * Mathf.PI * 2f;
                rts[i] = rt;
                imgs[i] = img;
                dirs[i] = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * UnityEngine.Random.Range(140f, 260f);
                spins[i] = UnityEngine.Random.Range(-240f, 240f);
            }

            float t = 0f;
            const float life = 0.6f;
            while (t < life)
            {
                float dt = Time.unscaledDeltaTime;
                t += dt;
                float k = Mathf.Clamp01(t / life);
                for (int i = 0; i < count; i++)
                {
                    if (rts[i] == null) continue; // 중간에 정리돼도 괜찮게
                    rts[i].anchoredPosition += dirs[i] * (dt * (1f - k));
                    rts[i].localRotation = Quaternion.Euler(0f, 0f, spins[i] * t);
                    rts[i].localScale = Vector3.one * Mathf.Lerp(1f, 0.2f, k);
                    Color c = imgs[i].color;
                    c.a = 1f - k;
                    imgs[i].color = c;
                }

                yield return null;
            }

            for (int i = 0; i < count; i++) if (rts[i] != null) Destroy(rts[i].gameObject);
        }

        // 0 → 1.2 쯤 넘쳤다가 1.0 으로
        private static float OutBack(float t)
        {
            const float s = 3f;
            float u = t - 1f;
            return 1f + (s + 1f) * u * u * u + s * u * u;
        }
    }
}
