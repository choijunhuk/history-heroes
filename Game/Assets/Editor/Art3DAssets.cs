using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace HistoryHeroes.EditorTools
{
    /// <summary>
    /// 3D 에셋 정리 (Art3D-Plan 1번): 게임에 쓰는 것만 Assets/Art/3D에 두고 git에 올린다.
    /// - 모델 FBX: 휴머노이드로 가져온다 (동작을 모든 몸이 같이 쓴다)
    /// - 동작: 큰 동작 묶음(UAL1·2, 각 24MB, git에 안 올림)에서 **쓰는 것만** .anim으로 뽑는다
    /// - 텍스처: 1024 이하, 노멀맵 표시
    /// </summary>
    public static class Art3DAssets
    {
        public const string Root = "Assets/Art/3D";
        public const string Models = Root + "/Models";
        public const string Textures = Root + "/Textures";
        public const string Animations = Root + "/Animations";
        private const string Source = "Assets/ThirdParty/Quaternius/Animations";

        /// <summary>게임에서 쓰는 동작 (FBX 안 이름 "Armature|이름" → Animations/이름.anim)</summary>
        public static readonly string[] Clips =
        {
            "Idle_Loop", "Walk_Loop", "Jog_Fwd_Loop", "Sprint_Loop",
            "Spell_Simple_Shoot", "Spell_Simple_Idle_Loop", "Pistol_Shoot", "Pistol_Idle_Loop",
            "Sword_Attack", "Sword_Idle", "Punch_Jab", "Punch_Cross",
            "Hit_Chest", "Hit_Head", "Death01", "Roll",
            "Sword_Regular_A", "Sword_Regular_B", "Sword_Regular_C", "Sword_Dash",
            "OverhandThrow", "Melee_Hook", "Hit_Knockback",
            "Zombie_Walk_Fwd_Loop", "Zombie_Scratch", "Zombie_Idle_Loop",
            "Shield_Dash", "Idle_Shield_Loop",
        };

        /// <summary>모델은 휴머노이드, 노멀맵 표시, 텍스처 1024</summary>
        public static string Import()
        {
            var log = new List<string>();
            foreach (string file in Directory.GetFiles(Models, "*.fbx"))
            {
                string path = file.Replace('\\', '/');
                var importer = AssetImporter.GetAtPath(path) as ModelImporter;
                if (importer == null) continue;
                bool hair = Path.GetFileName(path).StartsWith("Hair_") || Path.GetFileName(path).StartsWith("Eyebrows_");
                importer.animationType = hair ? ModelImporterAnimationType.Generic : ModelImporterAnimationType.Human;
                importer.avatarSetup = hair ? ModelImporterAvatarSetup.NoAvatar : ModelImporterAvatarSetup.CreateFromThisModel;
                importer.importAnimation = false;
                importer.materialImportMode = ModelImporterMaterialImportMode.ImportViaMaterialDescription;
                importer.SaveAndReimport();
                var avatar = AssetDatabase.LoadAssetAtPath<Avatar>(path);
                log.Add($"{Path.GetFileName(path)}: {(hair ? "머리카락(Generic)" : avatar != null && avatar.isHuman && avatar.isValid ? "휴머노이드 OK" : "휴머노이드 실패")}");
            }

            foreach (string file in Directory.GetFiles(Textures, "*.png"))
            {
                var importer = AssetImporter.GetAtPath(file.Replace('\\', '/')) as TextureImporter;
                if (importer == null) continue;
                bool normal = file.Contains("_Normal");
                if (importer.maxTextureSize == 1024 && (importer.textureType == TextureImporterType.NormalMap) == normal) continue;
                importer.textureType = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
                importer.maxTextureSize = 1024;
                importer.SaveAndReimport();
            }

            return string.Join("\n", log);
        }

        /// <summary>쓰는 동작만 .anim으로 (휴머노이드 근육 곡선 그대로 — 어느 휴머노이드 몸에도 맞는다)</summary>
        public static string ExtractClips()
        {
            Directory.CreateDirectory(Animations);
            var found = new Dictionary<string, AnimationClip>();
            foreach (string fbx in new[] { $"{Source}/UAL1_Standard.fbx", $"{Source}/UAL2_Standard.fbx" })
            {
                foreach (Object o in AssetDatabase.LoadAllAssetsAtPath(fbx))
                {
                    if (o is AnimationClip c && c.name.StartsWith("Armature|")) found[c.name.Substring("Armature|".Length)] = c;
                }
            }

            if (found.Count == 0) return "동작 묶음이 없다 (Assets/ThirdParty/Quaternius/Animations — 받은 원본, git에 없음)";
            var log = new StringBuilder();
            long bytes = 0;
            foreach (string name in Clips)
            {
                if (!found.TryGetValue(name, out AnimationClip clip))
                {
                    log.AppendLine("없음 " + name);
                    continue;
                }

                string path = $"{Animations}/{name}.anim";
                AnimationClip copy = Object.Instantiate(clip);
                copy.name = name;
                var existing = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
                if (existing != null)
                {
                    EditorUtility.CopySerialized(copy, existing); // 그 자리에서 (GUID 그대로 — 컨트롤러 연결 유지, Codex 3D 리뷰 10번)
                    existing.name = name;
                    EditorUtility.SetDirty(existing);
                    Object.DestroyImmediate(copy);
                }
                else
                {
                    AssetDatabase.CreateAsset(copy, path);
                }
                bytes += new FileInfo(path).Length;
            }

            AssetDatabase.SaveAssets();
            log.AppendLine($"{Clips.Length}개 · {bytes / 1e6:0.0}MB");
            return log.ToString();
        }

        /// <summary>모델마다 렌더러 · 재질 칸 · 뼈 이름 일부 (머리 붙이기 전에 뼈 이름이 같은지 본다)</summary>
        public static string Describe(string file)
        {
            var go = AssetDatabase.LoadAssetAtPath<GameObject>($"{Models}/{file}.fbx");
            if (go == null) return "없음 " + file;
            var sb = new StringBuilder(file).Append('\n');
            foreach (var r in go.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                sb.Append($"  {r.name} 꼭짓점 {r.sharedMesh.vertexCount} 칸 {r.sharedMesh.subMeshCount} 뼈 {r.bones.Length} 뿌리 {(r.rootBone != null ? r.rootBone.name : "-")} [");
                foreach (Material m in r.sharedMaterials) sb.Append(m != null ? m.name : "null").Append(',');
                sb.Append("]\n");
            }

            var names = new List<string>();
            foreach (Transform t in go.GetComponentsInChildren<Transform>(true))
            {
                if (t.name.Contains("Head") || t.name.Contains("Neck") || t.name.Contains("neck") || t.name.Contains("head")) names.Add(t.name);
            }

            sb.Append("  머리·목 뼈: ").Append(string.Join(", ", names));
            return sb.ToString();
        }
    }
}
