using System.Collections.Generic;
using HistoryHeroes.Common;
using HistoryHeroes.Economy;
using HistoryHeroes.Equipment;
using HistoryHeroes.Records;
using HistoryHeroes.Relics;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace HistoryHeroes.UI
{
    /// <summary>
    /// 장비 상자와 유물 뽑기 상점. 값과 결과를 함께 저장한 뒤 결과를 보여 준다.
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
        [Header("유물 뽑기")]
        [SerializeField] private RelicTable relics;
        [SerializeField] private Button equipmentTabButton;
        [SerializeField] private Button relicTabButton;
        [SerializeField] private GameObject relicSection;
        [SerializeField] private GameObject equipmentSection;
        [SerializeField] private TMP_Text relicInfoLabel;
        [SerializeField] private Button relicPayButton;
        [SerializeField] private TMP_Text relicPayLabel;
        [SerializeField] private Button relic1Button;
        [SerializeField] private TMP_Text relic1Label;
        [SerializeField] private Button relic10Button;
        [SerializeField] private TMP_Text relic10Label;
        [SerializeField] private Button relicFreeButton;
        [SerializeField] private TMP_Text relicFreeLabel;
        [SerializeField] private Button relicRatesButton;
        [SerializeField] private TMP_Text relicMessageLabel;
        [SerializeField] private GameObject relicResultsRoot;
        [SerializeField] private TMP_Text relicResultsLabel;
        [SerializeField] private Image[] relicResultIcons;
        [SerializeField] private TMP_Text[] relicResultLabels;
        [SerializeField] private Button relicResultsCloseButton;
        [SerializeField] private ScrollRect relicResultsScroll;
        [SerializeField] private ScrollRect ratesScroll;

        private static readonly Color RowNormal = new Color(0.13f, 0.15f, 0.2f);
        private static readonly Color RowDisabled = new Color(0.1f, 0.1f, 0.12f);

        private bool _exchangeMode;
        private bool _goldShopMode;
        private bool _waitingForAd;
        private bool _relicMode;
        private bool _payWithGems;
        private List<ButtonList.Row> _lastResults = new List<ButtonList.Row>();
        private System.Action _onClosed;

        public bool IsOpen => root.activeSelf;

        private void Awake()
        {
            root.SetActive(false);
            if (ratesRoot != null) ratesRoot.SetActive(false);
            if (relicResultsRoot != null) relicResultsRoot.SetActive(false);
            equipmentTabButton.onClick.AddListener(() => { _relicMode = false; Render(true); });
            relicTabButton.onClick.AddListener(() => { _relicMode = true; Render(true); });
            relicPayButton.onClick.AddListener(() => { _payWithGems = !_payWithGems; Render(false); });
            relic1Button.onClick.AddListener(() => BuyRelics(1, _payWithGems ? RelicGachaService.Pay.Gems : RelicGachaService.Pay.Shards));
            relic10Button.onClick.AddListener(() => BuyRelics(10, _payWithGems ? RelicGachaService.Pay.Gems : RelicGachaService.Pay.Shards));
            relicFreeButton.onClick.AddListener(() => BuyRelics(1, RelicGachaService.Pay.Free));
            relicRatesButton.onClick.AddListener(ShowRates);
            relicResultsCloseButton.onClick.AddListener(() => relicResultsRoot.SetActive(false));
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
            _relicMode = false;
            _payWithGems = false;
            _lastResults.Clear();
            messageLabel.text = string.Empty;
            relicMessageLabel.text = string.Empty;
            ratesRoot.SetActive(false);
            relicResultsRoot.SetActive(false);
            transform.SetAsLastSibling();
            root.SetActive(true);
            Render(true);
        }

        /// <summary>도감에서도 같은 유물 뽑기 화면을 연다.</summary>
        public void OpenRelics(System.Action onClosed = null)
        {
            Open(onClosed);
            _relicMode = true;
            Render(true);
        }

        private void Close()
        {
            ratesRoot.SetActive(false);
            relicResultsRoot.SetActive(false);
            root.SetActive(false);
            System.Action callback = _onClosed;
            _onClosed = null;
            callback?.Invoke();
        }

        private void Render(bool scrollToTop)
        {
            SaveData save = SaveSystem.Data;
            string today = Clock.Today;
            bool unlocked = EquipmentInventory.Unlocked(save);
            walletLabel.text = _relicMode
                ? $"<sprite name=gem>보석 {Wallet.Gems(save):N0} · <sprite name=ancient_shard>고대 파편 {RelicService.Shards(save):N0}"
                : $"골드 {save.gold:N0} · 보석 {Wallet.Gems(save):N0} · 고급 소환권 {save.ItemCount(Wallet.Ticket)} · 장비 조각 {save.ItemCount(Wallet.GearShard)}";
            relicSection.SetActive(_relicMode);
            equipmentSection.SetActive(!_relicMode);
            equipmentTabButton.interactable = _relicMode;
            relicTabButton.interactable = !_relicMode;
            if (_relicMode)
            {
                RenderRelics(save, today);
                return;
            }

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

        private void RenderRelics(SaveData save, string today)
        {
            bool unlocked = RelicService.Unlocked(save) && RelicGachaService.CanOpen(relics);
            int shards = RelicService.Shards(save);
            int gems = Wallet.Gems(save);
            int missing = 0;
            if (relics != null)
                foreach (RelicData relic in relics.relics)
                    if (relic != null && RelicService.Stars(save, relic.id) == 0) missing++;
            int pityLeft = unlocked ? RelicGachaService.PullsUntilPity(save, relics) : 0;
            relicInfoLabel.text = !RelicService.Unlocked(save)
                ? "2구간 보스를 깨면 유물 뽑기가 열려요. 첫 클리어 유물은 보관돼요."
                : $"유물 뽑기 · 없는 유물 {missing}종 · 고대 파편 {shards:N0}\n누적 {save.relicGacha.totalPulls}회 · {(pityLeft > 0 ? $"새 유물 보장까지 {pityLeft}회" : "24종 모두 보유: 새 유물 보장 종료")}";
            if (relics == null)
            {
                relicInfoLabel.text = "유물 데이터가 없어 뽑기를 열 수 없어요.";
                relicPayButton.interactable = relic1Button.interactable = relic10Button.interactable = relicFreeButton.interactable = relicRatesButton.interactable = false;
                return;
            }
            relicPayLabel.text = _payWithGems ? $"결제: 보석 {gems:N0} (눌러서 파편으로)" : $"결제: 고대 파편 {shards:N0} (눌러서 보석으로)";
            int oneCost = _payWithGems ? relics.pullGemCost : relics.pullShardCost;
            int tenCost = _payWithGems ? relics.tenPullGemCost : relics.tenPullShardCost;
            string icon = _payWithGems ? "gem" : "ancient_shard";
            int balance = _payWithGems ? gems : shards;
            relic1Label.text = $"1회 · <sprite name={icon}>{oneCost:N0}";
            relic10Label.text = $"10회 · <sprite name={icon}>{tenCost:N0}";
            relic1Button.interactable = unlocked && balance >= oneCost;
            relic10Button.interactable = unlocked && balance >= tenCost;
            bool free = RelicGachaService.CanFree(save, today);
            relicFreeLabel.text = free ? "무료 1회 (오늘)" : "오늘 무료는 받았어요";
            relicFreeButton.interactable = unlocked && free;
            relicPayButton.interactable = unlocked;
            relicRatesButton.interactable = relics != null;
        }

        private void BuyRelics(int count, RelicGachaService.Pay pay)
        {
            if (!RelicGachaService.CanOpen(relics))
            {
                relicMessageLabel.text = "<color=#FF7A6B>유물 데이터에 문제가 있어 뽑을 수 없어요. 재화는 그대로예요.</color>";
                return;
            }

            List<RelicGachaService.Pull> pulls = null;
            SaveSystem.CommitResult result = SaveSystem.Commit(() =>
                RelicGachaService.TryPull(SaveSystem.Data, relics, count, pay, Clock.Today, Clock.UnixSeconds, out pulls));
            if (result == SaveSystem.CommitResult.SaveFailed)
                relicMessageLabel.text = SaveSystem.SaveFailedText;
            else if (result == SaveSystem.CommitResult.Rejected)
                relicMessageLabel.text = "<color=#FFB347>재화가 부족하거나 오늘 무료 뽑기를 이미 받았어요.</color>";
            else
            {
                relicMessageLabel.text = $"유물 {pulls.Count}개를 받았어요. 결과를 확인하세요.";
                ShowRelicResults(pulls);
            }

            Render(false);
        }

        private void ShowRelicResults(List<RelicGachaService.Pull> pulls)
        {
            relicResultsLabel.text = $"유물 뽑기 결과 · {pulls.Count}개";
            for (int i = 0; i < relicResultLabels.Length; i++)
            {
                bool visible = i < pulls.Count;
                relicResultLabels[i].transform.parent.gameObject.SetActive(visible);
                if (!visible) continue;
                RelicGachaService.Pull pull = pulls[i];
                relicResultIcons[i].sprite = pull.Data.icon;
                relicResultIcons[i].gameObject.SetActive(pull.Data.icon != null);
                string state = pull.IsNew ? "<color=#7CE38B>NEW</color>" : pull.StarUp ? "<color=#FFD966>★UP</color>"
                    : pull.Shards > 0 ? $"<sprite name=ancient_shard>+{pull.Shards}" : $"다음 별 {pull.Duplicates}/{relics.DuplicatesNeeded(pull.Stars)}";
                string pity = pull.FromPity ? " · 새 유물 보장" : string.Empty;
                relicResultLabels[i].text = $"{pull.Data.displayName}\n<color=#FFD966>★{pull.Stars}</color>  {state}{pity}";
            }

            relicResultsRoot.SetActive(true);
            Canvas.ForceUpdateCanvases();
            relicResultsScroll.verticalNormalizedPosition = 1f;
        }

        private void RenderExchange(SaveData save, bool scrollToTop)
        {
            EquipmentTable table = gacha.equipment;
            int shards = save.ItemCount(Wallet.GearShard);
            bool full = EquipmentInventory.BagFull(save, table);
            var rows = new List<ButtonList.Row>();
            if (full) rows.Add(new ButtonList.Row("<color=#FFB347>가방이 가득 찼어요 (200칸). 분해·합성으로 자리를 비우면 교환할 수 있어요</color>", RowDisabled, null, false));
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
                    bool ok = shards >= cost && !full;
                    rows.Add(new ButtonList.Row(text, ok ? RowNormal : RowDisabled, () => Exchange(id), ok));
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
            SaveSystem.CommitResult r = SaveSystem.Commit(() => Meta.HeroGrowth.TryBuyHeroShard(SaveSystem.Data, heroId, Clock.Today));
            if (r == SaveSystem.CommitResult.Rejected) return;
            messageLabel.text = r == SaveSystem.CommitResult.Saved
                ? $"<color=#7CE38B>영웅 조각 +1 (가진 것 {Meta.HeroGrowth.Shards(SaveSystem.Data, heroId)})</color>"
                : SaveSystem.SaveFailedText;
            Render(false);
        }

        // ---------------------------------------------------------------- 사기

        private void Buy(BoxRates box, int count, GachaService.Pay pay)
        {
            if (!GachaService.CanOpen(gacha, box))
            {
                messageLabel.text = "<color=#FF7A6B>상자 데이터에 문제가 있어 열 수 없어요 (값은 빠지지 않았어요)</color>";
                return;
            }

            // 재화 차감 + 결과 + 천장 카운터가 한 번에 저장된 뒤에만 결과를 보여준다. 저장을 못 하면 전부 되돌린다
            List<GachaService.Pull> pulls = null;
            SaveSystem.CommitResult r = SaveSystem.Commit(() =>
            {
                SaveData save = SaveSystem.Data;
                if (!EquipmentInventory.Unlocked(save) || !GachaService.TryPay(save, gacha, box, count, pay, Clock.Today)) return false;
                pulls = GachaService.Open(save, gacha, box, count, Clock.UnixSeconds);
                return true;
            });

            if (r == SaveSystem.CommitResult.SaveFailed)
            {
                messageLabel.text = SaveSystem.SaveFailedText;
                Render(false);
                return;
            }

            if (r == SaveSystem.CommitResult.Saved) ShowResults(box, pulls);
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
            EquipmentInventory.AddResult? got = null;
            SaveSystem.CommitResult r = SaveSystem.Commit(() =>
            {
                got = EquipmentInventory.TryExchange(SaveSystem.Data, gacha.equipment, dataId);
                return got.HasValue;
            });

            if (r == SaveSystem.CommitResult.Rejected)
            {
                if (EquipmentInventory.BagFull(SaveSystem.Data, gacha.equipment)) messageLabel.text = "<color=#FFB347>가방이 가득 찼어요. 장비 화면에서 분해·합성하고 다시 와 주세요</color>";
                return;
            }

            EquipmentData data = gacha.equipment.Find(dataId);
            messageLabel.text = r == SaveSystem.CommitResult.Saved
                ? "교환: " + EquipmentInventory.AddLine(got.Value, data).Replace("장비  ", string.Empty)
                : SaveSystem.SaveFailedText;
            Render(false);
        }

        private void ShowRates()
        {
            if (_relicMode)
            {
                ratesLabel.text = RelicGachaService.RatesText(SaveSystem.Data, relics)
                    + "\n<size=85%><color=#A9B4C2>사용한 보석·뽑기는 철회가 제한됩니다. 이 버전은 실제 결제가 없는 시험판입니다.</color></size>";
                ratesRoot.SetActive(true);
                Canvas.ForceUpdateCanvases();
                ratesScroll.verticalNormalizedPosition = 1f;
                return;
            }
            ratesLabel.text = GachaService.RatesText(gacha, gacha.premium) + "\n" + GachaService.RatesText(gacha, gacha.normal) +
                              "\n<size=85%><color=#A9B4C2>화면의 확률과 실제 추첨은 같은 표를 씁니다. 이 버전은 가짜 보석이라 실제 결제는 없어요</color></size>";
            ratesRoot.SetActive(true);
            Canvas.ForceUpdateCanvases();
            ratesScroll.verticalNormalizedPosition = 1f;
        }
    }
}
