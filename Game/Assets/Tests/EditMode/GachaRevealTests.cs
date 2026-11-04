using System;
using System.Collections.Generic;
using System.Reflection;
using HistoryHeroes.Equipment;
using HistoryHeroes.UI;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace HistoryHeroes.Tests
{
    public sealed class GachaRevealTests
    {
        private GameObject _canvas;
        private GameObject _host;
        private GachaReveal _reveal;

        [SetUp]
        public void SetUp()
        {
            _canvas = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas));
            _host = new GameObject("RevealHost", typeof(RectTransform));
            _host.transform.SetParent(_canvas.transform, false);
            _host.GetComponent<RectTransform>().sizeDelta = new Vector2(1080f, 1920f);
            var template = new GameObject("Template", typeof(RectTransform), typeof(TextMeshProUGUI)).GetComponent<TextMeshProUGUI>();
            template.font = UnityEditor.AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Fonts/NanumGothic-Bold SDF.asset");
            template.fontSharedMaterial = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>("Assets/Fonts/NanumGothic-Bold SDF Body.mat");
            template.transform.SetParent(_host.transform, false);
            _reveal = _host.AddComponent<GachaReveal>();
            _reveal.Initialize(template);
        }

        [TearDown]
        public void TearDown() => UnityEngine.Object.DestroyImmediate(_canvas);

        [Test]
        public void NewRewardLabelsUseTheExistingFontMaterial()
        {
            TMP_Text template = _host.transform.Find("Template").GetComponent<TMP_Text>();
            _reveal.Show("일반 상자 개봉", null, null,
                new[] { new GachaReveal.Entry("수습병의 허리띠", "일반 · 장비 획득", null, Color.white) }, 0, false, false, null);
            foreach (TMP_Text label in _host.transform.Find("GachaReveal").GetComponentsInChildren<TMP_Text>(true))
            {
                Assert.AreSame(template.font, label.font);
                Assert.AreSame(template.fontSharedMaterial, label.fontSharedMaterial, label.name);
            }
        }

        [Test]
        public void SkipAndClose_ExposeAllCommittedItemsOnce_ThenRestoreForNextOpening()
        {
            int closed = 0;
            var entries = new List<GachaReveal.Entry>();
            for (int i = 0; i < 10; i++)
                entries.Add(new GachaReveal.Entry("아이템 " + i, "희귀", null, Color.cyan));

            _reveal.Show("10회 결과", null, null, entries, 9, false, false, () => closed++);
            Assert.IsTrue(_reveal.IsOpen);
            Assert.AreEqual(10, _reveal.VisibleCardCount);
            Assert.IsFalse(_reveal.ResultsVisible);

            _reveal.Skip();
            Assert.IsTrue(_reveal.ResultsVisible);
            Assert.AreEqual(10, _reveal.VisibleCardCount);
            var visibleNames = _host.GetComponentsInChildren<TMP_Text>(true);
            Assert.IsTrue(Array.Exists(visibleNames, label => label.text == "아이템 0"));
            Assert.IsTrue(Array.Exists(visibleNames, label => label.text == "아이템 9"));
            foreach (CanvasGroup card in _host.GetComponentsInChildren<CanvasGroup>(true))
                if (card.name == "ResultCard")
                {
                    Assert.AreEqual(1f, card.alpha);
                    Assert.AreEqual(Vector3.one, card.transform.localScale);
                }
            _reveal.Hide();
            _reveal.Hide();
            Assert.AreEqual(1, closed, "닫기와 비활성화가 겹쳐도 입력 잠금은 한 번만 해제");
            Assert.IsFalse(_reveal.IsOpen);

            _reveal.Show("1회 결과", null, null, new[] { new GachaReveal.Entry("새 유물", "NEW", null, Color.yellow) }, 0, true, true, () => closed++);
            _reveal.Skip();
            Assert.AreEqual(1, _reveal.VisibleCardCount);
            Assert.AreEqual(1, Array.FindAll(_host.GetComponentsInChildren<CanvasGroup>(true), card => card.name == "ResultCard").Length);
            _host.SetActive(false);
            // EditMode는 MonoBehaviour 비활성화 콜백을 자동 실행하지 않으므로 계약을 직접 검사한다.
            typeof(GachaReveal).GetMethod("OnDisable", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(_reveal, null);
            Assert.AreEqual(2, closed, "화면 전환 중에도 잠금이 남지 않음");
        }

        [Test]
        public void EquipmentAtlas_ProvidesAllSixSlotPictures()
        {
            Sprite[] art = Resources.LoadAll<Sprite>("UI/Gacha/equipment");
            var select = typeof(ShopPanel).GetMethod("EquipmentIcon", BindingFlags.NonPublic | BindingFlags.Static);
            Assert.IsNotNull(select);
            foreach (EquipSlot slot in Enum.GetValues(typeof(EquipSlot)))
            {
                string expected = "equip_" + slot.ToString().ToLowerInvariant();
                Assert.IsTrue(Array.Exists(art, sprite => sprite.name == expected), expected + " sprite import");
                Sprite selected = (Sprite)select.Invoke(null, new object[] { slot, art });
                Assert.IsNotNull(selected, expected + " slot selection");
                Assert.AreEqual(expected, selected.name);
            }
        }

        [TestCase(1600f)]
        [TestCase(1720f)]
        [TestCase(2140f)]
        public void PortraitHeight_KeepsTitleResultsAndConfirmationInsidePage(float height)
        {
            RectTransform host = _host.GetComponent<RectTransform>();
            host.sizeDelta = new Vector2(1080f, height);
            var entries = new List<GachaReveal.Entry>();
            for (int i = 0; i < 10; i++) entries.Add(new GachaReveal.Entry("장비 " + i, "희귀", null, Color.cyan));
            _reveal.Show("10회 결과", null, null, entries, 9, false, false, null);
            _reveal.Skip();
            Canvas.ForceUpdateCanvases();

            RectTransform panel = (RectTransform)_host.transform.Find("GachaReveal");
            RectTransform title = (RectTransform)panel.Find("Title");
            RectTransform viewport = (RectTransform)panel.Find("ResultsViewport");
            RectTransform confirm = (RectTransform)panel.Find("Continue");
            Bounds RectBounds(RectTransform target)
            {
                var corners = new Vector3[4];
                target.GetWorldCorners(corners);
                var bounds = new Bounds(host.InverseTransformPoint(corners[0]), Vector3.zero);
                for (int i = 1; i < corners.Length; i++) bounds.Encapsulate(host.InverseTransformPoint(corners[i]));
                return bounds;
            }
            Bounds titleBounds = RectBounds(title);
            Bounds resultBounds = RectBounds(viewport);
            Bounds confirmBounds = RectBounds(confirm);
            Assert.LessOrEqual(titleBounds.max.y, host.rect.yMax);
            Assert.GreaterOrEqual(confirmBounds.min.y, host.rect.yMin);
            Assert.Greater(resultBounds.min.y, confirmBounds.max.y);
            Assert.Less(resultBounds.max.y, titleBounds.min.y);
            ScrollRect scroll = viewport.GetComponent<ScrollRect>();
            Assert.GreaterOrEqual(scroll.content.rect.height, scroll.viewport.rect.height);
        }
    }
}
