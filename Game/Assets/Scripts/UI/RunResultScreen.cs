using System;
using System.Collections.Generic;
using System.Text;
using HistoryHeroes.Common;
using HistoryHeroes.Dungeon;
using HistoryHeroes.Modes;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace HistoryHeroes.UI
{
    /// <summary>
    /// 판 결과 화면 (Content.md 2-4). 던전: 결과 → [기록 남기기] / [로비로]. 보스: 결과 → [다시 도전] / [로비로].
    /// 열릴 때 연출 (실제 시간): 판이 아래에서 올라오고(졌을 땐 천천히 나타남) → 제목이 통 튀어나오고 → 보상 카드 숫자가 0에서 올라가며(coin 틱, reward) →
    /// "첫 클리어!" 띠 → 단추가 나타난다. 화면을 새로 누르면 바로 끝 상태 (단추는 손을 뗀 뒤 눌린다).
    /// </summary>
    public sealed class RunResultScreen : MonoBehaviour
    {
        [SerializeField] private GameObject root;
        [SerializeField] private TMP_Text titleLabel;
        [SerializeField] private TMP_Text bodyLabel;
        [SerializeField] private Button primaryButton;
        [SerializeField] private TMP_Text primaryLabel;
        [SerializeField] private Button secondaryButton;
        [SerializeField] private TMP_Text secondaryLabel;

        [Header("보상 카드 (그림 + 숫자)")]
        [SerializeField] private RectTransform cardRow;
        [Tooltip("꺼 둔 카드 하나 (자식 Icon · Amount). 보상마다 복제한다")]
        [SerializeField] private GameObject cardTemplate;
        [Tooltip("reward_키 그림 (Art/UI/Rewards)")]
        [SerializeField] private Sprite[] rewardSprites;
        [Tooltip("카드가 있으면 본문을 이만큼 아래에서 시작한다")]
        [SerializeField] private float cardRowHeight = 190f;

        [Header("별 줄 (던전 클리어)")]
        [SerializeField] private Sprite starSprite;
        [SerializeField] private Sprite sparkleSprite;

        private Action _primary;
        private Action _secondary;
        private readonly List<GameObject> _cards = new List<GameObject>();
        private Vector2 _bodyPos;
        private Vector2 _bodySize;
        private Vector2 _cardRowPos;

        // 별 줄: 큰 별 3개 + 아래 조건 글. 이번에 새로 딴 별은 회색에서 금색으로 하나씩 쾅 (열리는 연출 중)
        private const float StarDrop = 230f;
        private const float StarDelay = 0.25f;
        private static readonly Color StarGold = new Color(1f, 0.8f, 0.2f);
        private static readonly Color StarGray = new Color(0.33f, 0.35f, 0.4f);
        private RectTransform _starRow;
        private readonly List<Image> _starImages = new List<Image>();
        private readonly List<Image> _starSparkles = new List<Image>();
        private readonly List<bool> _starNew = new List<bool>();
        private readonly List<bool> _starOwned = new List<bool>();
        private readonly List<bool> _starDone = new List<bool>();
        private readonly List<int> _starOrder = new List<int>(); // 새 별 몇 번째인가 (-1 = 새 별 아님)
        private float _starDrop;

        // 열리는 연출
        private const float CountDelay = 0.25f;
        private const float CountSeconds = 0.7f;
        private readonly List<TMP_Text> _countLabels = new List<TMP_Text>();
        private readonly List<int> _countTargets = new List<int>();
        private readonly List<bool> _countDone = new List<bool>();
        private CanvasGroup _panelGroup;
        private CanvasGroup _primaryGroup;
        private CanvasGroup _secondaryGroup;
        private RectTransform _firstRibbon;
        private bool _victory;
        private bool _animating;
        private bool _ribbonShown;
        private float _t0;
        private float _lastTick;
        private float _unlockAt;
        private int _lastCountShown;

        public bool IsOpen => root.activeSelf;

        private void Awake()
        {
            root.SetActive(false);
            _bodyPos = bodyLabel.rectTransform.anchoredPosition;
            _bodySize = bodyLabel.rectTransform.sizeDelta;
            if (cardRow != null) _cardRowPos = cardRow.anchoredPosition;
            if (cardTemplate != null) cardTemplate.SetActive(false);
            primaryButton.onClick.AddListener(() => _primary?.Invoke());
            secondaryButton.onClick.AddListener(() => _secondary?.Invoke());
        }

        /// <param name="saveRecord">기록을 남길 수 있으면 그 화면을 여는 동작, 못 남기면 null</param>
        public void Show(RunSummary s, Action saveRecord)
        {
            Open();
            titleLabel.text = s.Victory ? "클리어!" : "쓰러졌다…";

            BuildStars(s.Victory ? s.StarRows : null);

            // 도달 방 · 받은 것 · 기록 용량을 먼저, 자세한 숫자는 아래 작게 (전체 UI 검토: "목표 40%" 같은 개발용 글은 뺀다)
            var sb = new StringBuilder();
            sb.AppendLine($"<size=120%>도달한 방  <b>{s.RoomReached} / {s.RoomCount}</b></size>");
            AppendRewards(sb, s.RewardLines);
            sb.AppendLine();
            sb.AppendLine(s.Capacity > 0 ? $"남길 수 있는 기록 용량  <b>{s.Capacity}pt</b>" : "너무 일찍 쓰러져서 남길 수 있는 기록이 없어요");
            sb.AppendLine();
            sb.AppendLine($"<size=85%>시간 {Clock(s.Seconds)} · 처치 {s.Kills} · 레벨 {s.Level}");
            if (!string.IsNullOrEmpty(s.TopDamage)) sb.AppendLine($"피해  {s.TopDamage}");
            sb.AppendLine($"만든 조합  {(s.Fusions.Count > 0 ? string.Join(", ", s.Fusions) : "없음")}");
            sb.AppendLine($"스킬  {(s.Skills.Count > 0 ? string.Join(" · ", s.Skills) : "없음")}</size>");
            bodyLabel.text = sb.ToString();

            if (saveRecord != null) SetButtons("기록 남기기", saveRecord, "로비로", GameSession.ToLobby);
            else SetButtons("로비로", GameSession.ToLobby, null, null);
            BeginIntro(s.Victory, s.FirstClear);
        }

        public void ShowBoss(BossSummary s)
        {
            Open();
            titleLabel.text = s.Victory ? (s.IsGodTrial ? "시련 통과!" : "격파!") : "패배…";

            var sb = new StringBuilder();
            sb.AppendLine(s.BossName);
            if (s.Victory) sb.AppendLine($"시간  {Clock(s.Seconds)}");
            string star2 = s.Star2MaxHits > 0 ? $"  (★2: {s.Star2MaxHits}번 이하)" : string.Empty;
            sb.AppendLine($"맞은 횟수  {s.HitsTaken}{star2}");
            sb.AppendLine();
            sb.AppendLine("쓴 기록");
            sb.AppendLine("  " + s.RecordLabel);
            sb.AppendLine("  기록은 이겨도 져도 사라지지 않아요");
            if (s.FirstClear)
            {
                sb.AppendLine();
                sb.AppendLine(FirstBossClearText(s));
            }

            AppendRewards(sb, s.RewardLines);
            bodyLabel.text = sb.ToString();
            SetButtons(GameSession.Practice ? "다시 연습" : "다시 도전 (하트 1)", Retry, "로비로", GameSession.ToLobby);
            BeginIntro(s.Victory, s.Victory && s.FirstClear);
        }

        public void ShowHeroTrial(Heroes.HeroData hero)
        {
            Open();
            transform.SetAsLastSibling(); // 같은 프레임에 열린 카드·보상 방보다 결과가 앞에 오게
            titleLabel.text = "체험 끝";
            bodyLabel.text = hero != null
                ? $"{hero.displayName} · Lv1 ★1\n\n{Heroes.HeroUnlock.LockText(hero)}\n\n체험은 보상과 기록을 남기지 않아요"
                : "체험은 보상과 기록을 남기지 않아요";
            string trialHeroId = hero != null ? hero.id : GameSession.TrialHeroId;
            SetButtons("영웅 보러 가기", () => GameSession.ToLobbyHero(trialHeroId), "로비", GameSession.ToLobby);
            BeginIntro(true, false);
        }

        public void ShowSurvival(Survival.SurvivalSummary s)
        {
            Open();
            titleLabel.text = s.Victory ? "생존!" : "쓰러졌다…";

            var sb = new StringBuilder();
            sb.AppendLine($"버틴 시간  {Clock(s.Seconds)}" + (s.NewBest ? "  <color=#FFD966>최고 기록!</color>" : $"  (최고 {Clock(s.BestSeconds)})"));
            sb.AppendLine($"처치  {s.Kills}");
            sb.AppendLine($"레벨  {s.Level}");
            if (!string.IsNullOrEmpty(s.TopDamage)) sb.AppendLine($"<size=85%>피해  {s.TopDamage}</size>");
            sb.AppendLine();
            sb.AppendLine("만든 조합");
            sb.AppendLine(s.Fusions != null && s.Fusions.Count > 0 ? "  " + string.Join(", ", s.Fusions) : "  없음");
            sb.AppendLine("  서바이벌은 기록을 남기지 않아요. 조합 발견은 도감에 남아요");
            AppendRewards(sb, s.RewardLines);
            bodyLabel.text = sb.ToString();
            SetButtons($"다시 도전 (하트 {GameSession.HeartCost})", Retry, "로비로", GameSession.ToLobby);
            BeginIntro(s.Victory, false);
        }

        /// <summary>그림 + 숫자만 있는 보상은 카드 줄로, 설명이 붙은 보상(별 상자 · 레벨 업 · 장비 …)은 글로</summary>
        private void AppendRewards(StringBuilder sb, List<string> lines)
        {
            List<Economy.RewardCards.Card> cards = Economy.RewardCards.Split(lines, out List<string> rest);
            ShowCards(cards);
            if (rest.Count == 0) return;
            sb.AppendLine();
            sb.AppendLine(cards.Count > 0 ? "<color=#FFD966>그리고</color>" : "<color=#FFD966>받은 것</color>");
            foreach (string line in rest) sb.AppendLine(IsBigLine(line) ? "  " + BigOpen + line + BigClose : "  " + line);
        }

        // 큰 보상 줄 (새 유물 · 펫 · 장비 · 계정 레벨업 · 별 상자 · 새 별 · 구간 해금): 금색 큰 글로, 숫자가 다 오른 뒤 하나씩 나타난다
        private const char BigOpen = '\u0001';
        private const char BigClose = '\u0002';
        private const float BigDelay = 0.08f;
        private readonly List<string> _bodyPre = new List<string>(); // 큰 줄 사이의 일반 글 (큰 줄 수 + 1개)
        private readonly List<string> _bigLines = new List<string>();
        private int _bigShown = -1;

        private static bool IsBigLine(string line) =>
            line.StartsWith("유물") || line.StartsWith("펫 획득") || line.StartsWith("장비") || line.StartsWith("별 상자") || line.StartsWith("새 별")
            || (line.Contains("→ ") && line.Contains("Lv ")) || line.Contains("구간이 열");

        /// <summary>본문 글에서 큰 줄 표시를 떼어 두고 아직 안 보이는 상태로 시작한다</summary>
        private void ParseBigLines()
        {
            _bodyPre.Clear();
            _bigLines.Clear();
            _bigShown = -1;
            string[] parts = bodyLabel.text.Split(BigOpen);
            _bodyPre.Add(parts[0]);
            for (int i = 1; i < parts.Length; i++)
            {
                int close = parts[i].IndexOf(BigClose);
                _bigLines.Add(parts[i].Substring(0, close));
                _bodyPre.Add(parts[i].Substring(close + 1));
            }
        }

        private void ShowBigLines(int count, bool sound)
        {
            if (_bigLines.Count == 0 || count == _bigShown) return;
            if (sound && count > _bigShown && _bigShown >= 0) Audio.AudioManager.Play("reward", 0.7f);
            _bigShown = count;
            var sb = new StringBuilder();
            for (int i = 0; i <= _bigLines.Count; i++)
            {
                sb.Append(_bodyPre[i]);
                if (i == _bigLines.Count) break;
                // 아직 안 보이는 줄은 투명 글자로 자리만 잡는다 (뒤 글이 밀리지 않게)
                if (i < count) sb.Append("<size=125%><color=#FFD54A><b>").Append(_bigLines[i]).Append("</b></color></size>");
                else sb.Append("<size=125%><color=#FFFFFF00>").Append(System.Text.RegularExpressions.Regex.Replace(_bigLines[i], "<[^>]+>", string.Empty)).Append("</color></size>");
            }

            bodyLabel.text = sb.ToString();
        }

        private void ShowCards(List<Economy.RewardCards.Card> cards)
        {
            foreach (GameObject card in _cards)
            {
                card.SetActive(false); // Destroy는 프레임 끝이라 그 전에 줄 배치에서 빠지게
                Destroy(card);
            }

            _cards.Clear();
            _countLabels.Clear();
            _countTargets.Clear();
            _countDone.Clear();
            bool any = cards != null && cards.Count > 0 && cardRow != null && cardTemplate != null;
            if (cardRow != null) cardRow.gameObject.SetActive(any);

            // 카드가 있으면 본문을 카드 줄 아래로 (위 끝만 내린다)
            if (cardRow != null) cardRow.anchoredPosition = _cardRowPos - new Vector2(0f, _starDrop); // 별 줄이 있으면 카드도 그만큼 아래로
            float drop = (any ? cardRowHeight : 0f) + _starDrop;
            bodyLabel.rectTransform.anchoredPosition = _bodyPos - new Vector2(0f, drop * 0.5f);
            bodyLabel.rectTransform.sizeDelta = _bodySize - new Vector2(0f, drop);
            if (!any) return;

            for (int i = 0; i < cards.Count; i++)
            {
                GameObject card = Instantiate(cardTemplate, cardRow);
                card.name = "Card_" + cards[i].Key;
                Image icon = card.transform.Find("Icon")?.GetComponent<Image>();
                if (icon != null) icon.sprite = SpriteOf(cards[i].Key);
                TMP_Text amount = card.GetComponentInChildren<TMP_Text>(true);
                if (amount != null)
                {
                    amount.text = "+0"; // 숫자는 BeginIntro 연출이 0에서 올려 준다
                    _countLabels.Add(amount);
                    _countTargets.Add(cards[i].Amount);
                    _countDone.Add(false);
                }
                UiPop pop = card.GetComponent<UiPop>();
                if (pop != null) pop.Delay = 0.15f + 0.08f * i; // 하나씩 차례로 튀어나오게
                card.SetActive(true);
                _cards.Add(card);
            }
        }

        private Sprite SpriteOf(string key)
        {
            if (rewardSprites == null) return null;
            foreach (Sprite s in rewardSprites)
            {
                if (s != null && s.name == "reward_" + key) return s;
            }

            return null;
        }

        /// <summary>무한의 균열·일일 도전 결과 (기록 저장 없음): 제목 + 본문 + 버튼 2개</summary>
        public void ShowText(string title, string body, string primary, Action onPrimary, string secondary, Action onSecondary)
        {
            Open();
            titleLabel.text = title;
            bodyLabel.text = body;
            SetButtons(primary, onPrimary ?? Retry, secondary, onSecondary ?? GameSession.ToLobby);
            BeginIntro(true, false);
        }

        // ---------------------------------------------------------------- 열리는 연출

        /// <summary>글을 다 채운 뒤 부른다: 판 올라옴 → 제목 튀어나옴 → 숫자 올라감 → 첫 클리어 띠 → 단추</summary>
        private void BeginIntro(bool victory, bool firstClear)
        {
            _victory = victory;
            _t0 = Time.unscaledTime;
            ParseBigLines();
            _lastTick = -1f;
            _lastCountShown = 0;
            _ribbonShown = false;
            _animating = true;
            _unlockAt = 0f;
            for (int i = 0; i < _starDone.Count; i++) _starDone[i] = false;
            if (_panelGroup == null) _panelGroup = GroupOf(root);
            if (_primaryGroup == null) _primaryGroup = GroupOf(primaryButton.gameObject);
            if (_secondaryGroup == null) _secondaryGroup = GroupOf(secondaryButton.gameObject);
            if (firstClear) EnsureFirstRibbon();
            if (_firstRibbon != null) _firstRibbon.gameObject.SetActive(false);
            _pendingFirstClear = firstClear;
            ApplyIntro(0f);
        }

        private bool _pendingFirstClear;

        private void Update()
        {
            if (!_animating || !root.activeSelf) return;
            float e = Time.unscaledTime - _t0;
            if (e > 0.15f && Pointer.current != null && Pointer.current.press.wasPressedThisFrame)
            {
                _t0 = Time.unscaledTime - 999f; // 건너뛰기: 바로 끝 상태
                e = 999f;
                _unlockAt = Time.unscaledTime + 0.3f; // 누른 손가락이 단추를 같이 누르지 않게
            }

            ApplyIntro(e);
        }

        private float CountStart => (_victory ? 0.45f : 0.9f) + 0.05f;

        private float CountEnd
        {
            get
            {
                float end = CountStart + (_countLabels.Count > 0 ? (_countLabels.Count - 1) * CountDelay + CountSeconds : 0f);
                int newStars = 0;
                foreach (bool n in _starNew) if (n) newStars++;
                return newStars > 0 ? Mathf.Max(end, CountStart + (newStars - 1) * StarDelay + 0.5f) : end; // 새 별 쾅 쾅도 끝난 뒤
            }
        }

        private void ApplyIntro(float e)
        {
            float slide = _victory ? 0.45f : 0.9f;
            float p = Mathf.Clamp01(e / slide);
            float ease = 1f - (1f - p) * (1f - p) * (1f - p);
            var panel = (RectTransform)root.transform;
            _panelGroup.alpha = _victory ? Mathf.Clamp01(p * 2.5f) : p; // 졌을 땐 올라오지 않고 천천히 나타난다
            panel.anchoredPosition = _victory ? new Vector2(0f, -panel.rect.height * 0.25f * (1f - ease)) : Vector2.zero;
            titleLabel.rectTransform.localScale = _victory
                ? Vector3.one * Mathf.LerpUnclamped(0.5f, 1f, UiPop.EaseOutBack(Mathf.Clamp01((e - 0.15f) / 0.5f)))
                : Vector3.one;

            // 보상 숫자 올라감
            int shown = 0;
            for (int i = 0; i < _countLabels.Count; i++)
            {
                float u = Mathf.Clamp01((e - CountStart - i * CountDelay) / CountSeconds);
                float eased = 1f - (1f - u) * (1f - u);
                int value = Mathf.RoundToInt(_countTargets[i] * eased);
                shown += value;
                _countLabels[i].text = "+" + Economy.Wallet.Format(value);
                if (u >= 1f && !_countDone[i])
                {
                    _countDone[i] = true;
                    if (e < 100f) Audio.AudioManager.Play("reward", 0.6f); // 건너뛸 땐 소리 없이
                }
            }

            if (shown != _lastCountShown && e < 100f && Time.unscaledTime - _lastTick > 0.07f)
            {
                _lastTick = Time.unscaledTime;
                Audio.AudioManager.Play("coin", 0.5f, 0.1f);
            }

            _lastCountShown = shown;

            ApplyStars(e);

            // 큰 보상 줄: 숫자가 다 오른 뒤 하나씩 (건너뛰면 한꺼번에)
            int bigCount = _bigLines.Count;
            ShowBigLines(e >= 100f ? bigCount : Mathf.Clamp(Mathf.FloorToInt((e - CountEnd) / BigDelay) + 1, 0, bigCount), e < 100f);

            // 첫 클리어 띠 (숫자가 다 오른 뒤 통)
            float ribbonAt = CountEnd + bigCount * BigDelay + 0.05f;
            if (_pendingFirstClear && _firstRibbon != null)
            {
                bool on = e >= ribbonAt;
                _firstRibbon.gameObject.SetActive(on);
                if (on)
                {
                    if (!_ribbonShown && e < 100f) Audio.AudioManager.Play("victory", 0.6f);
                    _ribbonShown = true;
                    _firstRibbon.localScale = Vector3.one * Mathf.LerpUnclamped(0.3f, 1f, UiPop.EaseOutBack(Mathf.Clamp01((e - ribbonAt) / 0.4f)));
                }
            }

            // 단추: 연출이 끝나면 나타나고 눌린다
            float buttonsAt = ribbonAt + (_pendingFirstClear ? 0.4f : 0.1f);
            float b = Mathf.Clamp01((e - buttonsAt) / 0.25f);
            bool unlocked = b >= 1f && Time.unscaledTime >= _unlockAt && !(Pointer.current != null && Pointer.current.press.isPressed && e > 100f);
            SetGroup(_primaryGroup, b, unlocked);
            SetGroup(_secondaryGroup, b, unlocked);
            if (unlocked) _animating = false;
        }

        private static void SetGroup(CanvasGroup g, float alpha, bool on)
        {
            g.alpha = alpha;
            g.interactable = on;
            g.blocksRaycasts = on;
        }

        private static CanvasGroup GroupOf(GameObject go)
        {
            CanvasGroup g = go.GetComponent<CanvasGroup>();
            return g != null ? g : go.AddComponent<CanvasGroup>(); // ??는 유니티 오브젝트에서 안전하지 않다
        }

        // ---------------------------------------------------------------- 별 줄

        private void BuildStars(StarRow[] rows)
        {
            if (_starRow != null)
            {
                _starRow.gameObject.SetActive(false); // Destroy는 프레임 끝이라 그 전에 꺼 둔다
                Destroy(_starRow.gameObject);
                _starRow = null;
            }

            _starImages.Clear();
            _starSparkles.Clear();
            _starNew.Clear();
            _starOwned.Clear();
            _starDone.Clear();
            _starOrder.Clear();
            _starDrop = 0f;
            if (rows == null || rows.Length == 0 || starSprite == null) return;

            _starDrop = StarDrop;
            _starRow = NewRect("StarRow", root.transform, new Vector2(0f, 445f), new Vector2(900f, 230f));
            int order = 0;
            for (int i = 0; i < rows.Length; i++)
            {
                RectTransform column = NewRect("Star" + i, _starRow, new Vector2((i - 1) * 300f, 0f), new Vector2(290f, 230f));
                Image star = NewImage("Icon", column, new Vector2(0f, 45f), new Vector2(140f, 140f), starSprite);
                star.preserveAspect = true;
                Image sparkle = NewImage("Sparkle", column, new Vector2(0f, 45f), new Vector2(240f, 240f), sparkleSprite != null ? sparkleSprite : starSprite);
                sparkle.color = new Color(1f, 0.9f, 0.5f, 0f);

                TMP_Text label = Instantiate(bodyLabel, column);
                label.gameObject.name = "Label";
                label.text = rows[i].Label;
                label.enableAutoSizing = false;
                label.fontSize = 30f;
                label.alignment = TextAlignmentOptions.Top;
                label.textWrappingMode = TextWrappingModes.Normal;
                label.color = rows[i].Owned ? new Color(1f, 0.93f, 0.7f) : new Color(0.6f, 0.62f, 0.68f);
                RectTransform lr = label.rectTransform;
                lr.anchorMin = lr.anchorMax = lr.pivot = new Vector2(0.5f, 0.5f);
                lr.anchoredPosition = new Vector2(0f, -60f);
                lr.sizeDelta = new Vector2(290f, 80f);

                _starImages.Add(star);
                _starSparkles.Add(sparkle);
                _starNew.Add(rows[i].New);
                _starOwned.Add(rows[i].Owned);
                _starDone.Add(false);
                _starOrder.Add(rows[i].New ? order++ : -1);
            }
        }

        private void ApplyStars(float e)
        {
            for (int i = 0; i < _starImages.Count; i++)
            {
                Image star = _starImages[i];
                if (!_starNew[i])
                {
                    star.color = _starOwned[i] ? StarGold : StarGray; // 전에 딴 별은 처음부터 금색, 못 딴 별은 회색
                    star.rectTransform.localScale = Vector3.one;
                    continue;
                }

                float u = (e - (CountStart + _starOrder[i] * StarDelay)) / 0.45f;
                if (u < 0f)
                {
                    star.color = StarGray;
                    star.rectTransform.localScale = Vector3.one;
                    continue;
                }

                float k = Mathf.Clamp01(u);
                star.color = Color.Lerp(StarGray, StarGold, Mathf.Clamp01(k * 4f));
                star.rectTransform.localScale = Vector3.one * Mathf.LerpUnclamped(1.9f, 1f, UiPop.EaseOutBack(k)); // 크게 → 쾅
                Image sparkle = _starSparkles[i];
                sparkle.color = new Color(1f, 0.9f, 0.5f, (1f - k) * (k > 0f ? 1f : 0f));
                sparkle.rectTransform.localScale = Vector3.one * (0.5f + 1.3f * k);
                sparkle.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 90f * k);
                if (!_starDone[i])
                {
                    _starDone[i] = true;
                    if (e < 100f) Audio.AudioManager.Play("reward", 0.8f);
                }
            }
        }

        private static RectTransform NewRect(string name, Transform parent, Vector2 position, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = position;
            rt.sizeDelta = size;
            return rt;
        }

        private static Image NewImage(string name, Transform parent, Vector2 position, Vector2 size, Sprite sprite)
        {
            RectTransform rt = NewRect(name, parent, position, size);
            var image = rt.gameObject.AddComponent<Image>();
            image.sprite = sprite;
            image.raycastTarget = false;
            return image;
        }

        /// <summary>"첫 클리어!" 띠 (제목 위, 살짝 기울임). 처음 필요할 때 코드로 만든다 (제목 글꼴을 빌린다)</summary>
        private void EnsureFirstRibbon()
        {
            if (_firstRibbon != null) return;
            var go = new GameObject("FirstClearRibbon", typeof(RectTransform), typeof(Image));
            _firstRibbon = (RectTransform)go.transform;
            _firstRibbon.SetParent(root.transform, false);
            _firstRibbon.anchorMin = _firstRibbon.anchorMax = new Vector2(0.5f, 0.5f);
            _firstRibbon.pivot = new Vector2(0.5f, 0.5f);
            _firstRibbon.anchoredPosition = new Vector2(0f, 790f);
            _firstRibbon.sizeDelta = new Vector2(440f, 96f);
            _firstRibbon.localRotation = Quaternion.Euler(0f, 0f, -4f);
            var image = go.GetComponent<Image>();
            image.color = new Color(0.72f, 0.12f, 0.2f, 1f);
            image.raycastTarget = false;
            TMP_Text text = Instantiate(titleLabel, _firstRibbon);
            text.gameObject.name = "Text";
            text.text = "첫 클리어!";
            text.color = new Color(1f, 0.95f, 0.7f);
            text.enableAutoSizing = false;
            text.fontSize = 60f;
            text.rectTransform.anchorMin = Vector2.zero;
            text.rectTransform.anchorMax = Vector2.one;
            text.rectTransform.offsetMin = text.rectTransform.offsetMax = Vector2.zero;
            text.rectTransform.localScale = Vector3.one;
        }

        private void Retry()
        {
            if (GameSession.TryRetry()) return;
            primaryLabel.text = "하트가 부족해요";
            primaryButton.interactable = false;
        }

        private void Open()
        {
            GamePause.Pause(this);
            HypeFx.HideDim(); // 패배 회색 막은 걷는다 (이 판이 따로 어둡다)
            root.SetActive(true);
            BuildStars(null);
            ShowCards(null); // 체험 · 균열 글 결과는 카드 없이
        }

        private void SetButtons(string primary, Action onPrimary, string secondary, Action onSecondary)
        {
            primaryLabel.text = primary;
            _primary = onPrimary;
            secondaryButton.gameObject.SetActive(onSecondary != null);
            if (onSecondary != null) secondaryLabel.text = secondary;
            _secondary = onSecondary;
        }

        private static string Clock(float seconds)
        {
            int m = Mathf.FloorToInt(seconds / 60f);
            int sec = Mathf.FloorToInt(seconds % 60f);
            return $"{m:00}:{sec:00}";
        }

        public static string FirstBossClearText(BossSummary summary)
        {
            if (summary.IsGodTrial)
                return $"첫 시련을 통과했어요. {KoreanText.Josa(summary.BossName, "이", "가")} 기록을 인정했어요.";
            if (summary.UnlockedChapter == 6 && summary.BossName == "망각의 왕")
                return "처음 격파! 신의 시련이 열렸어요";
            return $"처음 격파! {summary.UnlockedChapter}구간이 열렸어요";
        }
    }
}
