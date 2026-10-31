using HistoryHeroes.Skills;
using HistoryHeroes.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace HistoryHeroes.EditorTools
{
    public static partial class SceneBuilder
    {
        private static CodexPanel BuildCodexPanel(Transform parent, SkillDatabase database)
        {
            RectTransform panel = Overlay(parent, "CodexPanel", out GameObject holder);
            var screen = holder.AddComponent<CodexPanel>();

            TMP_Text title = Label("Title", panel, "조합 도감", 64, TextAlignmentOptions.Center, new Color(1f, 0.85f, 0.35f));
            Place(title.rectTransform, Center, Center, new Vector2(0f, 790f), new Vector2(900f, 100f));
            TMP_Text progress = Label("Progress", panel, "발견 0 / 0", 36, TextAlignmentOptions.Center);
            Place(progress.rectTransform, Center, Center, new Vector2(0f, 690f), new Vector2(900f, 60f));
            TMP_Text message = Label("Message", panel, "", 27, TextAlignmentOptions.Center);
            Place(message.rectTransform, Center, Center, new Vector2(0f, 610f), new Vector2(900f, 60f));
            // 잠긴 조합은 작은 카드로 모으고, 긴 인물 설명은 그 아래에서 읽는다.
            RectTransform viewport = Rect("Viewport", panel, Center, Center, Center, new Vector2(0f, -20f), new Vector2(920f, 1120f));
            Img(viewport, new Color(0f, 0f, 0f, 0.001f), null).raycastTarget = true; // 끌기를 받는 판
            viewport.gameObject.AddComponent<RectMask2D>();
            RectTransform content = Rect("Content", viewport, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), Vector2.zero, new Vector2(0f, 100f));
            var layout = content.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 12f;
            layout.padding = new RectOffset(12, 12, 12, 24);
            layout.childControlHeight = true;
            layout.childControlWidth = true;
            layout.childForceExpandHeight = false;
            var labels = new TMP_Text[database.recipes.Count];
            var buttons = new Button[database.recipes.Count];
            var icons = new Image[database.recipes.Count];
            RectTransform gridRoot = Rect("Recipes", content, Center, Center, Center, Vector2.zero, Vector2.zero);
            var grid = gridRoot.gameObject.AddComponent<GridLayoutGroup>();
            grid.cellSize = new Vector2(286f, 220f);
            grid.spacing = new Vector2(12f, 12f);
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = 3;
            grid.childAlignment = TextAnchor.UpperCenter;
            gridRoot.gameObject.AddComponent<LayoutElement>().preferredHeight = Mathf.CeilToInt(labels.Length / 3f) * 232f;
            for (int i = 0; i < labels.Length; i++)
            {
                RectTransform row = Rect($"Recipe{i}", gridRoot, Center, Center, Center, Vector2.zero, grid.cellSize);
                Img(row, new Color(0.14f, 0.17f, 0.23f), null).raycastTarget = false;
                RectTransform icon = Rect("Icon", row, Center, Center, Center, new Vector2(0f, 63f), new Vector2(64f, 64f));
                icons[i] = Img(icon, Color.gray, Icon("codex"));
                icons[i].preserveAspect = true;
                icons[i].raycastTarget = false;
                labels[i] = Label("Recipe", row, "???", 26, TextAlignmentOptions.Center);
                Place(labels[i].rectTransform, Center, Center, new Vector2(0f, -12f), new Vector2(266f, 86f));
                buttons[i] = Btn("Target", row, "목표", new Vector2(0f, -79f), new Vector2(230f, 46f), ButtonBlue, out TMP_Text targetLabel);
                targetLabel.fontSize = 24f;
                buttons[i].gameObject.SetActive(false);
            }
            TMP_Text list = Label("List", content, "", 32, TextAlignmentOptions.TopLeft);
            list.lineSpacing = 6f;
            content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            var scroll = viewport.gameObject.AddComponent<ScrollRect>();
            scroll.viewport = viewport;
            scroll.content = content;
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 40f;
            Button relicButton = Btn("Relics", panel, "유물 도감", new Vector2(-220f, -720f), new Vector2(400f, 110f), new Color(0.45f, 0.32f, 0.12f), out _);
            Button back = Btn("Back", panel, "뒤로", new Vector2(220f, -720f), new Vector2(400f, 110f), ButtonGray, out _);

            using (var props = new Props(screen))
            {
                props.Ref("root", panel.gameObject).Ref("progressLabel", progress).Ref("listLabel", list).Ref("messageLabel", message)
                    .Refs("recipeLabels", labels).Refs("recipeButtons", buttons).Refs("recipeIcons", icons).Ref("lockedIcon", Icon("codex")).Ref("scroll", scroll)
                    .Ref("backButton", back).Ref("database", database);
            }

            return screen;
        }
    }
}
