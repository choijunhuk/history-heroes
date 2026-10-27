using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;
using UnityEngine.UI;

namespace HistoryHeroes.EditorTools
{
    /// <summary>
    /// UI 부품 (그래픽 평가 3번, 개발자: "UI가 그냥 html 느낌"): 납작한 네모 대신 게임다운 버튼·창·막대.
    /// - 그림: Tools/ui_kit.py가 그린 흰·회색 부품 (Image.color가 곱해져 색이 된다). 9칸 자르기 테두리는 ui_kit.py와 같은 값.
    /// - 아이콘: game-icons.net (CC BY 3.0, Docs/Credits.md) 흰 실루엣 → 색을 입혀 쓴다.
    /// - 글꼴: 제목·버튼은 Black Han Sans (OFL), 본문은 나눔고딕. 글자마다 어두운 테두리 + 그림자 (TMP 재질).
    /// </summary>
    public static partial class SceneBuilder
    {
        private const string KitDir = "Assets/Art/UI/Kit";
        private const string IconDir = "Assets/Art/UI/Icons";
        private const string TitleFontPath = "Assets/Fonts/BlackHanSans-Regular.ttf";
        private const string TitleFontAssetPath = "Assets/Fonts/BlackHanSans SDF.asset";

        private sealed class UiKitSprites
        {
            public Sprite Button, Panel, Row, Frame, FrameGold, Shadow, Glow, Ring, Rays, Ribbon, BarBg, BarFill, Slot, Badge, Tab, Screen, Pattern, Vignette, Star, Sparkle, Divider;
        }

        private static UiKitSprites _kit;
        private static TMP_FontAsset _titleFont;
        private static Material _bodyMat, _titleMat, _buttonMat;

        /// <summary>9칸 자르기 테두리 (왼쪽, 아래, 오른쪽, 위) — Tools/ui_kit.py BORDERS와 같다</summary>
        private static readonly Dictionary<string, Vector4> KitBorders = new Dictionary<string, Vector4>
        {
            { "ui_button", new Vector4(34, 30, 34, 30) },
            { "ui_panel", new Vector4(44, 44, 44, 44) },
            { "ui_row", new Vector4(28, 28, 28, 28) },
            { "ui_frame", new Vector4(40, 40, 40, 40) },
            { "ui_frame_gold", new Vector4(44, 44, 44, 44) },
            { "ui_shadow", new Vector4(60, 60, 60, 60) },
            { "ui_badge", new Vector4(22, 22, 22, 22) },
            { "ui_ribbon", new Vector4(96, 0, 96, 0) },
            { "ui_bar_bg", new Vector4(24, 22, 24, 22) },
            { "ui_slot", new Vector4(30, 30, 30, 30) },
            { "ui_tab", new Vector4(30, 20, 30, 20) },
        };

        // 화면 바탕 · 줄 색 (어두운 남색 계열, 위가 밝다)
        private static readonly Color ScreenTint = new Color(0.17f, 0.22f, 0.36f);
        private static readonly Color RowTint = new Color(0.26f, 0.32f, 0.5f);

        /// <summary>BuildAll 처음에: 그림 가져오기 설정 → 제목 글꼴 → 불러오기 (글자 재질 포함)</summary>
        private static void SetupUiKit()
        {
            ImportUiSprites(KitDir, true);
            ImportUiSprites(IconDir, false);
            SetupTitleFont();
            LoadUiKit();
        }

        private static void ImportUiSprites(string dir, bool kit)
        {
            if (!Directory.Exists(dir)) return;
            foreach (string file in Directory.GetFiles(dir, "*.png"))
            {
                string path = file.Replace('\\', '/');
                if (!(AssetImporter.GetAtPath(path) is TextureImporter importer)) continue;
                string name = Path.GetFileNameWithoutExtension(path);
                Vector4 border = kit && KitBorders.TryGetValue(name, out Vector4 b) ? b : Vector4.zero;
                TextureWrapMode wrap = name == "ui_pattern" ? TextureWrapMode.Repeat : TextureWrapMode.Clamp;
                var settings = new TextureImporterSettings();
                importer.ReadTextureSettings(settings);
                bool fullRect = !kit || settings.spriteMeshType == SpriteMeshType.FullRect; // 9칸 자르기를 월드 그림(SpriteRenderer)에서도 쓰려면 사각 메시
                bool ok = importer.textureType == TextureImporterType.Sprite && importer.spriteBorder == border && importer.wrapMode == wrap
                          && !importer.mipmapEnabled && importer.textureCompression == TextureImporterCompression.Uncompressed && importer.alphaIsTransparency && fullRect;
                if (ok) continue;
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.spriteBorder = border;
                importer.spritePixelsPerUnit = 100f;
                importer.mipmapEnabled = false;
                importer.alphaIsTransparency = true;
                importer.filterMode = FilterMode.Bilinear;
                importer.wrapMode = wrap;
                importer.textureCompression = TextureImporterCompression.Uncompressed; // 그라데이션이 계단지지 않게 (작은 그림들이라 용량 부담 적음)
                if (kit)
                {
                    importer.ReadTextureSettings(settings);
                    settings.spriteMeshType = SpriteMeshType.FullRect;
                    importer.SetTextureSettings(settings);
                }

                importer.SaveAndReimport();
            }
        }

