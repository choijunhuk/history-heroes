using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace HistoryHeroes.EditorTools
{
    /// <summary>
    /// 무대·스킬 3D 소품 (그래픽 평가 2·4번, 개발자: "맵도 너무 단조로운거 같은데"): 블렌더 창이 만든 FBX를 가져와 프리팹으로.
    /// - Fx: 회전 칼날 · 부메랑 · 얼음 파편 · 유성 · 방패 / Deco: 상자 3종 · 돌덩이 · 돌기둥 · 아치 문 · 벽가 장식 · 풀밭 장식 / Floor: 밟고 지나가는 바닥 장식
    /// - 재질 이름 = 역할 (Art3DProps.RoleMaterial). 프리팹 안에서 모델을 세워(-90°) 게임의 XY 바닥·-Z 위에 맞춘다 → 그냥 놓으면 된다.
    /// - 원본은 D:\HH_Blender (블렌더 창 작업 폴더). 없으면 이미 복사한 파일로 프리팹만 다시 만든다.
    /// </summary>
    public static class Art3DStage
    {
        public const string Root = Art3DAssets.Root + "/Stage";
        public const string PrefabDir = Root + "/Prefabs";

        private static readonly (string source, string folder)[] Sources =
        {
            (@"D:\HH_Blender\Fx", Root + "/Fx"),
            (@"D:\HH_Blender\Deco", Root + "/Deco"),
            (@"D:\HH_Blender\Floor", Root + "/Floor"),
        };

        /// <summary>모델 앞쪽 돌리기 (FBX 기준). 얼음 파편·칼날은 끝이 Unity -X라 날아가는 쪽(+X)으로 돌린다</summary>
        private static readonly Dictionary<string, float> TurnY = new Dictionary<string, float>
        {
            ["Fx_IceShard"] = 180f, ["Fx_Blade"] = 180f,
        };

        /// <summary>그림자를 드리우지 않는 것 (바닥 장식 · 작은 풀 · 스킬 소품은 가볍게)</summary>
        private static bool CastsShadow(string name) =>
            !(name.StartsWith("Fx_") || name.StartsWith("Floor_") || name == "Puddle_Mesh" || name == "Bones" || name == "Weeds"
              || name == "Deco_GrassTuft" || name == "Deco_Flowers");

        [MenuItem("History Heroes/3D 무대 소품 가져오기")]
        public static string BuildAll()
        {
            int copied = CopySources();
            AssetDatabase.Refresh();
            AssetUtil.EnsureFolder(PrefabDir);
            var made = new List<string>();
            foreach ((string _, string folder) in Sources)
            {
                if (!Directory.Exists(folder)) continue;
                foreach (string file in Directory.GetFiles(folder, "*.fbx"))
                {
                    string path = file.Replace('\\', '/');
                    ImportSettings(path);
                    if (MakePrefab(path) != null) made.Add(Path.GetFileNameWithoutExtension(path));
                }
            }

            int smoothed = SmoothProps();
            AssetDatabase.SaveAssets();
            return $"복사 {copied} · 프리팹 {made.Count} · 소품 부드럽게 {smoothed}: {string.Join(", ", made)}";
        }

        /// <summary>바뀐 FBX만 복사 (블렌더 창이 다시 내보내면 덮어쓴다)</summary>
        private static int CopySources()
        {
            int copied = 0;
            foreach ((string source, string folder) in Sources)
            {
                if (!Directory.Exists(source)) continue;
                AssetUtil.EnsureFolder(folder);
                foreach (string file in Directory.GetFiles(source, "*.fbx"))
                {
                    string target = Path.Combine(folder, Path.GetFileName(file));
                    if (File.Exists(target) && File.GetLastWriteTimeUtc(target) >= File.GetLastWriteTimeUtc(file)) continue;
                    File.Copy(file, target, true);
                    copied++;
                }
            }

            return copied;
        }

        private static void ImportSettings(string path)
        {
            if (!(AssetImporter.GetAtPath(path) is ModelImporter importer)) return;
            bool smooth = importer.importNormals == ModelImporterNormals.Calculate && Mathf.Approximately(importer.normalSmoothingAngle, SmoothAngle);
            if (importer.animationType == ModelImporterAnimationType.None && !importer.importAnimation && !importer.isReadable && smooth) return;
            importer.animationType = ModelImporterAnimationType.None;
            importer.importAnimation = false;
            importer.importBlendShapes = false;
            importer.isReadable = false;
            importer.materialImportMode = ModelImporterMaterialImportMode.ImportViaMaterialDescription; // 재질 이름(역할)을 읽으려고
            SetSmooth(importer, SmoothAngle);
            importer.SaveAndReimport();
        }

        /// <summary>
        /// 각진 면을 부드럽게 (개발자: "너무 각지다 애들이"): 면 사이 각이 이보다 작으면 이어서 둥글게 비춘다.
        /// 상자 모서리(90°)는 그대로 날카롭고, 통·나무·덤불·수정의 잔 면은 둥글게 보인다
        /// </summary>
        private const float SmoothAngle = 55f;

        public static void SetSmooth(ModelImporter importer, float angle)
        {
            importer.importNormals = ModelImporterNormals.Calculate;
            importer.normalCalculationMode = ModelImporterNormalCalculationMode.AngleWeighted;
            importer.normalSmoothingAngle = angle;
            importer.importTangents = ModelImporterTangents.CalculateMikk;
        }

        /// <summary>영웅 소품(모자·무기)·펫 FBX도 같은 방식으로 부드럽게 (무늬 없는 단색이라 이음매 걱정 없음)</summary>
        public static int SmoothProps()
        {
            int changed = 0;
            foreach (string folder in new[] { Art3DProps.Folder, Art3DAssets.Root + "/Pets" })
            {
                if (!Directory.Exists(folder)) continue;
                foreach (string file in Directory.GetFiles(folder, "*.fbx"))
                {
                    if (!(AssetImporter.GetAtPath(file.Replace('\\', '/')) is ModelImporter importer)) continue;
                    float angle = folder.EndsWith("Pets") ? 70f : 60f;
                    if (importer.importNormals == ModelImporterNormals.Calculate && Mathf.Approximately(importer.normalSmoothingAngle, angle)) continue;
                    SetSmooth(importer, angle);
                    importer.SaveAndReimport();
                    changed++;
                }
            }

            return changed;
        }

        private static GameObject MakePrefab(string fbxPath)
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(fbxPath);
            if (model == null) return null;
            string name = Path.GetFileNameWithoutExtension(fbxPath);
            var root = new GameObject(name);
            GameObject body = Object.Instantiate(model, root.transform, false);
            body.name = "Model";
            float turn = TurnY.TryGetValue(name, out float t) ? t : 0f;
            // 모델 위(+Y) → 게임 위(-Z), 모델 앞(-Z) → 화면 아래(-Y, 카메라 쪽)
            body.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f) * Quaternion.Euler(0f, turn, 0f);
            bool shadow = CastsShadow(name);
            foreach (Renderer r in body.GetComponentsInChildren<Renderer>(true))
            {
                Material[] mats = r.sharedMaterials;
                for (int i = 0; i < mats.Length; i++) mats[i] = Art3DProps.RoleMaterial(Art3DProps.RoleOf(mats[i]));
                r.sharedMaterials = mats;
                r.shadowCastingMode = shadow ? ShadowCastingMode.On : ShadowCastingMode.Off;
                r.receiveShadows = true;
                r.lightProbeUsage = LightProbeUsage.Off;
                r.reflectionProbeUsage = ReflectionProbeUsage.Off;
            }

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, $"{PrefabDir}/{name}.prefab");
            Object.DestroyImmediate(root);
            return prefab;
        }

        /// <summary>장면 빌더가 쓰는 프리팹 (없으면 null → 예전 상자·원기둥 모양)</summary>
        public static GameObject Prefab(string name) => AssetDatabase.LoadAssetAtPath<GameObject>($"{PrefabDir}/{name}.prefab");
    }
}
