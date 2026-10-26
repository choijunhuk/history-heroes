using System.Collections.Generic;
using HistoryHeroes.Common;
using HistoryHeroes.Progress;
using HistoryHeroes.Skills;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace HistoryHeroes.UI
{
    /// <summary>
    /// 레벨업 카드 화면 (GameDesign 6-4, 6-7 · UX.md 3장).
    /// 뜨는 동안 게임이 멈춘다. 한 번에 여러 레벨이 오르면 한 장씩 차례로 보여준다.
    /// 리롤: 광고 보고 판당 2회, 조합 카드는 고정. 조합 카드는 "이번 판 숨기기"로 치울 수 있다.
    /// </summary>
    public sealed class LevelUpScreen : MonoBehaviour
    {
        [SerializeField] private ExperienceSystem experience;
        [SerializeField] private SkillManager skills;

        [Header("화면")]
        [SerializeField] private GameObject root;
        [SerializeField] private SkillCardView[] cards = new SkillCardView[3];
        [SerializeField] private Button rerollButton;
        [SerializeField] private TMP_Text rerollLabel;
        [SerializeField] private Button hideFusionButton;

        [Header("규칙")]
        [SerializeField, Min(0)] private int rerollsPerRun = 2;

        private bool _lastAdFailed;
        private List<SkillCard> _current = new List<SkillCard>();
        private int _pending;
        private int _rerollsLeft;
        private bool _waitingForAd;

        /// <summary>지금 층(방 번호). 전설 확률이 층에 따라 바뀐다 (GameDesign 6-5). 던전이 넣어준다.</summary>
        public int Floor { get; set; } = 1;

        public bool IsOpen => root.activeSelf;

        /// <summary>지금 카드에 조합 카드가 있나 (튜토리얼 안내)</summary>
        public bool ShowsFusion => IsOpen && _current.Exists(c => c.Type == CardType.Fusion);

        /// <summary>판 이어하기: 남은 리롤 (껐다 켜서 리롤을 다시 채우지 못하게)</summary>
        public int RerollsLeft => _rerollsLeft;

        public void RestoreRerolls(int left)
        {
            if (left >= 0) _rerollsLeft = Mathf.Min(left, rerollsPerRun);
        }

        private void Awake()
        {
            _rerollsLeft = rerollsPerRun;
            root.SetActive(false);
            rerollButton.onClick.AddListener(Reroll);
            hideFusionButton.onClick.AddListener(HideFusion);
        }

        private void OnEnable() => experience.LeveledUp += OnLeveledUp;
        private void OnDisable() => experience.LeveledUp -= OnLeveledUp;

        private void OnLeveledUp(int level)
        {
            _pending++;
            if (!IsOpen) Open();
        }

        private bool _legendaryWarning;

        /// <summary>창이 뜬 뒤 이 시간 동안은 못 누른다 (조이스틱을 쥔 손가락이 카드를 잘못 누르지 않게, 조사 A2)</summary>
        [SerializeField] private float inputGuardSeconds = 0.4f;
        private CanvasGroup _guard;
        private float _guardUntil;
        private bool _waitForRelease;

        private void Update()
        {
            if (!IsOpen || _guard == null) return;
            if (_waitForRelease && !PointerHeld()) _waitForRelease = false;
            bool ready = Time.unscaledTime >= _guardUntil && !_waitForRelease;
            if (_guard.interactable == ready) return;
            _guard.interactable = ready;
            _guard.alpha = ready ? 1f : 0.6f;
        }

        private static bool PointerHeld()
        {
            UnityEngine.InputSystem.Pointer pointer = UnityEngine.InputSystem.Pointer.current;
            return pointer != null && pointer.press.isPressed;
        }

        /// <summary>새 카드가 뜰 때마다 (연달아 오른 레벨업 포함) 다시 잠깐 막는다</summary>
        private void GuardInput()
        {
            if (_guard == null)
            {
                _guard = root.GetComponent<CanvasGroup>();
                if (_guard == null) _guard = root.AddComponent<CanvasGroup>(); // ??는 유니티 오브젝트에서 안전하지 않다
            }
            _guardUntil = Time.unscaledTime + inputGuardSeconds;
            _waitForRelease = PointerHeld();
            _guard.interactable = false;
            _guard.alpha = 0.6f;
        }

        private void Open()
        {
            // 던전 ★3 "전설 없이 클리어"를 아직 안 땄고 이번 판에 전설을 안 골랐으면 경고한다
            _legendaryWarning = GameSession.Mode == BattleMode.Dungeon && !skills.ChoseLegendary
                && !Economy.Stars.Has(Records.SaveSystem.Data, GameSession.Chapter, Economy.Stars.Dungeon, 3);
            GamePause.Pause(this);
            root.SetActive(true);
            _current = skills.CreateOffer(Floor);
            GuardInput();
            Render();
        }

        private void Render()
        {
            for (int i = 0; i < cards.Length; i++)
            {
                bool visible = i < _current.Count;
                cards[i].gameObject.SetActive(visible);
                if (!visible) continue;

                SkillCard card = _current[i];
                cards[i].Show(card, skills, () => Choose(card), _legendaryWarning);
            }

            bool hasFusion = _current.Exists(c => c.Type == CardType.Fusion);
            // 보상을 구체적으로 쓴다 (조사 A2). 다 쓰면 버튼을 숨긴다
            rerollLabel.text = _waitingForAd ? "광고 보는 중…"
                : _lastAdFailed ? $"광고를 못 불러왔어요 · 다시 (남은 {_rerollsLeft})"
                : $"광고 보고 다시 뽑기 (남은 {_rerollsLeft})";
            rerollButton.gameObject.SetActive(_rerollsLeft > 0 || _waitingForAd);
            rerollButton.interactable = _rerollsLeft > 0 && !_waitingForAd;
            hideFusionButton.gameObject.SetActive(hasFusion);

            // 숨기기 버튼이 없으면 리롤 버튼을 가운데로
            var rerollRect = (RectTransform)rerollButton.transform;
            Vector2 position = rerollRect.anchoredPosition;
            position.x = hasFusion ? -Mathf.Abs(((RectTransform)hideFusionButton.transform).anchoredPosition.x) : 0f;
            rerollRect.anchoredPosition = position;
        }

        /// <summary>개발·테스트용: 정한 카드로 창을 연다 (카드 힌트·경고 화면 점검).</summary>
        public void DevShow(List<SkillCard> cardsToShow)
        {
            _legendaryWarning = false;
            GamePause.Pause(this);
            root.SetActive(true);
            _current = cardsToShow;
            GuardInput();
            Render();
        }

        /// <summary>개발·테스트용: 카드를 고르지 않고 창을 닫는다 (남은 레벨업도 버린다).</summary>
        public void DevCancel()
        {
            _pending = 0;
            if (!IsOpen) return;
            root.SetActive(false);
            GamePause.Resume(this);
        }

        /// <summary>index번째 카드 고르기 (개발용 단축키, 테스트용)</summary>
        public void ChooseIndex(int index)
        {
            if (IsOpen && index >= 0 && index < _current.Count) Choose(_current[index]);
        }

        private void Choose(SkillCard card)
        {
            if (_waitingForAd) return;
            skills.Choose(card);
            _legendaryWarning &= !skills.ChoseLegendary; // 전설을 골랐으면 이번 판 ★3은 이미 실패
            Records.SaveSystem.SaveIfDirty(); // 스킬 선택 확정 = 안전한 저장 시점 (조합 도감 발견, 12-2)
            _pending--;

            if (_pending > 0)
            {
                _current = skills.CreateOffer(Floor);
                GuardInput();
                Render();
                return;
            }

            root.SetActive(false);
            GamePause.Resume(this);
        }

        private void Reroll()
        {
            if (_rerollsLeft <= 0 || _waitingForAd) return;
            _waitingForAd = true;
            Render();

            Ads.Current.Show("levelup_reroll", success =>
            {
                _waitingForAd = false;
                _lastAdFailed = !success;
                if (success) // 실패·취소면 횟수를 쓰지 않는다
                {
                    _rerollsLeft--;
                    _current = skills.Reroll(_current, Floor);
                }

                Render();
            });
        }

        private void HideFusion()
        {
            if (_waitingForAd) return;
            _current = skills.HideFusion(_current, Floor);
            Render();
        }

        private void OnDestroy()
        {
            GamePause.Resume(this);
        }
    }
}
