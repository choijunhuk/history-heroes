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

        private readonly IRewardedAd _ads = new InstantRewardedAd();
        private List<SkillCard> _current = new List<SkillCard>();
        private int _pending;
        private int _rerollsLeft;
        private bool _waitingForAd;

        /// <summary>지금 층(방 번호). 전설 확률이 층에 따라 바뀐다 (GameDesign 6-5). 던전이 넣어준다.</summary>
        public int Floor { get; set; } = 1;

        public bool IsOpen => root.activeSelf;

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

        private void Open()
        {
            GamePause.Pause(this);
            root.SetActive(true);
            _current = skills.CreateOffer(Floor);
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
                cards[i].Show(card, skills, () => Choose(card));
            }

            bool hasFusion = _current.Exists(c => c.Type == CardType.Fusion);
            rerollLabel.text = $"리롤 (광고) {_rerollsLeft}/{rerollsPerRun}";
            rerollButton.interactable = _rerollsLeft > 0 && !_waitingForAd;
            hideFusionButton.gameObject.SetActive(hasFusion);

            // 숨기기 버튼이 없으면 리롤 버튼을 가운데로
            var rerollRect = (RectTransform)rerollButton.transform;
            Vector2 position = rerollRect.anchoredPosition;
            position.x = hasFusion ? -Mathf.Abs(((RectTransform)hideFusionButton.transform).anchoredPosition.x) : 0f;
            rerollRect.anchoredPosition = position;
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
            _pending--;

            if (_pending > 0)
            {
                _current = skills.CreateOffer(Floor);
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

            _ads.Show("levelup_reroll", success =>
            {
                _waitingForAd = false;
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
