using HistoryHeroes.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace HistoryHeroes.EditorTools
{
    /// <summary>첫 실행 동의 창 (LiveOps 19장). 로비의 맨 위에 덮는다.</summary>
    public static partial class SceneBuilder
    {
        private static ConsentPanel BuildConsentPanel(Transform root)
        {
            RectTransform panel = Overlay(root, "ConsentPanel", out GameObject holder);
            var screen = holder.AddComponent<ConsentPanel>();

            TMP_Text title = Label("Title", panel, "시작하기 전에", 64, TextAlignmentOptions.Center, new Color(1f, 0.85f, 0.35f));
            Place(title.rectTransform, Center, Center, new Vector2(0f, 640f), new Vector2(900f, 100f));
            TMP_Text game = Label("GameName", panel, "History Heroes (역사 영웅)", 40, TextAlignmentOptions.Center);
            Place(game.rectTransform, Center, Center, new Vector2(0f, 540f), new Vector2(940f, 60f));
            TMP_Text rating = Label("Rating", panel, "", 28, TextAlignmentOptions.Center, new Color(0.7f, 0.9f, 1f));
            Place(rating.rectTransform, Center, Center, new Vector2(0f, 470f), new Vector2(940f, 50f));

            Button allButton = ConsentRow(panel, "ConsentAll", "전체 동의", 36, 340f, 120f, out GameObject allMark);
            Button termsButton = ConsentRow(panel, "ConsentTerms", "(필수) 이용약관 동의", 30, 190f, 110f, out GameObject termsMark);
            Button privacyButton = ConsentRow(panel, "ConsentPrivacy", "(필수) 개인정보 수집·이용 동의", 30, 70f, 110f, out GameObject privacyMark);
            Button ageButton = ConsentRow(panel, "ConsentAge", "(필수) 만 14세 이상입니다", 30, -50f, 110f, out GameObject ageMark);
            // 보기 단추는 줄 오른쪽 끝, 줄 글자와 겹치지 않게 줄 글자 폭을 줄여 둔다 (ConsentRow)
            Button termsView = Btn("ConsentTermsView", panel, "보기", new Vector2(360f, 190f), new Vector2(170f, 80f), ButtonBlue, out TMP_Text tv);
            Button privacyView = Btn("ConsentPrivacyView", panel, "보기", new Vector2(360f, 70f), new Vector2(170f, 80f), ButtonBlue, out TMP_Text pv);
            tv.fontSize = pv.fontSize = 30f;

            TMP_Text helper = Label("AgeHelp", panel, "만 14세 미만은 보호자 동의가 필요해서 지금은 이용할 수 없어요.", 26, TextAlignmentOptions.Center, new Color(1f, 1f, 1f, 0.7f));
            Place(helper.rectTransform, Center, Center, new Vector2(0f, -150f), new Vector2(940f, 80f));
            TMP_Text notice = Label("Notice", panel, "", 28, TextAlignmentOptions.Center);
            Place(notice.rectTransform, Center, Center, new Vector2(0f, -250f), new Vector2(940f, 80f));
            Button start = Btn("ConsentStart", panel, "시작하기", new Vector2(0f, -400f), new Vector2(760f, 150f), ButtonGreen, out TMP_Text startLabel);
            startLabel.fontSize = 52;

            // ----- 약관 글 보기 (InfoPanel과 같은 스크롤 글) -----
            RectTransform viewer = Rect("Viewer", panel, Vector2.zero, Vector2.one, Center, Vector2.zero, Vector2.zero);
            Img(viewer, PanelColor, null).raycastTarget = true;
            TMP_Text vTitle = Label("ViewerTitle", viewer, "", 56, TextAlignmentOptions.Center, new Color(1f, 0.85f, 0.35f));
            Place(vTitle.rectTransform, Center, Center, new Vector2(0f, 830f), new Vector2(900f, 90f));
            RectTransform viewport = Rect("Viewport", viewer, Center, Center, Center, new Vector2(0f, 20f), new Vector2(1000f, 1500f));
            Img(viewport, new Color(0f, 0f, 0f, 0.25f), null).raycastTarget = true;
            viewport.gameObject.AddComponent<RectMask2D>();
            RectTransform content = Rect("Content", viewport, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), Vector2.zero, new Vector2(0f, 100f));
            TMP_Text body = Label("Body", content, "", 30, TextAlignmentOptions.TopLeft);
            Stretch(body.rectTransform);
            body.rectTransform.offsetMin = new Vector2(30f, 0f);
            body.rectTransform.offsetMax = new Vector2(-30f, -24f);
            body.lineSpacing = 8f;
            var fitter = content.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            var layout = content.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(30, 30, 24, 40);
            layout.childControlHeight = true;
            layout.childControlWidth = true;
            layout.childForceExpandHeight = false;
            var scroll = viewport.gameObject.AddComponent<ScrollRect>();
            scroll.content = content;
            scroll.viewport = viewport;
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 40f;
            Button back = Btn("ConsentViewerBack", viewer, "닫기", new Vector2(0f, -850f), new Vector2(400f, 100f), ButtonGray, out _);

            using (var props = new Props(screen))
            {
                props.Ref("root", panel.gameObject).Ref("ratingLabel", rating).Ref("noticeLabel", notice)
                    .Ref("allButton", allButton).Ref("termsButton", termsButton).Ref("privacyButton", privacyButton).Ref("ageButton", ageButton)
                    .Ref("allMark", allMark).Ref("termsMark", termsMark).Ref("privacyMark", privacyMark).Ref("ageMark", ageMark)
                    .Ref("termsView", termsView).Ref("privacyView", privacyView).Ref("startButton", start)
                    .Ref("viewer", viewer.gameObject).Ref("viewerTitle", vTitle).Ref("viewerBody", body).Ref("viewerScroll", scroll).Ref("viewerBack", back);
            }

            return screen;
        }

        /// <summary>체크 줄: 줄 전체가 누르는 단추, 왼쪽에 네모 칸(체크 시 초록 채움). 오른쪽 180은 [보기] 자리</summary>
        private static Button ConsentRow(RectTransform parent, string name, string text, float size, float y, float height, out GameObject mark)
        {
            RectTransform row = Rect(name, parent, Center, Center, Center, new Vector2(0f, y), new Vector2(960f, height));
            Image bg = Img(row, new Color(0.14f, 0.17f, 0.24f, 1f), null);
            bg.raycastTarget = true;
            var button = row.gameObject.AddComponent<Button>();
            button.targetGraphic = bg;
            RectTransform box = Rect("Box", row, Center, Center, Center, new Vector2(-400f, 0f), new Vector2(64f, 64f));
            Img(box, new Color(0.05f, 0.06f, 0.1f, 1f), null);
            RectTransform fill = Rect("Mark", box, Center, Center, Center, Vector2.zero, new Vector2(40f, 40f));
            Image markImage = fill.gameObject.AddComponent<Image>();
            markImage.color = new Color(0.35f, 0.85f, 0.5f);
            markImage.raycastTarget = false;
            mark = fill.gameObject;
            mark.SetActive(false);
            TMP_Text label = Label("Label", row, text, size, TextAlignmentOptions.Left);
            Place(label.rectTransform, Center, Center, new Vector2(-30f, 0f), new Vector2(640f, height - 10f));
            return button;
        }
    }
}
