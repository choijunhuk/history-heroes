using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Android;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;

namespace HistoryHeroes.EditorTools
{
    /// <summary>
    /// 안드로이드 출시 도구: 앱 아이콘 만들기 + 출시용 빌드 (GitHub용 APK · 구글 플레이용 AAB).
    /// 서명 키는 저장소 밖 (~/.history-heroes/signing.properties)에 둔다. 없으면 이 컴퓨터의 디버그 키로 서명한다
    /// (디버그 키는 컴퓨터마다 달라서, 다른 컴퓨터에서 만든 APK로는 업데이트가 안 된다).
    /// 쓰는 법: Tools/dev/release.sh (APK 빌드 + GitHub 릴리스), 메뉴 History Heroes → 앱 아이콘 다시 만들기 · 안드로이드 APK/AAB 만들기.
    /// </summary>
    public static class AndroidRelease
    {
        public const string OutDir = "Builds/Android"; // Game/ 기준, git 제외
        public const string ResultPath = OutDir + "/last-build.txt"; // release.sh가 끝을 기다린다

        private const string IconDir = "Assets/Art/AppIcon";
        private const string IconHero = "Assets/Art/3D/Characters/Khan3D.prefab"; // 시작 영웅 칭기즈 칸
        private const string StoreIconPath = "../Docs/Release/icon-512.png"; // 플레이 스토어 · README용
        private const int Size = 1024;
        private const float HeroYaw = -24f; // 살짝 비스듬히 (3/4 얼굴)

        // 아이콘 색: 몽골 초원의 푸른 하늘 + 금빛 햇살
        private static readonly Color SkyCenter = new Color(1.00f, 0.86f, 0.48f);
        private static readonly Color SkyMiddle = new Color(0.20f, 0.52f, 0.86f);
        private static readonly Color SkyEdge = new Color(0.07f, 0.13f, 0.32f);
        private static readonly Color Outline = new Color(24f / 255f, 22f / 255f, 34f / 255f); // ui_kit 외곽선과 같은 색

        // ───────────────────────── 아이콘 ─────────────────────────

        [MenuItem("History Heroes/앱 아이콘 다시 만들기")]
        private static void BuildIconMenu() => Debug.Log(BuildIcon());

        /// <summary>
        /// 영웅 3D 모델을 찍어 아이콘을 만든다: 배경층 · 앞층(적응형 아이콘), 전체 · 둥근 아이콘(옛 기기), 스토어 512px.
        /// 투명 배경은 검정 · 흰 배경으로 두 번 찍어 차이로 알파를 구한다 (파이프라인이 알파를 지우더라도 정확하다).
        /// </summary>
        public static string BuildIcon()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(IconHero);
            if (prefab == null) return "없음 " + IconHero;

            RenderHero(prefab, Size, out float[] premult, out float[] alpha, out int shot);
            Downsample(ref premult, ref alpha, shot, Size); // 레티나 화면이면 2배로 찍힌다 → 줄여서 가장자리를 부드럽게

            Color[] bg = Background(Size);
            Color[] full = Compose(bg, premult, alpha, scale: 1f, bottom: 0f, outline: 22);
            Color[] fg = Compose(null, premult, alpha, scale: 0.72f, bottom: 0.12f, outline: 16); // 적응형: 가운데 지름 66%만 보인다 → 얼굴을 가운데쯤, 잘린 가슴 끝은 가림 밖(아래 16%)

            Directory.CreateDirectory(IconDir);
            Save($"{IconDir}/icon_background.png", bg, Size);
            Save($"{IconDir}/icon_foreground.png", fg, Size);
            Save($"{IconDir}/icon_full.png", full, Size);
            Save(StoreIconPath, Resize(full, Size, 512), 512);

            AssetDatabase.Refresh();
            Texture2D tBg = ImportIcon("icon_background"), tFg = ImportIcon("icon_foreground"), tFull = ImportIcon("icon_full");