        /// <summary>장면을 새로 연 뒤에도 다시 잡는다 (ReloadShared)</summary>
        private static void LoadUiKit()
        {
            Sprite S(string name) => AssetDatabase.LoadAssetAtPath<Sprite>($"{KitDir}/{name}.png");
            _kit = new UiKitSprites
            {
                Button = S("ui_button"), Panel = S("ui_panel"), Row = S("ui_row"), Frame = S("ui_frame"), FrameGold = S("ui_frame_gold"),
                Shadow = S("ui_shadow"), Glow = S("ui_glow"), Ring = S("ui_ring"), Rays = S("ui_rays"), Ribbon = S("ui_ribbon"), BarBg = S("ui_bar_bg"),
                BarFill = S("ui_bar_fill"), Slot = S("ui_slot"), Badge = S("ui_badge"), Tab = S("ui_tab"), Screen = S("ui_screen"),
                Pattern = S("ui_pattern"), Vignette = S("ui_vignette"), Star = S("ui_star"), Sparkle = S("ui_sparkle"), Divider = S("ui_divider"),
            };
            if (_kit.Button == null) _kit = null; // 그림이 없으면 예전 납작한 모양
            _titleFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(TitleFontAssetPath);
            if (_font != null) _bodyMat = TextPreset(_font, "Body", 0.12f, 0.55f, 0.6f);
            if (_titleFont != null)
            {
                _titleMat = TextPreset(_titleFont, "Title", 0.24f, 0.9f, 0.85f);
                _buttonMat = TextPreset(_titleFont, "Button", 0.2f, 0.8f, 0.6f);
            }
        }

        /// <summary>아이콘 (Assets/Art/UI/Icons/icon_&lt;key&gt;.png). 없으면 null</summary>
        private static Sprite Icon(string key) => AssetDatabase.LoadAssetAtPath<Sprite>($"{IconDir}/icon_{key}.png");

        private static void SetupTitleFont()
        {
            _titleFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(TitleFontAssetPath);
            var source = AssetDatabase.LoadAssetAtPath<Font>(TitleFontPath);
            if (_titleFont == null && source != null)
            {
                _titleFont = TMP_FontAsset.CreateFontAsset(source, 72, 9, GlyphRenderMode.SDFAA, 1024, 1024, AtlasPopulationMode.Dynamic, true);
                _titleFont.name = Path.GetFileNameWithoutExtension(TitleFontAssetPath);
                AssetDatabase.CreateAsset(_titleFont, TitleFontAssetPath);
                _titleFont.material.name = _titleFont.name + " Material";
                AssetDatabase.AddObjectToAsset(_titleFont.material, _titleFont);
                foreach (Texture2D atlas in _titleFont.atlasTextures)
                {
                    atlas.name = _titleFont.name + " Atlas";
                    AssetDatabase.AddObjectToAsset(atlas, _titleFont);
                }
            }

            if (_titleFont == null) return;
            // 제목 글꼴에 없는 글자(드문 한글·기호)는 나눔고딕으로
            _titleFont.fallbackFontAssetTable ??= new List<TMP_FontAsset>();
            if (_font != null && !_titleFont.fallbackFontAssetTable.Contains(_font))
            {
                _titleFont.fallbackFontAssetTable.Add(_font);
                EditorUtility.SetDirty(_titleFont);
            }

            AssetDatabase.SaveAssets();
        }

