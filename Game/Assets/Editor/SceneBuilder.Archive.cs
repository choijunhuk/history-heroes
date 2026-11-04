using HistoryHeroes.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace HistoryHeroes.EditorTools
{
    public static partial class SceneBuilder
    {
        private static void ApplyArchiveLayout(RectTransform panel, RecordArchivePanel screen)
        {
            var help = panel.Find("Help") as RectTransform;
            Place(help, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -144f), new Vector2(1000f, 100f));

            RectTransform view = Rect("ArchiveViewport", panel, Vector2.zero, Vector2.one, Center, Vector2.zero, Vector2.zero);
            view.offsetMin = new Vector2(24f, 12f);
            view.offsetMax = new Vector2(-24f, -260f);
            Img(view, new Color(0f, 0f, 0f, 0.001f), null).raycastTarget = true;
            view.gameObject.AddComponent<RectMask2D>();
            RectTransform content = Rect("ArchiveContent", view, new Vector2(0f, 1f), Vector2.one, new Vector2(0.5f, 1f), Vector2.zero, Vector2.zero);
            VerticalLayoutGroup list = content.gameObject.AddComponent<VerticalLayoutGroup>();
            list.spacing = 20f;
            list.padding = new RectOffset(0, 0, 8, 24);
            list.childControlWidth = list.childControlHeight = true;
            list.childForceExpandHeight = false;
            content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            ScrollRect scroll = view.gameObject.AddComponent<ScrollRect>();
            scroll.viewport = view;
            scroll.content = content;
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 40f;

            var icons = new Image[12];
            var labels = new TMP_Text[12];
            var tempIcons = new Image[4];
            var tempLabels = new TMP_Text[4];
            for (int i = 0; i < 4; i++)
            {
                bool temporary = i == 3;
                var card = (RectTransform)panel.Find(temporary ? "Temp" : $"Slot{i + 1}");
                card.SetParent(content, false);
                card.gameObject.AddComponent<LayoutElement>().minHeight = 220f;
                VerticalLayoutGroup column = card.gameObject.AddComponent<VerticalLayoutGroup>();
                column.padding = new RectOffset(24, 24, 24, 24);
                column.spacing = 16f;
                column.childControlWidth = column.childControlHeight = true;
                column.childForceExpandHeight = false;
                column.childAlignment = TextAnchor.MiddleCenter;
                var title = card.Find("Title").GetComponent<TMP_Text>();
                title.fontSize = 40f;
                title.enableAutoSizing = false;
                var details = card.Find("Details").GetComponent<TMP_Text>();
                details.fontSize = 36f;
                details.enableAutoSizing = false;

                RectTransform skills = Rect("SkillPreview", card, Center, Center, Center, Vector2.zero, new Vector2(0f, 220f));
                skills.gameObject.AddComponent<LayoutElement>().preferredHeight = 220f;
                HorizontalLayoutGroup row = skills.gameObject.AddComponent<HorizontalLayoutGroup>();
                row.spacing = 12f;
                row.childControlWidth = row.childControlHeight = true;
                for (int j = 0; j < 4; j++)
                {
                    RectTransform cell = Rect($"Skill{j + 1}", skills, Center, Center, Center, Vector2.zero, new Vector2(220f, 220f));
                    cell.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;
                    RectTransform iconRect = Rect("Icon", cell, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -4f), new Vector2(96f, 96f));
                    Image icon = Img(iconRect, Color.white, null);
                    icon.raycastTarget = false;
                    icon.preserveAspect = true;
                    TMP_Text label = Label("Name", cell, "", 30f, TextAlignmentOptions.Top, Color.white);
                    Place(label.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0f), Vector2.zero, new Vector2(0f, 112f));
                    label.enableAutoSizing = true;
                    label.fontSizeMin = 26f;
                    label.fontSizeMax = 30f;
                    (temporary ? tempIcons : icons)[temporary ? j : i * 4 + j] = icon;
                    (temporary ? tempLabels : labels)[temporary ? j : i * 4 + j] = label;
                }

                RectTransform actions = Rect("Actions", card, Center, Center, Center, Vector2.zero, new Vector2(0f, 104f));
                actions.gameObject.AddComponent<LayoutElement>().preferredHeight = 104f;
                HorizontalLayoutGroup buttons = actions.gameObject.AddComponent<HorizontalLayoutGroup>();
                buttons.spacing = 24f;
                buttons.childControlWidth = buttons.childControlHeight = true;
                string[] names = temporary ? new[] { "MoveTemp" } : new[] { "Lock", "Delete" };
                foreach (string name in names) card.Find(name).SetParent(actions, false);
            }

            using (var props = new Props(screen))
                props.Refs("slotSkillIcons", icons).Refs("slotSkillLabels", labels)
                    .Refs("tempSkillIcons", tempIcons).Refs("tempSkillLabels", tempLabels).Ref("scroll", scroll);
        }
    }
}
