using System.Collections.Generic;
using System.IO;
using HistoryHeroes.Common;
using HistoryHeroes.Enemy;
using HistoryHeroes.Player;
using HistoryHeroes.Skills;
using HistoryHeroes.UI;
using UnityEditor;
using UnityEngine;

namespace HistoryHeroes.EditorTools
{
    /// <summary>
    /// 그래픽 방향 비교 샘플 (Art.md 3장 "비교 실험"): A = 무료 에셋 (Kenney Tiny Dungeon, CC0, 픽셀), B = AI가 코드로 그린 그림 (SVG).
    /// 순서 (Play, 전투 화면에서): Import() → Stage() → Freeze(true) → Shot("base"|"a"|"b", 경로) → Freeze(false).
    /// 같은 멈춘 순간을 그림만 바꿔 찍으므로 공정하게 비교된다. 진짜 프리팹·장면은 바꾸지 않는다 (고른 다음 빌더에 넣는다).
    /// </summary>
    public static class ArtSamples
    {
        private const string Root = "Assets/Art/Samples";
        private static readonly object Freezer = new object();

        /// <summary>가져오기 설정: A(픽셀)는 점 필터 · 16 PPU, B는 부드럽게 · 그림마다 PPU. 이어 붙이는 무늬는 FullRect</summary>
        public static string Import()
        {
            var lines = new List<string>();
            foreach (string file in Directory.GetFiles(Root, "*.png", SearchOption.AllDirectories))
            {
                string path = file.Replace('\\', '/');
                var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer == null) continue;
                bool pixel = path.Contains("/A/");
                string name = Path.GetFileNameWithoutExtension(path);
                bool tiled = name == "floor" || name == "floor_steppe" || name == "stone_tile";

                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.mipmapEnabled = false;
                importer.alphaIsTransparency = true;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.filterMode = pixel ? FilterMode.Point : FilterMode.Bilinear;
                importer.wrapMode = tiled ? TextureWrapMode.Repeat : TextureWrapMode.Clamp;
                importer.spritePixelsPerUnit = pixel ? 16f : BPixelsPerUnit(name);
                var settings = new TextureImporterSettings();
                importer.ReadTextureSettings(settings);
                settings.spriteMeshType = tiled ? SpriteMeshType.FullRect : SpriteMeshType.Tight;
                importer.SetTextureSettings(settings);
                importer.SaveAndReimport();
                lines.Add($"{path} {importer.spritePixelsPerUnit}");
            }

            return string.Join("\n", lines);
        }

        /// <summary>B 그림 크기: 캐릭터 256px → 1.28칸, 화살 256px → 0.64칸, 칼날 192px → 0.64칸, 바닥 256px → 2칸, 돌 무늬 128px → 1칸</summary>
        private static float BPixelsPerUnit(string name)
        {
            switch (name)
            {
                case "khan":
                case "charger":
                case "archer": return 200f;
                case "arrow_player":
                case "arrow_enemy": return 400f;
                case "spin_blade": return 300f;
                case "floor_steppe": return 128f;
                case "stone_tile": return 128f;
                case "lightning": return 192f;
                default: return 192f; // 바위 · 상자: 1칸 (장애물 크기만큼 늘어난다)
            }
        }

