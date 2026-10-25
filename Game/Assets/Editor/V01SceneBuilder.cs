using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TextCore.LowLevel;

namespace HistoryHeroes.EditorTools
{
    /// <summary>
    /// v0.1 테스트 장면을 한 번에 만든다: 레이어 → 임시 도형 → 한글 글꼴 → 데이터(SO) → 프리팹 → 장면·UI.
    /// 여러 번 실행해도 된다 (에셋은 덮어쓰고 GUID는 유지). 숫자는 Docs/Design/Skills.md, Stats.md 기준.
    /// 메뉴: History Heroes > v0.1 테스트 장면 만들기
    /// </summary>
    public static partial class V01SceneBuilder
    {
        public const int PlayerLayer = 8;
        public const int EnemyLayer = 9;
        public const int WallLayer = 10;
        public const int PickupLayer = 11;

        private const string ArtDir = "Assets/Art/Placeholder";
        private const string DataDir = "Assets/Data";
        private const string PrefabDir = "Assets/Prefabs";
        private const string ScenePath = "Assets/Scenes/Dungeon_v01.unity";
        private const string FontPath = "Assets/Fonts/NanumGothic-Bold.ttf";
        private const string FontAssetPath = "Assets/Fonts/NanumGothic-Bold SDF.asset";

        private static Sprite _circle;
        private static Sprite _square;
        private static TMP_FontAsset _font;

        [MenuItem("History Heroes/v0.1 테스트 장면 만들기")]
        public static string BuildAll()
        {
            // 장면을 **먼저** 새로 연다. 나중에 열면 그 사이 만든 에셋이 메모리에서 내려가 참조가 비어 버린다.
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            SetupLayers();
            CreateSprites();
            SetupFont();

            Data data = CreateData();
            Prefabs prefabs = CreatePrefabs(data);
            LinkData(data, prefabs);
            AssetDatabase.SaveAssets();

            BuildScene(scene, data, prefabs);
            AssetDatabase.SaveAssets();
            return $"완료: {ScenePath}";
        }

        private static int Mask(int layer) => 1 << layer;

        // ---------- 레이어 ----------
        private static void SetupLayers()
        {
            Object tagManagerAsset = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0];
            var tagManager = new SerializedObject(tagManagerAsset);
            SerializedProperty layers = tagManager.FindProperty("layers");

            void SetLayer(int index, string name)
            {
                SerializedProperty p = layers.GetArrayElementAtIndex(index);
                if (string.IsNullOrEmpty(p.stringValue) || p.stringValue == name) p.stringValue = name;
                else Debug.LogWarning($"레이어 {index}에 이미 '{p.stringValue}'가 있어서 '{name}'으로 바꾸지 않았습니다.");
            }

            SetLayer(PlayerLayer, "Player");
            SetLayer(EnemyLayer, "Enemy");
            SetLayer(WallLayer, "Wall");
            SetLayer(PickupLayer, "Pickup");
            tagManager.ApplyModifiedPropertiesWithoutUndo();

            // 플레이어와 적은 서로 밀지 않는다 (돌진병이 몸을 통과해야 "피했다/맞았다"가 판정으로만 갈린다).
            // 적끼리는 부딪혀서 흩어진다. 판정은 Overlap/CircleCast라 충돌 표와 관계없다.
            Physics2D.IgnoreLayerCollision(PlayerLayer, EnemyLayer, true);
            Physics2D.IgnoreLayerCollision(PickupLayer, PlayerLayer, true);
            Physics2D.IgnoreLayerCollision(PickupLayer, EnemyLayer, true);
        }

        // ---------- 임시 도형 (흰 원·흰 사각형 PNG) ----------
        private static void CreateSprites()
        {
            _circle = SaveShape("Circle", circle: true);
            _square = SaveShape("Square", circle: false);
        }

        private static Sprite SaveShape(string name, bool circle)
        {
            string path = $"{ArtDir}/{name}.png";
            if (!File.Exists(path))
            {
                AssetUtil.EnsureFolder(ArtDir);
                const int size = 64;
                var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
                float center = (size - 1) * 0.5f;
                for (int y = 0; y < size; y++)
                {
                    for (int x = 0; x < size; x++)
                    {
                        float a = 1f;
                        if (circle)
                        {
                            float d = Mathf.Sqrt((x - center) * (x - center) + (y - center) * (y - center));
                            a = Mathf.Clamp01(size * 0.5f - d);
                        }

                        texture.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                    }
                }

                File.WriteAllBytes(path, texture.EncodeToPNG());
                Object.DestroyImmediate(texture);
                AssetDatabase.ImportAsset(path);
            }

            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            if (importer.textureType != TextureImporterType.Sprite || !Mathf.Approximately(importer.spritePixelsPerUnit, 64f))
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.spritePixelsPerUnit = 64f; // 1유닛 = 64픽셀 → 도형 하나가 1×1
                importer.filterMode = FilterMode.Bilinear;
                importer.mipmapEnabled = false;
                importer.SaveAndReimport();
            }

            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        // ---------- 한글 글꼴 (TMP 동적 글꼴: 쓰는 글자만 그때그때 아틀라스에 추가) ----------
        private static void SetupFont()
        {
            _font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontAssetPath);
            if (_font == null)
            {
                var source = AssetDatabase.LoadAssetAtPath<Font>(FontPath);
                _font = TMP_FontAsset.CreateFontAsset(source, 64, 6, GlyphRenderMode.SDFAA, 1024, 1024, AtlasPopulationMode.Dynamic, true);
                _font.name = Path.GetFileNameWithoutExtension(FontAssetPath);
                AssetDatabase.CreateAsset(_font, FontAssetPath);

                _font.material.name = _font.name + " Material";
                AssetDatabase.AddObjectToAsset(_font.material, _font);
                foreach (Texture2D atlas in _font.atlasTextures)
                {
                    atlas.name = _font.name + " Atlas";
                    AssetDatabase.AddObjectToAsset(atlas, _font);
                }

                AssetDatabase.SaveAssets();
            }

            // 새로 만드는 모든 TMP 글자가 이 글꼴을 쓰게
            var settings = Resources.Load<TMP_Settings>("TMP Settings");
            if (settings != null)
            {
                // 한글은 기본이 "글자마다 줄바꿈"이라 "회/복"처럼 끊긴다 → 띄어쓰기 단위로
                using (var p = new Props(settings)) p.Ref("m_defaultFontAsset", _font).Bool("m_UseModernHangulLineBreakingRules", true);
            }
        }
    }
}
