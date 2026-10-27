using System;
using HistoryHeroes.Common;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace HistoryHeroes.UI
{
    /// <summary>
    /// 보상 방 (Content 2-2: 6·11번 방, 전투 없음): 셋 중 하나 — 체력 회복 / 스킬 카드 1장 더 / 골드.
    /// 고르는 동안 게임이 멈춘다. 숨 돌리는 자리 (Modes 2-7).
    /// </summary>
    public sealed class RewardRoomPanel : MonoBehaviour
    {
        public enum Choice
        {
            Heal,
            Card,
            Gold,
        }

        [SerializeField] private GameObject root;
        [SerializeField] private TMP_Text titleLabel;
        [SerializeField] private Button[] buttons;
        [SerializeField] private TMP_Text[] labels;
        [Tooltip("버튼마다 아이콘 (보상 방 기본 = 회복 · 카드 · 상자). 석비는 축복 아이콘으로 바꿔 그린다")]
        [SerializeField] private Image[] icons = new Image[0];

        private Action<Choice> _onChosen;
        private Action<int> _onIndex;
        private Sprite[] _defaultIcons;
        private Color[] _defaultColors;

        public bool IsOpen => root.activeSelf;

        private void Awake()
        {
            root.SetActive(false);
            _defaultIcons = new Sprite[icons.Length];
            for (int i = 0; i < icons.Length; i++) _defaultIcons[i] = icons[i] != null ? icons[i].sprite : null;
            _defaultColors = new Color[buttons.Length];
            for (int i = 0; i < buttons.Length; i++)
            {
                var choice = (Choice)i;
                buttons[i].onClick.AddListener(() => Choose(choice));
                _defaultColors[i] = buttons[i].targetGraphic != null ? buttons[i].targetGraphic.color : Color.white;
            }
        }

        /// <summary>버튼 아이콘·색 (null = 기본)</summary>
        private void Paint(Sprite[] sprites, Color[] colors)
        {
            for (int i = 0; i < icons.Length; i++)
            {
                if (icons[i] == null) continue;
                Sprite s = sprites != null && i < sprites.Length && sprites[i] != null ? sprites[i] : _defaultIcons != null && i < _defaultIcons.Length ? _defaultIcons[i] : null;
                icons[i].sprite = s;
                icons[i].enabled = s != null;
            }

            for (int i = 0; i < buttons.Length; i++)
            {
                if (buttons[i].targetGraphic == null || _defaultColors == null || i >= _defaultColors.Length) continue;
                buttons[i].targetGraphic.color = colors != null && i < colors.Length ? colors[i] : _defaultColors[i];
            }
        }

        /// <param name="healPercent">회복 비율 (0.3 = 30%)</param>
        public void Open(float healPercent, int gold, Action<Choice> onChosen)
        {
            _onChosen = onChosen;
            _onIndex = null;
            Paint(null, null);
            titleLabel.text = "보상 방 — 하나를 고르세요";
            labels[0].text = healPercent > 0f ? $"체력 회복\n<size=75%>최대 체력의 {Mathf.RoundToInt(healPercent * 100f)}%</size>" : "체력 회복\n<size=75%>오늘은 회복 없음</size>";
            buttons[0].interactable = healPercent > 0f;
            labels[1].text = "스킬 카드 1장 더\n<size=75%>레벨업 한 번</size>";
            labels[2].text = $"골드\n<size=75%>+{gold}</size>";
            root.SetActive(true);
            GamePause.Pause(this);
        }

        /// <summary>
        /// 아무 선택 3개 (확장 1차 3-5장 기억의 석비: 축복 2개 + 지나가기). texts[i]가 비어 있으면 그 칸은 숨긴다
        /// </summary>
        public void OpenChoice(string title, string[] texts, Sprite[] sprites, Action<int> onChosen, Color[] colors = null)
        {
            _onChosen = null;
            _onIndex = onChosen;
            Paint(sprites, colors);
            titleLabel.text = title;
            for (int i = 0; i < buttons.Length; i++)
            {
                bool has = texts != null && i < texts.Length && !string.IsNullOrEmpty(texts[i]);
                buttons[i].gameObject.SetActive(has);
                buttons[i].interactable = has;
                if (has) labels[i].text = texts[i];
            }

            root.SetActive(true);
            GamePause.Pause(this);
        }

        /// <summary>개발·벤치용: 바로 고르기</summary>
        public void Choose(Choice choice)
        {
            if (!IsOpen) return;
            root.SetActive(false);
            GamePause.Resume(this);
            foreach (Button b in buttons) b.gameObject.SetActive(true);
            Action<int> byIndex = _onIndex;
            _onIndex = null;
            if (byIndex != null)
            {
                byIndex((int)choice);
                return;
            }

            Action<Choice> callback = _onChosen;
            _onChosen = null;
            callback?.Invoke(choice);
        }

        private void OnDisable() => GamePause.Resume(this);
    }
}
