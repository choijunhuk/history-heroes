using System;
using HistoryHeroes.Skills;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace HistoryHeroes.UI
{
    /// <summary>
    /// 레벨업 카드 한 장. 카드 색 = 등급 (일반 회색, 희귀 파랑, 전설 금색, 조합 무지개). (UX.md 3장)
    /// </summary>
    public sealed class SkillCardView : MonoBehaviour
    {
        [SerializeField] private Button button;
        [SerializeField] private Image background;
        [SerializeField] private TMP_Text kindLabel;
        [SerializeField] private TMP_Text nameLabel;
        [SerializeField] private TMP_Text levelLabel;
        [SerializeField] private TMP_Text descriptionLabel;
        [SerializeField] private TMP_Text materialLabel;

        public static readonly Color Common = new Color(0.42f, 0.45f, 0.5f);
        public static readonly Color Rare = new Color(0.2f, 0.45f, 0.9f);
        public static readonly Color Legendary = new Color(0.9f, 0.68f, 0.12f);

        private bool _rainbow;
        private Action _onClick;

        private void Awake()
        {
            button.onClick.AddListener(() => _onClick?.Invoke());
        }

        public void Show(SkillCard card, SkillManager skills, Action onClick)
        {
            _onClick = onClick;
            _rainbow = card.Type == CardType.Fusion;
            materialLabel.gameObject.SetActive(false);

            switch (card.Type)
            {
                case CardType.Fusion:
                    FusionRecipe r = card.Recipe;
                    kindLabel.text = r.type == FusionType.Evolution ? "조합 · 진화" : "조합 · 합체";
                    nameLabel.text = r.result.displayName;
                    levelLabel.text = "NEW";
                    descriptionLabel.text = $"{r.materialA.displayName} + {r.materialB.displayName}\n\n{r.result.description}";
                    break;

                case CardType.Fallback:
                    kindLabel.text = "기본";
                    nameLabel.text = "체력 회복";
                    levelLabel.text = string.Empty;
                    descriptionLabel.text = "최대체력의 20%를 회복한다.";
                    background.color = Common;
                    break;

                default:
                    SkillData s = card.Skill;
                    kindLabel.text = s.Kind == SkillKind.Passive ? "패시브" : "액티브";
                    nameLabel.text = s.displayName;
                    levelLabel.text = card.Type == CardType.NewSkill ? "NEW" : $"Lv{card.NextLevel - 1} → {card.NextLevel}";
                    descriptionLabel.text = s.description;
                    background.color = RarityColor(s.rarity);
                    materialLabel.gameObject.SetActive(skills.IsFusionMaterial(s));
                    break;
            }
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
