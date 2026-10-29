using HistoryHeroes.Equipment;
using HistoryHeroes.Meta;
using HistoryHeroes.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace HistoryHeroes.EditorTools
{
    /// <summary>v0.4 판 밖 성장 화면 (장비, 상점). 로비 장면에 들어간다</summary>
    public static partial class SceneBuilder
    {
        /// <summary>위아래로 미는 버튼 목록: 보이는 칸(viewport) + 내용(세로 배치) + 줄 모양 하나</summary>
        private static ButtonList BuildButtonList(Transform parent, string name, Vector2 position, Vector2 size, float rowHeight = 96f, float fontSize = 27f)
        {
            RectTransform viewport = Rect(name, parent, Center, Center, Center, position, size);
            Img(viewport, new Color(0f, 0f, 0f, 0.2f), null).raycastTarget = true;
            viewport.gameObject.AddComponent<RectMask2D>();

            RectTransform content = Rect("Content", viewport, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), Vector2.zero, new Vector2(0f, 0f));
            var layout = content.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 8f;
            layout.padding = new RectOffset(8, 8, 8, 8);
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            Button template = Btn("RowTemplate", content, "", Vector2.zero, new Vector2(size.x - 16f, rowHeight), RowColor, out TMP_Text label);
            label.fontSize = fontSize;
            label.alignment = TextAlignmentOptions.MidlineLeft;
            label.rectTransform.offsetMin = new Vector2(24f, 4f);
            label.rectTransform.offsetMax = new Vector2(-24f, -4f);
            template.gameObject.AddComponent<LayoutElement>().preferredHeight = rowHeight;

            var scroll = viewport.gameObject.AddComponent<ScrollRect>();
            scroll.viewport = viewport;
            scroll.content = content;
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 40f;

            var list = viewport.gameObject.AddComponent<ButtonList>();
            using (var props = new Props(list)) props.Ref("template", template).Ref("content", content).Ref("scroll", scroll);
            return list;
        }

        private static EquipmentPanel BuildEquipmentPanel(Transform parent, MetaTable meta)
        {
            RectTransform panel = Overlay(parent, "EquipmentPanel", out GameObject holder);
            var screen = holder.AddComponent<EquipmentPanel>();

            TMP_Text title = Label("Title", panel, "장비", 56, TextAlignmentOptions.Center, new Color(1f, 0.85f, 0.35f));
            Place(title.rectTransform, Center, Center, new Vector2(0f, 850f), new Vector2(900f, 80f));
            TMP_Text summary = Label("Summary", panel, "", 26, TextAlignmentOptions.Center);
            Place(summary.rectTransform, Center, Center, new Vector2(0f, 760f), new Vector2(1000f, 100f));

            var slotButtons = new Button[EquipmentInventory.SlotCount];
            var slotLabels = new TMP_Text[EquipmentInventory.SlotCount];
            EquipSlot[] layout = { EquipSlot.Weapon, EquipSlot.Armor, EquipSlot.Necklace, EquipSlot.Belt, EquipSlot.Gloves, EquipSlot.Boots };
            for (int i = 0; i < layout.Length; i++)
            {
                int index = (int)layout[i];
                int row = i / 2;
                int column = i % 2;
                slotButtons[index] = Btn($"Slot{index + 1}", panel, "", new Vector2(column == 0 ? -252f : 252f, 625f - row * 105f), new Vector2(496f, 90f), RowColor, out TMP_Text label);
                label.fontSize = 23;
                label.enableAutoSizing = true;
                label.fontSizeMin = 18f;
                label.fontSizeMax = 23f;
                label.alignment = TextAlignmentOptions.MidlineLeft;
                label.rectTransform.offsetMin = new Vector2(16f, 4f);
                label.rectTransform.offsetMax = new Vector2(-16f, -4f);
                slotLabels[index] = label;
            }

            Button enhance = Btn("Enhance", panel, "강화", new Vector2(-252f, 300f), new Vector2(496f, 90f), ButtonGreen, out TMP_Text enhanceLabel);
            enhanceLabel.fontSize = 28;
            Button enhanceMax = Btn("EnhanceMax", panel, "최대까지", new Vector2(252f, 300f), new Vector2(496f, 90f), ButtonGreen, out TMP_Text enhanceMaxLabel);
            enhanceMaxLabel.fontSize = 28;

            Button bag = Btn("BagTab", panel, "가방", new Vector2(-335f, 200f), new Vector2(325f, 84f), ButtonBlue, out TMP_Text bagLabel);
            bagLabel.fontSize = 30;
            Button synth = Btn("SynthTab", panel, "합성", new Vector2(0f, 200f), new Vector2(325f, 84f), ButtonBlue, out TMP_Text synthLabel);
            synthLabel.fontSize = 30;
            Button auto = Btn("AutoDismantle", panel, "중복 자동 분해", new Vector2(335f, 200f), new Vector2(325f, 84f), ButtonGray, out TMP_Text autoLabel);
            autoLabel.fontSize = 24;

            ButtonList list = BuildButtonList(panel, "List", new Vector2(0f, -170f), new Vector2(1000f, 620f));

            TMP_Text detail = Label("Detail", panel, "", 26, TextAlignmentOptions.Center);
            Place(detail.rectTransform, Center, Center, new Vector2(0f, -550f), new Vector2(1000f, 120f));

            Button equip = Btn("Equip", panel, "장착", new Vector2(-335f, -675f), new Vector2(325f, 96f), ButtonGreen, out TMP_Text equipLabel);
            Button lockButton = Btn("Lock", panel, "잠그기", new Vector2(0f, -675f), new Vector2(325f, 96f), ButtonBlue, out TMP_Text lockLabel);
            Button dismantle = Btn("Dismantle", panel, "분해", new Vector2(335f, -675f), new Vector2(325f, 96f), new Color(0.5f, 0.25f, 0.25f), out TMP_Text dismantleLabel);
            dismantleLabel.fontSize = 26;
            Button back = Btn("Back", panel, "뒤로", new Vector2(0f, -800f), new Vector2(400f, 100f), ButtonGray, out _);

            using (var props = new Props(screen))
            {
                props.Ref("root", panel.gameObject).Ref("metaTable", meta).Ref("titleLabel", title).Ref("summaryLabel", summary)
                    .Refs("slotButtons", slotButtons).Refs("slotLabels", slotLabels)
                    .Ref("enhanceButton", enhance).Ref("enhanceLabel", enhanceLabel).Ref("enhanceMaxButton", enhanceMax).Ref("enhanceMaxLabel", enhanceMaxLabel)
                    .Ref("bagTab", bag).Ref("synthTab", synth).Ref("autoButton", auto).Ref("autoLabel", autoLabel)
                    .Ref("list", list).Ref("detailLabel", detail)
                    .Ref("equipButton", equip).Ref("equipLabel", equipLabel).Ref("lockButton", lockButton).Ref("lockLabel", lockLabel)
                    .Ref("dismantleButton", dismantle).Ref("dismantleLabel", dismantleLabel).Ref("backButton", back);
            }

            return screen;
        }

        private static ShopPanel BuildShopPanel(Transform parent, GachaTable gacha)
        {
            RectTransform panel = Overlay(parent, "ShopPanel", out GameObject holder);
            var screen = holder.AddComponent<ShopPanel>();

            TMP_Text title = Label("Title", panel, "상점", 56, TextAlignmentOptions.Center, new Color(1f, 0.85f, 0.35f));
            Place(title.rectTransform, Center, Center, new Vector2(0f, 850f), new Vector2(900f, 80f));
            TMP_Text wallet = Label("Wallet", panel, "", 26, TextAlignmentOptions.Center);
            Place(wallet.rectTransform, Center, Center, new Vector2(0f, 775f), new Vector2(1000f, 50f));

            Button equipmentTab = Btn("EquipmentTab", panel, "장비 상자", new Vector2(-252f, 690f), new Vector2(496f, 78f), ButtonBlue, out _);
            Button relicTab = Btn("RelicTab", panel, "유물 뽑기", new Vector2(252f, 690f), new Vector2(496f, 78f), ButtonBlue, out _);

            RectTransform equipmentSection = Rect("EquipmentSection", panel, Vector2.zero, Vector2.one, Center, Vector2.zero, Vector2.zero);
            Button normalBuy = Btn("NormalBuy", equipmentSection, "일반 상자", new Vector2(-252f, 560f), new Vector2(496f, 100f), ButtonBlue, out TMP_Text normalBuyLabel);
            normalBuyLabel.fontSize = 28;
            Button normalFree = Btn("NormalFree", equipmentSection, "광고 보고 무료", new Vector2(252f, 560f), new Vector2(496f, 100f), ButtonBlue, out TMP_Text normalFreeLabel);
            normalFreeLabel.fontSize = 28;

            TMP_Text premiumInfo = Label("PremiumInfo", equipmentSection, "", 28, TextAlignmentOptions.Center);
            Place(premiumInfo.rectTransform, Center, Center, new Vector2(0f, 460f), new Vector2(1000f, 60f));
            Button premium1 = Btn("Premium1", equipmentSection, "1회", new Vector2(-335f, 370f), new Vector2(325f, 100f), new Color(0.45f, 0.32f, 0.12f), out TMP_Text premium1Label);
            premium1Label.fontSize = 27;
            Button premium10 = Btn("Premium10", equipmentSection, "10연", new Vector2(0f, 370f), new Vector2(325f, 100f), new Color(0.45f, 0.32f, 0.12f), out TMP_Text premium10Label);
            premium10Label.fontSize = 27;
            Button ticket = Btn("Ticket", equipmentSection, "소환권", new Vector2(335f, 370f), new Vector2(325f, 100f), new Color(0.45f, 0.32f, 0.12f), out TMP_Text ticketLabel);
            ticketLabel.fontSize = 25;

            Button rates = Btn("Rates", equipmentSection, "확률 보기", new Vector2(-335f, 265f), new Vector2(325f, 84f), ButtonGray, out TMP_Text ratesButtonLabel);
            ratesButtonLabel.fontSize = 28;
            Button exchange = Btn("Exchange", equipmentSection, "조각 교환소", new Vector2(0f, 265f), new Vector2(325f, 84f), ButtonGray, out TMP_Text exchangeLabel);
            exchangeLabel.fontSize = 28;
            Button goldShop = Btn("GoldShop", equipmentSection, "골드 상점", new Vector2(335f, 265f), new Vector2(325f, 84f), ButtonGray, out TMP_Text goldShopLabel);
            goldShopLabel.fontSize = 28;

            TMP_Text message = Label("Message", equipmentSection, "", 26, TextAlignmentOptions.Center);
            Place(message.rectTransform, Center, Center, new Vector2(0f, 165f), new Vector2(1000f, 60f));
            ButtonList list = BuildButtonList(equipmentSection, "List", new Vector2(0f, -285f), new Vector2(1000f, 810f), 90f, 26f);
            Button back = Btn("Back", panel, "뒤로", new Vector2(0f, -800f), new Vector2(400f, 100f), ButtonGray, out _);

            // 장비 상자와 같은 화면 안에 두어 도감에서도 한 화면만 열면 된다.
            RectTransform relicSection = Rect("RelicSection", panel, Center, Center, Center, new Vector2(0f, -20f), new Vector2(1040f, 1320f));
            Img(relicSection, PanelColor, null).raycastTarget = true;
            TMP_Text relicInfo = Label("RelicInfo", relicSection, "", 34, TextAlignmentOptions.Center);
            Place(relicInfo.rectTransform, Center, Center, new Vector2(0f, 545f), new Vector2(1000f, 160f));
            Button relicPay = Btn("RelicPay", relicSection, "결제 재화", new Vector2(0f, 395f), new Vector2(820f, 86f), ButtonBlue, out TMP_Text relicPayLabel);
            relicPayLabel.fontSize = 30f;
            Button relic1 = Btn("Relic1", relicSection, "1회", new Vector2(-252f, 140f), new Vector2(496f, 108f), ButtonBlue, out TMP_Text relic1Label);
            Button relic10 = Btn("Relic10", relicSection, "10회", new Vector2(252f, 140f), new Vector2(496f, 108f), ButtonGreen, out TMP_Text relic10Label);
            Button relicFree = Btn("RelicFree", relicSection, "무료 1회 (오늘)", new Vector2(0f, 15f), new Vector2(700f, 90f), ButtonBlue, out TMP_Text relicFreeLabel);
            Button relicRates = Btn("RelicRates", relicSection, "확률 보기", new Vector2(0f, -100f), new Vector2(500f, 86f), ButtonBlue, out _);
            TMP_Text relicNotice = Label("RelicNotice", relicSection,
                "<color=#A9B4C2>확률·보장·중복 규칙은 확률 보기에서 확인하세요</color>", 28, TextAlignmentOptions.Center);
            Place(relicNotice.rectTransform, Center, Center, new Vector2(0f, -215f), new Vector2(980f, 80f));
            TMP_Text relicLegal = Label("RelicLegal", relicSection,
                "<color=#A9B4C2>보석으로 뽑을 수 있습니다. 사용한 보석·뽑기는 철회가 제한됩니다. 이 버전은 실제 결제가 없는 시험판입니다.</color>", 27, TextAlignmentOptions.Center);
            Place(relicLegal.rectTransform, Center, Center, new Vector2(0f, 280f), new Vector2(980f, 110f));
            TMP_Text relicMessage = Label("RelicMessage", relicSection, "", 30, TextAlignmentOptions.Center);
            Place(relicMessage.rectTransform, Center, Center, new Vector2(0f, -335f), new Vector2(980f, 100f));

            // 확률 보기 (덮는 판)
            RectTransform ratesPanel = Rect("RatesPanel", panel, Vector2.zero, Vector2.one, Center, Vector2.zero, Vector2.zero);
            Img(ratesPanel, PanelColor, null).raycastTarget = true; // 뒤 화면이 비치지 않게
            TMP_Text ratesTitle = Label("RatesTitle", ratesPanel, "확률 보기", 56, TextAlignmentOptions.Center, new Color(1f, 0.85f, 0.35f));
            Place(ratesTitle.rectTransform, Center, Center, new Vector2(0f, 820f), new Vector2(900f, 90f));
            RectTransform ratesViewport = Rect("RatesViewport", ratesPanel, Center, Center, Center, new Vector2(0f, 70f), new Vector2(1000f, 1250f));
            Img(ratesViewport, new Color(0f, 0f, 0f, 0.25f), null).raycastTarget = true;
            ratesViewport.gameObject.AddComponent<RectMask2D>();
            RectTransform ratesContent = Rect("Content", ratesViewport, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), Vector2.zero, new Vector2(0f, 100f));
            var ratesLayout = ratesContent.gameObject.AddComponent<VerticalLayoutGroup>();
            ratesLayout.padding = new RectOffset(24, 24, 24, 36);
            ratesLayout.childControlHeight = true;
            ratesLayout.childControlWidth = true;
            ratesLayout.childForceExpandHeight = false;
            ratesContent.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            TMP_Text ratesText = Label("RatesText", ratesContent, "", 30, TextAlignmentOptions.TopLeft);
            ratesText.lineSpacing = 8f;
            ScrollRect ratesScroll = ratesViewport.gameObject.AddComponent<ScrollRect>();
            ratesScroll.viewport = ratesViewport;
            ratesScroll.content = ratesContent;
            ratesScroll.horizontal = false;
            ratesScroll.movementType = ScrollRect.MovementType.Clamped;
            ratesScroll.scrollSensitivity = 40f;
            Button ratesClose = Btn("RatesClose", ratesPanel, "닫기", new Vector2(0f, -700f), new Vector2(400f, 100f), ButtonGray, out _);

            RectTransform resultsPanel = Rect("RelicResults", panel, Vector2.zero, Vector2.one, Center, Vector2.zero, Vector2.zero);
            Img(resultsPanel, PanelColor, null).raycastTarget = true;
            TMP_Text resultsTitle = Label("ResultsTitle", resultsPanel, "유물 뽑기 결과", 56, TextAlignmentOptions.Center, new Color(1f, 0.85f, 0.35f));
            Place(resultsTitle.rectTransform, Center, Center, new Vector2(0f, 820f), new Vector2(900f, 90f));
            TMP_Text resultsLabel = Label("ResultsLabel", resultsPanel, "", 34, TextAlignmentOptions.Center);
            Place(resultsLabel.rectTransform, Center, Center, new Vector2(0f, 730f), new Vector2(1000f, 60f));
            RectTransform resultsViewport = Rect("ResultsViewport", resultsPanel, Center, Center, Center, new Vector2(0f, -10f), new Vector2(1000f, 1330f));
            Img(resultsViewport, new Color(0f, 0f, 0f, 0.25f), null).raycastTarget = true;
            resultsViewport.gameObject.AddComponent<RectMask2D>();
            RectTransform resultsContent = Rect("Content", resultsViewport, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), Vector2.zero, new Vector2(0f, 1190f));
            var grid = resultsContent.gameObject.AddComponent<GridLayoutGroup>();
            grid.cellSize = new Vector2(478f, 210f);
            grid.spacing = new Vector2(12f, 16f);
            grid.padding = new RectOffset(12, 12, 12, 12);
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = 2;
            var resultIcons = new Image[10];
            var resultLabels = new TMP_Text[10];
            for (int i = 0; i < 10; i++)
            {
                RectTransform card = Rect($"RelicCard{i + 1}", resultsContent, Center, Center, Center, Vector2.zero, grid.cellSize);
                Img(card, RowColor, null);
                Image icon = Rect("Icon", card, Center, Center, Center, new Vector2(-164f, 0f), new Vector2(100f, 100f)).gameObject.AddComponent<Image>();
                icon.preserveAspect = true;
                icon.raycastTarget = false;
                TMP_Text label = Label("CardText", card, "", 29, TextAlignmentOptions.MidlineLeft);
                Place(label.rectTransform, Center, Center, new Vector2(55f, 0f), new Vector2(340f, 180f));
                resultIcons[i] = icon;
                resultLabels[i] = label;
            }
            ScrollRect resultsScroll = resultsViewport.gameObject.AddComponent<ScrollRect>();
            resultsScroll.viewport = resultsViewport;
            resultsScroll.content = resultsContent;
            resultsScroll.horizontal = false;
            resultsScroll.movementType = ScrollRect.MovementType.Clamped;
            resultsScroll.scrollSensitivity = 40f;
            Button resultsClose = Btn("RelicResultsClose", resultsPanel, "닫기", new Vector2(0f, -800f), new Vector2(400f, 100f), ButtonBlue, out _);

            using (var props = new Props(screen))
            {
                props.Ref("root", panel.gameObject).Ref("gacha", gacha).Ref("walletLabel", wallet)
                    .Ref("equipmentTabButton", equipmentTab).Ref("relicTabButton", relicTab).Ref("relicSection", relicSection.gameObject)
                    .Ref("equipmentSection", equipmentSection.gameObject).Ref("relicInfoLabel", relicInfo).Ref("relicPayButton", relicPay).Ref("relicPayLabel", relicPayLabel)
                    .Ref("relic1Button", relic1).Ref("relic1Label", relic1Label).Ref("relic10Button", relic10).Ref("relic10Label", relic10Label)
                    .Ref("relicFreeButton", relicFree).Ref("relicFreeLabel", relicFreeLabel).Ref("relicRatesButton", relicRates)
                    .Ref("relicMessageLabel", relicMessage).Ref("relicResultsRoot", resultsPanel.gameObject).Ref("relicResultsLabel", resultsLabel)
                    .Refs("relicResultIcons", resultIcons).Refs("relicResultLabels", resultLabels).Ref("relicResultsCloseButton", resultsClose)
                    .Ref("relicResultsScroll", resultsScroll).Ref("ratesScroll", ratesScroll)
                    .Ref("normalBuyButton", normalBuy).Ref("normalBuyLabel", normalBuyLabel).Ref("normalFreeButton", normalFree).Ref("normalFreeLabel", normalFreeLabel)
                    .Ref("premiumInfoLabel", premiumInfo).Ref("premium1Button", premium1).Ref("premium1Label", premium1Label)
                    .Ref("premium10Button", premium10).Ref("premium10Label", premium10Label).Ref("ticketButton", ticket).Ref("ticketLabel", ticketLabel)
                    .Ref("ratesButton", rates).Ref("exchangeButton", exchange).Ref("exchangeLabel", exchangeLabel).Ref("messageLabel", message)
                    .Ref("list", list).Ref("ratesRoot", ratesPanel.gameObject).Ref("ratesLabel", ratesText).Ref("ratesCloseButton", ratesClose)
                    .Ref("backButton", back).Ref("goldShopButton", goldShop).Ref("goldShopLabel", goldShopLabel);
            }

            return screen;
        }

        private static RunePanel BuildRunePanel(Transform parent, MetaTable meta, Skills.SkillDatabase database)
        {
            RectTransform panel = Overlay(parent, "RunePanel", out GameObject holder);
            var screen = holder.AddComponent<RunePanel>();

            TMP_Text title = Label("Title", panel, "룬", 56, TextAlignmentOptions.Center, new Color(1f, 0.85f, 0.35f));
            Place(title.rectTransform, Center, Center, new Vector2(0f, 850f), new Vector2(900f, 80f));
            TMP_Text info = Label("Info", panel, "", 26, TextAlignmentOptions.Center);
            Place(info.rectTransform, Center, Center, new Vector2(0f, 765f), new Vector2(1000f, 80f));

            var slotButtons = new Button[2];
            var slotLabels = new TMP_Text[2];
            for (int i = 0; i < 2; i++)
            {
                slotButtons[i] = Btn($"RuneSlot{i + 1}", panel, "", new Vector2(-252f + i * 504f, 660f), new Vector2(496f, 100f), RowColor, out TMP_Text label);
                label.fontSize = 28;
                slotLabels[i] = label;
            }

            ButtonList list = BuildButtonList(panel, "List", new Vector2(0f, 120f), new Vector2(1000f, 940f), 100f, 27f);
            TMP_Text detail = Label("Detail", panel, "", 27, TextAlignmentOptions.Center);
            Place(detail.rectTransform, Center, Center, new Vector2(0f, -470f), new Vector2(1000f, 150f));
            detail.lineSpacing = 6f;

            Button equip = Btn("RuneEquip", panel, "끼기", new Vector2(-335f, -640f), new Vector2(325f, 100f), ButtonGreen, out TMP_Text equipLabel);
            equipLabel.fontSize = 27;
            Button upgrade = Btn("RuneUpgrade", panel, "강화", new Vector2(0f, -640f), new Vector2(325f, 100f), new Color(0.5f, 0.4f, 0.15f), out TMP_Text upgradeLabel);
            upgradeLabel.fontSize = 26;
            Button remove = Btn("RuneRemove", panel, "칸 비우기", new Vector2(335f, -640f), new Vector2(325f, 100f), ButtonGray, out TMP_Text removeLabel);
            removeLabel.fontSize = 27;
            Button back = Btn("Back", panel, "뒤로", new Vector2(0f, -790f), new Vector2(400f, 100f), ButtonGray, out _);

            using (var props = new Props(screen))
            {
                props.Ref("root", panel.gameObject).Ref("metaTable", meta).Ref("database", database).Ref("titleLabel", title).Ref("infoLabel", info)
                    .Refs("slotButtons", slotButtons).Refs("slotLabels", slotLabels).Ref("list", list).Ref("detailLabel", detail)
                    .Ref("equipButton", equip).Ref("equipLabel", equipLabel).Ref("upgradeButton", upgrade).Ref("upgradeLabel", upgradeLabel)
                    .Ref("removeButton", remove).Ref("backButton", back);
            }

            return screen;
        }

        /// <summary>v1.0 신규 7일 미션 + 출석부</summary>
        private static NewcomerPanel BuildNewcomerPanel(Transform parent)
        {
            RectTransform panel = Overlay(parent, "NewcomerPanel", out GameObject holder);
            var screen = holder.AddComponent<NewcomerPanel>();
            TMP_Text title = Label("Title", panel, "신규 7일 미션", 60, TextAlignmentOptions.Center, new Color(1f, 0.85f, 0.35f));
            Place(title.rectTransform, Center, Center, new Vector2(0f, 840f), new Vector2(900f, 90f));
            TMP_Text attendance = Label("Attendance", panel, "", 32, TextAlignmentOptions.Center);
            Place(attendance.rectTransform, Center, Center, new Vector2(0f, 730f), new Vector2(1000f, 110f));
            ButtonList list = BuildButtonList(panel, "List", new Vector2(0f, -20f), new Vector2(1000f, 1340f), 150f, 28f);
            Button back = Btn("Back", panel, "뒤로", new Vector2(0f, -790f), new Vector2(400f, 100f), ButtonGray, out _);
            using (var props = new Props(screen)) props.Ref("root", panel.gameObject).Ref("titleLabel", title).Ref("attendanceLabel", attendance).Ref("list", list).Ref("backButton", back);
            return screen;
        }

        private static ChoicePanel BuildChoicePanel(Transform parent)
        {
            RectTransform panel = Overlay(parent, "ChoicePanel", out GameObject holder);
            var screen = holder.AddComponent<ChoicePanel>();
            TMP_Text title = Label("Title", panel, "", 56, TextAlignmentOptions.Center, new Color(1f, 0.85f, 0.35f));
            Place(title.rectTransform, Center, Center, new Vector2(0f, 820f), new Vector2(900f, 90f));
            TMP_Text info = Label("Info", panel, "", 30, TextAlignmentOptions.Center);
            Place(info.rectTransform, Center, Center, new Vector2(0f, 730f), new Vector2(1000f, 70f));
            ButtonList list = BuildButtonList(panel, "List", new Vector2(0f, 40f), new Vector2(1000f, 1240f), 120f, 30f);
            Button later = Btn("Later", panel, "나중에", new Vector2(0f, -760f), new Vector2(400f, 100f), ButtonGray, out _);
            using (var props = new Props(screen))
            {
                props.Ref("root", panel.gameObject).Ref("titleLabel", title).Ref("infoLabel", info).Ref("list", list).Ref("laterButton", later);
            }

            return screen;
        }

        private static PetPanel BuildPetPanel(Transform parent, MetaTable meta)
        {
            RectTransform panel = Overlay(parent, "PetPanel", out GameObject holder);
            var screen = holder.AddComponent<PetPanel>();

            TMP_Text title = Label("Title", panel, "펫", 56, TextAlignmentOptions.Center, new Color(1f, 0.85f, 0.35f));
            Place(title.rectTransform, Center, Center, new Vector2(0f, 850f), new Vector2(900f, 80f));
            TMP_Text info = Label("Info", panel, "", 26, TextAlignmentOptions.Center);
            Place(info.rectTransform, Center, Center, new Vector2(0f, 765f), new Vector2(1000f, 80f));

            ButtonList list = BuildButtonList(panel, "List", new Vector2(0f, 420f), new Vector2(1000f, 560f), 128f, 30f);
            TMP_Text detail = Label("Detail", panel, "", 32, TextAlignmentOptions.Top);
            Place(detail.rectTransform, Center, Center, new Vector2(0f, -240f), new Vector2(1000f, 660f));
            detail.lineSpacing = 8f;

            Button equip = Btn("PetEquip", panel, "장착", new Vector2(-250f, -660f), new Vector2(440f, 110f), ButtonGreen, out TMP_Text equipLabel);
            equipLabel.fontSize = 30;
            Button level = Btn("PetLevelUp", panel, "레벨업", new Vector2(250f, -660f), new Vector2(440f, 110f), new Color(0.5f, 0.4f, 0.15f), out TMP_Text levelLabel);
            levelLabel.fontSize = 27;
            Button back = Btn("Back", panel, "뒤로", new Vector2(0f, -800f), new Vector2(400f, 100f), ButtonGray, out _);

            using (var props = new Props(screen))
            {
                props.Ref("root", panel.gameObject).Ref("metaTable", meta).Ref("titleLabel", title).Ref("infoLabel", info)
                    .Ref("list", list).Ref("detailLabel", detail).Ref("equipButton", equip).Ref("equipLabel", equipLabel)
                    .Ref("levelButton", level).Ref("levelLabel", levelLabel).Ref("backButton", back);
            }

            return screen;
        }

        private static RelicPanel BuildRelicPanel(Transform parent, HistoryHeroes.Relics.RelicTable relics)
        {
            RectTransform panel = Overlay(parent, "RelicPanel", out GameObject holder);
            var screen = holder.AddComponent<RelicPanel>();

            TMP_Text title = Label("Title", panel, "유물 도감", 56, TextAlignmentOptions.Center, new Color(1f, 0.85f, 0.35f));
            Place(title.rectTransform, Center, Center, new Vector2(0f, 850f), new Vector2(900f, 80f));
            TMP_Text summary = Label("Summary", panel, "", 26, TextAlignmentOptions.Center);
            Place(summary.rectTransform, Center, Center, new Vector2(0f, 760f), new Vector2(1000f, 100f));
            ButtonList list = BuildButtonList(panel, "List", new Vector2(0f, 90f), new Vector2(1000f, 1180f), 100f, 27f);
            TMP_Text detail = Label("Detail", panel, "", 26, TextAlignmentOptions.Center);
            Place(detail.rectTransform, Center, Center, new Vector2(65f, -575f), new Vector2(850f, 110f));
            Image detailIcon = Rect("DetailIcon", panel, Center, Center, Center, new Vector2(-440f, -575f), new Vector2(90f, 90f)).gameObject.AddComponent<Image>();
            detailIcon.preserveAspect = true;
            detailIcon.raycastTarget = false;
            detailIcon.gameObject.SetActive(false);
            Button draw = Btn("RelicDraw", panel, "뽑기", new Vector2(-220f, -720f), new Vector2(400f, 110f), ButtonBlue, out _);
            Button back = Btn("Back", panel, "뒤로", new Vector2(220f, -720f), new Vector2(400f, 110f), ButtonBlue, out _);

            using (var props = new Props(screen))
            {
                props.Ref("root", panel.gameObject).Ref("table", relics).Ref("summaryLabel", summary).Ref("list", list)
                    .Ref("detailLabel", detail).Ref("detailIcon", detailIcon).Ref("drawButton", draw).Ref("backButton", back);
            }

            return screen;
        }

        private static TrainingPanel BuildTrainingPanel(Transform parent, MetaTable meta)
        {
            RectTransform panel = Overlay(parent, "TrainingPanel", out GameObject holder);
            var screen = holder.AddComponent<TrainingPanel>();

            TMP_Text title = Label("Title", panel, "수련", 56, TextAlignmentOptions.Center, new Color(1f, 0.85f, 0.35f));
            Place(title.rectTransform, Center, Center, new Vector2(0f, 820f), new Vector2(900f, 80f));
            TMP_Text info = Label("Info", panel, "", 26, TextAlignmentOptions.Center);
            Place(info.rectTransform, Center, Center, new Vector2(0f, 730f), new Vector2(1000f, 80f));
            ButtonList list = BuildButtonList(panel, "List", new Vector2(0f, 80f), new Vector2(1000f, 1160f), 150f, 30f);
            TMP_Text message = Label("Message", panel, "", 28, TextAlignmentOptions.Center);
            Place(message.rectTransform, Center, Center, new Vector2(0f, -560f), new Vector2(1000f, 60f));
            Button back = Btn("Back", panel, "뒤로", new Vector2(0f, -700f), new Vector2(400f, 110f), ButtonGray, out _);

            using (var props = new Props(screen))
            {
                props.Ref("root", panel.gameObject).Ref("metaTable", meta).Ref("infoLabel", info).Ref("list", list)
                    .Ref("messageLabel", message).Ref("backButton", back);
            }

            return screen;
        }
    }
}
