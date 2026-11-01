using System;
using System.Collections.Generic;
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
        [SerializeField] private Outline banOutline;
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

        // 등급 색 = 테두리·아이콘 칸·뒤 빛 (카드 바탕은 어두운 남색으로 같다)
        // 일반은 차분한 테두리, 희귀·전설만 강하게 (전체 UI 검토 6번)
        public static readonly Color Common = new Color(0.46f, 0.51f, 0.62f);
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
        public void Show(SkillCard card, SkillManager skills, Action onClick, bool legendaryWarning = false, bool banMode = false)
        {
            _onClick = onClick;
            _rainbow = card.Type == CardType.Fusion;
            if (banOutline != null) banOutline.enabled = banMode && (card.Type == CardType.NewSkill || card.Type == CardType.LevelUp);
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
                    // 이미 가진 스킬은 바뀌는 수치만 (설명의 숫자는 1레벨 기준이라 같이 두면 헷갈린다). 못 구하면 원래 설명
                    bool melee = skills.HeroClass == Heroes.HeroClass.Warrior;
                    string upgrade = card.Type == CardType.LevelUp ? UpgradeLine(s, card.NextLevel - 1, card.NextLevel, melee) : string.Empty;
                    descriptionLabel.text = upgrade.Length > 0 ? $"<color=#FFE27A>{upgrade}</color>" : s.DescriptionFor(skills.HeroClass);
                    Paint(RarityColor(s.rarity), GlowFor(s.rarity), s.icon);
                    hint = skills.CardHint(card);
                    if (legendaryWarning && s.rarity == Rarity.Legendary)
                    {
                        const string star = "<color=#FF7A6B>★3 도전 중: 고르면 이번 판 ★3 실패</color>";
                        hint = string.IsNullOrEmpty(hint) ? star : hint + "\n" + star;
                    }

                    break;
            }

            if (banMode)
            {
                hint = skills.BanHint(card);
                if ((card.Type == CardType.NewSkill || card.Type == CardType.LevelUp) && frame != null) frame.color = new Color(0.9f, 0.22f, 0.25f);
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

        /// <summary>
        /// 레벨업 카드의 "지금 → 고른 뒤" 줄 (전체 UI 검토 6번). 레벨 따라 바뀌는 수치만, 많아야 2개.
        /// 예: "개수 2 → 3", "피해 180% → 225%", "치명타 확률 +6% → +10%"
        /// </summary>
        /// <param name="melee">전사(근접)면 멀티샷 줄을 "베기당"으로</param>
        public static string UpgradeLine(SkillData skill, int from, int to, bool melee = false)
        {
            var parts = new List<string>(2);
            void Add(string name, string now, string next)
            {
                if (now != next && parts.Count < 2) parts.Add($"{name} {now} → {next}");
            }

            if (skill is ActiveSkillData a)
            {
                Add("개수", a.CountAt(from).ToString(), a.CountAt(to).ToString());
                if (a.healFractionFirstLevel > 0f)
                    Add("회당 회복(최대체력)", Percent(a.HealFractionAt(from)), Percent(a.HealFractionAt(to)));
                else
                    Add(DamageName(a), Percent(DamageValue(a, from)), Percent(DamageValue(a, to)));
                Add("지속", $"{a.DurationAt(from):0.#}초", $"{a.DurationAt(to):0.#}초");
                Add("둔화", Percent(a.SlowAt(from)), Percent(a.SlowAt(to)));
                if (a.id == "owl_eye") Add("표적 거리", $"{a.RadiusAt(from):0.#}", $"{a.RadiusAt(to):0.#}");
                else if (a.radius > 0f) Add("범위", Percent(a.RadiusAt(from) / a.radius), Percent(a.RadiusAt(to) / a.radius));
            }
            else if (skill is PassiveSkillData p)
            {
                foreach (PassiveSkillData.StatEntry e in p.stats)
                {
                    string name = e.stat == Stats.StatType.HpRegen ? "초당 회복(최대체력)" : Equipment.EquipmentText.StatName(e.stat);
                    Add(name, "+" + Percent(e.ValueAt(from)), "+" + Percent(e.ValueAt(to)));
                }

                foreach (PassiveSkillData.AttackEntry e in p.attack)
                {
                    Add(AttackName(e.kind), $"+{e.ValueAt(from)}", $"+{e.ValueAt(to)}");
                    // 멀티샷은 늘수록 발(베기)마다 피해가 준다 — 고르는 대가를 숨기지 않는다 (Codex 검토 2번)
                    if (e.kind == Player.AttackModifierKind.ExtraProjectiles)
                    {
                        float penalty = Player.PlayerController.MultishotPenalty;
                        Add(melee ? "베기당 피해" : "발당 피해", Percent(1f - penalty * e.ValueAt(from)), Percent(1f - penalty * e.ValueAt(to)));
                    }
                }
            }

            return string.Join("\n", parts);
        }

        /// <summary>
        /// "피해" 칸의 실제 뜻 (Codex 검토로 레벨 따라 크는 액티브 25종을 효과 코드와 맞춰 봄): 대부분 한 번 맞히는 피해.
        /// 다른 뜻인 것만 여기 적는다. 칠성단은 EightGateBehaviour (초당, 배율을 실제 값으로 바꿔 보여 준다)
        /// </summary>
        private static readonly Dictionary<string, string> DamageNames = new Dictionary<string, string>
        {
            ["guard_shield"] = "보호막(최대체력)",
            ["blizzard"] = "초당 피해",
            ["thunder_fire"] = "번개 피해",
            ["dav_spiral"] = "발당 피해",
        };

        private static string DamageName(ActiveSkillData a)
        {
            if (a.behaviourPrefab is EightGateBehaviour) return "초당 피해";
            return a.id != null && DamageNames.TryGetValue(a.id, out string name) ? name : "피해";
        }

        /// <summary>칠성단은 DamageAt을 1레벨 대비 배율로 쓴다 (EightGateBehaviour): 실제 초당 피해로 바꿔 보여 준다</summary>
        private static float DamageValue(ActiveSkillData a, int level)
        {
            if (a.behaviourPrefab is EightGateBehaviour gate) return gate.WindDamagePerSecond * a.DamageAt(level) / Mathf.Max(0.01f, a.DamageAt(1));
            return a.DamageAt(level);
        }

        private static string Percent(float v) => $"{v * 100f:0.#}%";

        private static string AttackName(Player.AttackModifierKind kind)
        {
            switch (kind)
            {
                case Player.AttackModifierKind.ExtraProjectiles: return "추가 공격";
                case Player.AttackModifierKind.Pierce: return "관통";
                case Player.AttackModifierKind.Ricochet: return "벽 튕김";
                default: return kind.ToString();
            }
        }

        private void SetHint(string hint)
        {
            bool has = !string.IsNullOrEmpty(hint);
            materialLabel.gameObject.SetActive(has);
            if (hintBox != null) hintBox.SetActive(has);
            materialLabel.text = has ? hint : string.Empty;
            // 목표 두 개와 조합 진행·빼기 경고가 여러 줄이어도 설명과 겹치지 않게 한다.
            float height = has ? Mathf.Clamp(materialLabel.GetPreferredValues(hint, materialLabel.rectTransform.rect.width, Mathf.Infinity).y, 96f, 176f) : 96f;
            materialLabel.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, height);
            if (hintBox != null) ((RectTransform)hintBox.transform).SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, height + 4f);
            if (descriptionBox != null) descriptionBox.offsetMin = new Vector2(descriptionBox.offsetMin.x, has ? height + 16f : BoxBottomPlain);
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
