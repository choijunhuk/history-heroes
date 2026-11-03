using HistoryHeroes.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace HistoryHeroes.EditorTools
{
    /// <summary>로비 우편함 (LiveOps 4장)</summary>
    public static partial class SceneBuilder
    {
        private static MailPanel BuildMailPanel(Transform parent)
        {
            RectTransform panel = Overlay(parent, "MailPanel", out GameObject holder);
            var screen = holder.AddComponent<MailPanel>();

            TMP_Text title = Label("Title", panel, "우편함", 56, TextAlignmentOptions.Center, new Color(1f, 0.85f, 0.35f));
            Place(title.rectTransform, Center, Center, new Vector2(0f, 850f), new Vector2(900f, 80f));

            // 줄 높이 210 · 제목 40 (나머지 글은 줄 안에서 34로 줄어든다)
            ButtonList list = BuildButtonList(panel, "List", new Vector2(0f, 30f), new Vector2(1000f, 1240f), 210f, 40f);
            TMP_Text message = Label("Message", panel, "", 30, TextAlignmentOptions.Center, new Color(1f, 0.75f, 0.6f)); // 저장 실패 안내
            Place(message.rectTransform, Center, Center, new Vector2(0f, -628f), new Vector2(1000f, 60f));

            TMP_Text empty = Label("Empty", panel, "우편이 없어요", 48, TextAlignmentOptions.Center, new Color(1f, 1f, 1f, 0.7f));
            Place(empty.rectTransform, Center, Center, new Vector2(0f, 0f), new Vector2(900f, 120f));

            Button claimAll = Btn("ClaimAll", panel, "모두 받기", new Vector2(0f, -740f), new Vector2(1000f, 130f), ButtonGreen, out TMP_Text claimAllLabel);
            claimAllLabel.fontSize = 48;
            Button back = Btn("Back", panel, "뒤로", new Vector2(0f, -870f), new Vector2(380f, 100f), ButtonGray, out _);

            using (var props = new Props(screen))
            {
                props.Ref("root", panel.gameObject).Ref("list", list).Ref("emptyLabel", empty)
                    .Ref("claimAllButton", claimAll).Ref("claimAllLabel", claimAllLabel).Ref("backButton", back).Ref("messageLabel", message);
            }

            return screen;
        }
    }
}
