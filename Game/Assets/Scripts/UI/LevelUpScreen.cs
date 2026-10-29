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
        [SerializeField] private Button banButton;
        [SerializeField] private TMP_Text banLabel;
        [SerializeField] private TMP_Text noticeLabel;

        [Header("규칙")]
        [SerializeField, Min(0)] private int rerollsPerRun = 2;

        private bool _lastAdFailed;
        private List<SkillCard> _current = new List<SkillCard>();
        private int _pending;
        private int _rerollsLeft;
        private bool _waitingForAd;
        private bool _banMode;
        private float _noticeUntil;
        private const string RerollNote = "리롤은 다시 뽑을 기회일 뿐, 더 좋은 카드를 보장하지 않아요";

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
            if (banButton != null) banButton.onClick.AddListener(ToggleBan);
        }

        private void OnEnable() => experience.LeveledUp += OnLeveledUp;
        private void OnDisable() => experience.LeveledUp -= OnLeveledUp;

        private void OnLeveledUp(int level)
        {
            if (!IsOpen) Audio.AudioManager.Play("levelup");
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
            if (!IsOpen) return;
            if (_noticeUntil > 0f && Time.unscaledTime >= _noticeUntil)
            {
                _noticeUntil = 0f;
                if (noticeLabel != null) noticeLabel.text = RerollNote;
            }
            if (_guard == null) return;
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
            _banMode = false;
            // 던전 ★3 "전설 없이 클리어"를 아직 안 땄고 이번 판에 전설을 안 골랐으면 경고한다
            _legendaryWarning = GameSession.Mode == BattleMode.Dungeon && !GameSession.Practice && !skills.ChoseLegendary
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
                cards[i].Show(card, skills, () => Choose(card), _legendaryWarning, _banMode);
            }

            bool hasFusion = _current.Exists(c => c.Type == CardType.Fusion);
            // 보상을 구체적으로 쓴다 (조사 A2). 다 쓰면 버튼을 숨긴다
            // 첫 번째는 무료, 두 번째부터 광고 (전체 검토 10번: 한 판에 광고 6번까지 가지 않게)
            rerollLabel.text = _waitingForAd ? "광고 보는 중…"
                : _lastAdFailed ? $"광고를 못 불러왔어요 · 다시 (남은 {_rerollsLeft})"
                : NextRerollFree ? $"무료로 다시 뽑기 (남은 {_rerollsLeft})"
                : $"광고 보고 다시 뽑기 (남은 {_rerollsLeft})";
            rerollButton.gameObject.SetActive(_rerollsLeft > 0 || _waitingForAd);
            rerollButton.interactable = _rerollsLeft > 0 && !_waitingForAd;
            hideFusionButton.gameObject.SetActive(hasFusion);
            bool canShowBan = !GameSession.Tutorial && skills.TutorialCards == null;
            if (banButton != null)
            {
                banButton.gameObject.SetActive(canShowBan);
                banButton.interactable = skills.Loadout.BansLeft > 0 && !_waitingForAd;
            }
            if (banLabel != null) banLabel.text = _banMode ? $"빼기 취소 (남은 {skills.Loadout.BansLeft})" : $"빼기 (남은 {skills.Loadout.BansLeft})";
            // 빼기는 리롤 옆에, 조합 숨기기는 그 아래에 둔다.
            var rerollRect = (RectTransform)rerollButton.transform;
            Vector2 position = rerollRect.anchoredPosition;
            position.x = canShowBan && banButton != null ? -220f : 0f;
            rerollRect.anchoredPosition = position;
            if (noticeLabel != null && _noticeUntil <= 0f) noticeLabel.text = RerollNote;
        }

        /// <summary>개발·테스트용: 정한 카드로 창을 연다 (카드 힌트·경고 화면 점검).</summary>
        public void DevShow(List<SkillCard> cardsToShow)
        {
            _banMode = false;
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
            _banMode = false;
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
            if (_banMode)
            {
                if (!skills.TryBanCard(_current, _current.IndexOf(card), Floor))
                {
                    ShowNotice(card.Type == CardType.Fusion ? "조합은 이번 판 숨기기를 써 주세요" : "이 카드는 뺄 수 없어요");
                    return;
                }

                PersistBans();
                _banMode = skills.Loadout.BansLeft > 0;
                GuardInput();
                Render();
                return;
            }
            // 재료 카드를 뺀 직후에도 옆의 조합 카드는 그대로 둔다. 숨기기로 바꿀 수 있다.
            if (card.Type == CardType.Fusion && card.Recipe != null
                && (skills.Loadout.IsBanned(card.Recipe.materialA) || skills.Loadout.IsBanned(card.Recipe.materialB)))
            {
                ShowNotice("뺀 재료가 있는 조합이에요. 이번 판 숨기기를 써 주세요");
                return;
            }
            Audio.AudioManager.Play("card");
            skills.Choose(card);
            // 뽕맛: 진화·합체·전설은 화면 가운데 큰 글 + 소리 (창이 닫히면 보인다)
            if (card.Type == CardType.Fusion && card.Recipe != null)
            {
                Audio.AudioManager.Play("fusion");
                HypeFx.Banner(card.Recipe.type == FusionType.Evolution ? "진화!" : "합체!", new Color(0.85f, 0.55f, 1f));
            }
            else if (card.Skill != null && card.Skill.rarity == Rarity.Legendary)
            {
                Audio.AudioManager.Play("legendary");
                HypeFx.Banner("전설!", new Color(1f, 0.82f, 0.3f));
            }
            _legendaryWarning &= !skills.ChoseLegendary; // 전설을 골랐으면 이번 판 ★3은 이미 실패
            if (!GameSession.Practice) Records.SaveSystem.SaveIfDirty(); // 스킬 선택 확정 = 안전한 저장 시점 (조합 도감 발견, 12-2)
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

        /// <summary>이번 판 첫 다시 뽑기는 광고 없이</summary>
        public bool NextRerollFree => _rerollsLeft >= rerollsPerRun;

        private void Reroll()
        {
            if (_rerollsLeft <= 0 || _waitingForAd) return;
            _banMode = false;
            if (NextRerollFree)
            {
                _rerollsLeft--;
                _lastAdFailed = false;
                _current = skills.Reroll(_current, Floor);
                Render();
                return;
            }

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

        private void ToggleBan()
        {
            if (_waitingForAd || skills.Loadout.BansLeft <= 0 || GameSession.Tutorial || skills.TutorialCards != null) return;
            _banMode = !_banMode;
            Render();
        }

        private void ShowNotice(string text)
        {
            if (noticeLabel == null) return;
            noticeLabel.text = text;
            _noticeUntil = Time.unscaledTime + 2.5f;
        }

        private void PersistBans()
        {
            if (GameSession.Practice || GameSession.Tutorial) return;
            Records.RunSnapshot snapshot = Records.SaveSystem.Data.run;
            bool changed = false;
            if (snapshot != null && snapshot.valid && snapshot.mode == (int)GameSession.Mode && snapshot.heroId == skills.HeroId)
            {
                skills.CaptureBans(snapshot);
                changed = true;
            }
            Records.RiftCheckpoint checkpoint = Records.SaveSystem.Data.rift.checkpoint;
            if (GameSession.Mode == BattleMode.Rift && checkpoint.valid && checkpoint.seed == RunRandom.Seed && checkpoint.heroId == skills.HeroId)
            {
                checkpoint.bannedSkillIds = new List<string>(skills.Loadout.BannedSkillIds);
                checkpoint.bansLeft = skills.Loadout.BansLeft;
                changed = true;
            }
            if (changed && !Records.SaveSystem.Save()) ShowNotice("빼기는 적용됐지만 저장하지 못했어요. 저장 공간을 확인해 주세요");
        }

        private void OnDestroy()
        {
            GamePause.Resume(this);
        }
    }
}
