using System.Reflection;
using HistoryHeroes.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace HistoryHeroes.Tests
{
    public sealed class ButtonJuiceTests
    {
        private static readonly MethodInfo Update = typeof(ButtonJuice).GetMethod("Update", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly MethodInfo Awake = typeof(ButtonJuice).GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly MethodInfo Disable = typeof(ButtonJuice).GetMethod("OnDisable", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly FieldInfo Scale = typeof(ButtonJuice).GetField("_scale", BindingFlags.Instance | BindingFlags.NonPublic);

        [Test]
        public void SelectedTabScaleSurvivesPressAndDisable()
        {
            var owner = new GameObject("SelectedTab", typeof(RectTransform), typeof(Button), typeof(ButtonJuice));
            try
            {
                var juice = owner.GetComponent<ButtonJuice>();
                Awake.Invoke(juice, null);
                var selected = new Vector3(1.05f, 1.16f, 1f);
                owner.transform.localScale = selected;
                juice.OnPointerDown(null);
                Update.Invoke(juice, null);
                Assert.Greater(owner.transform.localScale.x, 0.97f);
                Assert.Greater(owner.transform.localScale.y, 1.07f);
                Disable.Invoke(juice, null);
                Assert.Less(Vector3.Distance(owner.transform.localScale, selected), 0.0001f);
            }
            finally
            {
                Object.DestroyImmediate(owner);
            }
        }

        [Test]
        public void ExternalScaleChangeDuringPressBecomesNewRestingSize()
        {
            var owner = new GameObject("ChangingTab", typeof(RectTransform), typeof(Button), typeof(ButtonJuice));
            try
            {
                var juice = owner.GetComponent<ButtonJuice>();
                Awake.Invoke(juice, null);
                Scale.SetValue(juice, 0.93f);
                Update.Invoke(juice, null);
                var selected = new Vector3(1.05f, 1.16f, 1f);
                owner.transform.localScale = selected;
                Update.Invoke(juice, null);
                Disable.Invoke(juice, null);
                Assert.Less(Vector3.Distance(owner.transform.localScale, selected), 0.0001f);
            }
            finally
            {
                Object.DestroyImmediate(owner);
            }
        }

        [Test]
        public void OrdinaryButtonStillResetsToItsOriginalSize()
        {
            var owner = new GameObject("Ordinary", typeof(RectTransform), typeof(Button), typeof(ButtonJuice));
            try
            {
                var juice = owner.GetComponent<ButtonJuice>();
                Awake.Invoke(juice, null);
                Scale.SetValue(juice, 0.93f);
                Update.Invoke(juice, null);
                Disable.Invoke(juice, null);
                Assert.Less(Vector3.Distance(owner.transform.localScale, Vector3.one), 0.0001f);
            }
            finally
            {
                Object.DestroyImmediate(owner);
            }
        }
    }
}
