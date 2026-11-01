using System.Collections.Generic;
using System.IO;
using System.Text;
using HistoryHeroes.Dev;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace HistoryHeroes.EditorTools
{
    /// <summary>
    /// 3D 그래픽 샘플 (개발자 2026-10-27: "2d 말고 3d는 안되려나", "너무 아기자기", "앞 뒤가 같은"):
    /// Quaternius CC0 — 실제 비율 캐릭터(Modular Character Outfits - Fantasy) + 휴머노이드 동작(Universal Animation Library 1·2) + 괴물(Bestiary).
    /// 게임 규칙(2D 물리, XY 평면)은 그대로 두고 보이는 것만 3D로 (Dev/ArtPreview3D).
    /// 에셋은 Assets/ThirdParty/Quaternius (방향이 정해지기 전에는 git에 올리지 않는다).
    /// 순서: Import() → Setup() → (Play, 전투) ArtSamples.Stage() → Begin() → Shot(경로)
    /// </summary>
    public static class Art3DSample
    {
        public const string Root = "Assets/ThirdParty/Quaternius";
        private const string Out = Root + "/Sample";
        private const string Ual1 = Root + "/Animations/UAL1_Standard.fbx";
        private const string Ual2 = Root + "/Animations/UAL2_Standard.fbx";
        private const string RendererPath = "Assets/Settings/UniversalRenderer3D.asset";

        private static readonly string[] Models = { "Characters/Male_Ranger", "Characters/Male_Peasant", "Monsters/Imp", "Monsters/Puglin" };
        private static readonly string[] AnimationFiles = { "Animations/UAL1_Standard", "Animations/UAL2_Standard" };

        /// <summary>가져오기 설정: 모두 휴머노이드 (동작을 다른 몸에 옮겨 쓰려고), 동작 파일의 *_Loop는 반복</summary>
        public static string Import()
        {
            var log = new List<string>();
            foreach (string m in Models) log.Add(Humanoid($"{Root}/{m}.fbx", false));
            foreach (string a in AnimationFiles) log.Add(Humanoid($"{Root}/{a}.fbx", true));
            return string.Join("\n", log);
        }

        private static string Humanoid(string path, bool animations)
        {
            var importer = AssetImporter.GetAtPath(path) as ModelImporter;
            if (importer == null) return "없음 " + path;
            importer.animationType = ModelImporterAnimationType.Human;
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            importer.importAnimation = animations;
            importer.materialImportMode = animations ? ModelImporterMaterialImportMode.None : ModelImporterMaterialImportMode.ImportViaMaterialDescription; // 재질 칸 이름을 보려고 (프리팹에서 URP Lit으로 바꾼다)
            if (animations)
            {
                ModelImporterClipAnimation[] clips = importer.defaultClipAnimations;
                foreach (ModelImporterClipAnimation c in clips)
                {
                    c.loopTime = c.name.Contains("Loop");
                    c.lockRootRotation = true;
                    c.lockRootHeightY = true;
                    c.lockRootPositionXZ = true;
                    c.keepOriginalOrientation = true;
                    c.keepOriginalPositionY = true;
                    c.keepOriginalPositionXZ = true;
                }

                importer.clipAnimations = clips;
            }

            importer.SaveAndReimport();
            var avatar = AssetDatabase.LoadAssetAtPath<Avatar>(path);
            return $"{path}: 휴머노이드 {(avatar != null && avatar.isHuman && avatar.isValid ? "OK" : "실패")}";
        }

        /// <summary>모델마다 렌더러 · 재질 칸 이름 · 크기 (재질을 붙이려고)</summary>
        public static string Describe()
        {
            var sb = new StringBuilder();
            foreach (string m in Models)
            {
                var go = AssetDatabase.LoadAssetAtPath<GameObject>($"{Root}/{m}.fbx");
                if (go == null)
                {
                    sb.AppendLine("없음 " + m);
                    continue;
                }

                Bounds b = new Bounds();
                bool first = true;
                sb.Append(m).Append(": ");
                foreach (var r in go.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                {
                    if (first) b = r.bounds;
                    else b.Encapsulate(r.bounds);
                    first = false;
                    sb.Append(r.name).Append("[");
                    foreach (Material mat in r.sharedMaterials) sb.Append(mat != null ? mat.name : "null").Append(",");
                    sb.Append("] ");
                }

                sb.AppendLine($" 크기 {b.size.x:0.00}×{b.size.y:0.00}×{b.size.z:0.00}");
            }

            return sb.ToString();
        }

        /// <summary>재질 (URP Lit) · 동작 컨트롤러 · 프리팹 4개 · 3D 렌더러(UniversalRenderer)를 만든다</summary>
        public static string Setup()
        {
            Directory.CreateDirectory(Out);
            var log = new List<string>();
            NormalMaps(log);

            var mats = new Dictionary<string, Material>
            {
                ["MI_Ranger"] = Lit("Ranger", "Characters/Textures/T_Ranger_BaseColor", "Characters/Textures/T_Ranger_Normal"),
                ["MI_Regular_Male"] = Lit("Skin", "Characters/Textures/T_Regular_Male_Dark_BaseColor", "Characters/Textures/T_Regular_Male_Normal"),
                ["MI_Peasant"] = Lit("Peasant", "Characters/Textures/T_Peasant_BaseColor", "Characters/Textures/T_Peasant_Normal"),
                ["MI_Imp"] = Lit("Imp", "Monsters/Textures/T_Imp_BaseColor_1", "Monsters/Textures/T_Imp_Normal"),
                ["MI_Puglin"] = Lit("Puglin", "Monsters/Textures/T_Puglin_BaseColor_1", "Monsters/Textures/T_Puglin_Normal"),
            };

            // 동작: 영웅은 멈추면 쏜다 (활 동작이 없어서 주문 쏘기로 대신), 적은 근접 공격
            AnimatorController hero = Controller("Hero3D", "Armature|Spell_Simple_Shoot");
            AnimatorController enemy = Controller("Enemy3D", "Armature|Sword_Regular_A");
            log.Add(Prefab("Khan3D", "Characters/Male_Ranger", hero, mats));
            log.Add(Prefab("Charger3D", "Characters/Male_Peasant", enemy, mats));
            log.Add(Prefab("Archer3D", "Monsters/Puglin", enemy, mats));
            log.Add(Prefab("Imp3D", "Monsters/Imp", enemy, mats));
            log.Add(Ground());
            log.Add(Renderer());
            AssetDatabase.SaveAssets();
            return string.Join("\n", log);
        }

        private static void NormalMaps(List<string> log)
        {
            foreach (string file in Directory.GetFiles(Root, "*Normal*.png", SearchOption.AllDirectories))
            {
                var importer = AssetImporter.GetAtPath(file.Replace('\\', '/')) as TextureImporter;
                if (importer == null || importer.textureType == TextureImporterType.NormalMap) continue;
                importer.textureType = TextureImporterType.NormalMap;
                importer.maxTextureSize = 1024;
                importer.SaveAndReimport();
            }

            foreach (string file in Directory.GetFiles(Root, "*.png", SearchOption.AllDirectories))
            {
                var importer = AssetImporter.GetAtPath(file.Replace('\\', '/')) as TextureImporter;
                if (importer == null || importer.maxTextureSize <= 1024) continue;
                importer.maxTextureSize = 1024; // 폰: 1024면 충분
                importer.SaveAndReimport();
            }

            log.Add("텍스처 설정");
        }

        private static Material Lit(string name, string baseMap, string normalMap)
        {
            string path = $"{Out}/M_{name}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(mat, path);
            }

            mat.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>($"{Root}/{baseMap}.png"));
            var normal = AssetDatabase.LoadAssetAtPath<Texture2D>($"{Root}/{normalMap}.png");
            if (normal != null)
            {
                mat.SetTexture("_BumpMap", normal);
                mat.EnableKeyword("_NORMALMAP");
            }

            mat.SetFloat("_Smoothness", 0.25f);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        private static AnimationClip Clip(string name)
        {
            foreach (string file in new[] { Ual1, Ual2 })
            {
                foreach (Object o in AssetDatabase.LoadAllAssetsAtPath(file))
                {
                    if (o is AnimationClip c && c.name == name) return c;
                }
            }

            Debug.LogWarning("[Art3DSample] 동작 없음: " + name);
            return null;
        }

        /// <summary>Speed(0~1) 섞기: 서기 → 뛰기. Attack 트리거 → 공격 한 번. Dead → 쓰러짐. Hit 트리거 → 맞음</summary>
        private static AnimatorController Controller(string name, string attackClip)
        {
            string path = $"{Out}/{name}.controller";
            AssetDatabase.DeleteAsset(path);
            AnimatorController c = AnimatorController.CreateAnimatorControllerAtPath(path);
            c.AddParameter("Speed", AnimatorControllerParameterType.Float);
            c.AddParameter("Attack", AnimatorControllerParameterType.Trigger);
            c.AddParameter("Hit", AnimatorControllerParameterType.Trigger);
            c.AddParameter("Dead", AnimatorControllerParameterType.Bool);
            AnimatorStateMachine sm = c.layers[0].stateMachine;

            AnimatorState move = c.CreateBlendTreeInController("Move", out BlendTree tree, 0);
            tree.blendParameter = "Speed";
            tree.AddChild(Clip("Armature|Idle_Loop"), 0f);
            tree.AddChild(Clip("Armature|Walk_Loop"), 0.35f);
            tree.AddChild(Clip("Armature|Jog_Fwd_Loop"), 1f);
            sm.defaultState = move;

            AnimatorState attack = sm.AddState("Attack");
            attack.motion = Clip(attackClip);
            attack.speed = 1.4f;
            AnimatorStateTransition toAttack = sm.AddAnyStateTransition(attack);
            toAttack.AddCondition(AnimatorConditionMode.If, 0f, "Attack");
            toAttack.AddCondition(AnimatorConditionMode.IfNot, 0f, "Dead");
            toAttack.duration = 0.08f;
            toAttack.canTransitionToSelf = true;
            AnimatorStateTransition back = attack.AddTransition(move);
            back.hasExitTime = true;
            back.exitTime = 0.85f;
            back.duration = 0.12f;

            AnimatorState hit = sm.AddState("Hit");
            hit.motion = Clip("Armature|Hit_Chest");
            AnimatorStateTransition toHit = sm.AddAnyStateTransition(hit);
            toHit.AddCondition(AnimatorConditionMode.If, 0f, "Hit");
            toHit.AddCondition(AnimatorConditionMode.IfNot, 0f, "Dead");
            toHit.duration = 0.05f;
            AnimatorStateTransition hitBack = hit.AddTransition(move);
            hitBack.hasExitTime = true;
            hitBack.exitTime = 0.9f;
            hitBack.duration = 0.1f;

            AnimatorState death = sm.AddState("Death");
            death.motion = Clip("Armature|Death01");
            AnimatorStateTransition toDeath = sm.AddAnyStateTransition(death);
            toDeath.AddCondition(AnimatorConditionMode.If, 0f, "Dead");
            toDeath.duration = 0.1f;
            toDeath.canTransitionToSelf = false;
            return c;
        }

        private static string Prefab(string name, string model, AnimatorController controller, Dictionary<string, Material> mats)
        {
            var source = AssetDatabase.LoadAssetAtPath<GameObject>($"{Root}/{model}.fbx");
            if (source == null) return "없음 " + model;
            var go = (GameObject)PrefabUtility.InstantiatePrefab(source);
            var animator = go.GetComponent<Animator>() ?? go.AddComponent<Animator>();
            animator.runtimeAnimatorController = controller;
            animator.avatar = AssetDatabase.LoadAssetAtPath<Avatar>($"{Root}/{model}.fbx");
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            int replaced = 0;
            foreach (var r in go.GetComponentsInChildren<Renderer>(true))
            {
                Material[] slots = r.sharedMaterials;
                for (int i = 0; i < slots.Length; i++)
                {
                    if (slots[i] != null && mats.TryGetValue(slots[i].name, out Material m))
                    {
                        slots[i] = m;
                        replaced++;
                    }
                }

                r.sharedMaterials = slots;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            }

            string path = $"{Out}/{name}.prefab";
            PrefabUtility.SaveAsPrefabAsset(go, path);
            Object.DestroyImmediate(go);
            return $"{path} (재질 {replaced}칸)";
        }

        /// <summary>땅 · 벽 · 상자 재질 (그림자를 받게 URP Lit): 바닥은 초원 무늬를 이어 붙이고, 벽·높은 장애물은 돌 무늬</summary>
        private static string Ground()
        {
            Texture2D Tex(string p)
            {
                var importer = AssetImporter.GetAtPath(p) as TextureImporter;
                if (importer != null && importer.wrapMode != TextureWrapMode.Repeat)
                {
                    importer.wrapMode = TextureWrapMode.Repeat;
                    importer.SaveAndReimport();
                }

                return AssetDatabase.LoadAssetAtPath<Texture2D>(p);
            }

            MakeSurface("Ground", Tex($"{Root}/Characters/Textures/T_Ground_Grass.png"), Color.white); // 노이즈로 만든 자연스러운 풀밭 (아기자기한 풀 그림 대신)
            MakeSurface("Stone", Tex("Assets/Art/Samples/B/stone_tile.png"), Color.white);
            MakeSurface("Crate", Tex("Assets/Art/Samples/B/crate.png"), Color.white);
            return "땅·돌·상자 재질";
        }

        private static void MakeSurface(string name, Texture2D tex, Color tint)
        {
            string path = $"{Out}/M_{name}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(mat, path);
            }

            mat.SetTexture("_BaseMap", tex);
            mat.SetColor("_BaseColor", tint);
            mat.SetFloat("_Smoothness", 0.1f);
            EditorUtility.SetDirty(mat);
        }

        /// <summary>3D는 2D 렌더러에서 조명이 안 된다 → UniversalRenderer를 URP 목록 두 번째에 넣는다 (기본은 그대로 2D)</summary>
        private static string Renderer()
        {
            var data = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(RendererPath);
            if (data == null)
            {
                data = ScriptableObject.CreateInstance<UniversalRendererData>();
                AssetDatabase.CreateAsset(data, RendererPath);
            }

            var pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>("Assets/Settings/UniversalRP.asset");
            var so = new SerializedObject(pipeline);
            SerializedProperty list = so.FindProperty("m_RendererDataList");
            for (int i = 0; i < list.arraySize; i++)
            {
                if (list.GetArrayElementAtIndex(i).objectReferenceValue == data) return $"3D 렌더러 이미 {i}번";
            }

            list.InsertArrayElementAtIndex(list.arraySize);
            list.GetArrayElementAtIndex(list.arraySize - 1).objectReferenceValue = data;
            so.ApplyModifiedPropertiesWithoutUndo();
            return $"3D 렌더러 {list.arraySize - 1}번에 넣음";
        }

        // ── Play 중 ──

        public static string Begin(float tilt = 40f, float distance = 19f, float fov = 34f)
        {
            if (!Application.isPlaying) return "Play 모드가 아님";
            GameObject P(string n) => AssetDatabase.LoadAssetAtPath<GameObject>($"{Out}/{n}.prefab");
            Material M(string n) => AssetDatabase.LoadAssetAtPath<Material>($"{Out}/M_{n}.mat");
            var pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>("Assets/Settings/UniversalRP.asset");
            int rendererIndex = 1;
            var so = new SerializedObject(pipeline);
            SerializedProperty list = so.FindProperty("m_RendererDataList");
            var data = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(RendererPath);
            for (int i = 0; i < list.arraySize; i++)
            {
                if (list.GetArrayElementAtIndex(i).objectReferenceValue == data) rendererIndex = i;
            }

            return ArtPreview3D.Begin(new ArtPreview3D.Set
            {
                hero = P("Khan3D"),
                charger = P("Imp3D"), // 농부 옷은 무료판에 머리가 없다 → 도깨비
                archer = P("Archer3D"),
                other = P("Imp3D"),
                ground = M("Ground"),
                stone = M("Stone"),
                crate = M("Crate"),
                blade = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/Samples/B/spin_blade.png"),
                rendererIndex = rendererIndex,
                tilt = tilt,
                distance = distance,
                fov = fov,
            });
        }

        public static string End()
        {
            ArtPreview3D.End();
            return "3D 끔";
        }

        /// <summary>게임 화면 그대로 (UI까지) — capture game_view 대신 카메라를 직접 찍는다</summary>
        public static string Shot(string path, int width = 540, int height = 1170) => QaTools.SnapshotWorld(path, width, height);
    }
}
