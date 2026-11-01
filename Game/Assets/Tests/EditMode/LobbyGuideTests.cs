using System.Reflection;
using HistoryHeroes.UI;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace HistoryHeroes.Tests
{
    public sealed class LobbyGuideTests
    {
        [Test]
        public void Guide_BlocksBehindPanelsFocusesTargetAndDismissesWithoutRepeating()
        {
            var root = new GameObject("LobbyGuideTest", typeof(RectTransform));
            try
            {
                var guide = root.AddComponent<LobbyGuide>();
                var banner = new GameObject("Banner", typeof(RectTransform), typeof(Image), typeof(Button));
                banner.transform.SetParent(root.transform, false);
                var label = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI)).GetComponent<TMP_Text>();
                label.transform.SetParent(banner.transform, false);
                var target = new GameObject("Target", typeof(RectTransform), typeof(Image), typeof(Button)).GetComponent<Button>();
                target.transform.SetParent(root.transform, false);
                var focus = new GameObject("Focus", typeof(RectTransform)).GetComponent<RectTransform>();
                focus.SetParent(root.transform, false);
                var blocker = new GameObject("Panel");
                blocker.transform.SetParent(root.transform, false);
                Set(guide, "banner", banner);
                Set(guide, "label", label);
                Set(guide, "dismissButton", banner.GetComponent<Button>());
                Set(guide, "focus", focus);
                Set(guide, "blockers", new[] { blocker });
                typeof(LobbyGuide).GetMethod("Awake", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(guide, null);

                Assert.IsFalse(guide.Show("안내", target));
                Assert.IsFalse(guide.Used);
                blocker.SetActive(false);
                Assert.IsTrue(guide.Show("안내", target));
                Assert.AreEqual(target.transform, focus.parent);
                Assert.IsTrue(guide.IsOpen);
                Assert.IsTrue(focus.gameObject.activeSelf);
                target.onClick.Invoke();
                Assert.IsFalse(guide.IsOpen);
                Assert.IsFalse(focus.gameObject.activeSelf);
                Assert.IsFalse(guide.Show("다음 안내", target));
                Assert.IsTrue(guide.Used);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static void Set(object instance, string name, object value) =>
            instance.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance).SetValue(instance, value);

        [Test]
        public void Guide_SaveFailureDefersHintAndAttemptsOnlyOncePerLobby()
        {
            var root = new GameObject("LobbyGuideFailureTest", typeof(RectTransform));
            try
            {
                var guide = root.AddComponent<LobbyGuide>();
                var banner = new GameObject("Banner", typeof(RectTransform));
                banner.transform.SetParent(root.transform, false);
                banner.SetActive(false);
                var label = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI)).GetComponent<TMP_Text>();
                label.transform.SetParent(banner.transform, false);
                var target = new GameObject("Target", typeof(RectTransform), typeof(Button)).GetComponent<Button>();
                target.transform.SetParent(root.transform, false);
                Set(guide, "banner", banner);
                Set(guide, "label", label);
                int attempts = 0;
                bool Remember() { attempts++; return false; }
                Assert.IsFalse(guide.Show("안내", target, Remember));
                Assert.IsFalse(guide.Show("안내", target, Remember));
                Assert.AreEqual(1, attempts);
                Assert.IsFalse(guide.IsOpen);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }
    }
}
