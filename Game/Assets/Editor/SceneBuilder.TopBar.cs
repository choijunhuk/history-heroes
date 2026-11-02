using HistoryHeroes.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace HistoryHeroes.EditorTools
{
    /// <summary>
    /// 서브 화면 위 띠 (2026-11-02 개발자: "홈 화면을 뺀 화면은 위 공간이 안 채워져 있다 · 모바일 게임스럽지 않다").
    /// 궁수의 전설 · 브롤스타즈처럼 왼쪽 뒤로 화살표 · 가운데 제목 리본 · 오른쪽 골드 · 보석 칩 (Docs/Design/UI-Research-2026-11-02.md 1장).
    /// 창마다 다시 만들지 않고, 장면 저장 전에 "제목(Title) + 뒤로(Back)"가 판 바로 아래 있는 창을 찾아 띠로 옮긴다.
    /// 원래 "뒤로" 단추는 그대로 두고 (창 스크립트 연결 유지) 화살표가 그 단추를 대신 누른다 (ProxyButton).
    /// </summary>
    public static partial class SceneBuilder
    {
        private static readonly Color TitleRibbonColor = new Color(0.86f, 0.26f, 0.24f);

        private static void ApplyTopBars(Transform safe)
        {
            foreach (Transform holder in safe)
            {
                var panel = holder.Find("Panel") as RectTransform;
                if (panel == null || panel.Find("Bleed") == null || panel.Find("TopBar") != null) continue;
                Transform titleT = panel.Find("Title"), backT = panel.Find("Back");
                if (titleT == null || backT == null) continue;
                if (!titleT.TryGetComponent(out TMP_Text title) || !backT.TryGetComponent(out Button back)) continue;
                TopBar(panel, title, back);
            }
        }

        private static void TopBar(RectTransform panel, TMP_Text title, Button back)
        {
            PinToEdges(panel);
            ClearBar(panel);
            RectTransform bar = Rect("TopBar", panel, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -8f), new Vector2(1048f, 116f));
            bar.SetSiblingIndex(1); // 바탕(Bleed) 바로 위

            // 왼쪽: 뒤로 화살표 → 원래 뒤로 단추를 누른다
            Button arrow = Btn("TopBack", bar, "", new Vector2(-466f, 0f), new Vector2(112f, 104f), ButtonBlue, out TMP_Text arrowLabel);
            Sprite backIcon = Icon("back");
            if (backIcon != null)
            {
                RectTransform icon = Rect("Icon", arrow.transform, Center, Center, Center, new Vector2(0f, 4f), new Vector2(60f, 60f));
                Img(icon, Color.white, backIcon).preserveAspect = true;
            }
            else arrowLabel.text = "<";

            using (var props = new Props(arrow.gameObject.AddComponent<ProxyButton>())) props.Ref("target", back);

            // 가운데: 제목 리본 (원래 제목 글을 옮긴다 — 창 스크립트가 바꾸는 제목도 그대로 따라온다)
            RectTransform ribbon = Rect("TitleRibbon", bar, Center, Center, Center, new Vector2(-150f, 0f), new Vector2(500f, 110f));
            if (_kit != null)
            {
                Image ribbonImage = Img(ribbon, TitleRibbonColor, _kit.Ribbon);
                ribbonImage.type = Image.Type.Sliced;
            }

            title.transform.SetParent(ribbon, false);
            Stretch(title.rectTransform);
            title.rectTransform.offsetMin = new Vector2(72f, 8f);
            title.rectTransform.offsetMax = new Vector2(-72f, -4f);
            title.alignment = TextAlignmentOptions.Center;
            title.color = Color.white;
            title.textWrappingMode = TextWrappingModes.NoWrap;
            title.enableAutoSizing = true;
            title.fontSizeMin = 30f;
            title.fontSizeMax = 52f;

            // 오른쪽: 골드 · 보석 (값은 칩이 직접 읽고, 바뀌면 숫자가 올라간다)
            WalletChipOn(Pill(bar, "gold", new Color(1f, 0.84f, 0.3f), new Vector2(202f, 0f), 206f), "gold", true);
            WalletChipOn(Pill(bar, "gem", new Color(0.55f, 0.85f, 1f), new Vector2(418f, 0f), 206f), "gem", true);
        }

        /// <summary>내용은 1080×1920 틀(가운데 ±960)로 짰다</summary>
        private const float FrameHalf = 960f;

        /// <summary>
        /// 키 큰 폰(1080×2340)에서 1920 틀이 가운데 떠 위 띠 아래 · 맨 아래가 비던 것 (개발자: "위 공간이 안 채워져"):
        /// 내용을 위 묶음 · 아래 묶음으로 나눠 위 묶음은 위 가장자리에, 아래 묶음은 아래 가장자리에 붙인다. 남는 높이는 가운데로 간다.
        /// · 세로 목록(ScrollRect)이 있으면 목록이 위아래로 늘어나 남는 높이를 쓴다 (목록 위 = 위 묶음, 아래 = 아래 묶음)
        /// · 목록이 없으면 가운데쯤의 가장 큰 빈틈에서 나눈다 (기록 칸 셋처럼 한 묶음이 갈라지지 않게)
        /// · 틀 절반보다 큰 칸 · 내용 높이만큼인 카드(ContentSizeFitter)는 가운데 그대로. 화면 전체 칸(구역)은 안으로 들어가 같은 일을 한다.
        /// 1920 화면에서는 위치가 그대로다.
        /// </summary>
        private static void PinToEdges(RectTransform container)
        {
            var items = new System.Collections.Generic.List<RectTransform>();
            RectTransform list = null;
            foreach (Transform child in container)
            {
                if (!(child is RectTransform rt) || rt.name == "Bleed" || rt.name == "TopBar") continue;
                bool fullStretch = rt.anchorMin == Vector2.zero && rt.anchorMax == Vector2.one && rt.offsetMin == Vector2.zero && rt.offsetMax == Vector2.zero;
                if (fullStretch)
                {
                    PinToEdges(rt);
                    continue;
                }

                if (rt.anchorMin != Center || rt.anchorMax != Center || rt.GetComponent<ContentSizeFitter>() != null) continue;
                if (rt.TryGetComponent(out ScrollRect scroll) && scroll.vertical && list == null) list = rt; // 키 큰 목록도 늘린다 (보석 목록 1040)
                else if (rt.sizeDelta.y <= FrameHalf) items.Add(rt);
            }

            float divider = list != null ? Mid(list) : Divider(items);
            if (list != null)
            {
                Vector2 size = list.sizeDelta, pivot = list.pivot, pos = list.anchoredPosition;
                float left = pos.x - pivot.x * size.x, right = pos.x + (1f - pivot.x) * size.x;
                float top = pos.y + (1f - pivot.y) * size.y, bottom = pos.y - pivot.y * size.y;
                list.anchorMin = new Vector2(0.5f, 0f);
                list.anchorMax = new Vector2(0.5f, 1f);
                list.offsetMin = new Vector2(left, bottom + FrameHalf);
                list.offsetMax = new Vector2(right, top - FrameHalf);
            }

            foreach (RectTransform rt in items)
            {
                Vector2 pos = rt.anchoredPosition;
                bool up = Mid(rt) >= divider;
                rt.anchorMin = rt.anchorMax = new Vector2(0.5f, up ? 1f : 0f);
                rt.anchoredPosition = new Vector2(pos.x, up ? pos.y - FrameHalf : pos.y + FrameHalf);
            }
        }

        private static float Top(RectTransform rt) => rt.anchoredPosition.y + (1f - rt.pivot.y) * rt.sizeDelta.y;
        private static float Bottom(RectTransform rt) => rt.anchoredPosition.y - rt.pivot.y * rt.sizeDelta.y;
        private static float Mid(RectTransform rt) => (Top(rt) + Bottom(rt)) * 0.5f;

        /// <summary>
        /// 틀 가운데 ±600 안에서 위아래로 가장 크게 빈 곳의 가운데. 뚜렷한 빈틈(80 이상)이 없으면 -480:
        /// 내용은 모두 위에, 맨 아래 단추 줄(도전 · 뒤로)만 아래에 (같은 칸이 나란한 목록이 갈라지지 않게)
        /// </summary>
        private static float Divider(System.Collections.Generic.List<RectTransform> items)
        {
            var spans = new System.Collections.Generic.List<Vector2>();
            foreach (RectTransform rt in items) spans.Add(new Vector2(Bottom(rt), Top(rt)));
            spans.Sort((a, b) => b.y.CompareTo(a.y)); // 위에서 아래로
            float best = 80f, divider = -480f, lowest = float.MaxValue;
            for (int i = 0; i < spans.Count; i++)
            {
                lowest = Mathf.Min(lowest, spans[i].x);
                if (i + 1 >= spans.Count) break;
                float gapTop = lowest, gapBottom = spans[i + 1].y;
                float gap = gapTop - gapBottom, mid = (gapTop + gapBottom) * 0.5f;
                if (gap > best && Mathf.Abs(mid) <= 600f)
                {
                    best = gap;
                    divider = mid;
                }
            }

            return divider;
        }

        /// <summary>위 띠(아래 끝 -124)에 걸린 위 묶음 내용은 띠 아래로 내린다 (보스 창 탭처럼 원래 맨 위에 있던 것)</summary>
        private static void ClearBar(RectTransform panel)
        {
            const float barBottom = -136f;
            foreach (Transform child in panel)
            {
                if (!(child is RectTransform rt) || rt.name == "Bleed" || rt.name == "TopBar") continue;
                if (rt.anchorMin.y != 1f || rt.anchorMax.y != 1f) continue;
                float top = Top(rt);
                if (top > barBottom) rt.anchoredPosition += new Vector2(0f, barBottom - top);
            }
        }

        /// <summary>칩(Pill)에 WalletChip을 붙인다. drive=false면 숫자는 다른 스크립트가 쓰고 보상 아이콘이 날아갈 곳만 된다</summary>
        private static void WalletChipOn(TMP_Text pillLabel, string kind, bool drive)
        {
            if (pillLabel == null) return;
            var chip = pillLabel.transform.parent.gameObject.AddComponent<WalletChip>();
            var so = new UnityEditor.SerializedObject(chip);
            so.FindProperty("kind").stringValue = kind;
            so.FindProperty("label").objectReferenceValue = pillLabel;
            so.FindProperty("drive").boolValue = drive;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
