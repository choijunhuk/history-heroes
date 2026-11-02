using HistoryHeroes.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace HistoryHeroes.EditorTools
{
    /// <summary>보상 획득 연출 (RewardPopup). 로비·전투 장면 빌더가 부른다</summary>
    public static partial class SceneBuilder
    {
        private static RewardPopup BuildRewardPopup(Transform parent)
        {
            RectTransform holder = Rect("RewardPopup", parent, Vector2.zero, Vector2.one, Center, Vector2.zero, Vector2.zero);
            var popup = holder.gameObject.AddComponent<RewardPopup>();
            RectTransform root = Rect("Root", holder, Vector2.zero, Vector2.one, Center, Vector2.zero, Vector2.zero);

            // 어두운 바탕: 안전 영역 밖까지 덮는다
            RectTransform dimRect = Rect("Dim", root, Vector2.zero, Vector2.one, Center, Vector2.zero, Vector2.zero);
            dimRect.offsetMin = new Vector2(-40f, -400f);
            dimRect.offsetMax = new Vector2(40f, 400f);
            Image dim = Img(dimRect, new Color(0.02f, 0.03f, 0.06f, 0.84f), null); // 뒤 화면이 비쳐 산만하지 않게 (0.7은 옅었다)
            dim.raycastTarget = true;

            // 뒤에서 천천히 도는 빛줄기와 빛무리
            RectTransform raysRect = Rect("Rays", root, Center, Center, Center, new Vector2(0f, 40f), new Vector2(1400f, 1400f));
            Image rays = Img(raysRect, new Color(1f, 0.88f, 0.5f, 0.32f), _kit != null ? _kit.Rays : null); // 은은하게 (0.55는 눈부셨다)
            RectTransform glowRect = Rect("Glow", root, Center, Center, Center, new Vector2(0f, 40f), new Vector2(800f, 800f));
            Image glow = Img(glowRect, new Color(1f, 0.88f, 0.5f, 0.6f), _kit != null ? _kit.Glow : null);

            RectTransform container = Rect("Cards", root, Center, Center, Center, new Vector2(0f, 40f), new Vector2(1000f, 600f));

            // 카드 하나: 칸 + 큰 그림(글자로 그린다) + 아래 숫자
            RectTransform card = Rect("CardTemplate", container, Center, Center, Center, Vector2.zero, new Vector2(220f, 240f));
            Image slot = Img(card, new Color(0.2f, 0.24f, 0.36f, 1f), _kit != null ? _kit.Slot : null);
            if (_kit != null)
            {
                slot.type = Image.Type.Sliced;
                slot.pixelsPerUnitMultiplier = BorderScale(240f, 160f);
                slot.color = new Color(0.45f, 0.5f, 0.7f, 1f);
            }

            TMP_Text icon = Label("Icon", card, "", 130f, TextAlignmentOptions.Center);
            Place(icon.rectTransform, Center, Center, new Vector2(0f, 38f), new Vector2(200f, 150f));
            icon.textWrappingMode = TextWrappingModes.NoWrap;
            icon.overflowMode = TextOverflowModes.Overflow;
            TMP_Text amount = Label("Amount", card, "", 36f, TextAlignmentOptions.Center);
            Place(amount.rectTransform, Center, Center, new Vector2(0f, -78f), new Vector2(204f, 70f));
            amount.enableAutoSizing = true;
            amount.fontSizeMin = 20f;
            amount.fontSizeMax = 36f;
            card.gameObject.SetActive(false);

            // 위쪽 "획득!" 띠
            RectTransform ribbonRect = Rect("Ribbon", root, Center, Center, Center, new Vector2(0f, 560f), new Vector2(760f, 140f));
            Image ribbonImage = Img(ribbonRect, new Color(0.86f, 0.24f, 0.22f), _kit != null ? _kit.Ribbon : null);
            if (_kit != null) ribbonImage.type = Image.Type.Sliced;
            TMP_Text title = Label("Title", ribbonRect, "획득!", 64f, TextAlignmentOptions.Center, Color.white);
            Stretch(title.rectTransform);

            // 아래 안내 (깜빡인다)
            TMP_Text hint = Label("Hint", root, "눌러서 받기", 40f, TextAlignmentOptions.Center, Color.white);
            Place(hint.rectTransform, Center, Center, new Vector2(0f, -560f), new Vector2(800f, 80f));

            // 화면 전체를 덮는 투명 누름 칸 (맨 위)
            RectTransform tapRect = Rect("Tap", root, Vector2.zero, Vector2.one, Center, Vector2.zero, Vector2.zero);
            tapRect.offsetMin = new Vector2(-40f, -400f);
            tapRect.offsetMax = new Vector2(40f, 400f);
            Image tapImage = Img(tapRect, new Color(0f, 0f, 0f, 0f), null);
            tapImage.raycastTarget = true;
            var tap = tapRect.gameObject.AddComponent<Button>();
            tap.targetGraphic = tapImage;
            tap.transition = Selectable.Transition.None;

            // 날아가는 복사본·반짝이는 맨 앞 층 (누름을 막지 않는다)
            RectTransform fly = Rect("FlyLayer", holder, Vector2.zero, Vector2.one, Center, Vector2.zero, Vector2.zero);

            using (var props = new Props(popup))
            {
                props.Ref("root", root.gameObject).Ref("dim", dim).Ref("titleLabel", title).Ref("ribbon", ribbonRect)
                    .Ref("rays", rays).Ref("glow", glow).Ref("cardContainer", container).Ref("cardTemplate", card.gameObject)
                    .Ref("hintLabel", hint).Ref("tapButton", tap).Ref("flyLayer", fly)
                    .Ref("sparkleSprite", _kit != null ? _kit.Sparkle : null);
            }

            return popup;
        }
    }
}
