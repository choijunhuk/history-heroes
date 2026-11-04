using HistoryHeroes.UI;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace HistoryHeroes.Tests
{
    public sealed class HeroPageLayoutTests
    {
        private Scene _scene;
        private RectTransform _panel;
        private RectTransform _view;
        private ScrollRect _scroll;

        [SetUp]
        public void OpenBuiltLobby()
        {
            _scene = EditorSceneManager.OpenPreviewScene("Assets/Scenes/Lobby.unity");
            foreach (GameObject root in _scene.GetRootGameObjects())
            {
                var holder = Find(root.transform, "HeroPanel");
                if (holder == null) continue;
                _panel = holder.Find("Panel") as RectTransform;
                break;
            }

            Assert.IsNotNull(_panel, "생성된 Lobby 장면에 영웅 화면이 있어야 합니다");
            _view = _panel.Find("GrowthViewport") as RectTransform;
            Assert.IsNotNull(_view, "성장 행동과 영웅 목록은 한 스크롤 영역이어야 합니다");
            _scroll = _view.GetComponent<ScrollRect>();
        }

        [TearDown]
        public void CloseBuiltLobby()
        {
            if (_scene.IsValid()) EditorSceneManager.ClosePreviewScene(_scene);
        }

        [TestCase(1720f)]
        [TestCase(2140f)]
        [TestCase(1600f)]
        public void GrowthActionsAndHeroRosterDoNotOverlap(float availableHeight)
        {
            SetAvailableHeight(availableHeight);
            Transform content = _scroll.content;
            string[] names = { "Power", "HeroPet", "HeroLevelUp", "Training", "HeroScrollViewport", "HeroMessage" };
            for (int i = 0; i < names.Length - 1; i++)
            {
                var upper = content.Find(names[i]) as RectTransform;
                var lower = content.Find(names[i + 1]) as RectTransform;
                Assert.IsNotNull(upper, names[i]);
                Assert.IsNotNull(lower, names[i + 1]);
                Assert.GreaterOrEqual(Bottom(upper) - Top(lower), 10f,
                    $"{availableHeight}: {names[i]}와 {names[i + 1]}가 겹치거나 너무 붙었습니다");
            }
        }

        [Test]
        public void NarrowSafeAreaCanScrollToEntireHeroRoster()
        {
            SetAvailableHeight(1600f);
            Assert.IsTrue(_scroll.vertical);
            Assert.IsFalse(_scroll.horizontal);
            Assert.IsTrue((_scroll.content.Find("HeroScrollViewport") as RectTransform).GetComponent<ScrollRect>().horizontal);
            var roster = _scroll.content.Find("HeroScrollViewport") as RectTransform;
            float travel = _scroll.content.rect.height - _view.rect.height;
            Assert.Greater(travel, 0f, "좁은 화면에서는 세로로 움직일 공간이 있어야 합니다");
            Assert.GreaterOrEqual(Bottom(roster) + travel, Bottom(_view) - 0.5f);
            Assert.LessOrEqual(Top(roster) + travel, Top(_view) + 0.5f);
            Assert.GreaterOrEqual(_view.offsetMin.y, 0f, "하단 탭 영역으로 내용이 내려가면 안 됩니다");
        }

        [Test]
        public void HeroPortraitAndHeaderRemainOutsideGrowthScroll()
        {
            Assert.AreEqual(_panel, _panel.Find("BigPortrait").parent);
            Assert.AreEqual(_panel, _panel.Find("DetailCard").parent);
            Assert.AreEqual(_panel, _panel.Find("TopBar").parent);
        }

        private void SetAvailableHeight(float height)
        {
            _panel.anchorMin = _panel.anchorMax = new Vector2(0.5f, 0.5f);
            _panel.sizeDelta = new Vector2(1080f, height);
            LayoutRebuilder.ForceRebuildLayoutImmediate(_panel);
            Canvas.ForceUpdateCanvases();
        }

        private static Transform Find(Transform root, string name)
        {
            if (root.name == name) return root;
            foreach (Transform child in root)
            {
                Transform found = Find(child, name);
                if (found != null) return found;
            }
            return null;
        }

        private float Top(RectTransform rect) => PositionInPanel(rect, rect.rect.yMax);

        private float Bottom(RectTransform rect) => PositionInPanel(rect, rect.rect.yMin);

        private float PositionInPanel(RectTransform rect, float localY)
        {
            float y = localY;
            while (rect != _panel)
            {
                var parent = rect.parent as RectTransform;
                Assert.IsNotNull(parent, "검사 대상은 영웅 화면 아래에 있어야 합니다");
                float anchor = Mathf.Lerp(rect.anchorMin.y, rect.anchorMax.y, rect.pivot.y);
                y += (anchor - parent.pivot.y) * parent.rect.height + rect.anchoredPosition.y;
                rect = parent;
            }
            return y;
        }
    }
}
