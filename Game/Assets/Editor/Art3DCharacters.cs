using System.Collections.Generic;
using System.IO;
using HistoryHeroes.Combat;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace HistoryHeroes.EditorTools
{
    /// <summary>
    /// 3D 캐릭터 조립 (Art3D-Plan 2·5번). 메뉴 **History Heroes → 3D 캐릭터 다시 만들기** = <see cref="BuildAll"/>.
    /// 무료판 옷(Ranger · Peasant)에는 머리가 없다 → 무료 기본 몸(Superhero_Male_FullBody)에서 **머리·목만** 떼어(뼈 무게로 고른 삼각형)
    /// 옷 모델의 뼈에 이름으로 붙인다 (모두 같은 65뼈 Quaternius 휴머노이드). 눈 · 눈썹 · 머리카락 · 수염도 같은 방법으로 붙인다.
    /// 괴물(Imp · Puglin)은 제 몸 그대로 재질만 바꾼다.
    /// - 다시 만들어도 에셋을 지우지 않고 **그 자리에서 고친다** (GUID가 그대로라 프리팹·컨트롤러 연결이 안 끊긴다 — Codex 3D 리뷰 10번)
    /// - 붙일 때 뼈 자리가 원본과 같은지 잰다 (다르면 머리가 어긋난다 — Codex 3D 리뷰 6번)
    /// - 장면 빌더(SceneBuilder.BuildAll)는 여기서 만든 프리팹을 경로로 읽기만 한다. 겉모습을 바꾸면 이것부터 다시 돌린다.
    /// </summary>
    public static class Art3DCharacters
    {
        private const string Models = Art3DAssets.Models;
        private const string Meshes = Art3DAssets.Root + "/Meshes";
        public const string HeadMesh = Meshes + "/Head_Male.asset";
        private const string Materials = Art3DAssets.Root + "/Materials";
        public const string Characters = Art3DAssets.Root + "/Characters";
        public const string EnemyCharacters = Characters + "/Enemies";
        private const string Controllers = Art3DAssets.Root + "/Controllers";
        private const string Textures = Art3DAssets.Textures;

        /// <summary>이 뼈(와 그 아래 뼈)에 무게가 절반 넘게 실린 꼭짓점 = 머리. 세 꼭짓점이 모두 머리인 삼각형만 남긴다</summary>
        private static readonly string[] HeadBones = { "Head", "neck_01" };

        [MenuItem("History Heroes/3D 캐릭터 다시 만들기")]
        private static void BuildAllMenu() => Debug.Log(BuildAll());

        /// <summary>머리 메시 · 공용 동작 컨트롤러 · 영웅 4명 · 적 17종</summary>
        public static string BuildAll()
        {
            var log = new List<string> { ExtractHead() };
            Art3DProps.Import();
            AnimatorController baseController = BaseController();
            foreach (Look look in Heroes) log.Add(Compose(look, Override(look.name, baseController, look.attack), Characters));
            foreach (EnemyLook e in Enemies) log.Add(Compose(e.look, Override(e.look.name, baseController, e.look.attack), EnemyCharacters));
            log.Add(Art3DPets.BuildAll());
            AssetDatabase.SaveAssets();
            return string.Join("\n", log);
        }

        /// <summary>예전 이름 (영웅만)</summary>
        public static string BuildHeroes() => BuildAll();

        // ── 머리 떼기 ──

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

            Directory.CreateDirectory(Meshes);
            var head = AssetDatabase.LoadAssetAtPath<Mesh>(HeadMesh);
            bool created = head == null;
            if (created) head = new Mesh();
            head.Clear();
            head.name = "Head_Male";
            head.vertices = Pick(pos, order);
            if (nor.Length > 0) head.normals = Pick(nor, order);
            if (tan.Length > 0) head.tangents = Pick(tan, order);
            if (uv.Length > 0) head.uv = Pick(uv, order);
            head.boneWeights = Pick(weights, order);
            head.bindposes = mesh.bindposes;
            head.triangles = keep.ToArray();
            head.RecalculateBounds();
            if (created) AssetDatabase.CreateAsset(head, HeadMesh);
            else EditorUtility.SetDirty(head);
            AssetDatabase.SaveAssets();
            return $"머리 메시: 꼭짓점 {head.vertexCount} / {mesh.vertexCount}, 삼각형 {keep.Count / 3}, 열린 가장자리 {OpenEdges(keep)}";
        }

        /// <summary>메시의 열린 가장자리 수 (목 자른 곳). 옷 깃이 덮는지 앞·옆 사진으로 본다 (Codex 3D 리뷰 7번)</summary>
        private static int OpenEdges(List<int> tris)
        {
            var count = new Dictionary<long, int>();
            for (int i = 0; i < tris.Count; i += 3)
            {
                for (int k = 0; k < 3; k++)
                {
                    int a = tris[i + k], b = tris[i + (k + 1) % 3];
                    long key = a < b ? ((long)a << 32) | (uint)b : ((long)b << 32) | (uint)a;
                    count[key] = count.TryGetValue(key, out int c) ? c + 1 : 1;
                }
            }

            int open = 0;
            foreach (int c in count.Values)
            {
                if (c == 1) open++;
            }

            return open;
        }

        // ── 겉모습 표 ──

        /// <summary>캐릭터 겉모습: 몸 모델 · 텍스처 · 색 · 숨길 조각 · 머리카락/수염/눈썹 · 공격 동작 · 그림자</summary>
        public sealed class Look
        {
            public string name;
            public string body;
            public string bodyTex;
            public string normalTex;
            public string emissive;
            public Color tint = Color.white;
            public bool head = true;
            public string[] hide = new string[0];
            public string[] hair = new string[0];
            public string hairTex = "T_Hair_1";
            public Color hairColor = new Color(0.1f, 0.075f, 0.06f); // 머리카락 텍스처는 회색 → 색을 곱한다
            public string attack = "Spell_Simple_Shoot";
            public bool shadows = true;
            /// <summary>상징 소품 (Art3DProps, 블렌더로 만든 고정 메시)</summary>
            public string[] props = new string[0];

            /// <summary>괴물 모델 (Imp · Puglin, 가벼운 _LOD 포함): 머리를 붙이지 않고 몸 재질만 바꾼다</summary>
            public bool Monster => body.StartsWith("Imp") || body.StartsWith("Puglin");
        }

        public static readonly Look[] Heroes =
        {
            // 칭기즈 칸: 몽골 털모자 + 수염 + 왼손 활 (두건은 뺀다. 활 동작이 생기기 전에는 주문 쏘기)
            new Look { name = "Khan", body = "Male_Ranger", bodyTex = "T_Ranger_3_BaseColor", hide = new[] { "Male_Ranger_Head_Hood" }, hair = new[] { "Hair_Buzzed", "Hair_Beard", "Eyebrows" }, props = new[] { "Khan_Hat", "Khan_Bow", "Khan_Quiver" } },
            // 관우: 녹색 두건 + 긴 머리 + 긴 수염, 오른손 청룡언월도로 휘두르기
            new Look { name = "Guan", body = "Male_Peasant", bodyTex = "T_Peasant_2_BaseColor", hair = new[] { "Hair_Long", "Hair_Beard", "Eyebrows" }, hairColor = new Color(0.04f, 0.035f, 0.035f), attack = "Sword_Regular_A", props = new[] { "Guan_Hat", "Guan_Glaive" } },
            // 나폴레옹: 이각모 + 짙은 파랑 군복 (초록 옷 무늬를 파랗게 바꾼 사본 T_Ranger_Navy), 오른손 권총 쏘기
            new Look { name = "Napoleon", body = "Male_Ranger", bodyTex = "T_Ranger_Navy_BaseColor", tint = new Color(0.9f, 0.92f, 1f), hide = new[] { "Male_Ranger_Head_Hood" }, hair = new[] { "Hair_SimpleParted", "Eyebrows" }, attack = "Pistol_Shoot", props = new[] { "Napoleon_Bicorne", "Napoleon_Pistol" } },
            // 테슬라: 가르마 머리 + 콧수염 + 어두운 옷(양복 느낌), 오른손 코일로 주문 쏘기 (전기)
            new Look { name = "Tesla", body = "Male_Peasant", bodyTex = "T_Peasant_BaseColor", tint = new Color(0.36f, 0.37f, 0.42f), hair = new[] { "Hair_SimpleParted", "Eyebrows" }, hairColor = new Color(0.07f, 0.06f, 0.055f), props = new[] { "Tesla_Mustache", "Tesla_Coil" } },
        };

        public sealed class EnemyLook
        {
            public string[] ids;
            public float height;
            public Look look;
        }

        /// <param name="light">가벼운 모델 (블렌더로 줄인 Imp_LOD, 삼각형 2.4천 / 원본 1.5만) — 무리로 나오는 적</param>
        private static Look Imp(string name, int tex, Color tint, string attack, bool shadows, bool glow = false, bool light = false) => new Look
        {
            name = name, body = light ? "Imp_LOD" : "Imp", bodyTex = $"T_Imp_BaseColor_{tex}", normalTex = "T_Imp_Normal", emissive = glow ? "T_Imp_Emissive" : null,
            tint = tint, head = false, attack = attack, shadows = shadows,
        };

        /// <param name="light">가벼운 모델 (Puglin_LOD, 삼각형 1.5천 / 원본 7.5천) — 서바이벌 무리</param>
        private static Look Puglin(string name, int tex, Color tint, string attack, bool shadows, bool light = false) => new Look
        {
            name = name, body = light ? "Puglin_LOD" : "Puglin", bodyTex = $"T_Puglin_BaseColor_{tex}", normalTex = "T_Puglin_Normal",
            tint = tint, head = false, attack = attack, shadows = shadows,
        };

        private static Look Soldier(string name, string body, string tex, Color tint, string attack, bool shadows, bool hood = true) => new Look
        {
            name = name, body = body, bodyTex = tex, tint = tint, attack = attack, shadows = shadows,
            hide = hood ? new string[0] : new[] { "Male_Ranger_Head_Hood" },
            hair = body == "Male_Peasant" ? new[] { "Hair_Buzzed", "Eyebrows" } : new[] { "Eyebrows" },
        };

        /// <summary>
        /// 적 모델 (데이터 id → 모델). 서바이벌 무리(돌진병·궁병·투척병·졸개)는 가장 가벼운 Puglin(꼭짓점 4.5천),
        /// 사람 모양은 옷 + 머리, 마법 쓰는 쪽은 Imp. 2D 때 배운 색을 옅게 곱한다. 그림자는 중간 보스·보스만 (250마리 성능 — Codex 3D 리뷰 4번).
        /// 일반 적은 블렌더로 줄인 가벼운 모델(_LOD, Art3D-Plan 8번), 중간 보스·보스는 원본. 키는 화면 점검(작다) 뒤 1.15배
        /// </summary>
        public static readonly EnemyLook[] Enemies =
        {
            new EnemyLook { ids = new[] { "charger" }, height = 1.49f, look = Puglin("E_Charger", 1, new Color(1f, 0.8f, 0.78f), "Melee_Hook", false, light: true) },
            new EnemyLook { ids = new[] { "archer" }, height = 1.49f, look = Puglin("E_Archer", 2, new Color(0.9f, 0.8f, 1f), "Spell_Simple_Shoot", false, light: true) },
            new EnemyLook { ids = new[] { "bomber" }, height = 1.49f, look = Puglin("E_Bomber", 3, new Color(1f, 0.88f, 0.7f), "OverhandThrow", false, light: true) },
            new EnemyLook { ids = new[] { "minion" }, height = 1.32f, look = Puglin("E_Minion", 3, new Color(0.7f, 0.66f, 0.8f), "Zombie_Scratch", false, light: true) },
            new EnemyLook { ids = new[] { "shield" }, height = 1.78f, look = Soldier("E_Shield", "Male_Peasant", "T_Peasant_BaseColor", new Color(0.75f, 0.8f, 0.9f), "Shield_Dash", false) },
            new EnemyLook { ids = new[] { "splitter", "splitter_small" }, height = 1.55f, look = Imp("E_Splitter", 1, new Color(0.75f, 1f, 0.78f), "Punch_Jab", false, light: true) },
            new EnemyLook { ids = new[] { "summoner" }, height = 1.61f, look = Imp("E_Summoner", 2, new Color(0.9f, 0.78f, 1f), "Spell_Simple_Shoot", false, glow: true, light: true) },
            // 중간 보스 (bossScale로 더 커진다)
            new EnemyLook { ids = new[] { "mid_bison" }, height = 1.61f, look = Puglin("M_Bison", 1, new Color(0.85f, 0.72f, 0.6f), "Shield_Dash", true) },
            new EnemyLook { ids = new[] { "mid_torch" }, height = 1.72f, look = Soldier("M_Torch", "Male_Ranger", "T_Ranger_3_BaseColor", new Color(1f, 0.7f, 0.5f), "Sword_Attack", true) },
            new EnemyLook { ids = new[] { "mid_golem" }, height = 1.61f, look = Puglin("M_Golem", 2, new Color(0.7f, 0.72f, 0.78f), "Punch_Cross", true) },
            new EnemyLook { ids = new[] { "mid_hunter", "hunter_clone" }, height = 1.72f, look = Soldier("M_Hunter", "Male_Ranger", "T_Ranger_BaseColor", new Color(0.42f, 0.38f, 0.55f), "Sword_Dash", true) },
            new EnemyLook { ids = new[] { "mid_executor" }, height = 1.78f, look = Soldier("M_Executor", "Male_Peasant", "T_Peasant_2_BaseColor", new Color(0.7f, 0.45f, 0.75f), "Sword_Regular_B", true) },
            // 보스
            new EnemyLook { ids = new[] { "boss_wraith" }, height = 1.67f, look = Imp("B_Wraith", 3, new Color(0.8f, 0.78f, 1f), "Spell_Simple_Shoot", true, glow: true) },
            new EnemyLook { ids = new[] { "boss_colossus" }, height = 1.67f, look = Puglin("B_Colossus", 2, new Color(0.75f, 0.78f, 0.85f), "Punch_Cross", true) },
            new EnemyLook { ids = new[] { "boss_twins" }, height = 1.72f, look = Soldier("B_Twins", "Male_Ranger", "T_Ranger_BaseColor", new Color(0.35f, 0.32f, 0.48f), "Sword_Regular_A", true) },
            new EnemyLook { ids = new[] { "boss_king" }, height = 1.72f, look = Imp("B_King", 2, new Color(0.8f, 0.6f, 1f), "Spell_Simple_Shoot", true, glow: true) },
            new EnemyLook { ids = new[] { "rift_warden" }, height = 1.72f, look = Imp("B_Warden", 1, new Color(0.7f, 0.8f, 1f), "Melee_Hook", true) },
        };

        /// <summary>영웅 모델 (id = HeroData.id). 아직 안 만들었으면 null (임시 도형 그대로)</summary>
        public static GameObject HeroModel(string id)
        {
            foreach (Look look in Heroes)
            {
                if (string.Equals(look.name, id, System.StringComparison.OrdinalIgnoreCase)) return AssetDatabase.LoadAssetAtPath<GameObject>($"{Characters}/{look.name}3D.prefab");
            }

            return null;
        }

        /// <summary>적 모델 (id = EnemyData.id) · 키. 없으면 null</summary>
        public static GameObject EnemyModel(string id, out float height)
        {
            foreach (EnemyLook e in Enemies)
            {
                if (System.Array.IndexOf(e.ids, id) < 0) continue;
                height = e.height;
                return AssetDatabase.LoadAssetAtPath<GameObject>($"{EnemyCharacters}/{e.look.name}3D.prefab");
            }

            height = 1.4f;
            return null;
        }

        // ── 재질 · 동작 · 조립 ──

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
            mat.SetFloat("_AlphaClip", cutout ? 1f : 0f);
            mat.SetFloat("_Cull", cutout ? 0f : 2f); // 머리카락 카드는 양면
            if (cutout)
            {
                mat.SetFloat("_Cutoff", 0.45f);
                mat.EnableKeyword("_ALPHATEST_ON");
            }
            else
            {
                mat.DisableKeyword("_ALPHATEST_ON");
            }

            if (emissive != null)
            {
                mat.SetTexture("_EmissionMap", AssetDatabase.LoadAssetAtPath<Texture2D>($"{Textures}/{emissive}.png"));
                mat.SetColor("_EmissionColor", Color.white * 1.5f);
                mat.EnableKeyword("_EMISSION");
                mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            }
            else
            {
                mat.SetTexture("_EmissionMap", null);
                mat.DisableKeyword("_EMISSION");
            }

            mat.enableInstancing = true;
            EditorUtility.SetDirty(mat);
            return mat;
        }

        /// <summary>
        /// 동작 컨트롤러 하나를 모두 같이 쓴다: Speed(0~1) 서기→걷기→뛰기, Attack 트리거, Hit 트리거, Dead.
        /// 공격 동작만 캐릭터마다 AnimatorOverrideController로 바꾼다 (GameDesign 5장). 있으면 그 자리에서 비우고 다시 채운다.
        /// </summary>
        public static AnimatorController BaseController()
        {
            Directory.CreateDirectory(Controllers);
            string path = $"{Controllers}/Humanoid.controller";
            var c = AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
            if (c == null) c = AnimatorController.CreateAnimatorControllerAtPath(path);
            AnimatorStateMachine sm = c.layers[0].stateMachine;
            foreach (ChildAnimatorState s in sm.states) sm.RemoveState(s.state);
            foreach (AnimatorStateTransition t in sm.anyStateTransitions) sm.RemoveAnyStateTransition(t);
            foreach (Object sub in AssetDatabase.LoadAllAssetsAtPath(path))
            {
                if (sub is BlendTree) Object.DestroyImmediate(sub, true); // 지난번 블렌드 트리 (상태를 지워도 남는다)
            }

            while (c.parameters.Length > 0) c.RemoveParameter(0);

            c.AddParameter("Speed", AnimatorControllerParameterType.Float);
            c.AddParameter("Attack", AnimatorControllerParameterType.Trigger);
            c.AddParameter("Hit", AnimatorControllerParameterType.Trigger);
            c.AddParameter("Dead", AnimatorControllerParameterType.Bool);
            AnimatorState move = c.CreateBlendTreeInController("Move", out BlendTree tree, 0);
            tree.blendParameter = "Speed";
            tree.AddChild(Clip("Idle_Loop"), 0f);
            tree.AddChild(Clip("Walk_Loop"), 0.35f);
            tree.AddChild(Clip("Jog_Fwd_Loop"), 1f);
            sm.defaultState = move;

            AnimatorState attack = sm.AddState("Attack");
            attack.motion = Clip("Spell_Simple_Shoot");
            attack.speed = 1.4f;
            AnimatorStateTransition toAttack = sm.AddAnyStateTransition(attack);
            toAttack.AddCondition(AnimatorConditionMode.If, 0f, "Attack");
            toAttack.AddCondition(AnimatorConditionMode.IfNot, 0f, "Dead");
            toAttack.duration = 0.06f;
            toAttack.canTransitionToSelf = true;
            AnimatorStateTransition back = attack.AddTransition(move);
            back.hasExitTime = true;
            back.exitTime = 0.8f;
            back.duration = 0.12f;

            AnimatorState hit = sm.AddState("Hit");
            hit.motion = Clip("Hit_Chest");
            AnimatorStateTransition toHit = sm.AddAnyStateTransition(hit);
            toHit.AddCondition(AnimatorConditionMode.If, 0f, "Hit");
            toHit.AddCondition(AnimatorConditionMode.IfNot, 0f, "Dead");
            toHit.duration = 0.05f;
            AnimatorStateTransition hitBack = hit.AddTransition(move);
            hitBack.hasExitTime = true;
            hitBack.exitTime = 0.85f;
            hitBack.duration = 0.1f;

            AnimatorState death = sm.AddState("Death");
            death.motion = Clip("Death01");
            AnimatorStateTransition toDeath = sm.AddAnyStateTransition(death);
            toDeath.AddCondition(AnimatorConditionMode.If, 0f, "Dead");
            toDeath.duration = 0.08f;
            toDeath.canTransitionToSelf = false;
            AnimatorStateTransition revive = death.AddTransition(move);
            revive.AddCondition(AnimatorConditionMode.IfNot, 0f, "Dead");
            revive.duration = 0.2f;
            EditorUtility.SetDirty(c);
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

        /// <summary>몸 모델 + (사람이면) 머리 · 눈 · 머리카락 → 캐릭터 프리팹 (Animator: 아바타 · 컨트롤러, 뿌리 동작 끔) + 키 정보</summary>
        public static string Compose(Look look, RuntimeAnimatorController controller, string folder)
        {
            var fbx = AssetDatabase.LoadAssetAtPath<GameObject>($"{Models}/{look.body}.fbx");
            if (fbx == null) return "없음 " + look.body;
            var go = (GameObject)PrefabUtility.InstantiatePrefab(fbx);
            PrefabUtility.UnpackPrefabInstance(go, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            go.name = look.name + "3D";
            var notes = new List<string>();

            string normal = look.normalTex ?? (look.body.Contains("Ranger") ? "T_Ranger_Normal" : "T_Peasant_Normal");
            Material outfit = Lit(look.name + "_Outfit", look.bodyTex, normal, look.tint, emissive: look.emissive);
            Material arms = Lit("Skin_Regular", "T_Regular_Male_Dark_BaseColor", "T_Regular_Male_Normal", Color.white);

            foreach (var r in go.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                if (System.Array.IndexOf(look.hide, r.name) >= 0)
                {
                    Object.DestroyImmediate(r.gameObject);
                    continue;
                }

                Material[] slots = r.sharedMaterials;
                for (int i = 0; i < slots.Length; i++) slots[i] = !look.Monster && slots[i] != null && slots[i].name.Contains("Regular_Male") ? arms : outfit;
                r.sharedMaterials = slots;
            }

            if (look.head && !look.Monster)
            {
                Material headSkin = Lit("Skin_Head", "T_Superhero_Male_Dark", "T_Superhero_Male_Normal", Color.white);
                Material eyes = Lit("Eyes", "T_Eye_Brown", null, Color.white);
                notes.Add(Attach(go, "Superhero_Male_FullBody", "SuperHero_Male", AssetDatabase.LoadAssetAtPath<Mesh>(HeadMesh), headSkin));
                notes.Add(Attach(go, "Superhero_Male_FullBody", "Eyes", null, eyes));
            }

            if (look.hair.Length > 0)
            {
                Material hair = Lit(look.name + "_Hair", look.hairTex + "_BaseColor", look.hairTex + "_Normal", look.hairColor, cutout: true);
                foreach (string h in look.hair)
                {
                    notes.Add(h == "Eyebrows" ? Attach(go, "Superhero_Male_FullBody", "Eyebrows", null, hair) : Attach(go, h, h, null, hair));
                }
            }

            float bodyHeight = MeasureHeight(go); // 모자·무기를 붙이기 전 키 (소품 때문에 몸이 작아지지 않게)
            foreach (string prop in look.props) notes.Add(Art3DProps.Attach(go, prop, look.shadows));

            var animator = go.GetComponent<Animator>() ?? go.AddComponent<Animator>();
            animator.avatar = AssetDatabase.LoadAssetAtPath<Avatar>($"{Models}/{look.body}.fbx");
            animator.runtimeAnimatorController = controller;
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.CullUpdateTransforms;

            foreach (Renderer r in go.GetComponentsInChildren<Renderer>(true))
            {
                r.shadowCastingMode = look.shadows ? UnityEngine.Rendering.ShadowCastingMode.On : UnityEngine.Rendering.ShadowCastingMode.Off;
                r.receiveShadows = true;
                if (r is SkinnedMeshRenderer smr) smr.updateWhenOffscreen = false;
            }

            var info = go.GetComponent<Model3DInfo>() ?? go.AddComponent<Model3DInfo>();
            info.height = bodyHeight;
            info.castShadows = look.shadows;

            Directory.CreateDirectory(folder);
            string path = $"{folder}/{go.name}.prefab";
            PrefabUtility.SaveAsPrefabAsset(go, path); // 같은 경로면 GUID 그대로 덮어쓴다
            Object.DestroyImmediate(go);
            notes.RemoveAll(string.IsNullOrEmpty);
            return notes.Count > 0 ? $"{path} (키 {info.height:0.00}) — {string.Join(", ", notes)}" : $"{path} (키 {info.height:0.00})";
        }

        /// <summary>서 있는 자세(바인드 자세)의 키: 렌더러 범위의 세로</summary>
        private static float MeasureHeight(GameObject go)
        {
            var bounds = new Bounds(go.transform.position, Vector3.zero);
            bool first = true;
            foreach (Renderer r in go.GetComponentsInChildren<Renderer>(true))
            {
                if (first) bounds = r.bounds;
                else bounds.Encapsulate(r.bounds);
                first = false;
            }

            return Mathf.Max(0.1f, bounds.max.y - Mathf.Min(0f, bounds.min.y));
        }

        /// <summary>에디터에서 바로 사진 (Play 없이): 앞모습(yaw 0) · 옆모습(yaw 90), 동작 한 순간. 3D 렌더러로 찍는다</summary>
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
        /// 붙일 조각: 다른 FBX의 스킨 메시(같은 65뼈)를 대상 모델의 뼈에 이름으로 묶는다. mesh가 null이면 source FBX의 renderer 이름으로 찾는다.
        /// 원본 bindpose를 그대로 쓰므로, 이 조각을 움직이는 뼈가 대상에서도 같은 자리에 있어야 한다 → 무게가 실린 뼈만 재서 1cm 넘게 다르면 알린다.
        /// </summary>
        /// <returns>문제가 없으면 빈 글, 있으면 알림</returns>
        public static string Attach(GameObject target, string sourceFbx, string rendererName, Mesh mesh, Material material)
        {
            var source = AssetDatabase.LoadAssetAtPath<GameObject>($"{Models}/{sourceFbx}.fbx");
            if (source == null) return $"{rendererName}: 원본 없음";
            SkinnedMeshRenderer from = null;
            foreach (var r in source.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                if (r.name == rendererName) from = r;
            }

            if (from == null) return $"{rendererName}: 렌더러 없음";
            var byName = new Dictionary<string, Transform>();
            foreach (Transform t in target.GetComponentsInChildren<Transform>(true)) byName[t.name] = t;
            var bones = new Transform[from.bones.Length];
            for (int i = 0; i < bones.Length; i++)
            {
                if (from.bones[i] != null) byName.TryGetValue(from.bones[i].name, out bones[i]);
            }

            Mesh used = mesh != null ? mesh : from.sharedMesh;
            string problem = CheckBones(from.bones, bones, used, rendererName);

            var part = new GameObject(rendererName);
            part.transform.SetParent(target.transform, false);
            var smr = part.AddComponent<SkinnedMeshRenderer>();
            smr.sharedMesh = used;
            smr.bones = bones;
            smr.rootBone = from.rootBone != null && byName.TryGetValue(from.rootBone.name, out Transform root) ? root : target.transform;
            smr.sharedMaterial = material;
            smr.updateWhenOffscreen = false;
            smr.localBounds = from.localBounds;
            return problem;
        }

        /// <summary>메시를 움직이는 뼈(무게 &gt; 0)가 대상에 다 있고, 바인드 자세에서 같은 자리인지 (원본 FBX와 대상 모두 원점에 놓고 잰다)</summary>
        private static string CheckBones(Transform[] sourceBones, Transform[] targetBones, Mesh mesh, string label)
        {
            var used = new HashSet<int>();
            foreach (BoneWeight w in mesh.boneWeights)
            {
                if (w.weight0 > 0f) used.Add(w.boneIndex0);
                if (w.weight1 > 0f) used.Add(w.boneIndex1);
                if (w.weight2 > 0f) used.Add(w.boneIndex2);
                if (w.weight3 > 0f) used.Add(w.boneIndex3);
            }

            float worst = 0f;
            string worstBone = null;
            foreach (int i in used)
            {
                if (i < 0 || i >= sourceBones.Length || sourceBones[i] == null) continue;
                if (targetBones[i] == null) return $"{label}: 뼈 {sourceBones[i].name} 없음";
                Transform sourceRoot = sourceBones[i].root, targetRoot = targetBones[i].root;
                Vector3 a = sourceRoot.InverseTransformPoint(sourceBones[i].position);
                Vector3 b = targetRoot.InverseTransformPoint(targetBones[i].position);
                float d = Vector3.Distance(a, b);
                if (d > worst)
                {
                    worst = d;
                    worstBone = sourceBones[i].name;
                }
            }

            return worst > 0.01f ? $"{label}: 뼈 {worstBone} {worst * 100f:0.0}cm 어긋남" : "";
        }
    }
}
