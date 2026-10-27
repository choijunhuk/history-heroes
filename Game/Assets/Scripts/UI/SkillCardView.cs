using System;
using HistoryHeroes.Skills;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace HistoryHeroes.UI
{
    /// <summary>
    /// 레벨업 카드 한 장. (UX.md 3장)
    /// - 카드 바탕색 = 등급 (일반 회색, 희귀 파랑, 전설 금색, 조합 무지개)
    /// - 맨 위 띠 색 = 종류 (액티브 주황, 패시브 초록, 조합 보라, 기본 회색) — 글자를 안 읽어도 구분된다
    /// </summary>
    public sealed class SkillCardView : MonoBehaviour
    {
        [SerializeField] private Button button;
        [SerializeField] private Image background;
        [SerializeField] private Image kindBand;
        [SerializeField] private TMP_Text kindLabel;
        [SerializeField] private TMP_Text nameLabel;
        [SerializeField] private TMP_Text levelLabel;
        [SerializeField] private TMP_Text descriptionLabel;
        [Tooltip("카드 아래 힌트 줄: 조합 진행(A3) · 막히는 조합 경고(A4) · ★3 경고")]
        [SerializeField] private TMP_Text materialLabel;
        [Tooltip("힌트 글자 뒤 어두운 판 (조합 카드의 무지개 바탕 위에서도 읽히게)")]
        [SerializeField] private GameObject hintBox;
        [Tooltip("힌트가 있으면 설명 칸을 위로 줄여 겹치지 않게 한다")]
        [SerializeField] private RectTransform descriptionBox;

        [Header("그래픽 평가 3번: 아이콘 · 등급 테두리 · 뒤 빛 · 튀어나오기")]
        [SerializeField] private Image frame;
        [SerializeField] private Image glow;
        [SerializeField] private Image iconSlot;
        [SerializeField] private Image icon;
        [SerializeField] private GameObject newBadge;
        [SerializeField] private UiPop pop;
        [Tooltip("카드 순서 (튀어나오는 시간을 조금씩 늦춘다)")]
        [SerializeField] private int order;
        [Header("아이콘이 없는 카드 (균열 강화 · 기본 회복)")]
        [SerializeField] private Sprite attackIcon;
        [SerializeField] private Sprite hpIcon;
        [SerializeField] private Sprite speedIcon;
        [SerializeField] private Sprite healIcon;

        private const float BoxBottomPlain = 58f;
        private const float BoxBottomWithHint = 112f;

        // 등급 색 = 테두리·아이콘 칸·뒤 빛 (카드 바탕은 어두운 남색으로 같다)
        public static readonly Color Common = new Color(0.66f, 0.72f, 0.82f);
        public static readonly Color Rare = new Color(0.32f, 0.62f, 1f);
        public static readonly Color Legendary = new Color(1f, 0.78f, 0.24f);
        public static readonly Color CardBase = new Color(0.2f, 0.25f, 0.4f);

        public static readonly Color ActiveBand = new Color(1f, 0.5f, 0.18f);
        public static readonly Color PassiveBand = new Color(0.22f, 0.78f, 0.45f);
        public static readonly Color FusionBand = new Color(0.62f, 0.34f, 0.95f);
        public static readonly Color BasicBand = new Color(0.45f, 0.5f, 0.6f);

        private bool _rainbow;
        private float _glowAlpha;
        private Action _onClick;

        private void Awake()
        {
            button.onClick.AddListener(() => _onClick?.Invoke());
        }

        /// <summary>등급에 맞춰 테두리·아이콘 칸·뒤 빛 색을 정한다 (뒤 빛은 희귀부터)</summary>
        private void Paint(Color rarity, float glowAlpha, Sprite iconSprite)
        {
            if (background != null) background.color = CardBase;
            if (frame != null) frame.color = rarity;
            if (iconSlot != null) iconSlot.color = Color.Lerp(rarity, CardBase, 0.35f);
            _glowAlpha = glowAlpha;
            if (glow != null)
            {
                glow.gameObject.SetActive(glowAlpha > 0f);
                glow.color = new Color(rarity.r, rarity.g, rarity.b, glowAlpha);
            }

            if (icon != null)
            {
                icon.sprite = iconSprite;
                icon.enabled = iconSprite != null;
            }
        }

        private static float GlowFor(Rarity rarity) => rarity == Rarity.Legendary ? 0.75f : rarity == Rarity.Rare ? 0.45f : 0f;

        /// <param name="legendaryWarning">던전 ★3(전설 없이 클리어) 도전 중이면 전설 카드에 경고 (Modes 3-2)</param>
        public void Show(SkillCard card, SkillManager skills, Action onClick, bool legendaryWarning = false)
        {
            _onClick = onClick;
            _rainbow = card.Type == CardType.Fusion;
            string hint = null;

            switch (card.Type)
            {
                case CardType.Fusion:
                    FusionRecipe r = card.Recipe;
                    kindLabel.text = r.type == FusionType.Evolution ? "조합 · 진화" : "조합 · 합체";
                    kindBand.color = FusionBand;
                    nameLabel.text = r.result.displayName;
                    levelLabel.text = "NEW";
                    descriptionLabel.text = $"{r.materialA.displayName} + {r.materialB.displayName}\n\n{r.result.description}";
                    hint = skills.BlockedWarning(r);
                    Paint(FusionBand, 0.8f, r.result.icon);
                    break;

                case CardType.RiftBoost:
                    kindLabel.text = "균열 강화";
                    kindBand.color = BasicBand;
                    nameLabel.text = card.BoostStat == Stats.StatType.Attack ? "공격력" : card.BoostStat == Stats.StatType.MaxHp ? "최대체력" : "공격 속도";
                    levelLabel.text = $"+{card.BoostValue * 100f:0}%";
                    descriptionLabel.text = "스킬을 다 키웠다!\n이번 판 동안 계속 쌓인다";
                    Paint(Common, 0f, card.BoostStat == Stats.StatType.Attack ? attackIcon : card.BoostStat == Stats.StatType.MaxHp ? hpIcon : speedIcon);
                    break;

                case CardType.Fallback:
                    kindLabel.text = "기본";
                    kindBand.color = BasicBand;
                    nameLabel.text = "체력 회복";
                    levelLabel.text = string.Empty;
                    descriptionLabel.text = "최대체력의 20%를 회복한다.";
                    Paint(Common, 0f, healIcon);
                    break;

                default:
                    SkillData s = card.Skill;
                    bool passive = s.Kind == SkillKind.Passive;
                    kindLabel.text = (passive ? "패시브" : "액티브") + (card.RuneBoosted ? " · <color=#E8D27A>룬</color>" : string.Empty); // 룬이 일하고 있다는 표시 (Runes 8장)
                    kindBand.color = passive ? PassiveBand : ActiveBand;
                    nameLabel.text = s.displayName;
                    levelLabel.text = card.Type == CardType.NewSkill ? "NEW" : $"Lv{card.NextLevel - 1} → {card.NextLevel}";
                    descriptionLabel.text = s.DescriptionFor(skills.HeroClass);
                    Paint(RarityColor(s.rarity), GlowFor(s.rarity), s.icon);
                    hint = skills.CardHint(card);
                    if (legendaryWarning && s.rarity == Rarity.Legendary)
                    {
                        const string star = "<color=#FF7A6B>★3 도전 중: 고르면 이번 판 ★3 실패</color>";
                        hint = string.IsNullOrEmpty(hint) ? star : hint + "\n" + star;
                    }

                    break;
            }

            SetHint(hint);
            if (newBadge != null)
            {
                // 새 스킬은 모서리 NEW 딱지로 보여 주고, 레벨 줄은 비운다 (같은 글이 두 번 보이지 않게)
                bool isNew = levelLabel.text == "NEW";
                newBadge.SetActive(isNew);
                if (isNew) levelLabel.text = string.Empty;
            }
            if (pop != null)
            {
                pop.Delay = order * 0.07f; // 한 장씩 차례로
                pop.Play();
            }
        }

        private void SetHint(string hint)
        {
            bool has = !string.IsNullOrEmpty(hint);
            materialLabel.gameObject.SetActive(has);
            if (hintBox != null) hintBox.SetActive(has);
            materialLabel.text = has ? hint : string.Empty;
            if (descriptionBox != null) descriptionBox.offsetMin = new Vector2(descriptionBox.offsetMin.x, has ? BoxBottomWithHint : BoxBottomPlain);
        }

        public static Color RarityColor(Rarity rarity)
        {
            switch (rarity)
            {
                case Rarity.Rare: return Rare;
                case Rarity.Legendary: return Legendary;
                default: return Common;
            }
        }

        private void Update()
        {
            // 게임이 멈춘 동안에도 돌아야 하므로 unscaledTime
            float now = Time.unscaledTime;
            if (glow != null && _glowAlpha > 0f)
            {
                Color g = glow.color;
                g.a = _glowAlpha * (0.75f + 0.25f * Mathf.Sin(now * 3f + order)); // 숨 쉬는 뒤 빛
                glow.color = g;
            }

            if (!_rainbow) return;
            Color rainbow = Color.HSVToRGB(Mathf.Repeat(now * 0.25f, 1f), 0.6f, 1f); // 조합 카드는 무지개 테두리
            if (frame != null) frame.color = rainbow;
            if (glow != null) glow.color = new Color(rainbow.r, rainbow.g, rainbow.b, glow.color.a);
        }
    }
}