        /// <summary>
        /// 글자 재질: 어두운 테두리 + 아래로 떨어진 그림자. 같은 글꼴의 여러 아틀라스에도 TMP가 이 설정을 옮겨 쓴다.
        /// outline: 테두리 두께 (0~0.5), shadow: 그림자 거리, shadowAlpha: 그림자 진하기
        /// </summary>
        private static Material TextPreset(TMP_FontAsset font, string suffix, float outline, float shadow, float shadowAlpha)
        {
            ShaderUtilities.GetShaderPropertyIDs();
            string path = $"Assets/Fonts/{font.name} {suffix}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(font.material) { name = $"{font.name} {suffix}" };
                AssetDatabase.CreateAsset(mat, path);
            }
            else
            {
                mat.shader = font.material.shader;
                mat.CopyPropertiesFromMaterial(font.material);
            }

            mat.SetFloat(ShaderUtilities.ID_OutlineWidth, outline);
            mat.SetColor(ShaderUtilities.ID_OutlineColor, new Color(0.06f, 0.05f, 0.1f, 1f));
            mat.EnableKeyword(ShaderUtilities.Keyword_Outline);
            mat.SetFloat(ShaderUtilities.ID_FaceDilate, outline * 0.5f); // 테두리가 글자 속을 파먹지 않게 살짝 두껍게
            mat.EnableKeyword(ShaderUtilities.Keyword_Underlay);
            mat.SetColor(ShaderUtilities.ID_UnderlayColor, new Color(0f, 0f, 0f, shadowAlpha));
            mat.SetFloat(ShaderUtilities.ID_UnderlayOffsetX, 0f);
            mat.SetFloat(ShaderUtilities.ID_UnderlayOffsetY, -shadow);
            mat.SetFloat(ShaderUtilities.ID_UnderlayDilate, outline);
            mat.SetFloat(ShaderUtilities.ID_UnderlaySoftness, 0.2f);
            ShaderUtilities.UpdateShaderRatios(mat);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        /// <summary>버튼 색을 선명하게 (예전 빌더 색은 어둡고 탁하다). 회색은 푸른 회색으로</summary>
        private static Color Vivid(Color c)
        {
            Color.RGBToHSV(c, out float h, out float s, out float v);
            if (s < 0.15f) return new Color(0.46f, 0.53f, 0.68f, c.a);
            s = Mathf.Clamp01(s * 1.1f + 0.12f);
            v = Mathf.Max(v, 0.86f);
            Color o = Color.HSVToRGB(h, s, v);
            o.a = c.a;
            return o;
        }

        /// <summary>어두운 줄·칸 색을 남색 쪽으로 밝힌다 (테두리·광택이 보이게)</summary>
        private static Color LiftRow(Color c)
        {
            float l = c.r * 0.3f + c.g * 0.59f + c.b * 0.11f;
            if (l >= 0.22f) return c;
            Color o = Color.Lerp(c, RowTint, 0.65f);
            o.a = c.a;
            return o;
        }

        /// <summary>전체 화면 바탕: 세로 그라데이션 + 옅은 마름모 무늬 + 가장자리 어둡게</summary>
        private static void ScreenBackdrop(Image image)
        {
            image.sprite = _kit.Screen;
            image.type = Image.Type.Simple;
            image.color = new Color(ScreenTint.r, ScreenTint.g, ScreenTint.b, image.color.a);
            var pattern = (RectTransform)new GameObject("Pattern", typeof(RectTransform)).transform;
            pattern.SetParent(image.transform, false);
            Stretch(pattern);
            Image p = pattern.gameObject.AddComponent<Image>();
            p.sprite = _kit.Pattern;
            p.type = Image.Type.Tiled;
            p.color = new Color(1f, 1f, 1f, 0.55f);
            p.raycastTarget = false;
            var vignette = (RectTransform)new GameObject("Vignette", typeof(RectTransform)).transform;
            vignette.SetParent(image.transform, false);
            Stretch(vignette);
            Image v = vignette.gameObject.AddComponent<Image>();
            v.sprite = _kit.Vignette;
            v.color = Color.white;
            v.raycastTarget = false;
        }

        /// <summary>9칸 자르기 그림의 테두리 배율: 작은 칸은 테두리도 작게 (pixelsPerUnitMultiplier가 크면 테두리가 얇다)</summary>
        private static float BorderScale(float height, float designHeight) => Mathf.Clamp(designHeight / Mathf.Max(1f, height), 0.75f, 3f);
    }
}
