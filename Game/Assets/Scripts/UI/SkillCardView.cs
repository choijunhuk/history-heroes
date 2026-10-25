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

        private const float BoxBottomPlain = 58f;
        private const float BoxBottomWithHint = 112f;

        public static readonly Color Common = new Color(0.42f, 0.45f, 0.5f);
        public static readonly Color Rare = new Color(0.2f, 0.45f, 0.9f);
        public static readonly Color Legendary = new Color(0.9f, 0.68f, 0.12f);

        public static readonly Color ActiveBand = new Color(0.93f, 0.42f, 0.16f);
        public static readonly Color PassiveBand = new Color(0.16f, 0.66f, 0.42f);
        public static readonly Color FusionBand = new Color(0.45f, 0.24f, 0.7f);
        public static readonly Color BasicBand = new Color(0.25f, 0.27f, 0.3f);

        private bool _rainbow;
        private Action _onClick;

        private void Awake()
        {
            button.onClick.AddListener(() => _onClick?.Invoke());
        }

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
                    break;

                case CardType.Fallback:
                    kindLabel.text = "기본";
                    kindBand.color = BasicBand;
                    nameLabel.text = "체력 회복";
                    levelLabel.text = string.Empty;
                    descriptionLabel.text = "최대체력의 20%를 회복한다.";
                    background.color = Common;
                    break;

                default:
                    SkillData s = card.Skill;
                    bool passive = s.Kind == SkillKind.Passive;
                    kindLabel.text = passive ? "패시브" : "액티브";
                    kindBand.color = passive ? PassiveBand : ActiveBand;
                    nameLabel.text = s.displayName;
                    levelLabel.text = card.Type == CardType.NewSkill ? "NEW" : $"Lv{card.NextLevel - 1} → {card.NextLevel}";
                    descriptionLabel.text = s.DescriptionFor(skills.HeroClass);
                    background.color = RarityColor(s.rarity);
                    hint = skills.CardHint(card);
                    if (legendaryWarning && s.rarity == Rarity.Legendary)
                    {
                        const string star = "<color=#FF7A6B>★3 도전 중: 고르면 이번 판 ★3 실패</color>";
                        hint = string.IsNullOrEmpty(hint) ? star : hint + "\n" + star;
                    }

                    break;
            }

            SetHint(hint);
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
            if (!_rainbow) return;
            // 게임이 멈춘 동안에도 돌아야 하므로 unscaledTime
            background.color = Color.HSVToRGB(Mathf.Repeat(Time.unscaledTime * 0.25f, 1f), 0.55f, 0.85f);
        }
    }
}
