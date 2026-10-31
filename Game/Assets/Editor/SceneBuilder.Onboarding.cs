using System.Collections.Generic;
using HistoryHeroes.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace HistoryHeroes.EditorTools
{
    public static partial class SceneBuilder
    {
        private static LobbyGuide BuildLobbyGuide(Transform root, RectTransform safe, RectTransform main)
        {
            // 모든 로비 창은 Panel 자식을 켠다. 생성할 때 모아 두어 매 프레임 탐색하지 않는다.
            var blockers = new List<GameObject>();
            foreach (Transform parent in new[] { root, safe })
            {
                foreach (Transform holder in parent)
                {
                    if (holder == main || holder == safe) continue;
                    Transform panel = holder.Find("Panel");
                    if (panel != null) blockers.Add(panel.gameObject);
                }
            }

            RectTransform guideRoot = Rect("FirstUseGuide", safe, Vector2.zero, Vector2.one, Center, Vector2.zero, Vector2.zero);
            var guide = guideRoot.gameObject.AddComponent<LobbyGuide>();
            RectTransform banner = Rect("Banner", guideRoot, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -460f), new Vector2(980f, 160f));
            Img(banner, new Color(0.05f, 0.07f, 0.12f, 0.9f), null).raycastTarget = true;
            Button dismiss = banner.gameObject.AddComponent<Button>();
            TMP_Text label = Label("Text", banner, "", 32, TextAlignmentOptions.Center);
            Stretch(label.rectTransform);
            label.rectTransform.offsetMin = new Vector2(24f, 12f);
            label.rectTransform.offsetMax = new Vector2(-24f, -12f);
            label.enableAutoSizing = true;
            label.fontSizeMin = 24f;
            label.fontSizeMax = 32f;
            label.raycastTarget = false;

            RectTransform focus = Rect("Focus", guideRoot, Vector2.zero, Vector2.one, Center, Vector2.zero, Vector2.zero);
            void Edge(string name, Vector2 min, Vector2 max, Vector2 size)
            {
                RectTransform edge = Rect(name, focus, min, max, Center, Vector2.zero, size);
                Img(edge, new Color(1f, 0.84f, 0.4f, 0.9f), null).raycastTarget = false;
            }
            Edge("Top", new Vector2(0f, 1f), Vector2.one, new Vector2(0f, 4f));
            Edge("Bottom", Vector2.zero, new Vector2(1f, 0f), new Vector2(0f, 4f));
            Edge("Left", Vector2.zero, new Vector2(0f, 1f), new Vector2(4f, 0f));
            Edge("Right", new Vector2(1f, 0f), Vector2.one, new Vector2(4f, 0f));
            banner.gameObject.SetActive(false);
            focus.gameObject.SetActive(false);
            using (var props = new Props(guide)) props.Ref("banner", banner.gameObject).Ref("label", label)
                .Ref("dismissButton", dismiss).Ref("focus", focus).Refs("blockers", blockers);
            return guide;
        }
    }
}