        /// <summary>
        /// 찍을 무대: 회전 칼날 Lv3 칭기즈 칸 (무적) + 돌진병 3 · 궁병 3 + 높은 벽 · 둥근 기둥 · 상자. 레벨업 창은 끈다.
        /// 장애물은 DungeonRun.Obstacle과 같은 이름·도형·색 (그림 미리보기가 같은 규칙으로 바꾼다).
        /// </summary>
        public static string Stage()
        {
            if (!Application.isPlaying) return "Play 모드가 아님";
            var player = Object.FindAnyObjectByType<PlayerController>();
            var skills = Object.FindAnyObjectByType<SkillManager>();
            if (player == null || skills == null) return "전투 화면이 아님";

            var levelUp = Object.FindAnyObjectByType<LevelUpScreen>();
            if (levelUp != null)
            {
                levelUp.DevCancel();
                levelUp.enabled = false;
            }

            Dev.DevCheats.KillAll();
            var db = AssetDatabase.LoadAssetAtPath<SkillDatabase>("Assets/Data/Skills/SkillDatabase.asset");
            skills.ClearAll();
            skills.DevSetLevel(db.Find("spin_blade"), 3, false);
            player.Teleport(new Vector2(0f, -3.2f));
            player.Health.Heal(1e9f);
            player.Health.Invulnerable = true;

            var root = new GameObject("ArtStage").transform;
            Obstacle(root, "High", new Vector2(-3.1f, 1.8f), new Vector2(1.2f, 3.4f), false, new Color(0.42f, 0.38f, 0.33f));
            Obstacle(root, "High", new Vector2(2.7f, 4.2f), new Vector2(0.9f, 0.9f), true, new Color(0.42f, 0.38f, 0.33f));
            Obstacle(root, "Breakable", new Vector2(2.6f, -0.8f), new Vector2(0.9f, 0.9f), false, new Color(0.62f, 0.45f, 0.25f));

            var chargers = new[] { new Vector2(-1.2f, 1.2f), new Vector2(0.9f, 2.4f), new Vector2(-0.6f, 4.6f) };
            var archers = new[] { new Vector2(1.8f, 5.6f), new Vector2(-1.9f, 6.2f), new Vector2(0.2f, 6.8f) };
            Spawn(root, "Enemy_Charger", chargers, player);
            Spawn(root, "Enemy_Archer", archers, player);
            return "무대 준비 (몇 초 뒤 Freeze(true) → Shot)";
        }

        public static string Freeze(bool on)
        {
            if (on) GamePause.Pause(Freezer);
            else GamePause.Resume(Freezer);
            return on ? "멈춤" : "풀림";
        }

        /// <summary>set: "base"(지금 도형) / "a" / "b". 멈춘 화면을 그 그림으로 바꿔 카메라 화면(UI 없이)을 PNG로</summary>
        public static string Shot(string set, string path)
        {
            if (!Application.isPlaying) return "Play 모드가 아님";
            if (set == "base") Dev.ArtPreview.End();
            else Dev.ArtPreview.Begin(LoadSet(set));
            return QaTools.SnapshotWorld(path);
        }

        private static Dev.ArtPreview.Set LoadSet(string id)
        {
            string dir = $"{Root}/{id.ToUpperInvariant()}";
            Sprite S(string n) => AssetDatabase.LoadAssetAtPath<Sprite>($"{dir}/{n}.png");
            bool pixel = id == "a";
            return new Dev.ArtPreview.Set
            {
                name = id,
                hero = S("khan"),
                charger = S("charger"),
                archer = S("archer"),
                arrowPlayer = S("arrow_player"),
                arrowEnemy = S("arrow_enemy"),
                blade = S("spin_blade"),
                floor = S(pixel ? "floor" : "floor_steppe"),
                stone = S("stone_tile"),
                rock = S("rock"),
                crate = S("crate"),
                arrowScale = pixel ? 0.6f : 1f,
                bladeScale = pixel ? 0.7f : 1f,
            };
        }

        private static void Obstacle(Transform root, string kind, Vector2 position, Vector2 size, bool circle, Color color)
        {
            var go = new GameObject(kind);
            go.transform.SetParent(root, false);
            go.transform.position = position;
            go.transform.localScale = new Vector3(size.x, size.y, 1f);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = circle ? PlaceholderSprites.Circle : PlaceholderSprites.Square;
            sr.sortingOrder = -6;
            sr.color = color;
        }

        private static void Spawn(Transform root, string prefabName, Vector2[] positions, PlayerController player)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<EnemyBase>($"Assets/Prefabs/Enemies/{prefabName}.prefab");
            foreach (Vector2 p in positions)
            {
                EnemyBase enemy = Object.Instantiate(prefab, p, Quaternion.identity, root);
                enemy.Spawn(player.transform, 50f, 1f, false);
            }
        }
    }
}
