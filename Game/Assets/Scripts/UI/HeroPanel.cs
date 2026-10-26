using System;
using HistoryHeroes.Common;
using HistoryHeroes.Economy;
using HistoryHeroes.Heroes;
using HistoryHeroes.Records;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace HistoryHeroes.UI
{
    /// <summary>
    /// 영웅 고르기 (UX.md 1장 "영웅" 탭의 v0.3 축소판, Heroes.md 2장).
    /// 영웅마다 능력치·대표 효과·해금 조건. 잠긴 영웅은 그 구간 보스를 깨거나 보석 1,500으로 바로 연다 (뽑기 없음).
    /// </summary>
    public sealed class HeroPanel : MonoBehaviour
    {
        [SerializeField] private GameObject root;
        [SerializeField] private HeroRoster roster;
        [SerializeField] private Button[] heroButtons;
        [SerializeField] private Image[] heroBackgrounds;
        [SerializeField] private TMP_Text[] heroLabels;
        [SerializeField] private Button selectButton;
        [SerializeField] private TMP_Text selectLabel;
        [SerializeField] private Button unlockButton;
        [SerializeField] private TMP_Text unlockLabel;
        [SerializeField] private Button backButton;

        private static readonly Color Normal = new Color(0.16f, 0.18f, 0.24f);
        private static readonly Color Focused = new Color(0.25f, 0.42f, 0.7f);
        private static readonly Color Locked = new Color(0.12f, 0.12f, 0.14f);

        private int _focus;
        private Action _onChanged;

        private void Awake()
        {
            root.SetActive(false);
            for (int i = 0; i < heroButtons.Length; i++)
            {
                int index = i;
                heroButtons[i].onClick.AddListener(() => Focus(index));
            }

            selectButton.onClick.AddListener(SelectFocused);
            unlockButton.onClick.AddListener(UnlockFocused);
            backButton.onClick.AddListener(() => root.SetActive(false));
        }

        public void Open(Action onChanged)
        {
            _onChanged = onChanged;
            HeroData selected = HeroUnlock.Selected(SaveSystem.Data, roster);
            _focus = Mathf.Max(0, roster.heroes.IndexOf(selected));
            root.SetActive(true);
            Render();
        }

        private void Focus(int index)
        {
            _focus = index;
            Render();
        }

        private void Render()
        {
            SaveData d = SaveSystem.Data;
            HeroData selected = HeroUnlock.Selected(d, roster);

            for (int i = 0; i < heroButtons.Length; i++)
            {
                bool exists = i < roster.heroes.Count;
                heroButtons[i].gameObject.SetActive(exists);
                if (!exists) continue;

                HeroData h = roster.heroes[i];
                bool unlocked = HeroUnlock.IsUnlocked(d, h);
                heroBackgrounds[i].color = i == _focus ? Focused : unlocked ? Normal : Locked;
                string mark = h == selected ? "  <color=#7CFC9A>(지금 영웅)</color>" : string.Empty;
                string lockText = unlocked ? string.Empty : $"\n<color=#FFB347>잠김 · {HeroUnlock.Condition(h)} (또는 보석 {Wallet.Format(h.unlockGems)})</color>";
                heroLabels[i].text =
                    $"<b>{h.displayName}</b>  <size=80%>{h.ClassLabel}</size>{mark}\n" +
                    $"<size=78%>체력 {h.maxHp:0} · 공격 {h.attack:0} · 공속 {h.attackSpeed:0.0} · 사거리 {h.range:0.#} · 이동 {h.moveSpeed:0.#}</size>\n" +
                    $"<size=78%><color=#FFD966>{h.signatureName}</color>: {h.signatureDescription}</size>" + lockText;
            }

            HeroData focus = roster.heroes[Mathf.Clamp(_focus, 0, roster.heroes.Count - 1)];
            bool focusUnlocked = HeroUnlock.IsUnlocked(d, focus);
            selectButton.interactable = focusUnlocked && focus != selected;
            selectLabel.text = !focusUnlocked ? "잠긴 영웅" : focus == selected ? "지금 영웅" : KoreanText.Josa(focus.displayName, "으로", "로") + " 하기";

            unlockButton.gameObject.SetActive(!focusUnlocked);
            bool enough = Wallet.Gems(d) >= focus.unlockGems;
            unlockButton.interactable = enough;
            unlockLabel.text = enough ? $"보석 {Wallet.Format(focus.unlockGems)}로 바로 해금" : $"보석이 모자라요 ({Wallet.Format(Wallet.Gems(d))}/{Wallet.Format(focus.unlockGems)})";
        }

        private void SelectFocused()
        {
            HeroData focus = roster.heroes[_focus];
            if (!HeroUnlock.IsUnlocked(SaveSystem.Data, focus)) return;
            SaveSystem.Data.selectedHero = focus.id;
            SaveSystem.Save();
            Render();
            _onChanged?.Invoke();
        }

        private void UnlockFocused()
        {
            HeroData focus = roster.heroes[_focus];
            if (!HeroUnlock.TryBuy(SaveSystem.Data, focus)) return;
            SaveSystem.Data.selectedHero = focus.id;
            SaveSystem.Save();
            Render();
            _onChanged?.Invoke();
        }
    }
}
