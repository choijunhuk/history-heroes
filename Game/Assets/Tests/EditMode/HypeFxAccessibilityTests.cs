using System;
using System.IO;
using System.Reflection;
using HistoryHeroes.Common;
using HistoryHeroes.Records;
using HistoryHeroes.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace HistoryHeroes.Tests
{
    public sealed class HypeFxAccessibilityTests
    {
        private const BindingFlags PrivateStatic = BindingFlags.NonPublic | BindingFlags.Static;
        private const BindingFlags PrivateInstance = BindingFlags.NonPublic | BindingFlags.Instance;

        [TestCase(true, 0f, 0f)]
        [TestCase(false, 0.9f, 0.65f)]
        public void BossDown_RespectsReducedFlashesWithoutMutingNormalImpact(bool reduce, float trauma, float flashAlpha)
        {
            string folder = Path.Combine(Path.GetTempPath(), "hh-hypefx-" + Guid.NewGuid().ToString("N"));
            SaveStore previousStore = SaveSystem.Store;
            float previousTimeScale = Time.timeScale;
            var fxField = typeof(HypeFx).GetField("_instance", PrivateStatic);
            var shakeField = typeof(CameraShake).GetField("_instance", PrivateStatic);
            object previousFx = fxField.GetValue(null);
            object previousShake = shakeField.GetValue(null);
            GameObject camera = null;
            GameObject owner = null;
            try
            {
                Directory.CreateDirectory(folder);
                SaveSystem.Use(new SaveStore(folder));
                SaveSystem.Data.settings.reduceFlashes = reduce;
                GamePause.ResetAll();

                camera = new GameObject("Shake");
                camera.SetActive(false);
                camera.AddComponent<Camera>();
                var shake = camera.AddComponent<CameraShake>();
                typeof(CameraShake).GetMethod("Awake", PrivateInstance).Invoke(shake, null);
                typeof(CameraShake).GetMethod("OnEnable", PrivateInstance).Invoke(shake, null);
                owner = new GameObject("Hype", typeof(RectTransform));
                owner.SetActive(false);
                var visual = owner.AddComponent<HypeFx>();
                var flash = new GameObject("Flash", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
                flash.transform.SetParent(owner.transform, false);
                typeof(HypeFx).GetField("flash", PrivateInstance).SetValue(visual, flash);
                typeof(HypeFx).GetMethod("OnEnable", PrivateInstance).Invoke(visual, null);

                HypeFx.BossDown(Vector3.zero);

                Assert.AreEqual(trauma, CameraShake.Trauma, 0.001f);
                Assert.AreEqual(flashAlpha, (float)typeof(HypeFx).GetField("_flashAlpha", PrivateInstance).GetValue(visual), 0.001f);
                Assert.AreEqual(flashAlpha > 0f, flash.gameObject.activeSelf);
            }
            finally
            {
                if (owner != null) typeof(HypeFx).GetMethod("OnDisable", PrivateInstance).Invoke(owner.GetComponent<HypeFx>(), null);
                if (camera != null) typeof(CameraShake).GetMethod("OnDisable", PrivateInstance).Invoke(camera.GetComponent<CameraShake>(), null);
                if (owner != null) UnityEngine.Object.DestroyImmediate(owner);
                if (camera != null) UnityEngine.Object.DestroyImmediate(camera);
                fxField.SetValue(null, previousFx);
                shakeField.SetValue(null, previousShake);
                GamePause.ResetAll();
                Time.timeScale = previousTimeScale;
                SaveSystem.Use(previousStore);
                if (Directory.Exists(folder)) Directory.Delete(folder, true);
            }
        }
    }
}
