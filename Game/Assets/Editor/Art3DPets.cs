using System.Collections.Generic;
using System.IO;
using HistoryHeroes.Combat;
using UnityEditor;
using UnityEngine;

namespace HistoryHeroes.EditorTools
{
    /// <summary>
    /// 펫 3D (Art3D-Plan 5번): 사냥매 · 적토마 · 마렝고 · 흰 비둘기. 블렌더 세션이 만든 고정 메시 (`Assets/Art/3D/Pets`, 직접 만든 것).
    /// 뼈·동작은 없다 → 게임에서는 Visual3D가 움직이는 쪽으로 돌리고, 몸통은 Spin3D(돌기 0)로 살짝 오르내린다 (새는 크게, 말은 작게).
    /// 색은 소품과 같이 재질 이름(역할)으로 고른다 (Art3DProps.RoleMaterial).
    /// <see cref="Art3DCharacters.BuildAll"/>이 같이 만든다. FBX가 아직 없으면 그 펫은 건너뛴다 (임시 도형 그대로).
    /// </summary>
    public static class Art3DPets
    {
        public const string Folder = Art3DAssets.Root + "/Pets";

        /// <summary>펫 id → (FBX 이름, 게임 속 키, 오르내림, 더 띄우는 높이 — 새는 영웅 어깨께로 날게, 월드 유닛)</summary>
        private static readonly Dictionary<string, (string file, float height, float bob, float lift)> Table = new Dictionary<string, (string, float, float, float)>
        {
            // 화면 점검 2차: 전투 크기에서 매·말로 읽히게 키웠다
            ["hawk"] = ("Pet_Hawk", 0.78f, 0.08f, 0.75f),
            ["red_hare"] = ("Pet_RedHare", 1.5f, 0.025f, 0f),
            ["marengo"] = ("Pet_Marengo", 1.4f, 0.025f, 0f),
            ["dove"] = ("Pet_Dove", 0.62f, 0.07f, 0.7f),
        };

        public static string BuildAll()
        {
            if (!Directory.Exists(Folder)) return "펫 모델 없음";
            var log = new List<string>();
            foreach (KeyValuePair<string, (string file, float height, float bob, float lift)> pair in Table)
            {
                string fbxPath = $"{Folder}/{pair.Value.file}.fbx";
                var importer = AssetImporter.GetAtPath(fbxPath) as ModelImporter;
                if (importer == null) continue;
                if (importer.animationType != ModelImporterAnimationType.None || importer.importAnimation)
                {
                    importer.animationType = ModelImporterAnimationType.None;
                    importer.importAnimation = false;
                    importer.materialImportMode = ModelImporterMaterialImportMode.ImportViaMaterialDescription;
                    importer.SaveAndReimport();
                }

                var fbx = AssetDatabase.LoadAssetAtPath<GameObject>(fbxPath);
                var root = new GameObject(pair.Value.file + "3D");
                var body = new GameObject("Body").transform; // 오르내리는 몸통 (뿌리는 Visual3D가 돌린다)
                body.SetParent(root.transform, false);
                foreach (MeshRenderer r in fbx.GetComponentsInChildren<MeshRenderer>(true))
                {
                    var filter = r.GetComponent<MeshFilter>();
                    if (filter == null || filter.sharedMesh == null) continue;
                    var part = new GameObject(r.name);
                    part.transform.SetParent(body, false);
                    Matrix4x4 m = r.transform.localToWorldMatrix; // FBX 뿌리 = 원점 (블렌더에서 그 자리로 내보냄)
                    part.transform.localPosition = m.GetColumn(3);
                    part.transform.localRotation = m.rotation;
                    part.transform.localScale = m.lossyScale;
                    part.AddComponent<MeshFilter>().sharedMesh = filter.sharedMesh;
                    var renderer = part.AddComponent<MeshRenderer>();
                    Material[] slots = r.sharedMaterials;
                    var mats = new Material[slots.Length];
                    for (int i = 0; i < slots.Length; i++) mats[i] = Art3DProps.RoleMaterial(Art3DProps.RoleOf(slots[i]));
                    renderer.sharedMaterials = mats;
                }

                var spin = body.gameObject.AddComponent<Spin3D>();
                using (var props = new Props(spin)) props.Float("degreesPerSecond", 0f).Float("bobHeight", pair.Value.bob).Float("bobSpeed", 2.5f).Bool("yUp", true);

                var info = root.AddComponent<Model3DInfo>();
                var bounds = new Bounds(Vector3.zero, Vector3.zero);
                foreach (Renderer r in root.GetComponentsInChildren<Renderer>()) bounds.Encapsulate(r.bounds);
                info.height = Mathf.Max(0.1f, bounds.max.y);
                info.castShadows = true;
                // 새는 더 높이 난다: 게임 속 배율(키 / 원래 키)을 되돌려 모델 공간에서 띄운다 (키는 띄우기 전에 쟀다)
                if (pair.Value.lift > 0f) body.localPosition = new Vector3(0f, pair.Value.lift * info.height / pair.Value.height, 0f);

                string path = $"{Folder}/{root.name}.prefab";
                PrefabUtility.SaveAsPrefabAsset(root, path);
                Object.DestroyImmediate(root);
                log.Add($"{path} (키 {info.height:0.00})");
            }

            AssetDatabase.SaveAssets();
            return log.Count > 0 ? string.Join("\n", log) : "펫 모델 없음";
        }

        /// <summary>펫 모델 (id = PetData.id) · 게임 속 키. 아직 없으면 null</summary>
        public static GameObject Model(string id, out float height)
        {
            height = 0.7f;
            if (!Table.TryGetValue(id, out (string file, float height, float bob, float lift) entry)) return null;
            height = entry.height;
            return AssetDatabase.LoadAssetAtPath<GameObject>($"{Folder}/{entry.file}3D.prefab");
        }
    }
}
