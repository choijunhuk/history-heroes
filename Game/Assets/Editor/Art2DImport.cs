using System.Collections.Generic;
using System.IO;
using HistoryHeroes.Common;
using UnityEditor;
using UnityEngine;

namespace HistoryHeroes.EditorTools
{
    /// <summary>
    /// 2D 그림 시험판: Assets/Art/2D 그림(Tools/art2d_import.py가 다듬은 코덱스 그림)의 가져오기 설정을 맞추고
    /// Resources/Art2DLibrary를 만든다. 메뉴 History Heroes → 2D 그림 다시 만들기.
    /// - 캐릭터 · 서 있는 소품: 받침점 아래 가운데 · 바닥 · 벽: 가운데 + 반복 · UI: 가운데 (9칸 테두리)
    /// </summary>
    public static class Art2DImport
    {
        public const string Root = "Assets/Art/2D";
        private const string LibraryPath = "Assets/Resources/Art2DLibrary.asset";

        // 3D 프리팹 이름 → 그림 이름, 그림 키 배율 (무기 · 깃이 위로 솟은 만큼)
        private static readonly (string model, string art, float scale)[] Characters =
        {
            ("Guan3D", "guan", 1.28f),
            ("YiSunsin3D", "yisunsin", 1.1f),
            ("E_Charger3D", "enemy_charger", 1.08f),
            ("E_Archer3D", "enemy_archer", 1.12f),
            ("E_Bomber3D", "enemy_bomber", 1.05f),
            ("Pet_Hawk3D", "pet_hawk", 1f),
        };

        private static readonly string[] StandingProps = { "door_closed", "door_open", "pillar", "crate", "brazier", "banner", "berry_bush", "skull_pile" };
        private static readonly string[] Tiles = { "floor_ch1", "wall_top", "wall_side", "water_tile" };

        // 구간 장치 3D 프리팹 이름 → 그림 이름 (RoomDevices.FitModel)
        private static readonly (string model, string art)[] Props =
        {
            ("Berry_Bush", "berry_bush"),
        };

        [MenuItem("History Heroes/2D 그림 다시 만들기")]
        public static string BuildAll()
        {
            if (!AssetDatabase.IsValidFolder(Root)) return "그림 없음: " + Root + " (Tools/art2d_import.py 먼저)";
            AssetDatabase.Refresh();
            int n = 0;
            foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", new[] { Root }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (Configure(path)) n++;
            }

            AssetDatabase.SaveAssets();
            Art2DLibrary lib = BuildLibrary(out int characters);
            return $"2D 그림 설정 {n}장 · 캐릭터 {characters}명 · {AssetDatabase.GetAssetPath(lib)}";
        }

        private static bool Configure(string path)
        {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) return false;
            string name = Path.GetFileNameWithoutExtension(path);
            bool character = path.Contains("/Characters/");
            bool standing = character || System.Array.IndexOf(StandingProps, name) >= 0;
            bool tile = System.Array.IndexOf(Tiles, name) >= 0;

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = !path.Contains("/UI/");
            importer.wrapMode = tile ? TextureWrapMode.Repeat : TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Bilinear;
            importer.maxTextureSize = 1024;
            importer.textureCompression = TextureImporterCompression.CompressedHQ;
            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteMeshType = SpriteMeshType.FullRect; // 반복 그리기(Tiled)는 사각 메시가 필요하다
            settings.spriteAlignment = (int)(standing ? SpriteAlignment.BottomCenter : SpriteAlignment.Center);
            settings.spritePixelsPerUnit = 100f;
            importer.SetTextureSettings(settings);
            if (path.Contains("/UI/"))
            {
                // 9칸 테두리: 그림 가장자리에서 장식 크기만큼 (카드·창 = 18%, 단추·띠 = 높이의 40%)
                Texture2D tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                if (tex != null)
                {
                    bool wide = tex.width > tex.height * 2;
                    float bx = wide ? tex.height * 0.4f : tex.width * 0.18f;
                    float by = wide ? tex.height * 0.4f : tex.height * 0.18f;
                    importer.spriteBorder = new Vector4(bx, by, bx, by);
                }
            }

            importer.SaveAndReimport();
            return true;
        }

        private static Sprite Load(string folder, string name) => AssetDatabase.LoadAssetAtPath<Sprite>($"{Root}/{folder}/{name}.png");

        private static Art2DLibrary BuildLibrary(out int characters)
        {
            AssetUtil.EnsureFolder("Assets/Resources");
            var lib = AssetUtil.LoadOrCreate<Art2DLibrary>(LibraryPath);
            lib.characters = new List<Art2DLibrary.Character>();
            characters = 0;
            foreach (var (model, art, scale) in Characters)
            {
                Sprite front = Load("Characters", art + "_front");
                if (front == null) continue;
                lib.characters.Add(new Art2DLibrary.Character
                {
                    modelName = model, front = front, back = Load("Characters", art + "_back") ?? front, side = Load("Characters", art + "_side"), heightScale = scale,
                });
                characters++;
            }

            lib.floor = Load("Stage", "floor_ch1");
            lib.wallTop = Load("Stage", "wall_top");
            lib.wallSide = Load("Stage", "wall_side");
            lib.doorClosed = Load("Stage", "door_closed");
            lib.doorOpen = Load("Stage", "door_open");
            lib.pillar = Load("Stage", "pillar");
            lib.crate = Load("Stage", "crate");
            lib.brazier = Load("Stage", "brazier");
            lib.banner = Load("Stage", "banner");
            lib.water = Load("Stage", "water_tile");
            lib.moss = Load("Stage", "moss_patch");
            lib.bones = Load("Stage", "skull_pile");
            lib.healOrb = Load("Stage", "heal_orb");
            lib.thorns = Load("Stage", "thorns_patch");
            lib.props = new List<Art2DLibrary.Prop>();
            foreach (var (model, art) in Props)
            {
                Sprite s = Load("Stage", art);
                if (s != null) lib.props.Add(new Art2DLibrary.Prop { modelName = model, sprite = s });
            }
            lib.cardFrame = Load("UI", "card_frame");
            lib.cardFrameRare = Load("UI", "card_frame_rare");
            lib.cardFrameLegend = Load("UI", "card_frame_legend");
            lib.panel = Load("UI", "panel");
            lib.buttonPrimary = Load("UI", "button_primary");
            lib.buttonSecondary = Load("UI", "button_secondary");
            lib.ribbon = Load("UI", "ribbon");
            EditorUtility.SetDirty(lib);
            AssetDatabase.SaveAssets();
            return lib;
        }
    }
}