            // Unity 6.3: 안드로이드는 적응형 아이콘만 (둥근 · 옛 방식은 폐기). 기본 아이콘은 그 밖의 곳(에디터 · 옛 기기)에 쓰인다
            PlatformIcon[] icons = PlayerSettings.GetPlatformIcons(NamedBuildTarget.Android, AndroidPlatformIconKind.Adaptive);
            foreach (PlatformIcon icon in icons) icon.SetTextures(tBg, tFg);
            PlayerSettings.SetPlatformIcons(NamedBuildTarget.Android, AndroidPlatformIconKind.Adaptive, icons);
            PlayerSettings.SetIcons(NamedBuildTarget.Unknown, new[] { tFull }, IconKind.Any);
            AssetDatabase.SaveAssets();
            return $"아이콘 저장 {IconDir} (적응형 앞 · 뒤, 전체) + {StoreIconPath} · 찍은 크기 {shot}";
        }

        /// <summary>Art3DCharacters.Snapshot과 같은 방식 (BeginStaticPreview 없이 Render하면 에디터 조명 상태가 깨져 멈춘다)</summary>
        private static void RenderHero(GameObject prefab, int size, out float[] premult, out float[] alpha, out int shot)
        {
            var pru = new PreviewRenderUtility();
            bool asyncShaders = ShaderUtil.allowAsyncCompilation;
            ShaderUtil.allowAsyncCompilation = false; // 셰이더를 다 만든 뒤 그린다 (안 그러면 빈 화면)
            try
            {
                Camera cam = pru.camera;
                cam.fieldOfView = 20f;
                cam.nearClipPlane = 0.05f;
                cam.farClipPlane = 50f;
                cam.allowHDR = false;
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.GetUniversalAdditionalCameraData().SetRenderer(1); // 3D 렌더러

                GameObject go = pru.InstantiatePrefabInScene(prefab);
                go.transform.rotation = Quaternion.Euler(0f, HeroYaw, 0f); // 모델은 -Z를 본다 → 카메라 쪽
                AnimationClip clip = Art3DCharacters.Clip("Idle_Loop");
                if (clip != null)
                {
                    AnimationMode.StartAnimationMode();
                    AnimationMode.BeginSampling();
                    AnimationMode.SampleAnimationClip(go, clip, 0.3f);
                    AnimationMode.EndSampling();
                }

                var bounds = new Bounds(go.transform.position, Vector3.zero);
                foreach (Renderer r in go.GetComponentsInChildren<Renderer>()) bounds.Encapsulate(r.bounds);
                float h = Mathf.Max(0.5f, bounds.size.y);
                // 머리 + 어깨만 (런처 아이콘은 손톱만 해서 얼굴이 커야 한다): 머리 꼭대기 조금 위 ~ 가슴
                var focus = new Vector3(bounds.center.x, bounds.max.y - h * 0.135f, bounds.center.z);
                float distance = h * 0.165f / Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad);
                cam.transform.position = focus + new Vector3(0f, h * 0.04f, -distance);
                cam.transform.LookAt(focus);

                pru.lights[0].intensity = 1.35f;
                pru.lights[0].transform.rotation = Quaternion.Euler(30f, 35f, 0f);
                pru.lights[1].intensity = 0.7f;
                pru.lights[1].transform.rotation = Quaternion.Euler(10f, -140f, 0f); // 뒤쪽 테두리 빛
                pru.ambientColor = new Color(0.5f, 0.5f, 0.55f);

                Color[] black = Shoot(pru, size, Color.black, out shot);
                Color[] white = Shoot(pru, size, Color.white, out _);

                premult = new float[black.Length * 3];
                alpha = new float[black.Length];
                for (int i = 0; i < black.Length; i++)
                {
                    Color b = black[i].linear, w = white[i].linear;
                    float a = Mathf.Clamp01(1f - ((w.r - b.r) + (w.g - b.g) + (w.b - b.b)) / 3f);
                    alpha[i] = a;
                    premult[i * 3] = b.r;
                    premult[i * 3 + 1] = b.g;
                    premult[i * 3 + 2] = b.b;
                }
            }
            finally
            {
                if (AnimationMode.InAnimationMode()) AnimationMode.StopAnimationMode();
                ShaderUtil.allowAsyncCompilation = asyncShaders;
                pru.Cleanup();
            }
        }

        private static Color[] Shoot(PreviewRenderUtility pru, int size, Color background, out int shot)
        {
            pru.camera.backgroundColor = background;
            pru.BeginStaticPreview(new Rect(0, 0, size, size));
            pru.Render(true);
            Texture2D tex = pru.EndStaticPreview();
            shot = tex.width;
            Color[] pixels = tex.GetPixels();
            Object.DestroyImmediate(tex);
            return pixels;
        }

        /// <summary>찍은 크기(big) → n (f×f 평균, 미리 곱한 선형 색이라 가장자리가 어둡지 않다)</summary>
        private static void Downsample(ref float[] premult, ref float[] alpha, int big, int n)
        {
            int f = Mathf.Max(1, big / n);
            float w = 1f / (f * f);
            var p = new float[n * n * 3];
            var a = new float[n * n];
            for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                int o = y * n + x;
                for (int dy = 0; dy < f; dy++)
                for (int dx = 0; dx < f; dx++)
                {
                    int s = Mathf.Min(y * f + dy, big - 1) * big + Mathf.Min(x * f + dx, big - 1);
                    a[o] += alpha[s] * w;
                    for (int c = 0; c < 3; c++) p[o * 3 + c] += premult[s * 3 + c] * w;
                }
            }

            premult = p;
            alpha = a;
        }

        /// <summary>하늘 배경: 가운데 위쪽 금빛 햇살 → 파랑 → 가장자리 남색, 햇살 줄기</summary>
        private static Color[] Background(int n)
        {
            var px = new Color[n * n];
            var sun = new Vector2(0.5f, 0.66f);
            for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                var uv = new Vector2((x + 0.5f) / n, (y + 0.5f) / n);
                Vector2 d = uv - sun;
                float r = d.magnitude;
                Color c = r < 0.45f
                    ? Color.Lerp(SkyCenter, SkyMiddle, Mathf.SmoothStep(0f, 1f, r / 0.45f))
                    : Color.Lerp(SkyMiddle, SkyEdge, Mathf.SmoothStep(0f, 1f, (r - 0.45f) / 0.45f));
                float ray = Mathf.Sin(Mathf.Atan2(d.y, d.x) * 9f) > 0.35f ? 1f : 0f;
                c = Color.Lerp(c, Color.white, ray * 0.10f * Mathf.Clamp01(1.1f - r));
                c.a = 1f;
                px[y * n + x] = c;
            }

            return px;
        }

        /// <summary>
        /// 영웅을 놓는다 (bg가 없으면 투명 바탕). scale = 캔버스 대비 크기, bottom = 아래 여백 비율, 가로 가운데.
        /// 외곽선 = 알파를 흐린 뒤 진하게 (둥근 굵은 테두리).
        /// </summary>
        private static Color[] Compose(Color[] bg, float[] premult, float[] alpha, float scale, float bottom, int outline)
        {
            int n = Size;
            var hp = new float[n * n * 3];
            var ha = new float[n * n];
            float w = n * scale, x0 = (n - w) * 0.5f, y0 = n * bottom;
            for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float sx = (x + 0.5f - x0) / scale - 0.5f, sy = (y + 0.5f - y0) / scale - 0.5f;
                if (sx < 0f || sy < 0f || sx > n - 1 || sy > n - 1) continue;
                int ix = Mathf.Min((int)sx, n - 2), iy = Mathf.Min((int)sy, n - 2);
                float fx = sx - ix, fy = sy - iy;
                int o = y * n + x;
                ha[o] = Bilinear(alpha, n, ix, iy, fx, fy, 1, 0);
                for (int c = 0; c < 3; c++) hp[o * 3 + c] = Bilinear(premult, n, ix, iy, fx, fy, 3, c);
            }

            float[] ring = BoxBlur(ha, n, outline);
            var px = new Color[n * n];
            for (int i = 0; i < px.Length; i++)
            {
                float oa = Mathf.Clamp01(Mathf.Max(ring[i] * 3.5f, ha[i]));
                Color under = bg != null ? bg[i] : new Color(0f, 0f, 0f, 0f);
                // 겹치기는 선형 색에서
                Color u = under.linear;
                float ua = under.a;
                Color o = Outline.linear;
                float r = Mathf.Lerp(u.r * ua, o.r, oa), g = Mathf.Lerp(u.g * ua, o.g, oa), b = Mathf.Lerp(u.b * ua, o.b, oa);
                float a = Mathf.Lerp(ua, 1f, oa);
                float hA = ha[i];
                r = r * (1f - hA) + hp[i * 3];
                g = g * (1f - hA) + hp[i * 3 + 1];
                b = b * (1f - hA) + hp[i * 3 + 2];
                a = a * (1f - hA) + hA;
                Color lin = a > 0.0001f ? new Color(r / a, g / a, b / a, a) : new Color(0f, 0f, 0f, 0f);
                Color gam = lin.gamma;
                gam.a = a;
                px[i] = gam;
            }

            return px;
        }

        private static float Bilinear(float[] src, int n, int x, int y, float fx, float fy, int stride, int c)
        {
            float a = src[(y * n + x) * stride + c], b = src[(y * n + x + 1) * stride + c];
            float d = src[((y + 1) * n + x) * stride + c], e = src[((y + 1) * n + x + 1) * stride + c];
            return Mathf.Lerp(Mathf.Lerp(a, b, fx), Mathf.Lerp(d, e, fx), fy);
        }

        /// <summary>상자 흐림 두 번 (가로 · 세로, 누적합)</summary>
        private static float[] BoxBlur(float[] src, int n, int radius)
        {
            float[] tmp = new float[src.Length], dst = new float[src.Length];
            float norm = 1f / (radius * 2 + 1);
            for (int y = 0; y < n; y++)
            {
                float sum = 0f;
                for (int x = -radius; x <= radius; x++) sum += src[y * n + Mathf.Clamp(x, 0, n - 1)];
                for (int x = 0; x < n; x++)
                {
                    tmp[y * n + x] = sum * norm;
                    sum += src[y * n + Mathf.Min(x + radius + 1, n - 1)] - src[y * n + Mathf.Max(x - radius, 0)];
                }
            }

            for (int x = 0; x < n; x++)
            {
                float sum = 0f;
                for (int y = -radius; y <= radius; y++) sum += tmp[Mathf.Clamp(y, 0, n - 1) * n + x];
                for (int y = 0; y < n; y++)
                {
                    dst[y * n + x] = sum * norm;
                    sum += tmp[Mathf.Min(y + radius + 1, n - 1) * n + x] - tmp[Mathf.Max(y - radius, 0) * n + x];
                }
            }

            return dst;
        }

        private static Color[] Resize(Color[] src, int from, int to)
        {
            var tex = new Texture2D(from, from, TextureFormat.RGBA32, false);
            tex.SetPixels(src);
            tex.Apply();
            var rt = RenderTexture.GetTemporary(to, to, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            Graphics.Blit(tex, rt);
            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = rt;
            var small = new Texture2D(to, to, TextureFormat.RGBA32, false);
            small.ReadPixels(new Rect(0, 0, to, to), 0, 0);
            small.Apply();
            RenderTexture.active = previous;
            RenderTexture.ReleaseTemporary(rt);
            Color[] px = small.GetPixels();
            Object.DestroyImmediate(tex);
            Object.DestroyImmediate(small);
            return px;
        }

        private static void Save(string path, Color[] px, int n)
        {
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false);
            tex.SetPixels(px);
            tex.Apply();
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
        }

        private static Texture2D ImportIcon(string name)
        {
            string path = $"{IconDir}/{name}.png";
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Default;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.maxTextureSize = Size;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        // ───────────────────────── 빌드 ─────────────────────────

        [MenuItem("History Heroes/안드로이드 APK 만들기 (GitHub · 폰 시험)")]
        private static void BuildApkMenu() => Debug.Log(Build());

        [MenuItem("History Heroes/구글 플레이용 AAB 만들기")]
        private static void BuildAabMenu() => Debug.Log(Build(aab: true));

        /// <summary>
        /// 출시용 빌드 (개발 빌드 아님 → 개발용 단축키 · 벤치 없음). aab면 구글 플레이 올리기용 .aab, 아니면 .apk.
        /// version/versionCode를 주면 이번 빌드에만 쓰고 되돌린다. 결과 한 줄은 <see cref="ResultPath"/>에도 쓴다 ("성공 ..." / "실패 ...").
        /// </summary>
        public static string Build(string version = null, int versionCode = 0, bool aab = false)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return Finish("실패: Play 중에는 빌드하지 않는다");

            string oldVersion = PlayerSettings.bundleVersion;
            int oldCode = PlayerSettings.Android.bundleVersionCode;
            bool oldBundle = EditorUserBuildSettings.buildAppBundle;
            Signing signing = LoadSigning(out string signNote);
            string path = $"{OutDir}/HistoryHeroes.{(aab ? "aab" : "apk")}";
            try
            {
                if (!string.IsNullOrEmpty(version)) PlayerSettings.bundleVersion = version;
                if (versionCode > 0) PlayerSettings.Android.bundleVersionCode = versionCode;
                EditorUserBuildSettings.buildAppBundle = aab;
                if (signing != null)
                {
                    PlayerSettings.Android.useCustomKeystore = true;
                    PlayerSettings.Android.keystoreName = signing.Keystore;
                    PlayerSettings.Android.keystorePass = signing.StorePass;
                    PlayerSettings.Android.keyaliasName = signing.Alias;
                    PlayerSettings.Android.keyaliasPass = signing.KeyPass;
                }

                Directory.CreateDirectory(OutDir);
                var options = new BuildPlayerOptions
                {
                    scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray(),
                    locationPathName = path,
                    target = BuildTarget.Android,
                    targetGroup = BuildTargetGroup.Android,
                    options = BuildOptions.CompressWithLz4HC,
                };
                BuildReport report = BuildPipeline.BuildPlayer(options);
                BuildSummary s = report.summary;
                if (s.result != BuildResult.Succeeded) return Finish($"실패: {s.result} (오류 {s.totalErrors}개, 콘솔 확인)");
                float mb = new FileInfo(path).Length / 1048576f;
                return Finish($"성공 {path} {mb:0.0}MB · {s.totalTime.TotalMinutes:0.0}분 · 버전 {PlayerSettings.bundleVersion} ({PlayerSettings.Android.bundleVersionCode}) · {signNote}");
            }
            catch (Exception e)
            {
                return Finish("실패: " + e.Message);
            }
            finally
            {
                PlayerSettings.bundleVersion = oldVersion;
                PlayerSettings.Android.bundleVersionCode = oldCode;
                EditorUserBuildSettings.buildAppBundle = oldBundle;
                PlayerSettings.Android.useCustomKeystore = false; // 키 경로 · 비밀번호가 프로젝트 설정에 남지 않게
                PlayerSettings.Android.keystoreName = "";
                PlayerSettings.Android.keystorePass = "";
                PlayerSettings.Android.keyaliasName = "";
                PlayerSettings.Android.keyaliasPass = "";
                AssetDatabase.SaveAssets(); // 빌드 도중 Unity가 빌드용 값(버전 · 키 경로)을 저장해 두므로, 되돌린 값으로 다시 저장
            }
        }

        private static string Finish(string line)
        {
            Directory.CreateDirectory(OutDir);
            File.WriteAllText(ResultPath, line);
            return line;
        }

        private sealed class Signing
        {
            public string Keystore, StorePass, Alias, KeyPass;
        }

        /// <summary>~/.history-heroes/signing.properties: keystore= · storePass= · alias= · keyPass= (keystore는 그 폴더 기준 경로도 된다)</summary>
        private static Signing LoadSigning(out string note)
        {
            string dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".history-heroes");
            string file = Path.Combine(dir, "signing.properties");
            if (!File.Exists(file))
            {
                note = "디버그 키 서명 (signing.properties 없음 → 다른 컴퓨터 APK와 업데이트 안 됨)";
                return null;
            }

            var map = new Dictionary<string, string>();
            foreach (string line in File.ReadAllLines(file))
            {
                int eq = line.IndexOf('=');
                if (eq > 0 && !line.TrimStart().StartsWith("#")) map[line.Substring(0, eq).Trim()] = line.Substring(eq + 1).Trim();
            }

            var s = new Signing
            {
                Keystore = Path.Combine(dir, map["keystore"]),
                StorePass = map["storePass"],
                Alias = map["alias"],
                KeyPass = map.TryGetValue("keyPass", out string k) ? k : map["storePass"],
            };
            note = "출시 키 서명 (" + Path.GetFileName(s.Keystore) + ")";
            return s;
        }
    }
}
