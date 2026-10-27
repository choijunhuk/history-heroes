using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace HistoryHeroes.EditorTools
{
    /// <summary>
    /// 3D 캐릭터 조립 (Art3D-Plan 2·5번).
    /// 무료판 옷(Ranger · Peasant)에는 머리가 없다 → 무료 기본 몸(Superhero_Male_FullBody)에서 **머리·목만** 떼어(뼈 무게로 고른 삼각형)
    /// 옷 모델의 뼈에 이름으로 붙인다 (모두 같은 65뼈 Quaternius 휴머노이드). 눈 · 눈썹 · 머리카락 · 수염도 같은 방법으로 붙인다.
    /// </summary>
    public static class Art3DCharacters
    {
        private const string Models = Art3DAssets.Models;
        private const string Meshes = Art3DAssets.Root + "/Meshes";
        public const string HeadMesh = Meshes + "/Head_Male.asset";

        /// <summary>이 뼈(와 그 아래 뼈)에 무게가 절반 넘게 실린 꼭짓점 = 머리. 세 꼭짓점이 모두 머리인 삼각형만 남긴다</summary>
        private static readonly string[] HeadBones = { "Head", "neck_01" };

        public static string ExtractHead()
        {
            var source = AssetDatabase.LoadAssetAtPath<GameObject>($"{Models}/Superhero_Male_FullBody.fbx");
            if (source == null) return "기본 몸 없음";
            SkinnedMeshRenderer body = null;
            foreach (var r in source.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                if (r.name == "SuperHero_Male") body = r;
            }

            if (body == null) return "SuperHero_Male 메시 없음";
            Mesh mesh = body.sharedMesh;

            // 머리 뼈와 그 자손
            var headSet = new HashSet<int>();
            for (int i = 0; i < body.bones.Length; i++)
            {
                for (Transform t = body.bones[i]; t != null; t = t.parent)
                {
                    if (System.Array.IndexOf(HeadBones, t.name) >= 0)
                    {
                        headSet.Add(i);
                        break;
                    }
                }
            }

            BoneWeight[] weights = mesh.boneWeights;
            var isHead = new bool[mesh.vertexCount];
            for (int v = 0; v < weights.Length; v++)
            {
                BoneWeight w = weights[v];
                float sum = (headSet.Contains(w.boneIndex0) ? w.weight0 : 0f) + (headSet.Contains(w.boneIndex1) ? w.weight1 : 0f)
                            + (headSet.Contains(w.boneIndex2) ? w.weight2 : 0f) + (headSet.Contains(w.boneIndex3) ? w.weight3 : 0f);
                isHead[v] = sum >= 0.5f;
            }

            int[] tris = mesh.triangles;
            var remap = new Dictionary<int, int>();
            var keep = new List<int>();
            for (int i = 0; i < tris.Length; i += 3)
            {
                if (!isHead[tris[i]] || !isHead[tris[i + 1]] || !isHead[tris[i + 2]]) continue;
                for (int k = 0; k < 3; k++)
                {
                    int old = tris[i + k];
                    if (!remap.TryGetValue(old, out int idx))
                    {
                        idx = remap.Count;
                        remap[old] = idx;
                    }

                    keep.Add(idx);
                }
            }

            var order = new int[remap.Count];
            foreach (KeyValuePair<int, int> pair in remap) order[pair.Value] = pair.Key;
            Vector3[] pos = mesh.vertices, nor = mesh.normals;
            Vector4[] tan = mesh.tangents;
            Vector2[] uv = mesh.uv;
            var head = new Mesh { name = "Head_Male" };
            head.vertices = Pick(pos, order);
            if (nor.Length > 0) head.normals = Pick(nor, order);
            if (tan.Length > 0) head.tangents = Pick(tan, order);
            if (uv.Length > 0) head.uv = Pick(uv, order);
            head.boneWeights = Pick(weights, order);
            head.bindposes = mesh.bindposes;
            head.triangles = keep.ToArray();
            head.RecalculateBounds();

            Directory.CreateDirectory(Meshes);
            AssetDatabase.DeleteAsset(HeadMesh);
            AssetDatabase.CreateAsset(head, HeadMesh);
            AssetDatabase.SaveAssets();
            return $"머리 메시: 꼭짓점 {head.vertexCount} / {mesh.vertexCount}, 삼각형 {keep.Count / 3}";
        }

        // ── 재질 · 동작 · 조립 ──

        private const string Materials = Art3DAssets.Root + "/Materials";
        private const string Characters = Art3DAssets.Root + "/Characters";
        private const string Controllers = Art3DAssets.Root + "/Controllers";
        private const string Textures = Art3DAssets.Textures;

        /// <summary>캐릭터 겉모습: 옷 모델 · 옷 텍스처 · 색 · 숨길 조각 · 머리카락/수염/눈썹 · 공격 동작</summary>
        public sealed class Look
        {
            public string name;
            public string body;
            public string bodyTex;
            public Color tint = Color.white;
            public bool head = true;
            public string[] hide = new string[0];
            public string[] hair = new string[0];
            public string hairTex = "T_Hair_1";
            public Color hairColor = new Color(0.1f, 0.075f, 0.06f); // 머리카락 텍스처는 회색 → 색을 곱한다
            public string attack = "Spell_Simple_Shoot";
        }

        public static readonly Look[] Heroes =
        {
            // 칭기즈 칸: 두건 쓴 궁수 + 수염 (활 동작이 생기기 전에는 주문 쏘기)
            new Look { name = "Khan", body = "Male_Ranger", bodyTex = "T_Ranger_3_BaseColor", hair = new[] { "Hair_Beard", "Eyebrows" } },
            // 관우: 긴 머리 + 긴 수염, 칼 휘두르기
            new Look { name = "Guan", body = "Male_Peasant", bodyTex = "T_Peasant_2_BaseColor", hair = new[] { "Hair_Long", "Hair_Beard", "Eyebrows" }, hairColor = new Color(0.04f, 0.035f, 0.035f), attack = "Sword_Regular_A" },
            // 나폴레옹: 두건 없는 푸른 옷, 권총 쏘기
            new Look { name = "Napoleon", body = "Male_Ranger", bodyTex = "T_Ranger_BaseColor", tint = new Color(0.55f, 0.66f, 1f), hide = new[] { "Male_Ranger_Head_Hood" }, hair = new[] { "Hair_SimpleParted", "Eyebrows" }, attack = "Pistol_Shoot" },
            // 테슬라: 가르마 머리, 주문 쏘기 (전기)
            new Look { name = "Tesla", body = "Male_Peasant", bodyTex = "T_Peasant_BaseColor", hair = new[] { "Hair_SimpleParted", "Eyebrows" }, hairColor = new Color(0.07f, 0.06f, 0.055f) },
        };

        public static Material Lit(string name, string baseTex, string normalTex, Color tint, bool cutout = false, string emissive = null)
        {
            Directory.CreateDirectory(Materials);
            string path = $"{Materials}/M_{name}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(mat, path);
            }

            mat.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>($"{Textures}/{baseTex}.png"));
            mat.SetColor("_BaseColor", tint);
            Texture2D normal = normalTex != null ? AssetDatabase.LoadAssetAtPath<Texture2D>($"{Textures}/{normalTex}.png") : null;
            mat.SetTexture("_BumpMap", normal);
            if (normal != null) mat.EnableKeyword("_NORMALMAP");
            else mat.DisableKeyword("_NORMALMAP");
            mat.SetFloat("_Smoothness", 0.2f);
            if (cutout)
            {
                mat.SetFloat("_AlphaClip", 1f);
                mat.SetFloat("_Cutoff", 0.45f);
                mat.SetFloat("_Cull", 0f); // 머리카락 카드는 양면
                mat.EnableKeyword("_ALPHATEST_ON");
            }

            if (emissive != null)
            {
                mat.SetTexture("_EmissionMap", AssetDatabase.LoadAssetAtPath<Texture2D>($"{Textures}/{emissive}.png"));
                mat.SetColor("_EmissionColor", Color.white * 1.5f);
                mat.EnableKeyword("_EMISSION");
                mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            }

            EditorUtility.SetDirty(mat);
            return mat;
        }

        /// <summary>
        /// 동작 컨트롤러 하나를 모두 같이 쓴다: Speed(0~1) 서기→걷기→뛰기, Attack 트리거, Hit 트리거, Dead.
        /// 공격 동작만 캐릭터마다 AnimatorOverrideController로 바꾼다 (GameDesign 5장).
        /// </summary>
        public static UnityEditor.Animations.AnimatorController BaseController()
        {
            Directory.CreateDirectory(Controllers);
            string path = $"{Controllers}/Humanoid.controller";
            AssetDatabase.DeleteAsset(path);
            var c = UnityEditor.Animations.AnimatorController.CreateAnimatorControllerAtPath(path);
            c.AddParameter("Speed", AnimatorControllerParameterType.Float);
            c.AddParameter("Attack", AnimatorControllerParameterType.Trigger);
            c.AddParameter("Hit", AnimatorControllerParameterType.Trigger);
            c.AddParameter("Dead", AnimatorControllerParameterType.Bool);
            var sm = c.layers[0].stateMachine;
            var move = c.CreateBlendTreeInController("Move", out UnityEditor.Animations.BlendTree tree, 0);
            tree.blendParameter = "Speed";
            tree.AddChild(Clip("Idle_Loop"), 0f);
            tree.AddChild(Clip("Walk_Loop"), 0.35f);
            tree.AddChild(Clip("Jog_Fwd_Loop"), 1f);
            sm.defaultState = move;

            var attack = sm.AddState("Attack");
            attack.motion = Clip("Spell_Simple_Shoot");
            attack.speed = 1.4f;
            var toAttack = sm.AddAnyStateTransition(attack);
            toAttack.AddCondition(UnityEditor.Animations.AnimatorConditionMode.If, 0f, "Attack");
            toAttack.AddCondition(UnityEditor.Animations.AnimatorConditionMode.IfNot, 0f, "Dead");
            toAttack.duration = 0.06f;
            toAttack.canTransitionToSelf = true;
            var back = attack.AddTransition(move);
            back.hasExitTime = true;
            back.exitTime = 0.8f;
            back.duration = 0.12f;

            var hit = sm.AddState("Hit");
            hit.motion = Clip("Hit_Chest");
            var toHit = sm.AddAnyStateTransition(hit);
            toHit.AddCondition(UnityEditor.Animations.AnimatorConditionMode.If, 0f, "Hit");
            toHit.AddCondition(UnityEditor.Animations.AnimatorConditionMode.IfNot, 0f, "Dead");
            toHit.duration = 0.05f;
            var hitBack = hit.AddTransition(move);
            hitBack.hasExitTime = true;
            hitBack.exitTime = 0.85f;
            hitBack.duration = 0.1f;

            var death = sm.AddState("Death");
            death.motion = Clip("Death01");
            var toDeath = sm.AddAnyStateTransition(death);
            toDeath.AddCondition(UnityEditor.Animations.AnimatorConditionMode.If, 0f, "Dead");
            toDeath.duration = 0.08f;
            toDeath.canTransitionToSelf = false;
            var revive = death.AddTransition(move);
            revive.AddCondition(UnityEditor.Animations.AnimatorConditionMode.IfNot, 0f, "Dead");
            revive.duration = 0.2f;
            return c;
        }

        public static AnimationClip Clip(string name) => AssetDatabase.LoadAssetAtPath<AnimationClip>($"{Art3DAssets.Animations}/{name}.anim");

        public static AnimatorOverrideController Override(string name, RuntimeAnimatorController baseController, string attackClip)
        {
            string path = $"{Controllers}/{name}.overrideController";
            var o = AssetDatabase.LoadAssetAtPath<AnimatorOverrideController>(path);
            if (o == null)
            {
                o = new AnimatorOverrideController(baseController);
                AssetDatabase.CreateAsset(o, path);
            }

            o.runtimeAnimatorController = baseController;
            o["Spell_Simple_Shoot"] = Clip(attackClip);
            EditorUtility.SetDirty(o);
            return o;
        }

        /// <summary>옷 모델 + 머리 + 눈 + 머리카락 → 캐릭터 프리팹 (Animator: 아바타 · 컨트롤러, 뿌리 동작 끔)</summary>
        public static string Compose(Look look, RuntimeAnimatorController controller)
        {
            var fbx = AssetDatabase.LoadAssetAtPath<GameObject>($"{Models}/{look.body}.fbx");
            if (fbx == null) return "없음 " + look.body;
            var go = (GameObject)PrefabUtility.InstantiatePrefab(fbx);
            PrefabUtility.UnpackPrefabInstance(go, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            go.name = look.name + "3D";

            Material outfit = Lit(look.name + "_Outfit", look.bodyTex, look.body.Contains("Ranger") ? "T_Ranger_Normal" : "T_Peasant_Normal", look.tint);
            Material arms = Lit("Skin_Regular", "T_Regular_Male_Dark_BaseColor", "T_Regular_Male_Normal", Color.white);
            Material headSkin = Lit("Skin_Head", "T_Superhero_Male_Dark", "T_Superhero_Male_Normal", Color.white);
            Material eyes = Lit("Eyes", "T_Eye_Brown", null, Color.white);
            Material hair = Lit(look.name + "_Hair", look.hairTex + "_BaseColor", look.hairTex + "_Normal", look.hairColor, cutout: true);

            foreach (var r in go.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                if (System.Array.IndexOf(look.hide, r.name) >= 0)
                {
                    Object.DestroyImmediate(r.gameObject);
                    continue;
                }

                Material[] slots = r.sharedMaterials;
                for (int i = 0; i < slots.Length; i++) slots[i] = slots[i] != null && slots[i].name.Contains("Regular_Male") ? arms : outfit;
                r.sharedMaterials = slots;
            }

            if (look.head)
            {
                Attach(go, "Superhero_Male_FullBody", "SuperHero_Male", AssetDatabase.LoadAssetAtPath<Mesh>(HeadMesh), headSkin);
                Attach(go, "Superhero_Male_FullBody", "Eyes", null, eyes);
            }

            foreach (string h in look.hair)
            {
                if (h == "Eyebrows") Attach(go, "Superhero_Male_FullBody", "Eyebrows", null, hair);
                else Attach(go, h, h, null, hair);
            }

            var animator = go.GetComponent<Animator>() ?? go.AddComponent<Animator>();
            animator.avatar = AssetDatabase.LoadAssetAtPath<Avatar>($"{Models}/{look.body}.fbx");
            animator.runtimeAnimatorController = controller;
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.CullUpdateTransforms;

            Directory.CreateDirectory(Characters);
            string path = $"{Characters}/{go.name}.prefab";
            PrefabUtility.SaveAsPrefabAsset(go, path);
            Object.DestroyImmediate(go);
            return path;
        }

        /// <summary>영웅 4명 조립 (머리 메시 · 재질 · 동작 컨트롤러 포함)</summary>
        public static string BuildHeroes()
        {
            var log = new List<string> { ExtractHead() };
            var baseController = BaseController();
            foreach (Look look in Heroes) log.Add(Compose(look, Override(look.name, baseController, look.attack)));
            AssetDatabase.SaveAssets();
            return string.Join("\n", log);
        }

        /// <summary>에디터에서 바로 사진 (Play 없이): 앞모습, 동작 한 순간. 3D 렌더러로 찍는다</summary>
        public static string Snapshot(string prefabPath, string outPath, string clip = "Idle_Loop", float time = 0.3f, float yaw = 0f)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefab == null) return "없음 " + prefabPath;
            var pru = new PreviewRenderUtility();
            bool asyncShaders = ShaderUtil.allowAsyncCompilation;
            ShaderUtil.allowAsyncCompilation = false; // 셰이더를 다 만든 뒤 그린다 (안 그러면 첫 장이 빈 화면)
            try
            {
                pru.camera.fieldOfView = 26f;
                pru.camera.nearClipPlane = 0.05f;
                pru.camera.farClipPlane = 50f;
                pru.camera.clearFlags = CameraClearFlags.SolidColor;
                pru.camera.backgroundColor = new Color(0.32f, 0.36f, 0.42f);
                UnityEngine.Rendering.Universal.CameraExtensions.GetUniversalAdditionalCameraData(pru.camera).SetRenderer(1);
                GameObject go = pru.InstantiatePrefabInScene(prefab);
                go.transform.rotation = Quaternion.Euler(0f, yaw, 0f); // 모델은 -Z를 본다 → 카메라(-Z 쪽)를 본다
                AnimationClip c = Clip(clip);
                if (c != null)
                {
                    AnimationMode.StartAnimationMode();
                    AnimationMode.BeginSampling();
                    AnimationMode.SampleAnimationClip(go, c, time);
                    AnimationMode.EndSampling();
                }

                var bounds = new Bounds(go.transform.position, Vector3.zero);
                foreach (Renderer r in go.GetComponentsInChildren<Renderer>()) bounds.Encapsulate(r.bounds);
                float h = Mathf.Max(0.5f, bounds.size.y);
                pru.camera.transform.position = bounds.center + new Vector3(0f, h * 0.05f, -h * 2.4f);
                pru.camera.transform.LookAt(bounds.center);
                pru.lights[0].intensity = 1.3f;
                pru.lights[0].transform.rotation = Quaternion.Euler(35f, 30f, 0f);
                pru.lights[1].intensity = 0.6f;
                pru.ambientColor = new Color(0.45f, 0.45f, 0.5f);
                pru.BeginStaticPreview(new Rect(0, 0, 420, 640));
                pru.Render(true);
                Texture2D tex = pru.EndStaticPreview();
                Directory.CreateDirectory(Path.GetDirectoryName(outPath));
                File.WriteAllBytes(outPath, tex.EncodeToPNG());
                return "저장 " + outPath;
            }
            finally
            {
                if (AnimationMode.InAnimationMode()) AnimationMode.StopAnimationMode();
                ShaderUtil.allowAsyncCompilation = asyncShaders;
                pru.Cleanup();
            }
        }

        private static T[] Pick<T>(T[] source, int[] order)
        {
            var result = new T[order.Length];
            for (int i = 0; i < order.Length; i++) result[i] = source[order[i]];
            return result;
        }

        /// <summary>
        /// 붙일 조각: 다른 FBX의 스킨 메시(같은 65뼈)를 대상 모델의 뼈에 이름으로 묶는다.
        /// mesh가 null이면 source FBX의 renderer 이름으로 찾는다.
        /// </summary>
        public static SkinnedMeshRenderer Attach(GameObject target, string sourceFbx, string rendererName, Mesh mesh, Material material)
        {
            var source = AssetDatabase.LoadAssetAtPath<GameObject>($"{Models}/{sourceFbx}.fbx");
            if (source == null) return null;
            SkinnedMeshRenderer from = null;
            foreach (var r in source.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                if (r.name == rendererName) from = r;
            }

            if (from == null) return null;
            var byName = new Dictionary<string, Transform>();
            foreach (Transform t in target.GetComponentsInChildren<Transform>(true)) byName[t.name] = t;
            var bones = new Transform[from.bones.Length];
            for (int i = 0; i < bones.Length; i++)
            {
                byName.TryGetValue(from.bones[i].name, out bones[i]);
            }

            var part = new GameObject(rendererName);
            part.transform.SetParent(target.transform, false);
            var smr = part.AddComponent<SkinnedMeshRenderer>();
            smr.sharedMesh = mesh != null ? mesh : from.sharedMesh;
            smr.bones = bones;
            smr.rootBone = from.rootBone != null && byName.TryGetValue(from.rootBone.name, out Transform root) ? root : null;
            smr.sharedMaterial = material;
            smr.updateWhenOffscreen = false;
            return smr;
        }
    }
}
