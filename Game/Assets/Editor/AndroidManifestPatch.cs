using System.IO;
using System.Xml;
using UnityEditor.Android;
using UnityEngine;

namespace HistoryHeroes.EditorTools
{
    /// <summary>
    /// 안드로이드 빌드 매니페스트에 꼭 필요한 권한을 넣는다.
    /// 진동(Haptics)은 자바 Vibrator를 직접 불러서 Unity가 VIBRATE 권한을 스스로 넣지 않는다 (Handheld.Vibrate를 쓸 때만 넣는다).
    /// 권한이 없으면 폰에서 진동이 SecurityException으로 조용히 빠진다 (v1.0 폰 시험에서 찾음).
    /// </summary>
    public sealed class AndroidManifestPatch : IPostGenerateGradleAndroidProject
    {
        private const string AndroidNs = "http://schemas.android.com/apk/res/android";
        private static readonly string[] Permissions = { "android.permission.VIBRATE" };

        public int callbackOrder => 10;

        public void OnPostGenerateGradleAndroidProject(string path)
        {
            string manifestPath = Path.Combine(path, "src", "main", "AndroidManifest.xml");
            if (!File.Exists(manifestPath))
            {
                Debug.LogWarning("[AndroidManifestPatch] 매니페스트가 없음: " + manifestPath);
                return;
            }

            var doc = new XmlDocument();
            doc.Load(manifestPath);
            XmlElement manifest = doc.DocumentElement;
            if (manifest == null) return;

            bool changed = false;
            foreach (string permission in Permissions)
            {
                if (Has(manifest, permission)) continue;
                XmlElement node = doc.CreateElement("uses-permission");
                node.SetAttribute("name", AndroidNs, permission);
                manifest.PrependChild(node);
                changed = true;
            }

            if (changed) doc.Save(manifestPath);
        }

        private static bool Has(XmlElement manifest, string permission)
        {
            foreach (XmlNode child in manifest.ChildNodes)
            {
                if (child is XmlElement e && e.Name == "uses-permission" && e.GetAttribute("name", AndroidNs) == permission) return true;
            }

            return false;
        }
    }
}
