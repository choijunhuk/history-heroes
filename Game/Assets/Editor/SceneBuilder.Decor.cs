using HistoryHeroes.Common;
using HistoryHeroes.Dungeon;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace HistoryHeroes.EditorTools
{
    /// <summary>
    /// 맵 꾸미기 (그래픽 평가 4번, 개발자: "맵도 너무 단조로운거 같은데"): 블렌더 소품으로
    /// - 방 위쪽 문 = 돌 아치 + 나무 문짝 (열리면 뒤로 젖혀지고 문 안이 빛난다)
    /// - 구간마다 다른 벽 위·벽 밖 장식 (깃발 · 화로 불 · 석상 · 톱니바퀴 · 수정 · 나무)
    /// - 구간마다 다른 바닥 장식 묶음 (방에 들어갈 때 StageLook이 흩뿌린다)
    /// - 서바이벌 풀밭: 풀포기 · 꽃 · 덤불, 울타리 밖 나무
    /// 모두 보기만 한다 (판정 없음). 블렌더 소품이 없으면 아무것도 안 한다.
    /// </summary>
    public static partial class SceneBuilder
    {
        private static StageLook _look;

        // ---------------------------------------------------------------- 빛나는 입자 재질 (불·문 빛·효과)

        /// <summary>더하기 섞기 입자 재질 (URP Particles/Unlit). 1 넘는 색이면 화면 후처리의 빛 번짐이 잡는다. texture null = 둥근 빛</summary>
        private static Material FxGlowMaterial(string name, Color hdr, Sprite texture = null, bool plain = false)
        {
            string path = $"{Art3DAssets.Root}/Materials/M_FxGlow_{name}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit"));
                AssetDatabase.CreateAsset(mat, path);
            }

            mat.SetFloat("_Surface", 1f);
            mat.SetFloat("_Blend", 2f); // 더하기
            mat.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            mat.SetFloat("_DstBlend", (float)BlendMode.One);
            mat.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
            mat.SetFloat("_DstBlendAlpha", (float)BlendMode.One);
            mat.SetFloat("_ZWrite", 0f);
            mat.SetOverrideTag("RenderType", "Transparent");
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            mat.renderQueue = (int)RenderQueue.Transparent;
            Sprite tex = texture != null ? texture : _kit != null ? _kit.Glow : null;
            mat.SetTexture("_BaseMap", plain || tex == null ? null : tex.texture); // plain = 꼬리 빛처럼 무늬 없이
            mat.SetColor("_BaseColor", hdr);
            mat.enableInstancing = true;
            EditorUtility.SetDirty(mat);
            return mat;
        }

        /// <summary>반투명 연기 재질 (알파 섞기)</summary>
        private static Material FxSmokeMaterial()
        {
            const string path = Art3DAssets.Root + "/Materials/M_FxSmoke.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit"));
                AssetDatabase.CreateAsset(mat, path);
            }

            mat.SetFloat("_Surface", 1f);
            mat.SetFloat("_Blend", 0f); // 알파
            mat.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            mat.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            mat.SetFloat("_ZWrite", 0f);
            mat.SetOverrideTag("RenderType", "Transparent");
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.renderQueue = (int)RenderQueue.Transparent;
            if (_kit != null && _kit.Glow != null) mat.SetTexture("_BaseMap", _kit.Glow.texture);
            mat.SetColor("_BaseColor", Color.white);
            mat.enableInstancing = true;
            EditorUtility.SetDirty(mat);
            return mat;
        }

        /// <summary>투사체 꼬리 빛 (빌더에서 붙인다, 풀에서 다시 나올 때 Projectile이 지운다)</summary>
        private static void AddTrail(GameObject go, Color color, float width, float time)
        {
            var trail = go.AddComponent<TrailRenderer>();
            trail.sharedMaterial = FxGlowMaterial("Trail", new Color(1.8f, 1.8f, 1.8f), plain: true);
            trail.time = time;
            trail.minVertexDistance = 0.04f;
            trail.widthMultiplier = width;
            trail.widthCurve = AnimationCurve.Linear(0f, 1f, 1f, 0f);
            var gradient = new Gradient();
            gradient.SetKeys(new[] { new GradientColorKey(color, 0f), new GradientColorKey(color, 1f) },
                new[] { new GradientAlphaKey(0.85f, 0f), new GradientAlphaKey(0f, 1f) });
            trail.colorGradient = gradient;
            trail.alignment = LineAlignment.View;
            trail.shadowCastingMode = ShadowCastingMode.Off;
            trail.receiveShadows = false;
        }

        // ---------------------------------------------------------------- 스킬 모양 · 맞음 불꽃 (그래픽 평가 2번)

        /// <summary>Resources/SkillArt: 스킬이 게임 중 꺼내 쓰는 블렌더 모델 + 꼬리 빛 재질</summary>
        private static void CreateSkillArt()
        {
            AssetUtil.EnsureFolder("Assets/Resources");
            var art = AssetUtil.LoadOrCreate<Skills.SkillArt>("Assets/Resources/SkillArt.asset");
            art.blade = Art3DStage.Prefab("Fx_Blade");
            art.boomerang = Art3DStage.Prefab("Fx_Boomerang");
            art.iceShard = Art3DStage.Prefab("Fx_IceShard");
            art.meteor = Art3DStage.Prefab("Fx_Meteor");
            art.shield = Art3DStage.Prefab("Fx_Shield");
            art.trail = FxGlowMaterial("Trail", new Color(1.8f, 1.8f, 1.8f), plain: true);
            EditorUtility.SetDirty(art);
        }

        /// <summary>장면에 하나: 맞음 불꽃 · 연기 · 퍼지는 고리 입자 장치 (FxBursts가 Emit만 한다)</summary>
        private static void BuildFxBursts()
        {
            var root = new GameObject("FxBursts");
            ParticleSystem System(string name, Material mat, int max, bool stretch, bool flat)
            {
                var go = new GameObject(name);
                go.transform.SetParent(root.transform, false);
                var ps = go.AddComponent<ParticleSystem>();
                ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                ParticleSystem.MainModule main = ps.main;
                main.loop = false;
                main.playOnAwake = false;
                main.maxParticles = max;
                main.simulationSpace = ParticleSystemSimulationSpace.World;
                main.startSpeed = 0f;
                ParticleSystem.EmissionModule emission = ps.emission;
                emission.enabled = false; // Emit()로만 낸다
                ParticleSystem.ShapeModule shape = ps.shape;
                shape.enabled = false;
                var renderer = go.GetComponent<ParticleSystemRenderer>();
                renderer.sharedMaterial = mat;
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                if (stretch)
                {
                    renderer.renderMode = ParticleSystemRenderMode.Stretch;
                    renderer.lengthScale = 2.2f;
                    renderer.velocityScale = 0.04f;
                }

                if (flat) renderer.alignment = ParticleSystemRenderSpace.World; // 땅에 누운 고리 (XY 평면)
                return ps;
            }

            ParticleSystem sparks = System("Sparks", FxGlowMaterial("Spark", new Color(2.6f, 2.2f, 1.6f)), 400, stretch: true, flat: false);
            ParticleSystem.MainModule sm = sparks.main;
            sm.gravityModifier = 0f;
            ParticleSystem.SizeOverLifetimeModule ss = sparks.sizeOverLifetime;
            ss.enabled = true;
            ss.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0.2f));
            ParticleSystem.LimitVelocityOverLifetimeModule drag = sparks.limitVelocityOverLifetime;
            drag.enabled = true;
            drag.drag = 6f; // 확 튀었다가 멈춘다

            ParticleSystem puffs = System("Puffs", FxSmokeMaterial(), 200, stretch: false, flat: false);
            ParticleSystem.SizeOverLifetimeModule ps2 = puffs.sizeOverLifetime;
            ps2.enabled = true;
            ps2.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.7f, 1f, 1.4f));
            ParticleSystem.ColorOverLifetimeModule pc = puffs.colorOverLifetime;
            pc.enabled = true;
            var fade = new Gradient();
            fade.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0.75f, 0f), new GradientAlphaKey(0f, 1f) });
            pc.color = fade;

            ParticleSystem rings = System("Rings", FxGlowMaterial("Ring", new Color(2f, 1.8f, 1.5f), _kit != null ? _kit.Ring : null), 40, stretch: false, flat: true);
            ParticleSystem.SizeOverLifetimeModule rs = rings.sizeOverLifetime;
            rs.enabled = true;
            rs.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 0.25f, 1f, 1f));
            ParticleSystem.ColorOverLifetimeModule rc = rings.colorOverLifetime;
            rc.enabled = true;
            rc.color = fade;

            var bursts = root.AddComponent<Combat.FxBursts>();
            using (var props = new Props(bursts)) props.Ref("sparks", sparks).Ref("puffs", puffs).Ref("rings", rings);
        }

        /// <summary>화로 불: 위(-Z)로 올라가며 노랑 → 주황 → 사라지는 입자</summary>
        private static ParticleSystem Fire(Transform parent, Vector3 worldPosition, float scale)
        {
            var go = new GameObject("Fire");
            go.transform.SetParent(parent, true);
            go.transform.position = worldPosition;
            go.transform.rotation = Quaternion.LookRotation(Vector3.back); // 입자는 +Z로 나간다 → 게임의 위(-Z)
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            ParticleSystem.MainModule main = ps.main;
            main.loop = true;
            main.playOnAwake = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.45f, 0.75f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.5f * scale, 1f * scale);
            main.startSize = new ParticleSystem.MinMaxCurve(0.25f * scale, 0.45f * scale);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.85f, 0.4f), new Color(1f, 0.55f, 0.2f));
            main.maxParticles = 24;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;

            ParticleSystem.EmissionModule emission = ps.emission;
            emission.rateOverTime = 18f;

            ParticleSystem.ShapeModule shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 10f;
            shape.radius = 0.13f * scale;

            ParticleSystem.ColorOverLifetimeModule col = ps.colorOverLifetime;
            col.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(new Color(1f, 0.95f, 0.6f), 0f), new GradientColorKey(new Color(1f, 0.5f, 0.15f), 0.5f), new GradientColorKey(new Color(0.7f, 0.15f, 0.05f), 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(0.9f, 0.15f), new GradientAlphaKey(0.6f, 0.6f), new GradientAlphaKey(0f, 1f) });
            col.color = gradient;

            ParticleSystem.SizeOverLifetimeModule size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0.35f));

            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = FxGlowMaterial("Fire", new Color(2.4f, 1.3f, 0.55f));
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return ps;
        }

        // ---------------------------------------------------------------- 방: 아치 문 · 벽 장식

        /// <summary>위 벽 가운데를 비우고 돌 아치 문을 세운다 (문 판정은 그대로). 아치가 없으면 false</summary>
        private static bool DressDoor(Transform dungeonRoot, GameObject doorObject, Door door)
        {
            GameObject gate = Art3DStage.Prefab("Door_Gate_Parts");
            if (gate == null) return false;
            var instance = (GameObject)Object.Instantiate(gate, dungeonRoot, false);
            instance.name = "DoorGate3D";
            instance.transform.position = new Vector3(0f, ArenaHalfHeight + 0.25f, 0f); // 앞면이 위 벽 안쪽 면(y = 7)에 맞는다
            Transform right = FindDeep(instance.transform, "Door_Leaf_XPos");
            Transform left = FindDeep(instance.transform, "Door_Leaf_XNeg");

            // 열린 문 안의 빛 (서 있는 판, 화면 쪽을 본다)
            var glow = new GameObject("OpenGlow");
            glow.transform.SetParent(instance.transform, false);
            glow.transform.localPosition = new Vector3(0f, 0.35f, -0.95f);
            glow.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);
            glow.transform.localScale = new Vector3(1.5f, 2.1f, 1f);
            glow.AddComponent<MeshFilter>().sharedMesh = Builtin(Stage3D.Shape.Floor);
            var glowRenderer = glow.AddComponent<MeshRenderer>();
            glowRenderer.sharedMaterial = FxGlowMaterial("DoorOpen", new Color(0.7f, 2.2f, 1.1f));
            glowRenderer.shadowCastingMode = ShadowCastingMode.Off;
            glow.SetActive(false);

            doorObject.GetComponent<SpriteRenderer>().enabled = false;
            using (var props = new Props(door)) props.Ref("leafRight", right).Ref("leafLeft", left).Ref("openGlow", glow);
            return true;
        }

        private static Transform FindDeep(Transform root, string name)
        {
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
            {
                if (t.name == name) return t;
            }

            return null;
        }

        /// <summary>장식 하나: 이름 · 자리 (z가 음수면 그만큼 위, 벽 위 = -WallHeight) · 위에서 본 돌림 · 크기</summary>
        private struct DecoItem
        {
            public string Prefab;
            public Vector3 Position;
            public float Turn;
            public float Scale;
            public bool Fire;

            public DecoItem(string prefab, float x, float y, float z = 0f, float turn = 0f, float scale = 1f, bool fire = false)
            {
                Prefab = prefab;
                Position = new Vector3(x, y, z);
                Turn = turn;
                Scale = scale;
                Fire = fire;
            }
        }

        /// <summary>
        /// 구간별 장식 (Content 3장 구간 이름에 맞춰): 위 벽 위(z = -1.4) · 벽 밖(z = 0) · 옆 벽 위.
        /// 방 안(놀이 공간)에는 놓지 않는다 → 판정·예고를 가리지 않는다.
        /// </summary>
        private static DecoItem[] DecoFor(string key)
        {
            const float top = ArenaHalfHeight + 0.5f;  // 위 벽 가운데 (두께 1)
            const float side = ArenaHalfWidth + 0.5f;  // 옆 벽 가운데
            const float wall = -WallHeight;            // 벽 위
            switch (key)
            {
                case "ch1": // 균열의 초원: 나무·덤불·풀, 붉은 깃발
                    return new[]
                    {
                        new DecoItem("Deco_Banner", -2.6f, top, wall), new DecoItem("Deco_Banner", 2.6f, top, wall),
                        new DecoItem("Deco_Tree", -3.4f, top + 2.2f, 0f, 20f), new DecoItem("Deco_Tree", 3.8f, top + 2.6f, 0f, -40f, 1.15f),
                        new DecoItem("Deco_Tree", -6.6f, 5.5f, 0f, 70f, 0.9f), new DecoItem("Deco_Tree", 6.8f, 7.5f, 0f, 10f),
                        new DecoItem("Deco_Bush", -1.6f, top + 1.2f), new DecoItem("Deco_Bush", 1.9f, top + 1.3f, 0f, 120f),
                        new DecoItem("Deco_Bush", -5.9f, 1.8f, 0f, 40f), new DecoItem("Deco_Bush", 6f, 2.6f, 0f, 200f),
                        new DecoItem("Deco_Rubble", 0.9f, top + 1.6f, 0f, 30f), new DecoItem("Deco_Flowers", -2.4f, top + 1.0f, 0f, 0f, 2f),
                    };
                case "ch2": // 잊힌 성채: 깃발·화로·석상·통
                    return new[]
                    {
                        new DecoItem("Deco_Banner", -2.6f, top, wall), new DecoItem("Deco_Banner", 2.6f, top, wall),
                        new DecoItem("Deco_Brazier", -side, top, wall, fire: true), new DecoItem("Deco_Brazier", side, top, wall, fire: true),
                        new DecoItem("Deco_Brazier", -side, 1.5f, wall, fire: true), new DecoItem("Deco_Brazier", side, 1.5f, wall, fire: true),
                        new DecoItem("Deco_Statue", -2.9f, top + 1.6f), new DecoItem("Deco_Statue", 2.9f, top + 1.6f, 0f, 0f),
                        new DecoItem("Deco_Barrel", 6.1f, 5.2f, 0f, 30f), new DecoItem("Deco_Barrel", -6.2f, 4.4f, 0f, -20f),
                        new DecoItem("Deco_Rubble", -1.2f, top + 1.4f, 0f, 160f),
                    };
                case "ch3": // 녹슨 공방: 톱니바퀴·통·화로
                    return new[]
                    {
                        new DecoItem("Deco_Gear", -2.7f, top + 1.3f, 0f, 0f, 1.2f), new DecoItem("Deco_Gear", 3f, top + 1.4f, 0f, 15f),
                        new DecoItem("Deco_Gear", -6.3f, 5f, 0f, 90f, 0.9f),
                        new DecoItem("Deco_Barrel", 1.4f, top + 1.2f), new DecoItem("Deco_Barrel", -1.2f, top + 1.3f, 0f, 60f),
                        new DecoItem("Deco_Barrel", 6.2f, 6.3f, 0f, 10f),
                        new DecoItem("Deco_Brazier", -side, top, wall, fire: true), new DecoItem("Deco_Brazier", side, top, wall, fire: true),
                    };
                case "ch4": // 그림자 협곡: 보라 수정·돌무더기
                    return new[]
                    {
                        new DecoItem("Deco_Crystal", -2.7f, top + 1.4f, 0f, 0f, 1.2f), new DecoItem("Deco_Crystal", 3.1f, top + 1.8f, 0f, 70f, 1.4f),
                        new DecoItem("Deco_Crystal", -6.4f, 6f, 0f, 30f), new DecoItem("Deco_Crystal", 6.4f, 4.4f, 0f, -50f, 1.1f),
                        new DecoItem("Deco_Rubble", 1.3f, top + 1.2f, 0f, 20f), new DecoItem("Deco_Rubble", -6f, 2.2f, 0f, 90f),
                        new DecoItem("Deco_Crystal", side, top, wall, 0f, 0.55f), new DecoItem("Deco_Crystal", -side, 2f, wall, 0f, 0.5f),
                    };
                default: // ch5 망각의 왕좌: 석상·수정·화로·깃발
                    return new[]
                    {
                        new DecoItem("Deco_Statue", -2.9f, top + 1.6f), new DecoItem("Deco_Statue", 2.9f, top + 1.6f),
                        new DecoItem("Deco_Banner", -1.6f, top, wall), new DecoItem("Deco_Banner", 1.6f, top, wall),
                        new DecoItem("Deco_Brazier", -side, top, wall, fire: true), new DecoItem("Deco_Brazier", side, top, wall, fire: true),
                        new DecoItem("Deco_Brazier", -side, -2f, wall, fire: true), new DecoItem("Deco_Brazier", side, -2f, wall, fire: true),
                        new DecoItem("Deco_Crystal", -6.4f, 6f, 0f, 0f, 1.3f), new DecoItem("Deco_Crystal", 6.4f, 6.4f, 0f, 120f, 1.3f),
                    };
            }
        }

        /// <summary>구간 장식 묶음 5개 (StageLook이 그 구간 것만 켠다)</summary>
        private static GameObject[] BuildDecoGroups(Transform arena)
        {
            if (Art3DStage.Prefab("Deco_Banner") == null) return new GameObject[0];
            var root = new GameObject("Deco").transform;
            root.SetParent(arena, false);
            var groups = new GameObject[5];
            for (int c = 1; c <= 5; c++)
            {
                string key = "ch" + c;
                var group = new GameObject(key);
                group.transform.SetParent(root, false);
                foreach (DecoItem item in DecoFor(key))
                {
                    GameObject prefab = Art3DStage.Prefab(item.Prefab);
                    if (prefab == null) continue;
                    var go = (GameObject)Object.Instantiate(prefab, group.transform, false);
                    go.name = item.Prefab;
                    go.transform.position = item.Position;
                    go.transform.rotation = Quaternion.Euler(0f, 0f, item.Turn);
                    go.transform.localScale = Vector3.one * item.Scale;
                    if (item.Fire) Fire(go.transform, item.Position + new Vector3(0f, 0f, -0.99f * item.Scale), item.Scale);
                }

                group.SetActive(c == 1);
                groups[c - 1] = group;
            }

            return groups;
        }

        /// <summary>구간별 바닥 장식 묶음 (StageLook.FloorSet)</summary>
        private static void FloorSets(SerializedProperty list)
        {
            var sets = new (string key, string[] flat, string[] edge)[]
            {
                ("ch1", new[] { "Puddle_Mesh" }, new[] { "Weeds", "Weeds", "Deco_Rubble" }),
                ("ch2", new[] { "Floor_Rug" }, new[] { "Deco_Rubble" }),
                ("ch3", new[] { "Floor_Grate", "Puddle_Mesh" }, new[] { "Deco_Rubble" }),
                ("ch4", new[] { "Puddle_Mesh" }, new[] { "Bones", "Deco_Rubble" }),
                ("ch5", new[] { "Floor_Rug" }, new[] { "Bones" }),
            };
            list.arraySize = sets.Length;
            for (int i = 0; i < sets.Length; i++)
            {
                SerializedProperty e = list.GetArrayElementAtIndex(i);
                e.FindPropertyRelative("key").stringValue = sets[i].key;
                SetPrefabs(e.FindPropertyRelative("flat"), sets[i].flat);
                SetPrefabs(e.FindPropertyRelative("edge"), sets[i].edge);
            }
        }

        private static void SetPrefabs(SerializedProperty array, string[] names)
        {
            array.arraySize = names.Length;
            for (int i = 0; i < names.Length; i++) array.GetArrayElementAtIndex(i).objectReferenceValue = Art3DStage.Prefab(names[i]);
        }

        /// <summary>구간 장식·바닥 장식을 StageLook에 잇는다</summary>
        private static void WireStageDecor(GameObject[] groups)
        {
            if (_look == null) return;
            using (var props = new Props(_look))
            {
                props.Refs("decoGroups", groups).Vector2("roomHalf", new Vector2(ArenaHalfWidth, ArenaHalfHeight));
                FloorSets(props.Property("floorSets"));
            }
        }

        // ---------------------------------------------------------------- 서바이벌 풀밭

        /// <summary>풀밭에 풀포기·꽃·덤불을 흩고, 울타리 밖에 나무를 두른다 (보기만, 판정 없음)</summary>
        private static void DressSurvivalField(Transform root)
        {
            GameObject tuft = Art3DStage.Prefab("Deco_GrassTuft"), flowers = Art3DStage.Prefab("Deco_Flowers");
            GameObject bush = Art3DStage.Prefab("Deco_Bush"), tree = Art3DStage.Prefab("Deco_Tree");
            if (tuft == null || tree == null) return;
            var field = new GameObject("Field").transform;
            field.SetParent(root, false);
            var random = new System.Random(11);
            float R(float a, float b) => a + (float)random.NextDouble() * (b - a);

            void Put(GameObject prefab, float x, float y, float scale)
            {
                var go = (GameObject)Object.Instantiate(prefab, field, false);
                go.transform.position = new Vector3(x, y, 0f);
                go.transform.rotation = Quaternion.Euler(0f, 0f, R(0f, 360f));
                go.transform.localScale = Vector3.one * scale;
                GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic); // 많아서 한 덩어리로 묶어 그린다
                foreach (Transform t in go.GetComponentsInChildren<Transform>()) GameObjectUtility.SetStaticEditorFlags(t.gameObject, StaticEditorFlags.BatchingStatic);
            }

            float inner = SurvivalHalf - 1.5f;
            // 풀포기는 무더기로 (한 무더기 4~7개)
            for (int i = 0; i < 60; i++)
            {
                float cx = R(-inner, inner), cy = R(-inner, inner);
                int n = 4 + random.Next(4);
                for (int j = 0; j < n; j++) Put(tuft, cx + R(-0.6f, 0.6f), cy + R(-0.6f, 0.6f), R(1.4f, 2.4f));
            }

            for (int i = 0; i < 45; i++) Put(flowers, R(-inner, inner), R(-inner, inner), R(1.6f, 2.4f));
            for (int i = 0; i < 26; i++) Put(bush, R(-inner, inner), R(-inner, inner), R(0.9f, 1.5f));

            // 울타리 밖 나무 띠 (가장자리에 가면 숲에 둘러싸인 들판으로 보인다)
            for (float t = -SurvivalHalf - 2f; t <= SurvivalHalf + 2f; t += 3.2f)
            {
                foreach ((float x, float y) in new[] { (t, SurvivalHalf + R(2.5f, 4.5f)), (t, -SurvivalHalf - R(2.5f, 4.5f)), (SurvivalHalf + R(2.5f, 4.5f), t), (-SurvivalHalf - R(2.5f, 4.5f), t) })
                {
                    Put(tree, x + R(-0.8f, 0.8f), y, R(0.9f, 1.4f));
                }
            }
        }
    }
}
