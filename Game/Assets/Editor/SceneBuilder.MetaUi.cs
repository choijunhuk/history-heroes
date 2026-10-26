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

            var slotButtons = new Button[3];
            var slotLabels = new TMP_Text[3];
            for (int i = 0; i < 3; i++)
            {
                slotButtons[i] = Btn($"Slot{i + 1}", panel, "", new Vector2(0f, 640f - i * 112f), new Vector2(1000f, 100f), RowColor, out TMP_Text label);
                label.fontSize = 28;
                label.alignment = TextAlignmentOptions.MidlineLeft;
                label.rectTransform.offsetMin = new Vector2(28f, 4f);
                label.rectTransform.offsetMax = new Vector2(-28f, -4f);
                slotLabels[i] = label;
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

            Button normalBuy = Btn("NormalBuy", panel, "일반 상자", new Vector2(-252f, 680f), new Vector2(496f, 100f), ButtonBlue, out TMP_Text normalBuyLabel);
            normalBuyLabel.fontSize = 28;
            Button normalFree = Btn("NormalFree", panel, "광고 보고 무료", new Vector2(252f, 680f), new Vector2(496f, 100f), ButtonBlue, out TMP_Text normalFreeLabel);
            normalFreeLabel.fontSize = 28;

            TMP_Text premiumInfo = Label("PremiumInfo", panel, "", 28, TextAlignmentOptions.Center);
            Place(premiumInfo.rectTransform, Center, Center, new Vector2(0f, 580f), new Vector2(1000f, 60f));
            Button premium1 = Btn("Premium1", panel, "1회", new Vector2(-335f, 490f), new Vector2(325f, 100f), new Color(0.45f, 0.32f, 0.12f), out TMP_Text premium1Label);
            premium1Label.fontSize = 27;
            Button premium10 = Btn("Premium10", panel, "10연", new Vector2(0f, 490f), new Vector2(325f, 100f), new Color(0.45f, 0.32f, 0.12f), out TMP_Text premium10Label);
            premium10Label.fontSize = 27;
            Button ticket = Btn("Ticket", panel, "소환권", new Vector2(335f, 490f), new Vector2(325f, 100f), new Color(0.45f, 0.32f, 0.12f), out TMP_Text ticketLabel);
            ticketLabel.fontSize = 25;

            Button rates = Btn("Rates", panel, "확률 보기", new Vector2(-335f, 385f), new Vector2(325f, 84f), ButtonGray, out TMP_Text ratesButtonLabel);
            ratesButtonLabel.fontSize = 28;
            Button exchange = Btn("Exchange", panel, "조각 교환소", new Vector2(0f, 385f), new Vector2(325f, 84f), ButtonGray, out TMP_Text exchangeLabel);
            exchangeLabel.fontSize = 28;
            Button goldShop = Btn("GoldShop", panel, "골드 상점", new Vector2(335f, 385f), new Vector2(325f, 84f), ButtonGray, out TMP_Text goldShopLabel);
            goldShopLabel.fontSize = 28;

            TMP_Text message = Label("Message", panel, "", 26, TextAlignmentOptions.Center);
            Place(message.rectTransform, Center, Center, new Vector2(0f, 300f), new Vector2(1000f, 60f));
            ButtonList list = BuildButtonList(panel, "List", new Vector2(0f, -230f), new Vector2(1000f, 960f), 90f, 26f);
            Button back = Btn("Back", panel, "뒤로", new Vector2(0f, -800f), new Vector2(400f, 100f), ButtonGray, out _);

            // 확률 보기 (덮는 판)
            RectTransform ratesPanel = Rect("RatesPanel", panel, Vector2.zero, Vector2.one, Center, Vector2.zero, Vector2.zero);
            Img(ratesPanel, PanelColor, null).raycastTarget = true; // 뒤 화면이 비치지 않게
            TMP_Text ratesText = Label("RatesText", ratesPanel, "", 28, TextAlignmentOptions.TopLeft);
            Place(ratesText.rectTransform, Center, Center, new Vector2(0f, 120f), new Vector2(980f, 1300f));
            ratesText.lineSpacing = 8f;
            Button ratesClose = Btn("RatesClose", ratesPanel, "닫기", new Vector2(0f, -700f), new Vector2(400f, 100f), ButtonGray, out _);

            using (var props = new Props(screen))
            {
                props.Ref("root", panel.gameObject).Ref("gacha", gacha).Ref("walletLabel", wallet)
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

        private static PetPanel BuildPetPanel(Transform parent, MetaTable meta)
        {
            RectTransform panel = Overlay(parent, "PetPanel", out GameObject holder);
            var screen = holder.AddComponent<PetPanel>();

            TMP_Text title = Label("Title", panel, "펫", 56, TextAlignmentOptions.Center, new Color(1f, 0.85f, 0.35f));
            Place(title.rectTransform, Center, Center, new Vector2(0f, 850f), new Vector2(900f, 80f));
            TMP_Text info = Label("Info", panel, "", 26, TextAlignmentOptions.Center);
            Place(info.rectTransform, Center, Center, new Vector2(0f, 765f), new Vector2(1000f, 80f));

            ButtonList list = BuildButtonList(panel, "List", new Vector2(0f, 330f), new Vector2(1000f, 760f), 170f, 28f);
            TMP_Text detail = Label("Detail", panel, "", 27, TextAlignmentOptions.Top);
            Place(detail.rectTransform, Center, Center, new Vector2(0f, -330f), new Vector2(1000f, 480f));
            detail.lineSpacing = 4f;
            detail.enableAutoSizing = true;
            detail.fontSizeMin = 20f;
            detail.fontSizeMax = 27f;

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
            Place(detail.rectTransform, Center, Center, new Vector2(0f, -575f), new Vector2(1000f, 110f));
            Button action = Btn("RelicAction", panel, "★ 올리기", new Vector2(-220f, -720f), new Vector2(400f, 110f), new Color(0.5f, 0.4f, 0.15f), out TMP_Text actionLabel);
            actionLabel.fontSize = 28;
            Button back = Btn("Back", panel, "뒤로", new Vector2(220f, -720f), new Vector2(400f, 110f), ButtonGray, out _);

            using (var props = new Props(screen))
            {
                props.Ref("root", panel.gameObject).Ref("table", relics).Ref("summaryLabel", summary).Ref("list", list)
                    .Ref("detailLabel", detail).Ref("actionButton", action).Ref("actionLabel", actionLabel).Ref("backButton", back);
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
