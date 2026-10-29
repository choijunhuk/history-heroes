using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace HistoryHeroes.EditorTools
{
    /// <summary>
    /// 영웅 상징 소품 (Art3D-Plan 5번): 몽골 털모자·활, 청룡언월도·두건, 이각모·권총, 콧수염·코일.
    /// 블렌더 세션(choij-23)이 무료 기본 몸(Superhero_Male_FullBody)의 바인드 자세에 맞춰 만든 고정 메시 (`Assets/Art/3D/Props`, 직접 만든 것).
    /// 붙이기: 기본 몸에서 그 뼈 기준 자리(뼈의 역행렬 × 소품 자리)를 구해 옷 모델의 같은 이름 뼈 아래에 둔다 →
    /// 옷마다 팔 길이가 조금 달라도(Ranger 손은 2.3cm 안쪽) 손에 쥔 채로 따라간다.
    /// 색은 소품 FBX의 재질 이름(역할)으로 우리 재질을 고른다 (Fur · Cloth · Gold · Wood · Metal · Black · Copper · Hair · Red · White · Blue).
    /// </summary>
    public static class Art3DProps
    {
        public const string Folder = Art3DAssets.Root + "/Props";
        private const string Materials = Art3DAssets.Root + "/Materials";

        /// <summary>소품 이름 → 붙일 뼈 (블렌더 notes.txt)</summary>
        public static readonly Dictionary<string, string> Bones = new Dictionary<string, string>
        {
            ["Khan_Hat"] = "Head", ["Khan_Bow"] = "hand_l", ["Khan_Quiver"] = "spine_03", // 등에 멘 화살통 (기마 궁수로 보이게 — Codex 화면 점검 5번)
            ["Guan_Glaive"] = "hand_r", ["Guan_Hat"] = "Head",
            ["Napoleon_Bicorne"] = "Head", ["Napoleon_Pistol"] = "hand_r",
            ["Tesla_Mustache"] = "Head", ["Tesla_Coil"] = "hand_r",
            ["YiSunsin_Helmet"] = "Head", ["YiSunsin_Armor"] = "spine_03", ["YiSunsin_Chongtong"] = "hand_r", // 이순신 (블렌더 choij-23)
            ["Yi_Helmet"] = "Head", ["Yi_Bow"] = "hand_l", // 이성계
            ["Davinci_Beret"] = "Head", ["Davinci_Scroll"] = "hand_l", ["Davinci_Crossbow"] = "hand_r", // 다빈치
            ["Zhuge_Fan"] = "hand_r", ["Zhuge_Hat"] = "Head", // 제갈량
            ["Jeanne_Sword"] = "hand_r", ["Jeanne_Banner"] = "spine_03", // 잔 다르크 (블렌더 2026-10-29, Tools/blender/hero_props.py)
            ["Boudica_Sword"] = "hand_r", // 부디카 켈트 잎 모양 검
            ["Thor_Hammer"] = "hand_r", // 토르 (신의 시련 1 · 플레이 토르)
            ["Enemy_TowerShield"] = "hand_l", // 방패병의 큰 나무 방패 (앞을 막는다)
            // 그래픽 재평가 6번 "적을 실루엣으로 구분": 무리 적마다 머리 · 등 · 손 장식 (블렌더 notes_enemies.txt)
            ["Enemy_HornHelm"] = "Head", ["Enemy_Hood"] = "Head", ["Enemy_ShortBow"] = "hand_l", ["Enemy_BombPack"] = "spine_03",
            ["Enemy_HandBomb"] = "hand_l", ["Enemy_Cap"] = "Head", ["Enemy_Dagger"] = "hand_l", ["Enemy_BackSpikes"] = "spine_03",
            ["Enemy_Staff"] = "hand_l", ["Enemy_SkullMask"] = "Head",
        };

        /// <summary>소품을 맞춘 기본 몸 (없으면 Superhero). 적 장식은 그 적의 몸에 맞춰 만들었다 (같은 뼈대의 큰 모델에도 맞는다)</summary>
        private static readonly Dictionary<string, string> Basis = new Dictionary<string, string>
        {
            ["Enemy_HornHelm"] = "Puglin_LOD", ["Enemy_Hood"] = "Puglin_LOD", ["Enemy_ShortBow"] = "Puglin_LOD", ["Enemy_BombPack"] = "Puglin_LOD",
            ["Enemy_HandBomb"] = "Puglin_LOD", ["Enemy_Cap"] = "Puglin_LOD", ["Enemy_Dagger"] = "Puglin_LOD",
            ["Enemy_BackSpikes"] = "Imp_LOD", ["Enemy_Staff"] = "Imp_LOD", ["Enemy_SkullMask"] = "Imp_LOD",
        };

        /// <summary>역할 이름 → 색 (sRGB) · 금속 여부 (블렌더 notes.txt의 색)</summary>
        private static readonly Dictionary<string, (Color color, bool metal)> Roles = new Dictionary<string, (Color, bool)>
        {
            ["Fur"] = (Hex(0x6F5238), false), ["Cloth"] = (Hex(0x307C3F), false), ["Gold"] = (Hex(0xE7C461), true),
            ["Wood"] = (Hex(0x815938), false), ["Metal"] = (Hex(0xC4C7CB), true), ["Black"] = (Hex(0x212124), false),
            ["Copper"] = (Hex(0xCE8955), true), ["Hair"] = (Hex(0x453527), false), ["Red"] = (Hex(0x992727), false),
            ["White"] = (Hex(0xE7E5DA), false), ["Blue"] = (Hex(0x2745A0), false),
            ["Bronze"] = (Hex(0xB0803A), true), // 부디카 검 손잡이 (켈트 청동)
            // 펫 (블렌더 pets/notes.txt, 선형 색을 화면 색으로)
            ["Feather"] = (Hex(0x7A5639), false), ["Beak"] = (Hex(0xEDBA47), false), ["Eye"] = (Hex(0x1C1917), false),
            ["Horse"] = (Hex(0x944530), false), ["Horse_Gray"] = (Hex(0xC9C9CC), false), ["Horse_Dapple"] = (Hex(0xB3B3B8), false),
            ["Mane_White"] = (Hex(0xF0F0ED), false), ["Gray"] = (Hex(0xABB0B5), false),
            // 무대·스킬 소품 (블렌더 Fx·Deco·Floor README, 그래픽 평가 2·4번)
            ["Cloth_Red"] = (Hex(0x992727), false), ["Ice"] = (Hex(0xB3E7FF), false), ["Rock_Hot"] = (Hex(0x3F3530), false), ["Lava"] = (Hex(0xFF9530), false),
            ["Stone"] = (Hex(0xA29E97), false), ["Stone_Dark"] = (Hex(0x6C6965), false), ["Marble"] = (Hex(0xDDDAD1), false), ["Brick"] = (Hex(0xA25D45), false),
            ["Iron"] = (Hex(0x59595D), true), ["Wood_Dark"] = (Hex(0x553824), false), ["Crystal_Shadow"] = (Hex(0x653090), false), ["Rust"] = (Hex(0x955530), false),
            ["Leaf"] = (Hex(0x4B8638), false), ["Leaf_Dark"] = (Hex(0x305D2C), false), ["Grass"] = (Hex(0x59993F), false), ["Petal"] = (Hex(0xF9DA59), false),
            ["Cloth_Dark"] = (Hex(0x551D1D), false), ["Cloth_Gold"] = (Hex(0xC4A24B), false), ["Water"] = (Hex(0x3F6173), false), ["Wet"] = (Hex(0x45423F), false),
            ["Bone"] = (Hex(0xCEC5AD), false),
            // 적 장식 (블렌더 notes_enemies.txt)
            ["Helm_Red"] = (Hex(0xD9442B), false), ["Horn"] = (Hex(0xF2E3C2), false), ["Hood_Green"] = (Hex(0x3DB54A), false),
            ["Wood_Light"] = (Hex(0xC9955A), false), ["Bomb"] = (Hex(0x2A2E3D), false), ["Cap_Yellow"] = (Hex(0xF5C518), false),
            ["Fuse_Glow"] = (Hex(0xFFC23D), false), ["Orb_Glow"] = (Hex(0x6FF3FF), false), ["Crystal_Green"] = (Hex(0x4CE08A), false),
            // 확장 1차 소품 (블렌더 Deco2 README)
            ["Berry"] = (Hex(0xD92B3A), false), ["Hay"] = (Hex(0xE0B04E), false), ["Hay_Dark"] = (Hex(0xB8862F), false), ["Meat"] = (Hex(0xA8452E), false),
            ["Hazard"] = (Hex(0xF2B705), false), ["Sign_Face"] = (Hex(0xEADFC4), false), ["Belt"] = (Hex(0x3B3D44), false), ["Plaster"] = (Hex(0xD8CDB8), false),
            ["Glyph_Glow"] = (Hex(0x8FF0FF), false),
        };

        /// <summary>스스로 빛나는 역할 → 빛 색 (화면 후처리의 빛 번짐이 잡는다: 1 넘게)</summary>
        private static readonly Dictionary<string, Color> Glows = new Dictionary<string, Color>
        {
            ["Copper"] = new Color(0.35f, 0.75f, 1f) * 1.6f, // 테슬라 코일 = 푸른 전기 빛 (Codex 화면 점검 5번)
            ["Ice"] = new Color(0.45f, 0.85f, 1f) * 1.3f,
            ["Lava"] = new Color(1f, 0.5f, 0.12f) * 3f,
            ["Crystal_Shadow"] = new Color(0.45f, 0.18f, 0.85f) * 0.9f,
            ["Glyph_Glow"] = new Color(0.55f, 0.95f, 1f) * 2f,
            ["Fuse_Glow"] = new Color(1f, 0.6f, 0.15f) * 3f, ["Orb_Glow"] = new Color(0.4f, 0.95f, 1f) * 2f, ["Crystal_Green"] = new Color(0.3f, 1f, 0.55f) * 0.8f,
        };

        private static Color Hex(int rgb) => new Color(((rgb >> 16) & 255) / 255f, ((rgb >> 8) & 255) / 255f, (rgb & 255) / 255f);

        /// <summary>소품 FBX 가져오기 설정: 뼈·동작 없음, 크기 그대로</summary>
        public static void Import()
        {
            if (!Directory.Exists(Folder)) return;
            foreach (string file in Directory.GetFiles(Folder, "*.fbx"))
            {
                var importer = AssetImporter.GetAtPath(file.Replace('\\', '/')) as ModelImporter;
                if (importer == null) continue;
                if (importer.animationType == ModelImporterAnimationType.None && !importer.importAnimation) continue;
                importer.animationType = ModelImporterAnimationType.None;
                importer.importAnimation = false;
                importer.importBlendShapes = false;
                importer.materialImportMode = ModelImporterMaterialImportMode.ImportViaMaterialDescription; // 재질 이름(역할)을 읽으려고
                importer.SaveAndReimport();
            }
        }

        public static Material RoleMaterial(string role)
        {
            if (!Roles.TryGetValue(role, out (Color color, bool metal) r)) r = (Color.gray, false);
            string path = $"{Materials}/M_Prop_{role}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(mat, path);
            }

            mat.SetColor("_BaseColor", r.color);
            mat.SetFloat("_Metallic", r.metal ? 0.85f : 0f);
            mat.SetFloat("_Smoothness", r.metal ? 0.6f : role == "Water" ? 0.9f : role == "Ice" ? 0.7f : 0.15f);
            if (Glows.TryGetValue(role, out Color glow))
            {
                // 구리 = 테슬라 코일만 쓴다 → 푸른 전기 빛 (멀리서도 테슬라로 보이게). 용암·얼음·그림자 수정도 빛난다
                mat.SetColor("_EmissionColor", glow);
                mat.EnableKeyword("_EMISSION");
                mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            }

            mat.enableInstancing = true;
            EditorUtility.SetDirty(mat);
            return mat;
        }

        /// <summary>FBX 재질 이름 → 역할 (블렌더가 "Gold" · "Gold.001" 등으로 내보낸다)</summary>
        public static string RoleOf(Material m)
        {
            if (m == null) return "Black";
            string n = m.name;
            int dot = n.IndexOf('.');
            if (dot > 0) n = n.Substring(0, dot);
            return n.Trim();
        }

        /// <summary>
        /// 캐릭터(조립 중인 옷 모델 인스턴스)에 소품을 붙인다. source = 소품을 맞춘 기본 몸 FBX.
        /// 반환: 문제가 있으면 알림 (없으면 빈 글)
        /// </summary>
        public static string Attach(GameObject target, string propName, bool castShadows)
        {
            var prop = AssetDatabase.LoadAssetAtPath<GameObject>($"{Folder}/{propName}.fbx");
            if (prop == null) return $"{propName}: 소품 없음";
            if (!Bones.TryGetValue(propName, out string boneName)) return $"{propName}: 뼈 모름";
            string basisName = Basis.TryGetValue(propName, out string b) ? b : "Superhero_Male_FullBody";
            var basis = AssetDatabase.LoadAssetAtPath<GameObject>($"{Art3DAssets.Models}/{basisName}.fbx");
            Transform sourceBone = Find(basis.transform, boneName);
            Transform targetBone = Find(target.transform, boneName);
            if (sourceBone == null || targetBone == null) return $"{propName}: 뼈 {boneName} 없음";

            // 기본 몸 바인드 자세에서 뼈 기준 자리 (두 FBX 모두 원점 · 회전 없음 · 배율 1)
            Matrix4x4 toBone = sourceBone.worldToLocalMatrix;
            var holder = new GameObject(propName);
            holder.transform.SetParent(targetBone, false);
            int parts = 0;
            foreach (MeshRenderer r in prop.GetComponentsInChildren<MeshRenderer>(true))
            {
                var filter = r.GetComponent<MeshFilter>();
                if (filter == null || filter.sharedMesh == null) continue;
                Matrix4x4 local = toBone * r.transform.localToWorldMatrix;
                var part = new GameObject(r.name);
                part.transform.SetParent(holder.transform, false);
                part.transform.localPosition = local.GetColumn(3);
                part.transform.localRotation = local.rotation;
                part.transform.localScale = local.lossyScale;
                part.AddComponent<MeshFilter>().sharedMesh = filter.sharedMesh;
                var renderer = part.AddComponent<MeshRenderer>();
                Material[] slots = r.sharedMaterials;
                var mats = new Material[slots.Length];
                for (int i = 0; i < slots.Length; i++) mats[i] = RoleMaterial(RoleOf(slots[i]));
                renderer.sharedMaterials = mats;
                renderer.shadowCastingMode = castShadows ? UnityEngine.Rendering.ShadowCastingMode.On : UnityEngine.Rendering.ShadowCastingMode.Off;
                parts++;
            }

            if (parts > 0 && Turns.TryGetValue(propName, out Vector3 turn)) TurnAroundCenter(holder.transform, Quaternion.Euler(turn));
            return parts > 0 ? "" : $"{propName}: 메시 없음";
        }

        /// <summary>
        /// 바인드 자세(T자세)에 맞춰 만든 소품이 서 있는 동작에서 눕는 것 → 소품 가운데를 축으로 돌려 바로 세운다 (뼈 기준 회전).
        /// 방패: 팔을 내리면 T자세의 세로가 가로가 된다 → 90° (사진으로 고른 값)
        /// </summary>
        private static readonly Dictionary<string, Vector3> Turns = new Dictionary<string, Vector3>
        {
            ["Enemy_TowerShield"] = new Vector3(0f, 0f, 90f),
        };

        /// <summary>holder 아래 조각들의 가운데에 축을 두고 돌린다 (자리는 그대로, 방향만)</summary>
        private static void TurnAroundCenter(Transform holder, Quaternion turn)
        {
            var parts = new List<Transform>();
            Vector3 center = Vector3.zero;
            foreach (Transform part in holder)
            {
                var filter = part.GetComponent<MeshFilter>();
                Vector3 meshCenter = filter != null && filter.sharedMesh != null ? filter.sharedMesh.bounds.center : Vector3.zero;
                center += part.localPosition + part.localRotation * Vector3.Scale(meshCenter, part.localScale);
                parts.Add(part);
            }

            if (parts.Count == 0) return;
            center /= parts.Count;
            var pivot = new GameObject("Pivot").transform;
            pivot.SetParent(holder, false);
            pivot.localPosition = center;
            foreach (Transform part in parts)
            {
                part.SetParent(pivot, true);
            }

            pivot.localRotation = turn;
        }

        private static Transform Find(Transform root, string name)
        {
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
            {
                if (t.name == name) return t;
            }

            return null;
        }
    }
}
