using System;
using System.Collections;
using System.Reflection;
using HistoryHeroes.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace HistoryHeroes.Tests
{
    public sealed class HeroPortraitsPerformanceTests
    {
        private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;

        [Test]
        public void OnEnable_BeforeReferencesInjected_DoesNotBuildEmptyStage()
        {
            var owner = new GameObject("PortraitOwner");
            try
            {
                var portraits = owner.AddComponent<HeroPortraits>();
                typeof(HeroPortraits).GetMethod("OnEnable", PrivateInstance).Invoke(portraits, null);
                Assert.IsNull(typeof(HeroPortraits).GetField("_stage", PrivateInstance).GetValue(portraits));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(owner);
            }
        }

        [Test]
        public void FocusChange_StopsPreviousAnimatorAndKeepsFocusedAnimatorRunning()
        {
            var owner = new GameObject("PortraitOwner");
            owner.SetActive(false);
            var first = new GameObject("First").AddComponent<Animator>();
            var second = new GameObject("Second").AddComponent<Animator>();
            try
            {
                var portraits = owner.AddComponent<HeroPortraits>();
                Type slotType = typeof(HeroPortraits).GetNestedType("Slot", BindingFlags.NonPublic);
                IList slots = (IList)typeof(HeroPortraits).GetField("_slots", PrivateInstance).GetValue(portraits);
                slots.Add(Slot(slotType, 0, first));
                slots.Add(Slot(slotType, 1, second));

                portraits.SetFocus(0);
                portraits.SetFocus(1);
                Assert.IsFalse(first.enabled, "이전 영웅은 더는 애니메이션을 평가하지 않는다");
                Assert.IsTrue(second.enabled, "고른 영웅은 움직인다");

                portraits.SetFocus(0);
                Assert.IsTrue(first.enabled);
                Assert.IsFalse(second.enabled);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(owner);
                UnityEngine.Object.DestroyImmediate(first.gameObject);
                UnityEngine.Object.DestroyImmediate(second.gameObject);
            }
        }

        [Test]
        public void SetLocked_BeforeLazyTextureCreation_KeepsBadgeAndTint()
        {
            var owner = new GameObject("PortraitOwner");
            owner.SetActive(false);
            var imageObject = new GameObject("Image", typeof(RectTransform), typeof(RawImage));
            var badge = new GameObject("LockBack");
            badge.transform.SetParent(imageObject.transform);
            try
            {
                var portraits = owner.AddComponent<HeroPortraits>();
                var image = imageObject.GetComponent<RawImage>();
                typeof(HeroPortraits).GetField("images", PrivateInstance).SetValue(portraits, new[] { image });
                Assert.IsNull(image.texture);

                portraits.SetLocked(0, true);
                Assert.IsTrue(badge.activeSelf);
                Assert.Less(image.color.r, 1f);
                Assert.AreEqual(0f, image.color.a);

                portraits.SetLocked(0, false);
                Assert.IsFalse(badge.activeSelf);
                Assert.AreEqual(0f, image.color.a);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(owner);
                UnityEngine.Object.DestroyImmediate(imageObject);
            }
        }

        [Test]
        public void Viewport_OnlyMarksIntersectingPortraitsForLazyCreation()
        {
            var owner = new GameObject("PortraitOwner");
            owner.SetActive(false);
            var viewportObject = new GameObject("Viewport", typeof(RectTransform), typeof(ScrollRect));
            var nearObject = new GameObject("Near", typeof(RectTransform), typeof(RawImage));
            var farObject = new GameObject("Far", typeof(RectTransform), typeof(RawImage));
            try
            {
                var portraits = owner.AddComponent<HeroPortraits>();
                var viewport = viewportObject.GetComponent<RectTransform>();
                viewport.sizeDelta = new Vector2(100f, 100f);
                var scroll = viewportObject.GetComponent<ScrollRect>();
                scroll.viewport = viewport;
                nearObject.transform.SetParent(viewport, false);
                farObject.transform.SetParent(viewport, false);
                nearObject.GetComponent<RectTransform>().sizeDelta = new Vector2(40f, 40f);
                farObject.GetComponent<RectTransform>().sizeDelta = new Vector2(40f, 40f);
                farObject.GetComponent<RectTransform>().anchoredPosition = new Vector2(200f, 0f);
                typeof(HeroPortraits).GetField("images", PrivateInstance).SetValue(portraits,
                    new[] { nearObject.GetComponent<RawImage>(), farObject.GetComponent<RawImage>() });
                typeof(HeroPortraits).GetField("_scroll", PrivateInstance).SetValue(portraits, scroll);
                MethodInfo isVisible = typeof(HeroPortraits).GetMethod("IsVisible", PrivateInstance);

                Assert.IsTrue((bool)isVisible.Invoke(portraits, new object[] { 0, viewport.rect }));
                Assert.IsFalse((bool)isVisible.Invoke(portraits, new object[] { 1, viewport.rect }));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(owner);
                UnityEngine.Object.DestroyImmediate(viewportObject);
            }
        }

        private static object Slot(Type type, int index, Animator animator)
        {
            object slot = Activator.CreateInstance(type, true);
            type.GetField("Index").SetValue(slot, index);
            type.GetField("Animator").SetValue(slot, animator);
            return slot;
        }
    }
}
