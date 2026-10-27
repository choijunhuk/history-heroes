using HistoryHeroes.Common;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace HistoryHeroes.EditorTools
{
    /// <summary>
    /// 3D 무대 (Art3D-Plan 3번): 전투 장면의 바닥·벽·장애물에 3D 모양을 입히고, 해·그림자·주변광을 둔다.
    /// 판정은 2D 그대로 (스프라이트 오브젝트의 Collider2D). 3D 모양은 Stage3D.Add로 자식에 붙이고 스프라이트는 숨긴다.
    /// 바닥 스프라이트(예고 원·함정·구덩이·문·연못)는 그대로 → 3D 바닥(z = +0.02) 위에 보인다.
    /// 무늬는 직접 만든 그림 (풀밭·돌바닥·돌벽·나무 상자·바위, Docs/Credits.md).
    /// </summary>
    public static partial class SceneBuilder
    {
        private const float WallHeight = 1.4f;

        private sealed class Stage
        {
            public Material RoomFloor, BossFloor, Grass, Stone, Crate, Rock;
            public Material WallSide, WallEnd, LongSide, LongEnd, Rim, BossOuter;
            public Stage3D Component;
        }

        private static Stage _stage;

        private static Stage CreateStageMaterials()
        {
            foreach (string tex in new[] { "T_Ground_Grass", "T_Floor_Stone", "T_Wall_Stone", "T_Crate", "T_Rock" })
            {
                var importer = AssetImporter.GetAtPath($"{Art3DAssets.Textures}/{tex}.png") as TextureImporter;
                if (importer == null || (importer.wrapMode == TextureWrapMode.Repeat && importer.maxTextureSize == 512)) continue;
                importer.wrapMode = TextureWrapMode.Repeat;
                importer.maxTextureSize = 512;
                importer.SaveAndReimport();
            }

            Material M(string name, string tex, Vector2 tiling, Color? tint = null)
            {
                Material m = Art3DCharacters.Lit("Stage_" + name, tex, null, tint ?? Color.white);
                m.SetTextureScale("_BaseMap", tiling);
                m.SetFloat("_Smoothness", 0.08f);
                EditorUtility.SetDirty(m);
                return m;
            }

            const float stone = 3f; // 돌바닥 무늬 한 장 = 3 유닛
            float wallLong = ArenaHalfHeight * 2f + 2f, wallWide = ArenaHalfWidth * 2f + 2f, survivalLong = SurvivalHalf * 2f + 2f;
            return new Stage
            {
                RoomFloor = M("RoomFloor", "T_Floor_Stone", new Vector2(ArenaHalfWidth * 2f / stone, ArenaHalfHeight * 2f / stone)),
                BossFloor = M("BossFloor", "T_Floor_Stone", new Vector2(BossArenaRadius * 2f / stone, BossArenaRadius * 2f / stone), new Color(0.82f, 0.84f, 0.95f)),
                Grass = M("Grass", "T_Ground_Grass", new Vector2(survivalLong / 6f, survivalLong / 6f)),
                Stone = M("Stone", "T_Wall_Stone", Vector2.one),
                Crate = M("Crate", "T_Crate", Vector2.one),
                Rock = M("Rock", "T_Rock", Vector2.one),
                WallSide = M("WallSide", "T_Wall_Stone", new Vector2(1f, wallLong / 1.5f)),
                WallEnd = M("WallEnd", "T_Wall_Stone", new Vector2(wallWide / 1.5f, 1f)),
                LongSide = M("LongSide", "T_Wall_Stone", new Vector2(1f, survivalLong / 1.5f)),
                LongEnd = M("LongEnd", "T_Wall_Stone", new Vector2(survivalLong / 1.5f, 1f)),
                Rim = M("Rim", "T_Wall_Stone", new Vector2(1f, 1f), new Color(0.85f, 0.85f, 0.95f)),
                BossOuter = M("BossOuter", "T_Floor_Stone", new Vector2(ArenaHalfWidth * 2f / stone, ArenaHalfHeight * 2f / stone), new Color(0.32f, 0.32f, 0.38f)),
            };
        }

        /// <summary>해(그림자) · 주변광 · 게임 중 장애물용 Stage3D (재질·기본 메시)</summary>
        private static void BuildLighting(Stage stage)
        {
            var sunObject = new GameObject("Sun");
            var sun = sunObject.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.intensity = 1.25f;
            sun.color = new Color(1f, 0.96f, 0.88f);
            sun.shadows = LightShadows.Hard; // 폰: 딱딱한 그림자 1단 (URP 설정)
            sun.shadowStrength = 0.7f;
            sunObject.transform.rotation = Quaternion.LookRotation(new Vector3(0.35f, -0.45f, 1f).normalized, Vector3.up); // 그림자는 오른쪽 아래로

            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.5f, 0.52f, 0.58f);
            RenderSettings.skybox = null;
            RenderSettings.fog = false;

            stage.Component = new GameObject("Stage3D").AddComponent<Stage3D>();
            using (var props = new Props(stage.Component))
            {
                props.Ref("stone", stage.Stone).Ref("crate", stage.Crate).Ref("rock", stage.Rock)
                    .Ref("cube", Builtin(Stage3D.Shape.Box)).Ref("cylinder", Builtin(Stage3D.Shape.Pillar))
                    .Ref("sphere", Builtin(Stage3D.Shape.Rock)).Ref("quad", Builtin(Stage3D.Shape.Floor));
            }
        }

        /// <summary>지름 1 원판 (XY 평면, -Z를 본다, 무늬는 반듯하게 x·y 그대로). 장면에 넣으려고 에셋으로 저장</summary>
        private static Mesh DiscMesh()
        {
            const string path = Art3DAssets.Root + "/Meshes/Disc.asset";
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            bool created = mesh == null;
            if (created) mesh = new Mesh { name = "Disc" };
            const int segments = 48;
            var vertices = new Vector3[segments + 1];
            var uv = new Vector2[segments + 1];
            var normals = new Vector3[segments + 1];
            var tris = new int[segments * 3];
            vertices[0] = Vector3.zero;
            uv[0] = new Vector2(0.5f, 0.5f);
            for (int i = 0; i <= segments; i++) normals[i] = Vector3.back;
            for (int i = 0; i < segments; i++)
            {
                float a = i * Mathf.PI * 2f / segments;
                var p = new Vector3(Mathf.Cos(a) * 0.5f, Mathf.Sin(a) * 0.5f, 0f);
                vertices[i + 1] = p;
                uv[i + 1] = new Vector2(p.x + 0.5f, p.y + 0.5f);
                // 카메라(-Z 쪽)에서 보이게 시계 방향 (Unity는 왼손 좌표)
                tris[i * 3] = 0;
                tris[i * 3 + 1] = (i + 1) % segments + 1;
                tris[i * 3 + 2] = i + 1;
            }

            mesh.Clear();
            mesh.vertices = vertices;
            mesh.uv = uv;
            mesh.normals = normals;
            mesh.triangles = tris;
            mesh.RecalculateBounds();
            mesh.RecalculateTangents();
            if (created) AssetDatabase.CreateAsset(mesh, path);
            else EditorUtility.SetDirty(mesh);
            return mesh;
        }

        private static Mesh Builtin(Stage3D.Shape shape) => Resources.GetBuiltinResource<Mesh>(Stage3D.BuiltinName(shape));

        /// <summary>바닥 스프라이트 → 3D 판 (스프라이트는 숨긴다)</summary>
        private static void DressFloor(GameObject block, Material mat)
        {
            Stage3D.Add(block.transform, Stage3D.Shape.Floor, 0f, mat, castShadows: false);
            block.GetComponent<SpriteRenderer>().enabled = false;
        }

        /// <summary>벽 스프라이트 → 3D 돌벽 (세로 벽 · 가로 벽은 무늬 반복이 다르다)</summary>
        private static void DressWall(GameObject block, Material side, Material end)
        {
            Vector3 s = block.transform.localScale;
            Stage3D.Add(block.transform, Stage3D.Shape.Box, WallHeight, s.y > s.x ? side : end, castShadows: true);
            block.GetComponent<SpriteRenderer>().enabled = false;
        }

        /// <summary>보스맵: 둥근 돌바닥 + 둘레 낮은 돌담 (판정은 EdgeCollider2D 원 그대로)</summary>
        private static void DressBossArena(Transform bossRoot, Stage stage)
        {
            foreach (string name in new[] { "BossRim", "BossFloor" })
            {
                Transform t = bossRoot.Find(name);
                if (t != null) t.GetComponent<SpriteRenderer>().enabled = false;
            }

            // 둥근 돌바닥: 내장 원기둥은 뚜껑 무늬가 소용돌이로 감겨서, 반듯한 무늬의 원판을 직접 만든다
            Transform floor = bossRoot.Find("BossFloor");
            var disc = new GameObject(Stage3D.ShapeName);
            disc.transform.SetParent(floor, false);
            disc.transform.localPosition = new Vector3(0f, 0f, 0.02f);
            disc.AddComponent<MeshFilter>().sharedMesh = DiscMesh();
            var discRenderer = disc.AddComponent<MeshRenderer>();
            discRenderer.sharedMaterial = stage.BossFloor;
            discRenderer.shadowCastingMode = ShadowCastingMode.Off;

            // 둘레 밖 (네모 벽까지): 어두운 돌바닥 — 검은 빈칸이 안 보이게
            var outer = new GameObject("BossOuterFloor");
            outer.transform.SetParent(bossRoot, false);
            outer.transform.localScale = new Vector3(ArenaHalfWidth * 2f, ArenaHalfHeight * 2f, 1f);
            Stage3D.Add(outer.transform, Stage3D.Shape.Floor, 0f, stage.BossOuter, castShadows: false).transform.localPosition = new Vector3(0f, 0f, 0.03f);

            const int segments = 28;
            var rim = new GameObject("BossRim3D").transform;
            rim.SetParent(bossRoot, false);
            float r = BossArenaRadius + 0.25f;
            float length = 2f * Mathf.PI * r / segments + 0.08f;
            for (int i = 0; i < segments; i++)
            {
                float a = (i + 0.5f) * Mathf.PI * 2f / segments;
                var seg = new GameObject($"Rim {i}").transform;
                seg.SetParent(rim, false);
                seg.position = new Vector3(Mathf.Cos(a) * r, Mathf.Sin(a) * r, 0f);
                seg.rotation = Quaternion.Euler(0f, 0f, a * Mathf.Rad2Deg);
                seg.localScale = new Vector3(0.45f, length, 1f);
                Stage3D.Add(seg, Stage3D.Shape.Box, 0.9f, stage.Rim, castShadows: true);
            }
        }
    }
}
