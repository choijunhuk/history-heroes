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
            // 영웅 8
            ("Khan3D", "khan", 1.12f), ("Guan3D", "guan", 1.28f), ("Napoleon3D", "napoleon", 1.1f), ("Tesla3D", "tesla", 1.08f),
            ("YiSunsin3D", "yisunsin", 1.1f), ("YiSeonggye3D", "yiseonggye", 1.15f), ("Davinci3D", "davinci", 1.1f), ("Zhuge3D", "zhuge", 1.12f),
            // 적 7
            ("E_Charger3D", "enemy_charger", 1.08f), ("E_Archer3D", "enemy_archer", 1.12f), ("E_Bomber3D", "enemy_bomber", 1.05f),
            ("E_Shield3D", "enemy_shield", 1.1f), ("E_Summoner3D", "enemy_summoner", 1.15f), ("E_Splitter3D", "enemy_splitter", 1f), ("E_Minion3D", "enemy_minion", 1f),
            // 중간 보스 5 · 보스 5 (+ 그림자 쌍둥이의 비치는 쪽)
            ("M_Bison3D", "mid_bison", 1.15f), ("M_Torch3D", "mid_torch", 1.2f), ("M_Golem3D", "mid_golem", 1.15f), ("M_Hunter3D", "mid_hunter", 1.12f), ("M_Executor3D", "mid_executor", 1.2f),
            ("B_Warden3D", "boss_warden", 1.25f), ("B_Wraith3D", "boss_wraith", 1.2f), ("B_Colossus3D", "boss_colossus", 1.25f), ("B_Twins3D", "boss_twins", 1.15f),
            ("B_TwinsGhost3D", "boss_twins", 1.15f), ("B_King3D", "boss_king", 1.3f),
            // 펫 4
            ("Pet_Hawk3D", "pet_hawk", 1f), ("Pet_Dove3D", "pet_dove", 1f), ("Pet_Marengo3D", "pet_marengo", 1f), ("Pet_RedHare3D", "pet_redhare", 1f),
        };

        private static readonly string[] StandingProps =
        {
            "door_closed", "door_open", "pillar", "crate", "brazier", "banner", "berry_bush", "skull_pile", "powder_keg", "iron_gate", "lever",
            "rock", "rock_cluster", "tree", "haystack", "ruin_house", "boiler", "pipe_column", "survival_brazier", "stone_sentinel",
        };

        private static bool IsTile(string name) => name.StartsWith("floor") || name.StartsWith("wall_") || name.StartsWith("ground_") || name == "water_tile" || name == "boss_floor" || name == "conveyor_tile";

        // 3D 프리팹 이름 → 그림 이름 (구간 장치 RoomDevices.FitModel · 스킬 물건 SkillArt.Attach · 서바이벌 맵 장식 Art2DStage)
        private static readonly (string model, string art)[] Props =
        {
            ("Berry_Bush", "berry_bush"), ("Powder_Keg", "powder_keg"), ("Iron_Gate", "iron_gate"), ("Saw_Gear", "saw_gear"), ("Lever", "lever"),
            ("Haystack", "haystack"), ("Survival_Brazier", "survival_brazier"), ("Ruin_House_A", "ruin_house"), ("Ruin_House_B", "ruin_house"),
            ("Ruin_House_C", "ruin_house"), ("Ruin_House_D", "ruin_house"), ("Pipe_Column", "pipe_column"), ("Boiler", "boiler"), ("Deco_Tree", "tree"),
            ("Fx_TurtleShip", "turtle_ship"), ("Fx_ArmoredTank", "armored_tank"), ("Fx_FlyingMachine", "flying_machine"), ("Fx_StoneSentinel", "stone_sentinel"),
            ("Fx_BigArrow", "big_arrow"), ("Fx_Cannonball", "cannonball"),
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
            bool tile = IsTile(name);

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
                    alpha = model == "B_TwinsGhost3D" ? 0.45f : 1f, // 맞지 않는 쪽 쌍둥이는 비친다
                });
                characters++;
            }

            lib.floor = Load("Stage", "floor_ch1");
            lib.wallTop = Load("Stage", "wall_top");
            lib.wallSide = Load("Stage", "wall_side");
            for (int c = 1; c <= 5; c++)
            {
                lib.floors[c - 1] = Load("Stage", $"floor_ch{c}");
                lib.wallTops[c - 1] = c == 1 ? lib.wallTop : Load("Stage", $"wall_top_ch{c}");
                lib.wallSides[c - 1] = c == 1 ? lib.wallSide : Load("Stage", $"wall_side_ch{c}");
            }

            lib.bossFloor = Load("Stage", "boss_floor");
            lib.bossRing = Load("Stage", "boss_ring");
            lib.grounds = new[] { Load("Stage", "ground_field"), Load("Stage", "ground_city"), Load("Stage", "ground_factory") };
            lib.fireVent = Load("Stage", "fire_vent");
            lib.conveyor = Load("Stage", "conveyor_tile");
            lib.steam = Load("Stage", "steam_vent");
            lib.rockfall = Load("Stage", "rock");
            lib.vines = Load("Stage", "vines_patch");
            lib.oblivion = Load("Stage", "oblivion_crack");
            lib.rockCluster = Load("Stage", "rock_cluster");
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
