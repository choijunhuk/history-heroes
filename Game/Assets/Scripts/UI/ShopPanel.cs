using System.Collections.Generic;
using HistoryHeroes.Common;
using HistoryHeroes.Economy;
using HistoryHeroes.Equipment;
using HistoryHeroes.Records;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace HistoryHeroes.UI
{
    /// <summary>
    /// 상점 (Gacha 5장, Equipment 8장): 일반 상자(🪙5,000 · 하루 1번 광고 무료), 고급 상자(💎250 / 10연 💎2,250 / 🎫 1장,
    /// "전설 확정까지 n회"), 확률 보기(추첨과 같은 표), 🔩 조각 교환소(원하는 희귀 60 · 전설 300).
    /// 구매 버튼 바로 옆에 확률 보기를 둔다 (게임산업법·구글 플레이). 결과는 열자마자 저장돼 있다.
    /// </summary>
    public sealed class ShopPanel : MonoBehaviour
    {
        [SerializeField] private GameObject root;
        [SerializeField] private GachaTable gacha;
        [SerializeField] private TMP_Text walletLabel;
        [SerializeField] private Button normalBuyButton;
        [SerializeField] private TMP_Text normalBuyLabel;
        [SerializeField] private Button normalFreeButton;
        [SerializeField] private TMP_Text normalFreeLabel;
        [SerializeField] private TMP_Text premiumInfoLabel;
        [SerializeField] private Button premium1Button;
        [SerializeField] private TMP_Text premium1Label;
        [SerializeField] private Button premium10Button;
        [SerializeField] private TMP_Text premium10Label;
        [SerializeField] private Button ticketButton;
        [SerializeField] private TMP_Text ticketLabel;
        [SerializeField] private Button ratesButton;
        [SerializeField] private Button exchangeButton;
        [SerializeField] private TMP_Text exchangeLabel;
        [SerializeField] private TMP_Text messageLabel;
        [SerializeField] private ButtonList list;
        [SerializeField] private GameObject ratesRoot;
        [SerializeField] private TMP_Text ratesLabel;
        [SerializeField] private Button ratesCloseButton;
        [SerializeField] private Button backButton;
        [Header("골드 상점 (매일 갱신, Economy 5장)")]
        [SerializeField] private Button goldShopButton;
        [SerializeField] private TMP_Text goldShopLabel;
        [SerializeField] private Heroes.HeroRoster roster;

        private static readonly Color RowNormal = new Color(0.13f, 0.15f, 0.2f);
        private static readonly Color RowDisabled = new Color(0.1f, 0.1f, 0.12f);

        private bool _exchangeMode;
        private bool _goldShopMode;
        private bool _waitingForAd;
        private List<ButtonList.Row> _lastResults = new List<ButtonList.Row>();
        private System.Action _onClosed;

        public bool IsOpen => root.activeSelf;

        private void Awake()
        {
            root.SetActive(false);
            if (ratesRoot != null) ratesRoot.SetActive(false);
            normalBuyButton.onClick.AddListener(() => Buy(gacha.normal, 1, GachaService.Pay.Gold));
            normalFreeButton.onClick.AddListener(WatchAdForNormal);
            premium1Button.onClick.AddListener(() => Buy(gacha.premium, 1, GachaService.Pay.Gems));
            premium10Button.onClick.AddListener(() => Buy(gacha.premium, 10, GachaService.Pay.Gems));
            ticketButton.onClick.AddListener(() => Buy(gacha.premium, 1, GachaService.Pay.Ticket));
            ratesButton.onClick.AddListener(ShowRates);
            ratesCloseButton.onClick.AddListener(() => ratesRoot.SetActive(false));
            exchangeButton.onClick.AddListener(() =>
            {
                _exchangeMode = !_exchangeMode;
                _goldShopMode = false;
                Render(true);
            });
            if (goldShopButton != null)
            {
                goldShopButton.onClick.AddListener(() =>
                {
                    _goldShopMode = !_goldShopMode;
                    _exchangeMode = false;
                    Render(true);
                });
            }
            backButton.onClick.AddListener(Close);
        }

        public void Open(System.Action onClosed = null)
        {
            _onClosed = onClosed;
            _exchangeMode = false;
            _goldShopMode = false;
            _lastResults.Clear();
            messageLabel.text = string.Empty;
            root.SetActive(true);
            Render(true);
        }

        private void Close()
        {
            root.SetActive(false);
            _onClosed?.Invoke();
        }

        private void Render(bool scrollToTop)
        {
            SaveData save = SaveSystem.Data;
            string today = Clock.Today;
            bool unlocked = EquipmentInventory.Unlocked(save);
            walletLabel.text = $"골드 {save.gold:N0} · 보석 {Wallet.Gems(save):N0} · 고급 소환권 {save.ItemCount(Wallet.Ticket)} · 장비 조각 {save.ItemCount(Wallet.GearShard)}";

            normalBuyLabel.text = $"일반 상자 · 골드 {gacha.normal.goldCost:N0}";
            normalBuyButton.interactable = unlocked && save.gold >= gacha.normal.goldCost;
            bool free = GachaService.CanFreeNormal(save, today);
            normalFreeButton.interactable = unlocked && free && !_waitingForAd;
            normalFreeLabel.text = _waitingForAd ? "광고 보는 중…" : free ? "광고 보고 무료 (오늘 1번)" : "오늘 무료는 받았어요";

            premiumInfoLabel.text = unlocked
                ? $"고급 상자 · 전설 2% · <color=#FFC94A>전설 확정까지 {GachaService.PullsUntilPity(save, gacha.premium)}회</color>"
                : "<color=#FFB347>1구간 보스를 깨면 장비와 상자가 열려요</color>";
            premium1Label.text = $"1회 · 보석 {gacha.premium.gemCost:N0}";
            premium10Label.text = $"10연 · 보석 {gacha.premium.gem10Cost:N0}";
            premium1Button.interactable = unlocked && Wallet.Gems(save) >= gacha.premium.gemCost;
            premium10Button.interactable = unlocked && Wallet.Gems(save) >= gacha.premium.gem10Cost;
            int tickets = save.ItemCount(Wallet.Ticket);
            ticketLabel.text = $"소환권 1장 (가진 것 {tickets})";
            ticketButton.interactable = unlocked && tickets > 0;
            exchangeButton.interactable = unlocked;
            exchangeLabel.text = _exchangeMode ? "상자 결과" : "조각 교환소";
            if (goldShopLabel != null) goldShopLabel.text = _goldShopMode ? "상자 결과" : "골드 상점";

            if (_goldShopMode) RenderGoldShop(save, today, scrollToTop);
            else if (_exchangeMode) RenderExchange(save, scrollToTop);
            else list.Show(_lastResults.Count > 0 ? _lastResults : Hint(), scrollToTop);
        }

        private static List<ButtonList.Row> Hint()
        {
            return new List<ButtonList.Row>
            {
                new ButtonList.Row("<color=#A9B4C2>상자에서 나온 장비가 여기 보여요. 희귀·전설 중복은 장비 조각이 돼요 (장비 화면에서 끌 수 있어요)</color>", RowDisabled, null, false),
            };
        }

        private void RenderExchange(SaveData save, bool scrollToTop)
        {
            EquipmentTable table = gacha.equipment;
            int shards = save.ItemCount(Wallet.GearShard);
            var rows = new List<ButtonList.Row>();
            foreach (EquipRarity rarity in new[] { EquipRarity.Legendary, EquipRarity.Rare })
            {
                foreach (EquipmentData data in table.items)
                {
                    if (data == null || data.rarity != rarity) continue;
                    int cost = EquipmentInventory.ExchangeCost(table, data);
                    string id = data.id;
                    bool owned = EquipmentInventory.Owns(save, id);
                    string second = data.hasSecondary ? $" · {data.SecondaryText}" : string.Empty;
                    string text = $"<color={EquipmentText.RarityColor(rarity)}>{data.displayName} ({EquipmentText.RarityName(rarity)})</color> · {EquipmentText.SlotName(data.slot)}{(owned ? "  <color=#A9B4C2>[가짐]</color>" : string.Empty)}\n<size=85%>장비 조각 {cost}{second}</size>";
                    rows.Add(new ButtonList.Row(text, shards >= cost ? RowNormal : RowDisabled, () => Exchange(id), shards >= cost));
                }
            }

            list.Show(rows, scrollToTop);
        }

        /// <summary>골드 상점: 🧩 영웅 조각 골드 500, 하루 3개 (영웅 골라서). 영웅 조각은 보석·뽑기로 팔지 않는다</summary>
        private void RenderGoldShop(SaveData save, string today, bool scrollToTop)
        {
            int left = Meta.HeroGrowth.ShardsLeftToday(save, today);
            var rows = new List<ButtonList.Row>
            {
                new ButtonList.Row($"<color=#A9B4C2>영웅 조각은 각성(★)에 써요 · 오늘 남은 것 {left}/{Meta.HeroGrowth.ShardsPerDay} · 매일 새로</color>", RowDisabled, null, false),
            };

            foreach (Heroes.HeroData h in roster.heroes)
            {
                if (h == null || !Heroes.HeroUnlock.IsUnlocked(save, h)) continue;
                string id = h.id;
                bool ok = left > 0 && save.gold >= Meta.HeroGrowth.ShardPrice;
                string text = $"{h.displayName} 영웅 조각 1개 · 골드 {Meta.HeroGrowth.ShardPrice:N0}\n<size=85%>가진 것 {Meta.HeroGrowth.Shards(save, id)} · ★{save.Hero(id).stars}</size>";
                rows.Add(new ButtonList.Row(text, ok ? RowNormal : RowDisabled, () => BuyHeroShard(id), ok));
            }

            list.Show(rows, scrollToTop);
        }

        private void BuyHeroShard(string heroId)
        {
            SaveData save = SaveSystem.Data;
            if (!Meta.HeroGrowth.TryBuyHeroShard(save, heroId, Clock.Today)) return;
            SaveSystem.Save();
            messageLabel.text = $"<color=#7CE38B>영웅 조각 +1 (가진 것 {Meta.HeroGrowth.Shards(save, heroId)})</color>";
            Render(false);
        }

        // ---------------------------------------------------------------- 사기

        private void Buy(BoxRates box, int count, GachaService.Pay pay)
        {
            SaveData save = SaveSystem.Data;
            if (!EquipmentInventory.Unlocked(save) || !GachaService.TryPay(save, gacha, box, count, pay, Clock.Today)) return;
            List<GachaService.Pull> pulls = GachaService.Open(save, gacha, box, count, Clock.UnixSeconds);
            SaveSystem.Save(); // 재화 차감 + 결과 + 천장 카운터가 한 번에 (연출 전에)
            ShowResults(box, pulls);
        }

        private void WatchAdForNormal()
        {
            if (_waitingForAd) return;
            _waitingForAd = true;
            Render(false);
            Ads.Current.Show("normal_box", success =>
            {
                _waitingForAd = false;
                if (success) Buy(gacha.normal, 1, GachaService.Pay.FreeAd); // 보상은 "보상 획득"에서 한 번만
                else messageLabel.text = "<color=#FFB347>광고를 끝까지 못 봐서 상자를 못 받았어요</color>";
                Render(false);
            });
        }

        private void ShowResults(BoxRates box, List<GachaService.Pull> pulls)
        {
            _exchangeMode = false;
            _goldShopMode = false;
            _lastResults = new List<ButtonList.Row>();
            int best = 0;
            foreach (GachaService.Pull p in pulls)
            {
                if (p.Data == null) continue;
                best = Mathf.Max(best, (int)p.Data.rarity);
                string note = p.FromPity ? "  <color=#FFC94A>[천장]</color>" : p.FromTenGuarantee ? "  <color=#6FA8FF>[10연 보장]</color>" : string.Empty;
                string line = EquipmentInventory.AddLine(p.Added, p.Data).Replace("장비  ", string.Empty);
                _lastResults.Add(new ButtonList.Row(line + note, RowNormal, null, false));
            }

            var top = (EquipRarity)best;
            messageLabel.text = $"{box.displayName} {pulls.Count}개 열었어요 · 가장 좋은 것 <color={EquipmentText.RarityColor(top)}>{EquipmentText.RarityName(top)}</color>";
            Render(true);
        }

        private void Exchange(string dataId)
        {
            SaveData save = SaveSystem.Data;
            EquipmentInventory.AddResult? got = EquipmentInventory.TryExchange(save, gacha.equipment, dataId);
            if (!got.HasValue) return;
            SaveSystem.Save();
            EquipmentData data = gacha.equipment.Find(dataId);
            messageLabel.text = "교환: " + EquipmentInventory.AddLine(got.Value, data).Replace("장비  ", string.Empty);
            Render(false);
        }

        private void ShowRates()
        {
            ratesLabel.text = GachaService.RatesText(gacha, gacha.premium) + "\n" + GachaService.RatesText(gacha, gacha.normal) +
                              "\n<size=85%><color=#A9B4C2>화면의 확률과 실제 추첨은 같은 표를 씁니다. 이 버전은 가짜 보석이라 실제 결제는 없어요</color></size>";
            ratesRoot.SetActive(true);
        }
    }
}
