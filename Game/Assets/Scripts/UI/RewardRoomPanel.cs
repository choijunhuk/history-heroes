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

        private Action<Choice> _onChosen;

        public bool IsOpen => root.activeSelf;

        private void Awake()
        {
            root.SetActive(false);
            for (int i = 0; i < buttons.Length; i++)
            {
                var choice = (Choice)i;
                buttons[i].onClick.AddListener(() => Choose(choice));
            }
        }

        /// <param name="healPercent">회복 비율 (0.3 = 30%)</param>
        public void Open(float healPercent, int gold, Action<Choice> onChosen)
        {
            _onChosen = onChosen;
            titleLabel.text = "보상 방 — 하나를 고르세요";
            labels[0].text = $"체력 회복\n<size=75%>최대 체력의 {Mathf.RoundToInt(healPercent * 100f)}%</size>";
            labels[1].text = "스킬 카드 1장 더\n<size=75%>레벨업 한 번</size>";
            labels[2].text = $"골드\n<size=75%>+{gold}</size>";
            root.SetActive(true);
            GamePause.Pause(this);
        }

        /// <summary>개발·벤치용: 바로 고르기</summary>
        public void Choose(Choice choice)
        {
            if (!IsOpen) return;
            root.SetActive(false);
            GamePause.Resume(this);
            Action<Choice> callback = _onChosen;
            _onChosen = null;
            callback?.Invoke(choice);
        }

        private void OnDisable() => GamePause.Resume(this);
    }
}
