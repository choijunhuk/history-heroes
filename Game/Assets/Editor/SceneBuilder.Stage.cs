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
            public Material WallSide, WallEnd, LongSide, LongEnd, Rim, BossOuter, Outside;
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
                Grass = M("Grass", "T_Ground_Grass", new Vector2(survivalLong / 9f, survivalLong / 9f)), // 무늬 한 장 9 유닛 (반복이 덜 보이게)
                Stone = M("Stone", "T_Wall_Stone", Vector2.one),
                Crate = M("Crate", "T_Crate", Vector2.one),
                Rock = M("Rock", "T_Rock", Vector2.one),
                WallSide = M("WallSide", "T_Wall_Stone", new Vector2(1f, wallLong / 1.5f)),
                WallEnd = M("WallEnd", "T_Wall_Stone", new Vector2(wallWide / 1.5f, 1f)),
                LongSide = M("LongSide", "T_Wall_Stone", new Vector2(1f, survivalLong / 1.5f)),
                LongEnd = M("LongEnd", "T_Wall_Stone", new Vector2(survivalLong / 1.5f, 1f)),
                Rim = M("Rim", "T_Wall_Stone", new Vector2(1f, 1f), new Color(0.85f, 0.85f, 0.95f)),
                BossOuter = M("BossOuter", "T_Floor_Stone", new Vector2(ArenaHalfWidth * 2f / stone, ArenaHalfHeight * 2f / stone), new Color(0.32f, 0.32f, 0.38f)),
                Outside = M("Outside", "T_Rock", new Vector2(OutsideSize / 4f, OutsideSize / 4f), new Color(0.3f, 0.29f, 0.28f)),
            };
        }

        /// <summary>해(그림자) · 주변광 · 게임 중 장애물용 Stage3D (재질·기본 메시) · 화면 후처리 · 구간별 빛·색 (StageLook)</summary>
        private static void BuildLighting(Stage stage, Camera camera)
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
                    .Ref("sphere", Builtin(Stage3D.Shape.Rock)).Ref("quad", Builtin(Stage3D.Shape.Floor))
                    // 블렌더 소품 (그래픽 평가 4번): 돌덩이 · 돌기둥 · 상자 3종
                    .Ref("block", Art3DStage.Prefab("Block_Stone")).Ref("pillar", Art3DStage.Prefab("Pillar_Stone"))
                    .Refs("crates", new[] { Art3DStage.Prefab("Crate_A"), Art3DStage.Prefab("Crate_B"), Art3DStage.Prefab("Crate_C") });
            }

            // 화면 후처리 (그래픽 평가 1번): 톤 매핑 · 약한 빛 번짐(1 넘는 밝은 색만) · 색 보정 · 가장자리 어둡게
            var volume = new GameObject("PostFx").AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = 1f;
            volume.sharedProfile = BattleVolumeProfile();

            var look = new GameObject("StageLook").AddComponent<StageLook>();
            _look = look;
            using (var props = new Props(look))
            {
                // 블렌더 돌 소품(돌덩이·돌기둥·아치·석상 받침)도 벽처럼 구간 색을 입는다
                props.Ref("sun", sun).Ref("volume", volume).Ref("viewCamera", camera)
                    .Refs("floorMaterials", new[] { stage.RoomFloor, stage.BossFloor, stage.BossOuter })
                    .Refs("wallMaterials", new[] { stage.WallSide, stage.WallEnd, stage.Rim, stage.Stone, Art3DProps.RoleMaterial("Stone"), Art3DProps.RoleMaterial("Stone_Dark") });
                Looks(props.Property("looks"));
            }
        }

        private const string BattleVolumePath = "Assets/Settings/BattleVolume.asset";

        /// <summary>전투 후처리 묶음 (에셋). 색 보정 값은 구간마다 StageLook이 게임 중 사본에 덮어쓴다</summary>
        private static VolumeProfile BattleVolumeProfile()
        {
            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(BattleVolumePath);
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<VolumeProfile>();
                AssetDatabase.CreateAsset(profile, BattleVolumePath);
            }

            T Get<T>() where T : VolumeComponent
            {
                if (profile.TryGet(out T component)) return component;
                component = profile.Add<T>(true);
                component.name = typeof(T).Name;
                AssetDatabase.AddObjectToAsset(component, profile);
                return component;
            }

            var tone = Get<UnityEngine.Rendering.Universal.Tonemapping>();
            tone.mode.Override(UnityEngine.Rendering.Universal.TonemappingMode.Neutral);

            var bloom = Get<UnityEngine.Rendering.Universal.Bloom>();
            bloom.threshold.Override(0.95f);
            bloom.intensity.Override(0.45f);
            bloom.scatter.Override(0.6f);
            bloom.highQualityFiltering.Override(false);
            bloom.downscale.Override(UnityEngine.Rendering.Universal.BloomDownscaleMode.Quarter); // 폰: 1/4 크기에서 번진다

            var color = Get<UnityEngine.Rendering.Universal.ColorAdjustments>();
            color.postExposure.Override(0.1f);
            color.contrast.Override(10f);
            color.saturation.Override(12f);
            color.colorFilter.Override(Color.white);

            var vignette = Get<UnityEngine.Rendering.Universal.Vignette>();
            vignette.intensity.Override(0.12f); // 재평가: 벽 옆 적·탄을 가리지 않게 약하게
            vignette.smoothness.Override(0.45f);
            vignette.color.Override(Color.black);

            EditorUtility.SetDirty(profile);
            AssetDatabase.SaveAssets();
            return profile;
        }

        /// <summary>
        /// 구간별 빛·색 (Content 3장 구간 이름): 1 균열의 초원 (따뜻한 햇빛) · 2 잊힌 성채 (차가운 돌) · 3 녹슨 공방 (녹·구리) ·
        /// 4 그림자 협곡 (보랏빛 저녁) · 5 망각의 왕좌 (어두운 보라 + 금빛) · 서바이벌 풀밭 (밝은 낮).
        /// 바닥은 전보다 한 단계 밝게 → 어두운 옷의 캐릭터가 묻히지 않게 (그래픽 평가 "조명·색")
        /// </summary>
        private static void Looks(SerializedProperty list)
        {
            var looks = new[]
            {
                // key, 바닥, 벽, 해, 해 밝기, 주변광, 화면 색, 노출, 대비, 채도, 배경
                ("ch1", new Color(1.12f, 1.06f, 0.92f), new Color(1.02f, 0.98f, 0.9f), new Color(1f, 0.94f, 0.8f), 1.4f, new Color(0.56f, 0.58f, 0.6f), new Color(1f, 0.98f, 0.93f), 0.12f, 12f, 16f, new Color(0.1f, 0.09f, 0.07f)),
                ("ch2", new Color(0.98f, 1.02f, 1.1f), new Color(0.88f, 0.93f, 1.04f), new Color(0.94f, 0.97f, 1f), 1.3f, new Color(0.5f, 0.54f, 0.64f), new Color(0.95f, 0.98f, 1.04f), 0.1f, 12f, 10f, new Color(0.06f, 0.07f, 0.1f)),
                ("ch3", new Color(1.12f, 0.95f, 0.78f), new Color(1f, 0.82f, 0.66f), new Color(1f, 0.86f, 0.66f), 1.35f, new Color(0.56f, 0.48f, 0.42f), new Color(1.04f, 0.97f, 0.9f), 0.1f, 14f, 12f, new Color(0.1f, 0.07f, 0.05f)),
                ("ch4", new Color(0.95f, 0.9f, 1.08f), new Color(0.78f, 0.72f, 0.92f), new Color(0.9f, 0.84f, 1f), 1.25f, new Color(0.48f, 0.45f, 0.62f), new Color(0.97f, 0.95f, 1.05f), 0.12f, 14f, 10f, new Color(0.07f, 0.05f, 0.1f)),
                ("ch5", new Color(0.92f, 0.86f, 1f), new Color(0.66f, 0.6f, 0.76f), new Color(1f, 0.9f, 0.74f), 1.3f, new Color(0.44f, 0.4f, 0.52f), new Color(1f, 0.96f, 1.02f), 0.14f, 16f, 10f, new Color(0.06f, 0.04f, 0.08f)),
                ("survival", Color.white, Color.white, new Color(1f, 0.97f, 0.88f), 1.35f, new Color(0.56f, 0.6f, 0.6f), new Color(1f, 1f, 0.97f), 0.08f, 10f, 14f, new Color(0.1f, 0.12f, 0.08f)),
            };

            list.arraySize = looks.Length;
            for (int i = 0; i < looks.Length; i++)
            {
                var l = looks[i];
                SerializedProperty e = list.GetArrayElementAtIndex(i);
                e.FindPropertyRelative("key").stringValue = l.Item1;
                e.FindPropertyRelative("floor").colorValue = l.Item2;
                e.FindPropertyRelative("wall").colorValue = l.Item3;
                e.FindPropertyRelative("sun").colorValue = l.Item4;
                e.FindPropertyRelative("sunIntensity").floatValue = l.Item5;
                e.FindPropertyRelative("ambient").colorValue = l.Item6;
                e.FindPropertyRelative("filter").colorValue = l.Item7;
                e.FindPropertyRelative("exposure").floatValue = l.Item8;
                e.FindPropertyRelative("contrast").floatValue = l.Item9;
                e.FindPropertyRelative("saturation").floatValue = l.Item10;
                e.FindPropertyRelative("background").colorValue = l.Item11;
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

        private const float OutsideSize = 70f;

        /// <summary>벽 바깥 땅 (어두운 흙바위): 방이 검은 빈칸에 떠 있지 않고 땅에 박혀 보이게. 서바이벌 풀밭은 이 위(z +0.02)에 깔린다</summary>
        private static void BuildOutside(Stage stage)
        {
            var outside = new GameObject("Outside");
            outside.transform.localScale = new Vector3(OutsideSize, OutsideSize, 1f);
            Stage3D.Add(outside.transform, Stage3D.Shape.Floor, 0f, stage.Outside, castShadows: false).transform.localPosition = new Vector3(0f, 0f, 0.06f);
        }

        private static Mesh Builtin(Stage3D.Shape shape) => Resources.GetBuiltinResource<Mesh>(Stage3D.BuiltinName(shape));

        /// <summary>바닥 스프라이트 → 3D 판 (스프라이트는 숨긴다)</summary>
        private static void DressFloor(GameObject block, Material mat)
        {
            Stage3D.Add(block.transform, Stage3D.Shape.Floor, 0f, mat, castShadows: false);
            block.GetComponent<SpriteRenderer>().enabled = false;
        }

        /// <summary>앞(화면 아래) 벽 높이: 카메라 쪽 벽이 높으면 그 너머(벽 앞에 선 영웅·예고)를 가린다 (3D 화면 점검 2번)</summary>
        private const float NearWallHeight = 0.4f;

        /// <summary>벽 스프라이트 → 3D 돌벽 (세로 벽 · 가로 벽은 무늬 반복이 다르다). 이름이 "Wall Bottom"이면 낮게</summary>
        private static void DressWall(GameObject block, Material side, Material end)
        {
            Vector3 s = block.transform.localScale;
            float height = block.name == "Wall Bottom" ? NearWallHeight : WallHeight;
            block.GetComponent<SpriteRenderer>().enabled = false;
            // 던전 방 위 벽만 (서바이벌 울타리에는 문이 없다)
            if (block.name == "Wall Top" && block.transform.parent != null && block.transform.parent.name == "Arena" && Art3DStage.Prefab("Door_Gate_Parts") != null)
            {
                // 가운데에 아치 문이 들어갈 틈 (판정 벽은 그대로 한 덩어리)
                const float gap = 2.3f;
                float half = (s.x - gap) * 0.5f;
                foreach (float sign in new[] { -1f, 1f })
                {
                    var piece = new GameObject(sign < 0f ? "Left" : "Right");
                    piece.transform.SetParent(block.transform, false);
                    piece.transform.localPosition = new Vector3(sign * (gap * 0.5f + half * 0.5f) / s.x, 0f, 0f);
                    piece.transform.localScale = new Vector3(half / s.x, 1f, 1f);
                    Stage3D.Add(piece.transform, Stage3D.Shape.Box, height, end, castShadows: true);
                }

                return;
            }

            Stage3D.Add(block.transform, Stage3D.Shape.Box, height, s.y > s.x ? side : end, castShadows: true);
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
                float rimHeight = Mathf.Sin(a) < -0.25f ? NearWallHeight : 0.9f; // 카메라 쪽(아래) 반은 낮게 — 영웅·예고를 가리지 않게
                Stage3D.Add(seg, Stage3D.Shape.Box, rimHeight, stage.Rim, castShadows: true);
            }
        }
    }
}
