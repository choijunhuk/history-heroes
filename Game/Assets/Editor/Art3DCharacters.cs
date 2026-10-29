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
    /// 무료판 옷(Ranger · Peasant)에는 머리가 없다 → 기본 몸(Superhero_Male/Female_FullBody)에서 **머리·목만** 떼어(뼈 무게로 고른 삼각형)
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
        public const string FemaleHeadMesh = Meshes + "/Head_Female.asset";
        private const string Materials = Art3DAssets.Root + "/Materials";
        public const string Characters = Art3DAssets.Root + "/Characters";
        public const string EnemyCharacters = Characters + "/Enemies";
        private const string Controllers = Art3DAssets.Root + "/Controllers";
        private const string Textures = Art3DAssets.Textures;

        /// <summary>이 뼈(와 그 아래 뼈)에 무게가 절반 넘게 실린 꼭짓점 = 머리. 세 꼭짓점이 모두 머리인 삼각형만 남긴다</summary>
        private static readonly string[] HeadBones = { "Head", "neck_01" };

        [MenuItem("History Heroes/3D 캐릭터 다시 만들기")]
        private static void BuildAllMenu() => Debug.Log(BuildAll());

        /// <summary>남녀 머리 메시 · 공용 동작 컨트롤러 · 영웅 · 적</summary>
        public static string BuildAll()
        {
            var log = new List<string> { ExtractHead(), ExtractHead("Superhero_Female_FullBody", "Superhero_Female", FemaleHeadMesh) };
            Art3DProps.Import();
            AnimatorController baseController = BaseController();
            foreach (Look look in Heroes) log.Add(Compose(look, Override(look.name, baseController, look.attack), Characters));
            foreach (EnemyLook e in Enemies) log.Add(Compose(e.look, Override(e.look.name, baseController, e.look.attack), EnemyCharacters));
            log.Add(Compose(TwinGhost, Override(TwinGhost.name, baseController, TwinGhost.attack), EnemyCharacters));
            log.Add(Art3DPets.BuildAll());
            AssetDatabase.SaveAssets();
            return string.Join("\n", log);
        }

        /// <summary>예전 이름 (영웅만)</summary>
        public static string BuildHeroes() => BuildAll();

        // ── 머리 떼기 ──

        public static string ExtractHead(string sourceFbx = "Superhero_Male_FullBody", string rendererName = "SuperHero_Male", string outputPath = HeadMesh)
        {
            var source = AssetDatabase.LoadAssetAtPath<GameObject>($"{Models}/{sourceFbx}.fbx");
            if (source == null) return "기본 몸 없음";
            SkinnedMeshRenderer body = null;
            foreach (var r in source.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                if (r.name == rendererName) body = r;
            }

            if (body == null) return $"{rendererName} 메시 없음";
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

            if (keep.Count == 0) return $"{rendererName}: 머리 삼각형 없음";

            var order = new int[remap.Count];
            foreach (KeyValuePair<int, int> pair in remap) order[pair.Value] = pair.Key;
            Vector3[] pos = mesh.vertices, nor = mesh.normals;
            Vector4[] tan = mesh.tangents;
            Vector2[] uv = mesh.uv;

            Directory.CreateDirectory(Meshes);
            var head = AssetDatabase.LoadAssetAtPath<Mesh>(outputPath);
            bool created = head == null;
            if (created) head = new Mesh();
            head.Clear();
            head.name = Path.GetFileNameWithoutExtension(outputPath);
            head.vertices = Pick(pos, order);
            if (nor.Length > 0) head.normals = Pick(nor, order);
            if (tan.Length > 0) head.tangents = Pick(tan, order);
            if (uv.Length > 0) head.uv = Pick(uv, order);
            head.boneWeights = Pick(weights, order);
            head.bindposes = mesh.bindposes;
            head.triangles = keep.ToArray();
            head.RecalculateBounds();
            if (created) AssetDatabase.CreateAsset(head, outputPath);
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
            public bool female;
            public string[] hide = new string[0];
            public string[] metalParts = new string[0];
            public Color metalColor = new Color(0.78f, 0.83f, 0.91f);
            public string[] goldParts = new string[0];
            public string[] hair = new string[0];
            public string hairTex = "T_Hair_1";
            public Color hairColor = new Color(0.1f, 0.075f, 0.06f); // 머리카락 텍스처는 회색 → 색을 곱한다
            public string attack = "Spell_Simple_Shoot";
            public bool shadows = true;
            /// <summary>상징 소품 (Art3DProps, 블렌더로 만든 고정 메시)</summary>
            public string[] props = new string[0];
            /// <summary>0보다 크면 비치는 유령 (모든 재질 알파, 그림자 없음 — 그림자 쌍둥이의 맞지 않는 쪽)</summary>
            public float ghost;

            /// <summary>재질 이름 꼬리 (유령은 따로)</summary>
            public string MatSuffix => ghost > 0f ? "_Ghost" : "";

            public float Alpha => ghost > 0f ? ghost : 1f;

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
            // 이순신: 짙은 남색 옷 + 수염 + 조선 장군 투구 · 두정갑 가슴판 · 오른손 총통으로 권총 쏘기 (확장: 새 영웅)
            new Look { name = "YiSunsin", body = "Male_Peasant", bodyTex = "T_Peasant_2_BaseColor", tint = new Color(0.62f, 0.7f, 1f), hair = new[] { "Hair_Buzzed", "Hair_Beard", "Eyebrows" }, hairColor = new Color(0.05f, 0.045f, 0.045f), attack = "Pistol_Shoot", props = new[] { "YiSunsin_Helmet", "YiSunsin_Armor", "YiSunsin_Chongtong" } },
            // 이성계: 붉은 갈색 옷 + 수염 + 고려 말 투구 · 왼손 각궁 (활 동작이 생기기 전에는 주문 쏘기)
            new Look { name = "YiSeonggye", body = "Male_Ranger", bodyTex = "T_Ranger_BaseColor", tint = new Color(1f, 0.72f, 0.62f), hide = new[] { "Male_Ranger_Head_Hood" }, hair = new[] { "Hair_Buzzed", "Hair_Beard", "Eyebrows" }, hairColor = new Color(0.06f, 0.05f, 0.045f), props = new[] { "Yi_Helmet", "Yi_Bow" } },
            // 다빈치: 갈색 옷 + 긴 머리·수염(회색) + 붉은 베레모 · 왼손 설계도 · 오른손 석궁 (권총 쏘기)
            new Look { name = "Davinci", body = "Male_Peasant", bodyTex = "T_Peasant_BaseColor", tint = new Color(0.95f, 0.82f, 0.68f), hair = new[] { "Hair_Long", "Hair_Beard", "Eyebrows" }, hairColor = new Color(0.62f, 0.6f, 0.57f), attack = "Pistol_Shoot", props = new[] { "Davinci_Beret", "Davinci_Scroll", "Davinci_Crossbow" } },
            // 제갈량: 흰·남색 학창의 (T_Peasant_2를 밝기 따라 남색→흰색으로 다시 칠한 그림) + 긴 머리·수염 + 윤건 · 오른손 깃털 부채 (주문 쏘기)
            new Look { name = "Zhuge", body = "Male_Peasant", bodyTex = "T_Zhuge_Robe_BaseColor", hair = new[] { "Hair_Long", "Hair_Beard", "Eyebrows" }, hairColor = new Color(0.05f, 0.045f, 0.045f), props = new[] { "Zhuge_Fan", "Zhuge_Hat" } },
            // 잔 다르크: 짧은 머리, 후드 없는 레인저와 어깨 갑옷. 남색·은빛 옷으로 구별한다.
            new Look { name = "Jeanne", female = true, body = "Female_Ranger", bodyTex = "T_Ranger_Navy_BaseColor", tint = Color.white, hide = new[] { "Female_Ranger_Head_Hood" }, metalParts = new[] { "Female_Ranger_Acc_Pauldrons", "Female_Ranger_Arms_Bracer" }, hair = new[] { "Hair_BuzzedFemale", "Eyebrows_Female" }, hairColor = new Color(0.34f, 0.22f, 0.14f), attack = "Sword_Regular_A", props = new[] { "Jeanne_Sword", "Jeanne_Banner" } },
            // 선덕여왕: 기존 농민 옷의 몸통을 금색, 나머지를 보라색으로 나눠 왕실 색을 낸다. 관식은 별도 제작 전까지 쓰지 않는다.
            new Look { name = "Seondeok", female = true, body = "Female_Peasant", bodyTex = "T_Zhuge_Robe_BaseColor", tint = new Color(0.66f, 0.38f, 0.82f), goldParts = new[] { "Female_Peasant_Body" }, hair = new[] { "Hair_Buns", "Eyebrows_Female" }, hairColor = new Color(0.09f, 0.065f, 0.09f), props = new[] { "Davinci_Scroll" } },
            // 부디카: 녹색 옷과 갈색 가죽 위에 청동 보호대. 새 검을 만들기 전까지 손은 비워 둔다.
            new Look { name = "Boudica", female = true, body = "Female_Ranger", bodyTex = "T_Ranger_BaseColor", tint = new Color(0.88f, 0.92f, 0.78f), hide = new[] { "Female_Ranger_Head_Hood" }, metalParts = new[] { "Female_Ranger_Acc_Pauldrons", "Female_Ranger_Arms_Bracer" }, metalColor = new Color(0.67f, 0.43f, 0.24f), hair = new[] { "Hair_Long", "Eyebrows_Female" }, hairColor = new Color(0.48f, 0.21f, 0.11f), attack = "Sword_Regular_A", props = new[] { "Boudica_Sword" } },
            // 같은 얼굴·망치를 플레이 영웅과 시련 형상이 공유한다. 보스 크기만 전투 프리팹에서 조절한다.
            new Look { name = "Thor", body = "Male_Ranger", bodyTex = "T_Ranger_BaseColor", tint = new Color(0.48f, 0.44f, 0.43f), hide = new[] { "Male_Ranger_Head_Hood" }, metalParts = new[] { "Male_Ranger_Acc_Pauldron", "Male_Ranger_Arms_Bracer" }, metalColor = new Color(0.66f, 0.38f, 0.24f), hair = new[] { "Hair_Long", "Hair_Beard", "Eyebrows" }, hairColor = new Color(0.75f, 0.37f, 0.17f), attack = "Sword_Regular_A", props = new[] { "Thor_Hammer" } },
            // 기존 옷의 밝은 부분은 상아색으로, 별도 다리 조각은 청록색으로 구분한다. 몸통의 금색과 관식은 유지한다.
            new Look { name = "Cleopatra", female = true, body = "Female_Peasant", bodyTex = "T_Zhuge_Robe_BaseColor", tint = new Color(0.98f, 0.94f, 0.82f), metalParts = new[] { "Female_Peasant_Legs" }, metalColor = new Color(0.12f, 0.55f, 0.53f), goldParts = new[] { "Female_Peasant_Body" }, hair = new[] { "Hair_Long", "Eyebrows_Female" }, hairColor = new Color(0.035f, 0.03f, 0.035f), props = new[] { "Cleopatra_Crown", "Cleopatra_Collar", "Davinci_Scroll" } },
            // 사람 머리·눈을 붙이지 않아 자칼 가면만 보인다. Enemy_Staff는 Imp 손 기준이라 손에는 달지 않는다.
            new Look { name = "Anubis", body = "Male_Ranger", bodyTex = "T_Ranger_BaseColor", tint = new Color(0.27f, 0.23f, 0.19f), head = false, hide = new[] { "Male_Ranger_Head_Hood" }, metalParts = new[] { "Male_Ranger_Acc_Pauldron", "Male_Ranger_Arms_Bracer" }, metalColor = new Color(0.8f, 0.64f, 0.27f), props = new[] { "Anubis_Mask" } },
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

        /// <summary>적에게 장식 소품을 입힌다 (그래픽 재평가 6번: 종류마다 실루엣이 다르게). hide = 숨길 원래 무기 이름</summary>
        private static Look Dress(Look look, string first, string second = null, string hide = null)
        {
            look.props = second != null ? new[] { first, second } : new[] { first };
            if (hide != null) look.hide = new[] { hide };
            return look;
        }

        private static Look Soldier(string name, string body, string tex, Color tint, string attack, bool shadows, bool hood = true, string[] props = null, float ghost = 0f) => new Look
        {
            name = name, body = body, bodyTex = tex, tint = tint, attack = attack, shadows = shadows && ghost <= 0f,
            hide = hood ? new string[0] : new[] { "Male_Ranger_Head_Hood" },
            hair = body == "Male_Peasant" ? new[] { "Hair_Buzzed", "Eyebrows" } : new[] { "Eyebrows" },
            props = props ?? new string[0],
            ghost = ghost,
        };

        /// <summary>그림자 쌍둥이의 맞지 않는 쪽: 보스와 같은 옷을 비치게 (2D 때 흐린 원 → 3D 유령)</summary>
        public static readonly Look TwinGhost = Soldier("B_TwinsGhost", "Male_Ranger", "T_Ranger_BaseColor", new Color(0.62f, 0.56f, 0.95f), "Sword_Regular_A", false, ghost: 0.38f);

        public const float TwinHeight = 1.72f;

        /// <summary>쌍둥이 유령 모델 (없으면 null)</summary>
        public static GameObject TwinGhostModel() => AssetDatabase.LoadAssetAtPath<GameObject>($"{EnemyCharacters}/{TwinGhost.name}3D.prefab");

        /// <summary>
        /// 적 모델 (데이터 id → 모델). 서바이벌 무리(돌진병·궁병·투척병·졸개)는 가장 가벼운 Puglin(꼭짓점 4.5천),
        /// 사람 모양은 옷 + 머리, 마법 쓰는 쪽은 Imp. 2D 때 배운 색을 옅게 곱한다. 그림자는 중간 보스·보스만 (250마리 성능 — Codex 3D 리뷰 4번).
        /// 일반 적은 블렌더로 줄인 가벼운 모델(_LOD, Art3D-Plan 8번), 중간 보스·보스는 원본. 키는 화면 점검(작다) 뒤 1.15배
        /// </summary>
        public static readonly EnemyLook[] Enemies =
        {
            new EnemyLook { ids = new[] { "charger" }, height = 1.49f, look = Dress(Puglin("E_Charger", 1, new Color(1f, 0.8f, 0.78f), "Melee_Hook", false, light: true), "Enemy_HornHelm") },
            new EnemyLook { ids = new[] { "archer" }, height = 1.49f, look = Dress(Puglin("E_Archer", 2, new Color(0.9f, 0.8f, 1f), "Spell_Simple_Shoot", false, light: true), "Enemy_Hood", "Enemy_ShortBow", hide: "Puglin_Stick") },
            new EnemyLook { ids = new[] { "bomber" }, height = 1.49f, look = Dress(Puglin("E_Bomber", 3, new Color(1f, 0.88f, 0.7f), "OverhandThrow", false, light: true), "Enemy_BombPack", "Enemy_HandBomb", hide: "Puglin_Stick") },
            new EnemyLook { ids = new[] { "minion" }, height = 1.32f, look = Dress(Puglin("E_Minion", 3, new Color(0.7f, 0.66f, 0.8f), "Zombie_Scratch", false, light: true), "Enemy_Cap", "Enemy_Dagger", hide: "Puglin_Stick") },
            new EnemyLook { ids = new[] { "memory_guardian" }, height = 1.32f, look = Dress(Puglin("E_MemoryGuardian", 3, new Color(0.82f, 0.7f, 0.44f), "Zombie_Scratch", false, light: true), "Enemy_Cap", "Enemy_Dagger", hide: "Puglin_Stick") },
            new EnemyLook { ids = new[] { "shield" }, height = 1.78f, look = Soldier("E_Shield", "Male_Peasant", "T_Peasant_BaseColor", new Color(0.75f, 0.8f, 0.9f), "Melee_Hook", false, props: new[] { "Enemy_TowerShield" }) }, // 방패를 앞에 세우고 밀어붙인다 (Shield_Dash는 방패가 눕는다 — 화면 점검 2차)
            new EnemyLook { ids = new[] { "splitter", "splitter_small" }, height = 1.55f, look = Dress(Imp("E_Splitter", 1, new Color(0.75f, 1f, 0.78f), "Punch_Jab", false, light: true), "Enemy_BackSpikes") },
            new EnemyLook { ids = new[] { "summoner" }, height = 1.61f, look = Dress(Imp("E_Summoner", 2, new Color(0.9f, 0.78f, 1f), "Spell_Simple_Shoot", false, glow: true, light: true), "Enemy_Staff", "Enemy_SkullMask", hide: "Imp_Mace") },
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
            // 긴 흰 머리·수염과 청동 장식으로 구별하고, 전격은 보스 탄막 효과를 쓴다.
            new EnemyLook { ids = new[] { "zeus_trial" }, height = 1.78f, look = new Look { name = "B_Zeus", body = "Male_Peasant", bodyTex = "T_Zhuge_Robe_BaseColor", tint = new Color(0.95f, 0.95f, 0.91f), metalParts = new[] { "Male_Peasant_Feet" }, metalColor = new Color(0.72f, 0.52f, 0.24f), hair = new[] { "Hair_Long", "Hair_Beard", "Eyebrows" }, hairColor = new Color(0.92f, 0.91f, 0.86f) } },
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
            if (id == "thor_trial")
            {
                height = 1.72f;
                return HeroModel("thor");
            }
            if (id == "anubis_trial")
            {
                height = 1.72f;
                return HeroModel("anubis");
            }
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

        /// <param name="alpha">1보다 작으면 비치는 재질 (그림자 쌍둥이의 맞지 않는 쪽)</param>
        public static Material Lit(string name, string baseTex, string normalTex, Color tint, bool cutout = false, string emissive = null, float alpha = 1f)
        {
            Directory.CreateDirectory(Materials);
            string path = $"{Materials}/M_{name}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(mat, path);
            }

            mat.SetTexture("_BaseMap", baseTex != null ? AssetDatabase.LoadAssetAtPath<Texture2D>($"{Textures}/{baseTex}.png") : null);
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

            // 비치는 재질 (URP Lit "Transparent", 알파 섞기). 아니면 불투명으로 되돌린다
            bool transparent = alpha < 0.999f;
            Color baseColor = mat.GetColor("_BaseColor");
            baseColor.a = transparent ? alpha : 1f;
            mat.SetColor("_BaseColor", baseColor);
            mat.SetFloat("_Surface", transparent ? 1f : 0f);
            mat.SetFloat("_Blend", 0f);
            // "반사광 지키기"가 켜져 있으면 URP 재질 검사가 다시 미리 곱하기(One + _ALPHAPREMULTIPLY_ON)로 바꿔 유령이 흐려진다 → 끈다
            mat.SetFloat("_BlendModePreserveSpecular", 0f);
            mat.SetFloat("_SrcBlend", transparent ? (float)UnityEngine.Rendering.BlendMode.SrcAlpha : (float)UnityEngine.Rendering.BlendMode.One);
            mat.SetFloat("_DstBlend", transparent ? (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha : (float)UnityEngine.Rendering.BlendMode.Zero);
            mat.SetFloat("_SrcBlendAlpha", transparent ? (float)UnityEngine.Rendering.BlendMode.One : (float)UnityEngine.Rendering.BlendMode.One);
            mat.SetFloat("_DstBlendAlpha", transparent ? (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha : (float)UnityEngine.Rendering.BlendMode.Zero);
            mat.SetFloat("_ZWrite", transparent ? 0f : 1f);
            mat.SetOverrideTag("RenderType", transparent ? "Transparent" : (cutout ? "TransparentCutout" : "Opaque"));
            if (transparent) mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            else mat.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.DisableKeyword("_ALPHAPREMULTIPLY_ON"); // 알파 섞기에 미리 곱하기가 남아 있으면 두 번 흐려진다
            mat.DisableKeyword("_ALPHAMODULATE_ON");
            mat.renderQueue = transparent ? (int)UnityEngine.Rendering.RenderQueue.Transparent : cutout ? (int)UnityEngine.Rendering.RenderQueue.AlphaTest : -1;

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
            Material outfit = Lit(look.name + "_Outfit", look.bodyTex, normal, look.tint, emissive: look.emissive, alpha: look.Alpha);
            Material arms = look.female
                ? Lit("Skin_Regular_Female" + look.MatSuffix, "T_Superhero_Female_Light_BaseColor", "T_Superhero_Female_Normal", look.ghost > 0f ? look.tint : Color.white, alpha: look.Alpha)
                : Lit("Skin_Regular" + look.MatSuffix, "T_Regular_Male_Dark_BaseColor", "T_Regular_Male_Normal", look.ghost > 0f ? look.tint : Color.white, alpha: look.Alpha);
            Material metal = null;
            if (look.metalParts.Length > 0)
            {
                metal = Lit(look.name + "_Steel", null, null, look.metalColor);
                metal.SetFloat("_Metallic", 0.75f);
                metal.SetFloat("_Smoothness", 0.6f);
                EditorUtility.SetDirty(metal);
            }
            Material gold = look.goldParts.Length > 0
                ? Lit(look.name + "_Gold", look.bodyTex, normal, new Color(1f, 0.8f, 0.38f))
                : null;

            foreach (var r in go.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                if (System.Array.IndexOf(look.hide, r.name) >= 0)
                {
                    Object.DestroyImmediate(r.gameObject);
                    continue;
                }

                Material[] slots = r.sharedMaterials;
                bool steelPart = metal != null && System.Array.IndexOf(look.metalParts, r.name) >= 0;
                bool goldPart = gold != null && System.Array.IndexOf(look.goldParts, r.name) >= 0;
                for (int i = 0; i < slots.Length; i++)
                {
                    if (steelPart) slots[i] = metal;
                    else if (goldPart) slots[i] = gold;
                    else slots[i] = !look.Monster && slots[i] != null && (slots[i].name.Contains("Regular_Male") || look.female && slots[i].name.Contains("Regular_Female")) ? arms : outfit;
                }
                r.sharedMaterials = slots;
            }

            if (look.head && !look.Monster)
            {
                Color skin = look.ghost > 0f ? look.tint : Color.white;
                Material headSkin = look.female
                    ? Lit("Skin_Head_Female" + look.MatSuffix, "T_Superhero_Female_Light_BaseColor", "T_Superhero_Female_Normal", skin, alpha: look.Alpha)
                    : Lit("Skin_Head" + look.MatSuffix, "T_Superhero_Male_Dark", "T_Superhero_Male_Normal", skin, alpha: look.Alpha);
                Material eyes = Lit("Eyes" + look.MatSuffix, "T_Eye_Brown", null, skin, alpha: look.Alpha);
                string headSource = look.female ? "Superhero_Female_FullBody" : "Superhero_Male_FullBody";
                string headRenderer = look.female ? "Superhero_Female" : "SuperHero_Male";
                notes.Add(Attach(go, headSource, headRenderer, AssetDatabase.LoadAssetAtPath<Mesh>(look.female ? FemaleHeadMesh : HeadMesh), headSkin));
                notes.Add(Attach(go, headSource, "Eyes", null, eyes));
            }

            if (look.hair.Length > 0)
            {
                Material hair = Lit(look.name + "_Hair", look.hairTex + "_BaseColor", look.hairTex + "_Normal", look.hairColor, cutout: look.ghost <= 0f, alpha: look.Alpha);
                foreach (string h in look.hair)
                {
                    notes.Add(h == "Eyebrows" ? Attach(go, "Superhero_Male_FullBody", "Eyebrows", null, hair) : Attach(go, h, h, null, hair));
                }
            }

            float bodyHeight = MeasureHeight(go); // 모자·무기를 붙이기 전 키 (소품 때문에 몸이 작아지지 않게)
            foreach (string prop in look.props) notes.Add(Art3DProps.Attach(go, prop, look.shadows));
            if (look.name == "B_Zeus")
            {
                foreach (Transform bone in go.GetComponentsInChildren<Transform>(true))
                {
                    if (bone.name != "hand_r") continue;
                    SceneBuilder.AttachZeusHandLightning(bone);
                    break;
                }
            }

            // Unity 객체는 ?? 로 "없음"을 못 가린다 (가짜 null) → 직접 확인. 복사 아바타 모델(Female_Peasant)엔 Animator가 없다
            Animator animator = go.GetComponent<Animator>();
            if (animator == null) animator = go.AddComponent<Animator>();
            string bodyPath = $"{Models}/{look.body}.fbx";
            // 복사 모드에는 대상 모델의 Avatar가 없으므로 원본을 프리팹에도 직접 연결한다.
            Avatar avatar = look.body == "Female_Peasant"
                ? (AssetImporter.GetAtPath(bodyPath) as ModelImporter)?.sourceAvatar
                : AssetDatabase.LoadAssetAtPath<Avatar>(bodyPath);
            animator.avatar = avatar;
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
