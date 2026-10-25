using HistoryHeroes.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace HistoryHeroes.EditorTools
{
    /// <summary>v0.2 화면: 보스 HUD, 기록 남기기 (전투·로비 공용).</summary>
    public static partial class SceneBuilder
    {
        private static readonly Color PanelColor = new Color(0.04f, 0.05f, 0.08f, 1f);
        private static readonly Color RowColor = new Color(0.13f, 0.15f, 0.2f);
        private static readonly Color ButtonGray = new Color(0.3f, 0.32f, 0.38f);
        private static readonly Color ButtonGreen = new Color(0.2f, 0.55f, 0.32f);
        private static readonly Color ButtonBlue = new Color(0.22f, 0.3f, 0.45f);

        // ---------------------------------------------------------------- 보스 HUD
        private static BossHud BuildBossHud(RectTransform hudRect, Transform root)
        {
            // 컴포넌트는 늘 켜져 있는 부모에 둔다 (막대 묶음 root를 끄고 켜고, 소개 코루틴도 여기서 돈다)
            RectTransform controller = Rect("BossHud", hudRect, Vector2.zero, Vector2.one, Center, Vector2.zero, Vector2.zero);
            var hud = controller.gameObject.AddComponent<BossHud>();
            RectTransform holder = Rect("Bar", controller, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -214f), new Vector2(-80f, 90f));

            RectTransform bar = Rect("Bar", holder, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), Vector2.zero, new Vector2(0f, 34f));
            Fill(bar, new Color(0.1f, 0.1f, 0.12f, 0.9f), new Color(0.65f, 0.35f, 0.9f), out Image fill);
            // 2/3, 1/3 단계 눈금
            for (int i = 1; i <= 2; i++)
            {
                RectTransform tick = Rect($"Tick{i}", bar, new Vector2(i / 3f, 0f), new Vector2(i / 3f, 1f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(4f, 0f));
                Img(tick, new Color(1f, 1f, 1f, 0.7f), _square);
            }

            TMP_Text nameLabel = Label("Name", holder, "보스", 32, TextAlignmentOptions.BottomLeft);
            Place(nameLabel.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 40f), new Vector2(0f, 46f));
            nameLabel.rectTransform.pivot = new Vector2(0.5f, 0f);
            TMP_Text phaseLabel = Label("Phase", holder, "1단계", 28, TextAlignmentOptions.BottomRight, new Color(1f, 1f, 1f, 0.75f));
            Place(phaseLabel.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 40f), new Vector2(0f, 46f));
            phaseLabel.rectTransform.pivot = new Vector2(0.5f, 0f);

            // 시작 소개 (가운데)
            RectTransform intro = Rect("BossIntro", root, Center, Center, Center, new Vector2(0f, 200f), new Vector2(1000f, 300f));
            Img(intro, new Color(0f, 0f, 0f, 0.6f), null);
            var group = intro.gameObject.AddComponent<CanvasGroup>();
            group.blocksRaycasts = false;
            group.interactable = false;
            TMP_Text introTitle = Label("Title", intro, "균열의 수문장", 72, TextAlignmentOptions.Center, new Color(1f, 0.85f, 0.35f));
            Place(introTitle.rectTransform, Center, Center, new Vector2(0f, 50f), new Vector2(960f, 100f));
            TMP_Text introBody = Label("Body", intro, "", 32, TextAlignmentOptions.Center);
            Place(introBody.rectTransform, Center, Center, new Vector2(0f, -60f), new Vector2(940f, 100f));

            using (var props = new Props(hud))
            {
                props.Ref("root", holder.gameObject).Ref("fill", fill).Ref("nameLabel", nameLabel).Ref("phaseLabel", phaseLabel)
                    .Ref("intro", group).Ref("introTitle", introTitle).Ref("introBody", introBody);
            }

            return hud;
        }

        // ---------------------------------------------------------------- 기록 남기기
        private const int RecordRows = 8; // 액티브 4 + 패시브 4

        private static RecordSaveScreen BuildRecordSave(Transform root, Skills.SkillDatabase database)
        {
            RectTransform holder = Rect("RecordSave", root, Vector2.zero, Vector2.one, Center, Vector2.zero, Vector2.zero);
            var screen = holder.gameObject.AddComponent<RecordSaveScreen>();

            RectTransform panel = Rect("Panel", holder, Vector2.zero, Vector2.one, Center, Vector2.zero, Vector2.zero);
            Img(panel, PanelColor, null).raycastTarget = true;

            TMP_Text title = Label("Title", panel, "기록 남기기", 64, TextAlignmentOptions.Center, new Color(1f, 0.85f, 0.35f));
            Place(title.rectTransform, Center, Center, new Vector2(0f, 830f), new Vector2(900f, 90f));
            TMP_Text subtitle = Label("Subtitle", panel, "", 28, TextAlignmentOptions.Center, new Color(1f, 1f, 1f, 0.75f));
            Place(subtitle.rectTransform, Center, Center, new Vector2(0f, 755f), new Vector2(1000f, 70f));

            TMP_Text capacity = Label("Capacity", panel, "용량 0 / 27pt", 36, TextAlignmentOptions.Center);
            Place(capacity.rectTransform, Center, Center, new Vector2(0f, 680f), new Vector2(900f, 56f));
            RectTransform bar = Rect("CapacityBar", panel, Center, Center, Center, new Vector2(0f, 628f), new Vector2(900f, 30f));
            Fill(bar, new Color(0.1f, 0.1f, 0.12f), new Color(0.3f, 0.75f, 0.45f), out Image capacityFill);

            var rows = new RecordSaveScreen.Row[RecordRows];
            for (int i = 0; i < RecordRows; i++) rows[i] = BuildRecordRow(panel, i, new Vector2(0f, 530f - i * 118f));

            Button autoFit = Btn("AutoFit", panel, "자동 맞추기", new Vector2(-340f, -470f), new Vector2(300f, 110f), ButtonBlue, out _);
            Button save = Btn("Save", panel, "저장", new Vector2(0f, -470f), new Vector2(320f, 120f), ButtonGreen, out TMP_Text saveLabel);
            Button skip = Btn("Skip", panel, "저장 안 함", new Vector2(340f, -470f), new Vector2(300f, 110f), ButtonGray, out TMP_Text skipLabel);
            TMP_Text help = Label("Help", panel, "레벨을 일부만 남길 수 있어요. 진화는 통째로, 합체는 레벨을 낮춰서 남길 수 있어요.", 24, TextAlignmentOptions.Center, new Color(1f, 1f, 1f, 0.55f));
            Place(help.rectTransform, Center, Center, new Vector2(0f, -580f), new Vector2(1000f, 80f));

            // 칸 고르기 (빈 칸이 없을 때)
            RectTransform slotPanel = Rect("SlotPanel", holder, Vector2.zero, Vector2.one, Center, Vector2.zero, Vector2.zero);
            Img(slotPanel, new Color(0f, 0f, 0f, 0.92f), null).raycastTarget = true;
            TMP_Text slotTitle = Label("Title", slotPanel, "바꿀 칸을 고르세요\n(그 칸의 기록은 지워져요)", 40, TextAlignmentOptions.Center);
            Place(slotTitle.rectTransform, Center, Center, new Vector2(0f, 560f), new Vector2(1000f, 130f));
            var slotButtons = new Button[3];
            var slotLabels = new TMP_Text[3];
            for (int i = 0; i < 3; i++)
            {
                slotButtons[i] = Btn($"Slot{i + 1}", slotPanel, "", new Vector2(0f, 330f - i * 250f), new Vector2(980f, 220f), RowColor, out TMP_Text label);
                label.fontSize = 28;
                label.alignment = TextAlignmentOptions.MidlineLeft;
                label.rectTransform.offsetMin = new Vector2(30f, 10f);
                label.rectTransform.offsetMax = new Vector2(-30f, -10f);
                slotLabels[i] = label;
            }

            Button slotCancel = Btn("Cancel", slotPanel, "취소", new Vector2(0f, -500f), new Vector2(400f, 110f), ButtonGray, out _);

            using (var props = new Props(screen))
            {
                props.Ref("root", panel.gameObject).Ref("titleLabel", title).Ref("subtitleLabel", subtitle)
                    .Ref("capacityLabel", capacity).Ref("capacityFill", capacityFill)
                    .Ref("autoFitButton", autoFit).Ref("saveButton", save).Ref("saveLabel", saveLabel)
                    .Ref("skipButton", skip).Ref("skipLabel", skipLabel)
                    .Ref("slotPanel", slotPanel.gameObject).Refs("slotButtons", slotButtons).Refs("slotLabels", slotLabels).Ref("slotCancel", slotCancel)
                    .Ref("database", database);
            }

            SetRows(screen, rows);
            return screen;
        }

        private static RecordSaveScreen.Row BuildRecordRow(RectTransform parent, int index, Vector2 position)
        {
            RectTransform row = Rect($"Row{index + 1}", parent, Center, Center, Center, position, new Vector2(1000f, 108f));
            Img(row, RowColor, null);

            RectTransform band = Rect("Band", row, new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(0f, 0.5f), Vector2.zero, new Vector2(14f, 0f));
            Image bandImage = Img(band, SkillCardView.ActiveBand, null);

            TMP_Text nameLabel = Label("Name", row, "스킬", 34, TextAlignmentOptions.MidlineLeft);
            Place(nameLabel.rectTransform, Center, Center, new Vector2(-250f, 16f), new Vector2(440f, 50f));
            TMP_Text note = Label("Note", row, "", 21, TextAlignmentOptions.MidlineLeft, new Color(1f, 0.85f, 0.4f));
            Place(note.rectTransform, Center, Center, new Vector2(-170f, -30f), new Vector2(600f, 36f));
            note.textWrappingMode = TextWrappingModes.NoWrap;
            note.overflowMode = TextOverflowModes.Ellipsis;

            TMP_Text level = Label("Level", row, "Lv 1 / 1", 32, TextAlignmentOptions.Center);
            Place(level.rectTransform, Center, Center, new Vector2(90f, 14f), new Vector2(200f, 50f));
            TMP_Text cost = Label("Cost", row, "0pt", 30, TextAlignmentOptions.Center, new Color(0.7f, 0.9f, 1f));
            Place(cost.rectTransform, Center, Center, new Vector2(225f, 14f), new Vector2(110f, 50f));

            Button minus = Btn("Minus", row, "-", new Vector2(345f, 0f), new Vector2(96f, 84f), ButtonGray, out TMP_Text minusLabel);
            minusLabel.fontSize = 48;
            Button plus = Btn("Plus", row, "+", new Vector2(450f, 0f), new Vector2(96f, 84f), ButtonBlue, out TMP_Text plusLabel);
            plusLabel.fontSize = 48;

            return new RecordSaveScreen.Row
            {
                root = row.gameObject, band = bandImage, nameLabel = nameLabel, levelLabel = level, costLabel = cost,
                noteLabel = note, minus = minus, plus = plus,
            };
        }

        /// <summary>[Serializable] 클래스 배열은 Props로 못 넣어서 SerializedObject로 직접 넣는다</summary>
        private static void SetRows(RecordSaveScreen screen, RecordSaveScreen.Row[] rows)
        {
            var so = new UnityEditor.SerializedObject(screen);
            UnityEditor.SerializedProperty list = so.FindProperty("rows");
            list.arraySize = rows.Length;
            for (int i = 0; i < rows.Length; i++)
            {
                UnityEditor.SerializedProperty e = list.GetArrayElementAtIndex(i);
                e.FindPropertyRelative("root").objectReferenceValue = rows[i].root;
                e.FindPropertyRelative("band").objectReferenceValue = rows[i].band;
                e.FindPropertyRelative("nameLabel").objectReferenceValue = rows[i].nameLabel;
                e.FindPropertyRelative("levelLabel").objectReferenceValue = rows[i].levelLabel;
                e.FindPropertyRelative("costLabel").objectReferenceValue = rows[i].costLabel;
                e.FindPropertyRelative("noteLabel").objectReferenceValue = rows[i].noteLabel;
                e.FindPropertyRelative("minus").objectReferenceValue = rows[i].minus;
                e.FindPropertyRelative("plus").objectReferenceValue = rows[i].plus;
            }

            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
