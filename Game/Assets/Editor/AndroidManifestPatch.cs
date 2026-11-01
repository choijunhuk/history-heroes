using System;
using System.IO;
using System.Xml;
using UnityEditor;
using UnityEditor.Android;
using UnityEditor.Build;
using UnityEngine;

namespace HistoryHeroes.EditorTools
{
    /// <summary>
    /// 안드로이드 빌드의 Gradle 프로젝트를 고친다.
    /// 1) 꼭 필요한 권한: 진동(Haptics)은 자바 Vibrator를 직접 불러서 Unity가 VIBRATE 권한을 스스로 넣지 않는다 (Handheld.Vibrate를 쓸 때만 넣는다).
    ///    권한이 없으면 폰에서 진동이 SecurityException으로 조용히 빠진다 (v1.0 폰 시험에서 찾음).
    /// 2) 카카오 로그인 (LiveOps 3-1): 네이티브 앱 키를 저장소 밖 ~/.history-heroes/kakao.properties(nativeAppKey=...)에서 읽어
    ///    매니페스트에 키 메타데이터 + 리다이렉트 액티비티(kakao&lt;키&gt;://oauth)를 넣는다. 파일이 없으면 넣지 않는다 → 빌드는 되고 게임은 카카오 버튼을 숨긴다.
    /// 3) 카카오 SDK 저장소: Unity의 settings.gradle은 RepositoriesMode.PREFER_SETTINGS라 라이브러리의 저장소 선언이 무시된다 → 여기서 넣는다.
    /// </summary>
    public sealed class AndroidManifestPatch : IPostGenerateGradleAndroidProject
    {
        private const string AndroidNs = "http://schemas.android.com/apk/res/android";
        private static readonly string[] Permissions = { "android.permission.VIBRATE" };

        /// <summary>KakaoBridge.java가 읽는 메타데이터 이름</summary>
        private const string KakaoKeyMeta = "com.choijunhuk.historyheroes.KAKAO_APP_KEY";
        private const string KakaoRedirectActivity = "com.kakao.sdk.auth.AuthCodeHandlerActivity";
        private const string KakaoRepository = "https://devrepo.kakao.com/nexus/content/groups/public/";

        public int callbackOrder => 10;

        public void OnPostGenerateGradleAndroidProject(string path)
        {
            AddKakaoRepository(Path.Combine(path, "..", "settings.gradle"));

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
                if (Has(manifest, "uses-permission", permission)) continue;
                XmlElement node = doc.CreateElement("uses-permission");
                node.SetAttribute("name", AndroidNs, permission);
                manifest.PrependChild(node);
                changed = true;
            }

            changed |= AddKakaoLogin(doc, manifest);
            if (changed) doc.Save(manifestPath);
        }

        /// <summary>앱 키 파일이 있으면 메타데이터 + 리다이렉트 액티비티. 키 값은 로그에 남기지 않는다</summary>
        private static bool AddKakaoLogin(XmlDocument doc, XmlElement manifest)
        {
            string key = ReadKakaoKey();
            if (key == null)
            {
                // 다른 컴퓨터에서는 키 없이 빌드해도 된다 (게임은 카카오 버튼을 숨긴다)
                Debug.LogWarning("[AndroidManifestPatch] ⚠ 카카오 앱 키 파일이 없음 (~/.history-heroes/kakao.properties, nativeAppKey=...) → 이 빌드에는 카카오 로그인이 없다");
                return false;
            }

            if (!(manifest["application"] is XmlElement application)) return false;
            // 예전 빌드가 넣은 것(다른 키일 수 있음)은 지우고 지금 키로 다시 넣는다
            Remove(application, "meta-data", KakaoKeyMeta);
            Remove(application, "activity", KakaoRedirectActivity);

            XmlElement meta = doc.CreateElement("meta-data");
            meta.SetAttribute("name", AndroidNs, KakaoKeyMeta);
            meta.SetAttribute("value", AndroidNs, key);
            application.AppendChild(meta);

            // 카카오 SDK가 이미 선언한 액티비티에 합쳐진다. 안드로이드 12+는 intent-filter가 있으면 exported를 꼭 적어야 한다
            XmlElement activity = doc.CreateElement("activity");
            activity.SetAttribute("name", AndroidNs, KakaoRedirectActivity);
            activity.SetAttribute("exported", AndroidNs, "true");
            XmlElement filter = doc.CreateElement("intent-filter");
            filter.AppendChild(Named(doc, "action", "android.intent.action.VIEW"));
            filter.AppendChild(Named(doc, "category", "android.intent.category.DEFAULT"));
            filter.AppendChild(Named(doc, "category", "android.intent.category.BROWSABLE"));
            XmlElement data = doc.CreateElement("data");
            data.SetAttribute("scheme", AndroidNs, "kakao" + key);
            data.SetAttribute("host", AndroidNs, "oauth");
            filter.AppendChild(data);
            activity.AppendChild(filter);
            application.AppendChild(activity);
            Debug.Log("[AndroidManifestPatch] 카카오 로그인 키 · 리다이렉트 액티비티를 넣음");
            return true;
        }

