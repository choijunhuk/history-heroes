using HistoryHeroes.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace HistoryHeroes.Tests
{
    public sealed class HeroRosterScrollTests
    {
        private static RectTransform Rect(string name, Transform parent, Vector2 size)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.sizeDelta = size;
            return rect;
        }

        private static PointerEventData Pointer(EventSystem system, Vector2 start, Vector2 begin)
        {
            return new PointerEventData(system)
            {
                button = PointerEventData.InputButton.Left,
                pressPosition = start,
                position = begin,
            };
        }

        [TestCase(true)]
        [TestCase(false)]
        public void NestedRosterRoutesOnlyVerticalDragToGrowthScroll(bool verticalDrag)
        {
            var root = new GameObject("TestCanvas", typeof(RectTransform), typeof(Canvas), typeof(EventSystem));
            try
            {
                var view = Rect("GrowthViewport", root.transform, new Vector2(200f, 200f));
                var growth = view.gameObject.AddComponent<ScrollRect>();
                var growthContent = Rect("GrowthContent", view, new Vector2(200f, 600f));
                growth.viewport = view;
                growth.content = growthContent;
                growth.vertical = true;
                growth.horizontal = false;
                growth.movementType = ScrollRect.MovementType.Unrestricted;

                var rosterView = Rect("HeroScrollViewport", growthContent, new Vector2(180f, 100f));
                var roster = rosterView.gameObject.AddComponent<HeroRosterScroll>();
                var rosterContent = Rect("HeroScrollContent", rosterView, new Vector2(500f, 100f));
                roster.viewport = rosterView;
                roster.content = rosterContent;
                roster.horizontal = true;
                roster.vertical = false;
                roster.movementType = ScrollRect.MovementType.Unrestricted;

                Canvas.ForceUpdateCanvases();
                var start = new Vector2(100f, 100f);
                var data = Pointer(root.GetComponent<EventSystem>(), start,
                    verticalDrag ? start + new Vector2(2f, 20f) : start + new Vector2(20f, 2f));
                roster.OnInitializePotentialDrag(data);
                roster.OnBeginDrag(data);
                data.position = verticalDrag ? start + new Vector2(4f, 65f) : start + new Vector2(65f, 4f);
                roster.OnDrag(data);
                roster.OnEndDrag(data);

                if (verticalDrag)
                {
                    Assert.Greater(Mathf.Abs(growthContent.anchoredPosition.y), 1f);
                    Assert.That(rosterContent.anchoredPosition.x, Is.EqualTo(0f).Within(0.001f));
                }
                else
                {
                    Assert.Greater(Mathf.Abs(rosterContent.anchoredPosition.x), 1f);
                    Assert.That(growthContent.anchoredPosition.y, Is.EqualTo(0f).Within(0.001f));
                }
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void RosterWithoutVerticalParentKeepsItsOwnHorizontalDrag()
        {
            var root = new GameObject("TestCanvas", typeof(RectTransform), typeof(Canvas), typeof(EventSystem));
            try
            {
                var view = Rect("RosterOnly", root.transform, new Vector2(180f, 100f));
                var roster = view.gameObject.AddComponent<HeroRosterScroll>();
                var content = Rect("Content", view, new Vector2(500f, 100f));
                roster.viewport = view;
                roster.content = content;
                roster.horizontal = true;
                roster.vertical = false;
                roster.movementType = ScrollRect.MovementType.Unrestricted;

                Canvas.ForceUpdateCanvases();
                var data = Pointer(root.GetComponent<EventSystem>(), new Vector2(100f, 100f), new Vector2(130f, 102f));
                roster.OnInitializePotentialDrag(data);
                roster.OnBeginDrag(data);
                data.position = new Vector2(160f, 103f);
                roster.OnDrag(data);
                roster.OnEndDrag(data);

                Assert.Greater(Mathf.Abs(content.anchoredPosition.x), 1f);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }
    }
}
