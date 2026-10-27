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
        };

        /// <summary>역할 이름 → 색 (sRGB) · 금속 여부 (블렌더 notes.txt의 색)</summary>
        private static readonly Dictionary<string, (Color color, bool metal)> Roles = new Dictionary<string, (Color, bool)>
        {
            ["Fur"] = (Hex(0x6F5238), false), ["Cloth"] = (Hex(0x307C3F), false), ["Gold"] = (Hex(0xE7C461), true),
            ["Wood"] = (Hex(0x815938), false), ["Metal"] = (Hex(0xC4C7CB), true), ["Black"] = (Hex(0x212124), false),
            ["Copper"] = (Hex(0xCE8955), true), ["Hair"] = (Hex(0x453527), false), ["Red"] = (Hex(0x992727), false),
            ["White"] = (Hex(0xE7E5DA), false), ["Blue"] = (Hex(0x2745A0), false),
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

        private static Material RoleMaterial(string role)
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
            mat.SetFloat("_Smoothness", r.metal ? 0.6f : 0.15f);
            if (role == "Copper")
            {
                // 테슬라 코일만 구리를 쓴다 → 푸른 전기 빛 (멀리서도 테슬라로 보이게 — Codex 화면 점검 5번)
                mat.SetColor("_EmissionColor", new Color(0.35f, 0.75f, 1f) * 1.6f);
                mat.EnableKeyword("_EMISSION");
                mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            }

            mat.enableInstancing = true;
            EditorUtility.SetDirty(mat);
            return mat;
        }

        /// <summary>FBX 재질 이름 → 역할 (블렌더가 "Gold" · "Gold.001" 등으로 내보낸다)</summary>
        private static string RoleOf(Material m)
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
            var basis = AssetDatabase.LoadAssetAtPath<GameObject>($"{Art3DAssets.Models}/Superhero_Male_FullBody.fbx");
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

            return parts > 0 ? "" : $"{propName}: 메시 없음";
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
