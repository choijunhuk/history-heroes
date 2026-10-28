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
        [Header("v0.4 장비")]
        [SerializeField] private Button equipButton;
        [SerializeField] private TMP_Text equipLabel;
        [SerializeField] private EquipmentPanel equipment;
        [Header("v0.4 영웅 성장")]
        [SerializeField] private Meta.MetaTable metaTable;
        [SerializeField] private Button levelButton;
        [SerializeField] private TMP_Text levelLabel;
        [SerializeField] private Button awakenButton;
        [SerializeField] private TMP_Text awakenLabel;
        [SerializeField] private Button trainingButton;
        [SerializeField] private TrainingPanel training;
        [SerializeField] private TMP_Text messageLabel;
        [SerializeField] private Button runeButton;
        [SerializeField] private TMP_Text runeLabel;
        [SerializeField] private RunePanel runes;
        [SerializeField] private Button petButton;
        [SerializeField] private TMP_Text petLabel;
        [SerializeField] private PetPanel pets;
        [Header("그래픽 재평가 3번: 고른 영웅을 크게 · 이름·역할·고유 능력 먼저 (있으면 줄 글은 짧게)")]
        [SerializeField] private RawImage bigPortrait;
        [SerializeField] private TMP_Text detailName;
        [SerializeField] private TMP_Text detailBody;

        private static readonly Color Normal = new Color(0.32f, 0.4f, 0.6f);
        private static readonly Color Focused = new Color(1f, 0.78f, 0.25f);
        private static readonly Color Locked = new Color(0.2f, 0.21f, 0.26f);

        private int _focus;
        private Action _onChanged;
        private HeroPortraits _portraits;

        private void Awake()
        {
            root.SetActive(false);
            _portraits = root.GetComponent<HeroPortraits>(); // 3D 초상 (Art3D-Plan 7번, 없으면 글만)
            for (int i = 0; i < heroButtons.Length; i++)
            {
                int index = i;
                heroButtons[i].onClick.AddListener(() => Focus(index));
            }

            selectButton.onClick.AddListener(SelectFocused);
            unlockButton.onClick.AddListener(UnlockFocused);
            backButton.onClick.AddListener(() => root.SetActive(false));
            if (equipButton != null) equipButton.onClick.AddListener(OpenEquipment);
            if (levelButton != null) levelButton.onClick.AddListener(LevelUpFocused);
            if (awakenButton != null) awakenButton.onClick.AddListener(AwakenFocused);
            if (trainingButton != null) trainingButton.onClick.AddListener(() => training.Open(Render));
            if (runeButton != null) runeButton.onClick.AddListener(() => runes.Open(FocusHero.id, FocusHero.displayName, Render));
            if (petButton != null) petButton.onClick.AddListener(() => pets.Open(FocusHero.id, FocusHero.displayName, Render));
        }

        private HeroData FocusHero => roster.heroes[Mathf.Clamp(_focus, 0, roster.heroes.Count - 1)];

        private void LevelUpFocused()
        {
            HeroData focus = FocusHero;
            SaveSystem.CommitResult r = SaveSystem.Commit(() => HeroUnlock.IsUnlocked(SaveSystem.Data, focus) && Meta.HeroGrowth.TryLevelUp(SaveSystem.Data, metaTable, focus.id));
            if (r == SaveSystem.CommitResult.Rejected) return;
            if (r == SaveSystem.CommitResult.Saved)
            {
                Missions.MissionEvents.Raise(Missions.MissionEvents.HeroLevel);
                SaveSystem.SaveIfDirty();
            }

            if (messageLabel != null) messageLabel.text = r == SaveSystem.CommitResult.Saved ? $"<color=#7CE38B>{focus.displayName} Lv {SaveSystem.Data.Hero(focus.id).level}</color>" : SaveSystem.SaveFailedText;
            Render();
        }

        private void AwakenFocused()
        {
            HeroData focus = FocusHero;
            SaveSystem.CommitResult r = SaveSystem.Commit(() => HeroUnlock.IsUnlocked(SaveSystem.Data, focus) && Meta.HeroGrowth.TryAwaken(SaveSystem.Data, metaTable, focus.id));
            if (r == SaveSystem.CommitResult.Rejected) return;
            if (messageLabel != null) messageLabel.text = r == SaveSystem.CommitResult.Saved ? $"<color=#FFD966>{focus.displayName} ★{SaveSystem.Data.Hero(focus.id).stars}</color>" : SaveSystem.SaveFailedText;
            Render();
        }

        /// <summary>고른 영웅의 장비 화면 (장비는 영웅마다 따로 낀다)</summary>
        private void OpenEquipment()
        {
            HeroData focus = roster.heroes[Mathf.Clamp(_focus, 0, roster.heroes.Count - 1)];
            if (equipment != null) equipment.Open(focus.id, focus.displayName, Render);
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
                if (_portraits != null)
                {
                    _portraits.SetLocked(i, !unlocked);
                    _portraits.SetFocus(_focus); // 고른 영웅 초상만 계속 움직인다
                }
                string mark = h == selected ? "  <color=#7CFC9A>(지금 영웅)</color>" : string.Empty;
                Records.HeroProgress progress = d.Hero(h.id);
                string growth = unlocked ? $"  <size=80%><color=#9FD8FF>Lv {progress.level}</color> <color=#FFD966>{new string('★', progress.stars)}{new string('☆', 5 - progress.stars)}</color></size>" : string.Empty;
                mark = growth + mark;
                string lockText = unlocked ? string.Empty : $"\n<color=#FFB347>잠김 · {HeroUnlock.LockText(h)}</color>";
                if (detailBody != null)
                {
                    // 칸에는 이름과 레벨만 (자세한 것은 위 큰 카드)
                    string state = !unlocked ? "<color=#FFB347>잠김</color>" : h == selected ? "<color=#7CFC9A>지금 영웅</color>" : $"Lv {progress.level} <color=#FFD966>{new string('★', progress.stars)}</color>";
                    heroLabels[i].text = $"<b>{h.displayName}</b>\n<size=70%>{state}</size>";
                    continue;
                }

                heroLabels[i].text =
                    $"<b>{h.displayName}</b>  <size=80%>{h.ClassLabel}</size>{mark}\n" +
                    $"<size=78%>체력 {h.maxHp:0} · 공격 {h.attack:0} · 공속 {h.attackSpeed:0.0} · 사거리 {h.range:0.#} · 이동 {h.moveSpeed:0.#}</size>\n" +
                    $"<size=78%><color=#FFD966>{h.signatureName}</color>: {h.signatureDescription}</size>\n" +
                    $"<size=70%><color=#A9B4C2>{Heroes.HeroSignature.StarText(h)}</color></size>" + lockText;
            }

            HeroData focus = roster.heroes[Mathf.Clamp(_focus, 0, roster.heroes.Count - 1)];
            bool focusUnlocked = HeroUnlock.IsUnlocked(d, focus);
            if (detailBody != null)
            {
                Records.HeroProgress fp = d.Hero(focus.id);
                string state = !focusUnlocked ? "<color=#FFB347>잠김</color>" : focus == selected ? "<color=#7CFC9A>지금 영웅</color>" : $"<color=#9FD8FF>Lv {fp.level}</color> <color=#FFD966>{new string('★', fp.stars)}{new string('☆', 5 - fp.stars)}</color>";
                if (detailName != null) detailName.text = $"{focus.displayName}  <size=55%>{focus.ClassLabel} · {state}</size>";
                string lockText = focusUnlocked ? string.Empty : $"\n<color=#FFB347>{HeroUnlock.LockText(focus)}</color>";
                detailBody.text =
                    $"<color=#FFD966><b>{focus.signatureName}</b></color>  {focus.signatureDescription}\n" +
                    $"<size=80%><color=#A9B4C2>{Heroes.HeroSignature.StarText(focus)}</color></size>\n" +
                    $"<size=80%>체력 {focus.maxHp:0} · 공격 {focus.attack:0} · 공속 {focus.attackSpeed:0.0} · 사거리 {focus.range:0.#} · 이동 {focus.moveSpeed:0.#}</size>" + lockText;
                if (bigPortrait != null && _portraits != null)
                {
                    bigPortrait.texture = _portraits.TextureOf(_focus);
                    bigPortrait.color = bigPortrait.texture == null ? new Color(1f, 1f, 1f, 0f) : focusUnlocked ? Color.white : new Color(0.66f, 0.66f, 0.74f, 1f); // 잠긴 영웅도 얼굴·옷이 보이게 (새 영웅 화면 점검 1번: "흰옷과 얼굴이 어둡다")
                }
            }

            selectButton.interactable = focusUnlocked && focus != selected;
            selectLabel.text = !focusUnlocked ? "잠긴 영웅" : focus == selected ? "지금 영웅" : KoreanText.Josa(focus.displayName, "으로", "로") + " 하기";

            if (equipButton != null)
            {
                bool gear = Equipment.EquipmentInventory.Unlocked(d);
                equipButton.gameObject.SetActive(focusUnlocked);
                equipButton.interactable = gear;
                equipLabel.text = !gear ? "장비 (1구간 보스 뒤)" : focus.displayName + "의 장비";
            }

            if (runeButton != null && metaTable != null && metaTable.runes != null)
            {
                runeButton.gameObject.SetActive(focusUnlocked);
                int slots = Runes.RuneService.SlotCount(d, metaTable.runes);
                bool pendingChoice = Runes.RuneService.PendingChoice(d, metaTable.runes) != null;
                runeLabel.text = slots == 0 ? "룬 (2구간 보스 뒤)" : pendingChoice ? "룬 <color=#FFD966>(보상!)</color>" : $"룬 ({slots}칸)";
            }

            if (petButton != null && metaTable != null && metaTable.pets != null)
            {
                petButton.gameObject.SetActive(focusUnlocked);
                Pets.PetData pet = metaTable.pets.Find(Pets.PetService.Equipped(d, focus.id));
                bool bond = Pets.PetService.IsBond(pet, focus.id);
                petLabel.text = !Pets.PetService.Unlocked(d) ? "펫 (첫 던전 클리어 뒤)"
                    : pet != null ? $"펫 · {pet.displayName}{(bond ? " <color=#FF7A8A>♥</color>" : "")}" : "펫 (비어 있음)";
            }

            if (levelButton != null && metaTable != null)
            {
                Records.HeroProgress p = d.Hero(focus.id);
                bool canLevel = Meta.HeroGrowth.CanLevelUp(d, metaTable, focus.id, out int levelCost);
                levelButton.gameObject.SetActive(focusUnlocked);
                levelButton.interactable = canLevel && d.gold >= levelCost;
                levelLabel.text = canLevel ? $"레벨업 Lv {p.level} → {p.level + 1}\n<size=80%>골드 {levelCost:N0}</size>" : "최대 레벨 (Lv 30)";

                int awakenCost = Meta.HeroGrowth.AwakenCost(d, metaTable, focus.id);
                int shards = Meta.HeroGrowth.Shards(d, focus.id);
                awakenButton.gameObject.SetActive(focusUnlocked);
                awakenButton.interactable = awakenCost > 0 && shards >= awakenCost;
                awakenLabel.text = awakenCost > 0 ? $"각성 ★{p.stars} → ★{p.stars + 1}\n<size=80%>영웅 조각 {shards}/{awakenCost}</size>" : "★5 완성";
            }

            unlockButton.gameObject.SetActive(!focusUnlocked);
            bool free = HeroUnlock.CanClaimFree(d, focus);
            bool enough = Wallet.Gems(d) >= focus.unlockGems;
            unlockButton.interactable = free || enough;
            unlockLabel.text = free ? $"던전 {HeroUnlock.FreePickRuns}판 기념 무료로 열기\n<size=80%>(보석 영웅 중 한 명만)</size>"
                : enough ? $"보석 {Wallet.Format(focus.unlockGems)}로 바로 해금"
                : $"보석이 모자라요 ({Wallet.Format(Wallet.Gems(d))}/{Wallet.Format(focus.unlockGems)})";
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
            if (!HeroUnlock.TryClaimFree(SaveSystem.Data, focus) && !HeroUnlock.TryBuy(SaveSystem.Data, focus)) return;
            SaveSystem.Data.selectedHero = focus.id;
            SaveSystem.Save();
            Render();
            _onChanged?.Invoke();
        }
    }
}