        private static string ReadKakaoKey()
        {
            string file = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".history-heroes", "kakao.properties");
            if (!File.Exists(file)) return null;
            foreach (string raw in File.ReadAllLines(file))
            {
                string line = raw.Trim();
                if (!line.StartsWith("nativeAppKey=", StringComparison.Ordinal)) continue;
                string key = line.Substring("nativeAppKey=".Length).Trim();
                return key.Length > 0 ? key : null;
            }

            return null;
        }

        /// <summary>dependencyResolutionManagement.repositories 맨 앞에 카카오 저장소를 넣는다 (이미 있으면 그대로)</summary>
        private static void AddKakaoRepository(string settingsPath)
        {
            if (!File.Exists(settingsPath))
            {
                Fail("settings.gradle이 없어 카카오 저장소를 넣지 못함: " + settingsPath);
                return;
            }

            string text = File.ReadAllText(settingsPath);
            if (text.Contains(KakaoRepository)) return;
            int block = text.IndexOf("dependencyResolutionManagement", StringComparison.Ordinal);
            int repos = block < 0 ? -1 : text.IndexOf("repositories", block, StringComparison.Ordinal);
            int brace = repos < 0 ? -1 : text.IndexOf('{', repos);
            if (brace < 0)
            {
                Fail("settings.gradle에서 dependencyResolutionManagement.repositories를 찾지 못함 (Unity 템플릿이 바뀌었나?)");
                return;
            }

            string repo = "\n        maven {\n            url '" + KakaoRepository + "'\n            content { includeGroup 'com.kakao.sdk' }\n        }";
            File.WriteAllText(settingsPath, text.Insert(brace + 1, repo));
        }

        /// <summary>출시 빌드는 멈춘다 (카카오 SDK를 못 받으면 Gradle이 어차피 실패하지만 원인을 바로 보이게). 개발 빌드는 오류만 남긴다</summary>
        private static void Fail(string message)
        {
            if (!EditorUserBuildSettings.development) throw new BuildFailedException("[AndroidManifestPatch] " + message);
            Debug.LogError("[AndroidManifestPatch] " + message);
        }

        private static void Remove(XmlElement parent, string tag, string name)
        {
            for (int i = parent.ChildNodes.Count - 1; i >= 0; i--)
            {
                if (parent.ChildNodes[i] is XmlElement e && e.Name == tag && e.GetAttribute("name", AndroidNs) == name) parent.RemoveChild(e);
            }
        }

        private static XmlElement Named(XmlDocument doc, string tag, string name)
        {
            XmlElement e = doc.CreateElement(tag);
            e.SetAttribute("name", AndroidNs, name);
            return e;
        }

        private static bool Has(XmlElement parent, string tag, string name)
        {
            foreach (XmlNode child in parent.ChildNodes)
            {
                if (child is XmlElement e && e.Name == tag && e.GetAttribute("name", AndroidNs) == name) return true;
            }

            return false;
        }
    }
}
