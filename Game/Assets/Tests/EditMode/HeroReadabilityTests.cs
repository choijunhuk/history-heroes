using System.Reflection;
using HistoryHeroes.UI;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace HistoryHeroes.Tests
{
    public sealed class HeroReadabilityTests
    {
        [Test]
        public void LongReadOnlySkillNoteGrowsAndNextChoiceRestoresTemplate()
        {
            var canvasObject = new GameObject("HeroDetailsTest", typeof(RectTransform), typeof(Canvas));
            try
            {
                var canvas = canvasObject.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                var viewport = new GameObject("Viewport", typeof(RectTransform), typeof(Image), typeof(ScrollRect));
                viewport.transform.SetParent(canvasObject.transform, false);
                var viewRect = (RectTransform)viewport.transform;
                viewRect.sizeDelta = new Vector2(960f, 800f);
                var content = new GameObject("Content", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
                content.transform.SetParent(viewport.transform, false);
                var contentRect = (RectTransform)content.transform;
                contentRect.anchorMin = new Vector2(0f, 1f);
                contentRect.anchorMax = Vector2.one;
                contentRect.pivot = new Vector2(0.5f, 1f);
                contentRect.sizeDelta = Vector2.zero;
                content.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
                var templateObject = new GameObject("Template", typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
                templateObject.transform.SetParent(content.transform, false);
                templateObject.GetComponent<LayoutElement>().preferredHeight = 170f;
                var labelObject = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
                labelObject.transform.SetParent(templateObject.transform, false);
                var templateLabel = labelObject.GetComponent<TextMeshProUGUI>();
                templateLabel.rectTransform.anchorMin = Vector2.zero;
                templateLabel.rectTransform.anchorMax = Vector2.one;
                templateLabel.rectTransform.offsetMin = new Vector2(24f, 4f);
                templateLabel.rectTransform.offsetMax = new Vector2(-24f, -4f);
                templateLabel.fontSize = 40f;
                templateLabel.fontSizeMin = 22f;
                templateLabel.fontSizeMax = 40f;
                templateLabel.enableAutoSizing = true;
                templateObject.SetActive(false);

                var textGroup = content.AddComponent<ButtonTextGroup>();
                textGroup.SetScan(true);
                var list = viewport.AddComponent<ButtonList>();
                Set(list, "template", templateObject.GetComponent<Button>());
                Set(list, "content", contentRect);
                var scroll = viewport.GetComponent<ScrollRect>();
                scroll.viewport = viewRect;
                scroll.content = contentRect;
                Set(list, "scroll", scroll);

                string longNote = "<b>전용 기술</b>\n<size=85%>" + new string('가', 200) + "</size>";
                list.Show(new[] { new ButtonList.Row(longNote, Color.white, null, false) });
                list.FitReadOnlyRows(40f);

                var row = content.transform.Find("Row0");
                var label = row.GetComponentInChildren<TMP_Text>();
                var layout = row.GetComponent<LayoutElement>();
                Assert.AreEqual(40f, label.fontSizeMin);
                Assert.AreEqual(40f, label.fontSizeMax);
                Assert.Greater(layout.preferredHeight, 198f, "긴 설명은 고정 높이에서 잘리면 안 됩니다");
                Assert.GreaterOrEqual(layout.preferredHeight - 48f, label.GetPreferredValues(longNote, label.rectTransform.rect.width, 0f).y - 0.5f);
                Assert.IsFalse(textGroup.enabled, "설명 읽는 동안 버튼 글꼴 묶음이 글씨를 줄이면 안 됩니다");

                list.Show(new[] { new ButtonList.Row("보상 선택", Color.white, () => { }) });
                Assert.IsTrue(textGroup.enabled);
                Assert.AreEqual(170f, layout.preferredHeight);
                Assert.AreEqual(templateLabel.fontSizeMin, label.fontSizeMin);
                Assert.AreEqual(templateLabel.fontSizeMax, label.fontSizeMax);
                Assert.IsTrue(label.enableAutoSizing);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(canvasObject);
            }
        }

        private static void Set(object instance, string field, object value)
            => instance.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(instance, value);
    }
}
