using System.Reflection;
using HistoryHeroes.Records;
using HistoryHeroes.UI;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace HistoryHeroes.Tests
{
    public sealed class MissionPresentationTests
    {
        [Test]
        public void ProgressAmount_ClampsAndUsesCompletedState()
        {
            var mission = new MissionProgress { target = 10, progress = 4 };
            Assert.AreEqual(0.4f, MissionPanel.ProgressAmount(mission), 0.001f);
            mission.progress = 15;
            Assert.AreEqual(1f, MissionPanel.ProgressAmount(mission));
            mission.progress = 0;
            mission.done = true;
            Assert.AreEqual(1f, MissionPanel.ProgressAmount(mission));
            mission.done = false;
            mission.target = 0;
            Assert.AreEqual(0f, MissionPanel.ProgressAmount(mission));
        }

        [Test]
        public void ButtonList_ProgressChangesWidthAndHidesWhenAbsent()
        {
            var root = new GameObject("Root", typeof(RectTransform));
            try
            {
                RectTransform content = new GameObject("Content", typeof(RectTransform)).GetComponent<RectTransform>();
                content.SetParent(root.transform, false);
                var templateObject = new GameObject("RowTemplate", typeof(RectTransform), typeof(Image), typeof(Button));
                templateObject.transform.SetParent(content, false);
                new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI)).transform.SetParent(templateObject.transform, false);
                var track = new GameObject("ProgressTrack", typeof(RectTransform));
                track.transform.SetParent(templateObject.transform, false);
                new GameObject("ProgressFill", typeof(RectTransform), typeof(Image)).transform.SetParent(track.transform, false);

                ButtonList list = root.AddComponent<ButtonList>();
                typeof(ButtonList).GetField("template", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(list, templateObject.GetComponent<Button>());
                typeof(ButtonList).GetField("content", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(list, content);

                list.Show(new[] { new ButtonList.Row("시험", Color.white, null, false, 0.4f) });
                Transform shownTrack = content.Find("Row0/ProgressTrack");
                Assert.IsTrue(shownTrack.gameObject.activeSelf);
                Assert.AreEqual(0.4f, ((RectTransform)shownTrack.Find("ProgressFill")).anchorMax.x, 0.001f);

                list.Show(new[] { new ButtonList.Row("시험", Color.white, null, false) });
                Assert.IsFalse(shownTrack.gameObject.activeSelf);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }
    }
}
