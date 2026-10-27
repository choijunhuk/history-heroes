using HistoryHeroes.Combat;
using HistoryHeroes.Common;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace HistoryHeroes.EditorTools
{
    /// <summary>
    /// 투사체·줍는 것 3D (Art3D-Plan 6번): 화살 · 총알 · 전기 구체 · 얼음 파편 · 경험치 보석.
    /// 판정은 2D 그대로 (스프라이트 오브젝트의 Projectile · 원 판정). 2D 그림은 숨기고 3D 모형을 가슴 높이(-Z)에 띄운다.
    /// 투사체는 transform.right = 날아가는 쪽이라 모형도 +X를 본다. 그림자는 끈다 (탄막이 많다).
    /// </summary>
    public static partial class SceneBuilder
    {
        private enum Fx
        {
            None,
            Arrow,
            EnemyArrow,
            Bullet,
            Orb,
            Shard,
            Wave,
        }

        /// <summary>투사체가 뜨는 높이 (가슴께)</summary>
        private const float ProjectileHeight = 0.9f;

        private static Material FxMat(string name, Color color, float emission = 0f, float metallic = 0f)
        {
            string path = $"{Art3DAssets.Root}/Materials/M_Fx_{name}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(mat, path);
            }

            mat.SetColor("_BaseColor", color);
            mat.SetFloat("_Metallic", metallic);
            mat.SetFloat("_Smoothness", metallic > 0f ? 0.6f : 0.25f);
            if (emission > 0f)
            {
                mat.SetColor("_EmissionColor", color * emission);
                mat.EnableKeyword("_EMISSION");
                mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            }
            else
            {
                mat.DisableKeyword("_EMISSION");
            }

            mat.enableInstancing = true;
            EditorUtility.SetDirty(mat);
            return mat;
        }

        /// <summary>내장 메시 하나를 원하는 크기(로컬 축 기준, 유닛)로 붙인다. 내장 메시 크기가 제각각이라 메시 크기로 나눈다</summary>
        private static Transform FxPart(Transform parent, Stage3D.Shape shape, Material mat, Vector3 position, Quaternion rotation, Vector3 size)
        {
            Mesh mesh = shape == Stage3D.Shape.Floor ? null : Resources.GetBuiltinResource<Mesh>(Stage3D.BuiltinName(shape));
            return FxPart(parent, mesh, mat, position, rotation, size);
        }

        private static Transform FxPart(Transform parent, Mesh mesh, Material mat, Vector3 position, Quaternion rotation, Vector3 size)
        {
            var go = new GameObject(mesh != null ? mesh.name : "Part");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            go.transform.localRotation = rotation;
            Vector3 ms = mesh != null ? mesh.bounds.size : Vector3.one;
            go.transform.localScale = new Vector3(size.x / Mathf.Max(0.0001f, ms.x), size.y / Mathf.Max(0.0001f, ms.y), size.z / Mathf.Max(0.0001f, ms.z));
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = false;
            return go.transform;
        }

        /// <summary>투사체 프리팹(배율 = 2D 크기)에 3D 모형을 붙이고 2D 그림을 숨긴다</summary>
        private static void DressProjectile(GameObject go, Vector2 size, Fx fx)
        {
            if (fx == Fx.None) return;
            var root = new GameObject("Model3D").transform;
            root.SetParent(go.transform, false);
            root.localScale = new Vector3(1f / Mathf.Max(0.01f, size.x), 1f / Mathf.Max(0.01f, size.y), 1f); // 2D 배율을 되돌려 유닛 그대로
            root.localPosition = new Vector3(0f, 0f, -ProjectileHeight);
            Quaternion alongX = Quaternion.Euler(0f, 0f, 90f); // 원기둥(Y축)을 날아가는 쪽(+X)으로 눕힌다
            switch (fx)
            {
                case Fx.Arrow:
                case Fx.EnemyArrow:
                {
                    bool enemy = fx == Fx.EnemyArrow;
                    Material shaft = enemy ? FxMat("EnemyShaft", new Color(0.95f, 0.2f, 0.55f), 1.2f) : FxMat("ArrowShaft", new Color(0.55f, 0.38f, 0.2f));
                    Material head = enemy ? FxMat("EnemyHead", new Color(1f, 0.45f, 0.7f), 1.5f) : FxMat("ArrowHead", new Color(0.82f, 0.84f, 0.88f), 0f, 0.8f);
                    Material fletch = enemy ? head : FxMat("Fletch", new Color(0.95f, 0.95f, 0.92f));
                    FxPart(root, Stage3D.Shape.Pillar, shaft, Vector3.zero, alongX, new Vector3(0.05f, 0.62f, 0.05f));
                    FxPart(root, Stage3D.Shape.Box, head, new Vector3(0.33f, 0f, 0f), Quaternion.Euler(0f, 0f, 45f), new Vector3(0.11f, 0.11f, 0.04f));
                    FxPart(root, Stage3D.Shape.Box, fletch, new Vector3(-0.27f, 0f, 0f), Quaternion.identity, new Vector3(0.13f, 0.015f, 0.1f));
                    FxPart(root, Stage3D.Shape.Box, fletch, new Vector3(-0.27f, 0f, 0f), Quaternion.Euler(90f, 0f, 0f), new Vector3(0.13f, 0.015f, 0.1f));
                    break;
                }
                case Fx.Bullet:
                    FxPart(root, Stage3D.Shape.Rock, FxMat("Bullet", new Color(1f, 0.82f, 0.35f), 1.4f, 0.6f), Vector3.zero, Quaternion.identity, new Vector3(0.26f, 0.13f, 0.13f));
                    break;
                case Fx.Orb:
                    FxPart(root, Stage3D.Shape.Rock, FxMat("Orb", new Color(0.5f, 0.88f, 1f), 2.2f), Vector3.zero, Quaternion.identity, Vector3.one * 0.34f);
                    break;
                case Fx.Wave:
                    // 관우 검기: 날아가는 쪽에 가로로 선 빛나는 날 (얇은 상자 + 끝이 가는 두 조각)
                    Material wave = FxMat("Wave", new Color(0.8f, 1f, 0.9f), 1.6f);
                    FxPart(root, Stage3D.Shape.Box, wave, Vector3.zero, Quaternion.identity, new Vector3(0.12f, 0.6f, 0.08f));
                    FxPart(root, Stage3D.Shape.Box, wave, new Vector3(-0.05f, 0.38f, 0f), Quaternion.Euler(0f, 0f, -25f), new Vector3(0.08f, 0.22f, 0.06f));
                    FxPart(root, Stage3D.Shape.Box, wave, new Vector3(-0.05f, -0.38f, 0f), Quaternion.Euler(0f, 0f, 25f), new Vector3(0.08f, 0.22f, 0.06f));
                    break;
                case Fx.Shard:
                    FxPart(root, Stage3D.Shape.Box, FxMat("Ice", new Color(0.65f, 0.9f, 1f), 1.3f), Vector3.zero, Quaternion.Euler(45f, 0f, 0f), new Vector3(0.44f, 0.09f, 0.09f));
                    break;
            }

            var sr = go.GetComponent<SpriteRenderer>();
            if (sr != null) sr.enabled = false;
        }

        /// <summary>경험치 보석: 2D 원은 숨기고 빛나는 팔면체 보석이 땅 위에서 돌며 오르내린다 (3D 화면 점검 4번)</summary>
        private static void DressGem(GameObject go)
        {
            float rootScale = Mathf.Max(0.01f, go.transform.localScale.x);
            var model = new GameObject("Model3D").transform;
            model.SetParent(go.transform, false);
            model.localScale = Vector3.one / rootScale;
            model.localPosition = new Vector3(0f, 0f, -0.32f / rootScale);
            FxPart(model, GemMesh(), FxMat("Gem", new Color(0.35f, 1f, 0.75f), 1.8f), Vector3.zero, Quaternion.identity, new Vector3(0.22f, 0.22f, 0.34f));
            // 게임 중 보석 배율은 떨굴 때 0.45(Z는 1), 합치면 커진다 → 월드 크기 맞추기로 높이 0.32에 뜨고 커진 만큼만 커진다
            using (var props = new Props(model.gameObject.AddComponent<Spin3D>())) props.Float("sizeReference", 0.45f).Vector3("worldOffset", new Vector3(0f, 0f, -0.32f));
            go.GetComponent<SpriteRenderer>().enabled = false;
        }

        /// <summary>팔면체 (위아래가 -Z/+Z로 뾰족, 지름 1). 면마다 꼭짓점을 따로 둬 각진 그림자</summary>
        private static Mesh GemMesh()
        {
            const string path = Art3DAssets.Root + "/Meshes/Gem.asset";
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            bool created = mesh == null;
            if (created) mesh = new Mesh { name = "Gem" };
            Vector3 top = new Vector3(0f, 0f, -0.5f), bottom = new Vector3(0f, 0f, 0.5f);
            Vector3[] ring = { new Vector3(0.5f, 0f, 0f), new Vector3(0f, 0.5f, 0f), new Vector3(-0.5f, 0f, 0f), new Vector3(0f, -0.5f, 0f) };
            var verts = new System.Collections.Generic.List<Vector3>();
            var tris = new System.Collections.Generic.List<int>();
            for (int i = 0; i < 4; i++)
            {
                Vector3 a = ring[i], b = ring[(i + 1) % 4];
                foreach (Vector3[] face in new[] { new[] { top, b, a }, new[] { bottom, a, b } })
                {
                    int start = verts.Count;
                    verts.AddRange(face);
                    tris.Add(start);
                    tris.Add(start + 1);
                    tris.Add(start + 2);
                }
            }

            mesh.Clear();
            mesh.SetVertices(verts);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            if (created) AssetDatabase.CreateAsset(mesh, path);
            else EditorUtility.SetDirty(mesh);
            return mesh;
        }
    }
}
