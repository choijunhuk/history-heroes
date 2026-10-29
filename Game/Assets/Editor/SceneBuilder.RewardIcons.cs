using System.Collections.Generic;
using System.IO;
using System.Reflection;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore;

namespace HistoryHeroes.EditorTools
{
    /// <summary>
    /// 보상 · 재화 그림 (2026-10-29 개발자: "보상 같은 데 그림이 너무 없다").
    /// - 그림: Art/UI/Rewards/reward_*.png (이미지 생성, Docs/Credits.md)
    /// - 글 속 아이콘: 그림을 한 장(아틀라스)에 모아 TMP 스프라이트 묶음을 만들고 TMP 기본 묶음으로 둔다
    ///   → 어느 글에서나 "&lt;sprite name=gold&gt; +33"처럼 쓸 수 있다 (Wallet.Reward.ToString)
    /// </summary>
    public static partial class SceneBuilder
    {
        private const string RewardDir = "Assets/Art/UI/Rewards";
        private const string RewardAtlasPath = RewardDir + "/RewardAtlas.png";
        private const string RewardSpriteAssetPath = RewardDir + "/RewardIcons.asset";
        private const int RewardCell = 128;

        /// <summary>그림 키 (파일 reward_키.png = 글 속 이름 &lt;sprite name=키&gt;)</summary>
        private static readonly string[] RewardKeys =
        {
            "gold", "gem", "hero_shard", "equip_shard", "ancient_shard", "ticket", "heart", "exp", "chest_normal", "chest_premium",
        };

        /// <summary>UI 그림 칸에 쓰는 컬러 보상 그림 (없으면 null → 예전 흰 아이콘을 쓴다)</summary>
        private static Sprite RewardIcon(string key) => AssetDatabase.LoadAssetAtPath<Sprite>($"{RewardDir}/reward_{key}.png");

        private static void SetupRewardIcons()
        {
            // 1) 그림 가져오기 설정: 스프라이트 · 읽기 가능 (아틀라스로 모으려고)
            var textures = new List<Texture2D>();
            var keys = new List<string>();
            foreach (string key in RewardKeys)
            {
                string path = $"{RewardDir}/reward_{key}.png";
                if (!(AssetImporter.GetAtPath(path) is TextureImporter importer)) continue;
                bool dirty = importer.textureType != TextureImporterType.Sprite || !importer.isReadable || importer.mipmapEnabled;
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.alphaIsTransparency = true;
                importer.isReadable = true;
                importer.mipmapEnabled = false;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                if (dirty) importer.SaveAndReimport();
                textures.Add(AssetDatabase.LoadAssetAtPath<Texture2D>(path));
                keys.Add(key);
            }

            if (textures.Count == 0) return;

            // 2) 한 장으로 모으기 (칸 128px, 한 줄 4칸)
            const int perRow = 4;
            int rows = (textures.Count + perRow - 1) / perRow;
            var atlas = new Texture2D(perRow * RewardCell, Mathf.NextPowerOfTwo(rows * RewardCell), TextureFormat.RGBA32, false);
            atlas.SetPixels32(new Color32[atlas.width * atlas.height]);
            var rects = new List<RectInt>();
            for (int i = 0; i < textures.Count; i++)
            {
                int x = (i % perRow) * RewardCell;
                int y = atlas.height - (i / perRow + 1) * RewardCell; // 위에서부터
                atlas.SetPixels(x, y, RewardCell, RewardCell, Scaled(textures[i], RewardCell));
                rects.Add(new RectInt(x, y, RewardCell, RewardCell));
            }

            atlas.Apply();
            File.WriteAllBytes(RewardAtlasPath, atlas.EncodeToPNG());
            Object.DestroyImmediate(atlas);
            AssetDatabase.ImportAsset(RewardAtlasPath, ImportAssetOptions.ForceUpdate);
            if (AssetImporter.GetAtPath(RewardAtlasPath) is TextureImporter atlasImporter)
            {
                atlasImporter.textureType = TextureImporterType.Default;
                atlasImporter.alphaIsTransparency = true;
                atlasImporter.mipmapEnabled = false;
                atlasImporter.SaveAndReimport();
            }

            Texture2D atlasTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(RewardAtlasPath);

            // 3) TMP 스프라이트 묶음 (있으면 그 자리에 고쳐 써서 GUID를 지킨다)
            var asset = AssetDatabase.LoadAssetAtPath<TMP_SpriteAsset>(RewardSpriteAssetPath);
            bool created = asset == null;
            if (created) asset = ScriptableObject.CreateInstance<TMP_SpriteAsset>();
            asset.name = "RewardIcons";
            asset.spriteSheet = atlasTexture;
            // 새로 만든 묶음은 내부 목록이 비어 있을(null) 수 있다 (첫 빌드에서 null 오류가 났다)
            foreach (string field in new[] { "m_GlyphTable", "m_SpriteCharacterTable" })
            {
                FieldInfo f = typeof(TMP_SpriteAsset).GetField(field, BindingFlags.NonPublic | BindingFlags.Instance);
                if (f != null && f.GetValue(asset) == null) f.SetValue(asset, System.Activator.CreateInstance(f.FieldType));
            }

            asset.spriteGlyphTable.Clear();
            asset.spriteCharacterTable.Clear();
            for (int i = 0; i < keys.Count; i++)
            {
                // 글줄에 맞게: 높이 = 글자 크기 정도, 받침선 아래로 18%
                var metrics = new GlyphMetrics(RewardCell, RewardCell, 0f, RewardCell * 0.82f, RewardCell);
                var glyph = new TMP_SpriteGlyph((uint)i, metrics, new GlyphRect(rects[i].x, rects[i].y, RewardCell, RewardCell), 1f, 0);
                asset.spriteGlyphTable.Add(glyph);
                asset.spriteCharacterTable.Add(new TMP_SpriteCharacter(0xFFFE, glyph) { name = keys[i] });
            }

            // 크기 기준 (글자 크기 = 그림 한 칸). 이 값은 내부 필드라 반사로 넣는다
            var face = new FaceInfo { pointSize = RewardCell, scale = 1.1f, ascentLine = RewardCell * 0.82f, descentLine = -RewardCell * 0.18f, lineHeight = RewardCell };
            typeof(TMP_SpriteAsset).GetField("m_FaceInfo", BindingFlags.NonPublic | BindingFlags.Instance)?.SetValue(asset, face);

            if (created) AssetDatabase.CreateAsset(asset, RewardSpriteAssetPath);
            Shader shader = Shader.Find("TextMeshPro/Sprite");
            if (asset.material == null && shader != null)
            {
                asset.material = new Material(shader) { name = "RewardIcons Material" };
                AssetDatabase.AddObjectToAsset(asset.material, asset);
            }

            if (asset.material != null) asset.material.mainTexture = atlasTexture;
            asset.UpdateLookupTables();
            EditorUtility.SetDirty(asset);

            // 4) TMP 기본 묶음으로 (예전 기본은 대체 묶음으로 남긴다)
            TMP_Settings settings = Resources.Load<TMP_Settings>("TMP Settings");
            if (settings != null)
            {
                var so = new SerializedObject(settings);
                SerializedProperty def = so.FindProperty("m_defaultSpriteAsset");
                var old = def.objectReferenceValue as TMP_SpriteAsset;
                if (old != null && old != asset)
                {
                    asset.fallbackSpriteAssets ??= new List<TMP_SpriteAsset>();
                    if (!asset.fallbackSpriteAssets.Contains(old)) asset.fallbackSpriteAssets.Add(old);
                }

                def.objectReferenceValue = asset;
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(settings);
            }

            AssetDatabase.SaveAssets();
        }

        /// <summary>그림을 size×size로 줄인 픽셀 (읽기 가능한 원본에서)</summary>
        private static Color[] Scaled(Texture2D source, int size)
        {
            var result = new Color[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    result[y * size + x] = source.GetPixelBilinear((x + 0.5f) / size, (y + 0.5f) / size);
                }
            }

            return result;
        }
    }
}
