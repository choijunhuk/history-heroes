using UnityEngine;
using UnityEngine.Rendering;

namespace HistoryHeroes.Common
{
    /// <summary>
    /// 3D 무대 (Art3D-Plan 3번): 벽·장애물·바닥 같은 2D 판정 오브젝트에 **보이기만 하는** 3D 모양을 붙인다.
    /// - 판정(Collider2D)은 그대로. 3D 모양에는 3D 충돌이 없다.
    /// - 스프라이트 오브젝트의 x·y 배율 = 크기. 3D 모양은 자식("Shape3D")으로, 높이는 -Z(위)로 솟는다.
    /// - 장면 빌더(에디터)와 방 레이아웃(게임 중, DungeonRun)이 같이 쓴다. 게임 중 재질·메시는 전투 장면의 Stage3D가 들고 있다.
    /// - CreatePrimitive는 쓰지 않는다: 빌드는 안 쓰는 엔진 코드(3D 충돌체)를 빼서 폰에서 실패할 수 있다. 기본 메시를 장면이 들고 있게 한다.
    /// </summary>
    public sealed class Stage3D : MonoBehaviour
    {
        public const string ShapeName = "Shape3D";

        [SerializeField] private Material stone;
        [SerializeField] private Material crate;
        [SerializeField] private Material rock;
        [Header("기본 메시 (빌더가 넣는다)")]
        [SerializeField] private Mesh cube;
        [SerializeField] private Mesh cylinder;
        [SerializeField] private Mesh sphere;
        [SerializeField] private Mesh quad;

        private static Stage3D _instance;
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static MaterialPropertyBlock _block;

        public static bool On => _instance != null;

        private void OnEnable() => _instance = this;

        private void OnDisable()
        {
            if (_instance == this) _instance = null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => _instance = null;

        public enum Kind
        {
            /// <summary>높은 장애물: 네모는 돌덩이, 동그라미는 돌기둥</summary>
            High,
            /// <summary>부서지는 상자</summary>
            Crate,
            /// <summary>서바이벌 바위 (둥글고 낮게)</summary>
            Rock,
        }

        /// <summary>게임 중 (방 레이아웃): 장애물 스프라이트에 3D 모양을 붙이고 스프라이트를 숨긴다. Stage3D가 없으면 2D 그대로</summary>
        public static void Dress(SpriteRenderer sprite, Kind kind, bool circle)
        {
            if (_instance == null || sprite == null) return;
            Material mat = StageLook.Resolve(kind == Kind.Crate ? _instance.crate : kind == Kind.Rock ? _instance.rock : _instance.stone); // 구간 색을 입힌 사본
            Vector3 s = sprite.transform.lossyScale;
            float size = Mathf.Max(s.x, s.y);
            float height = kind == Kind.Crate ? Mathf.Min(1.1f, size) : kind == Kind.Rock ? size * 0.55f : circle ? 1.8f : 1.5f;
            Add(sprite.transform, kind == Kind.Rock ? Shape.Rock : circle ? Shape.Pillar : Shape.Box, height, mat, castShadows: true);
            sprite.enabled = false;
        }

        public enum Shape
        {
            Box,
            Pillar,
            Rock,
            Floor,
        }

        /// <summary>
        /// owner(스프라이트 오브젝트, x·y 배율 = 크기, 지름 1 기준) 밑에 3D 모양을 붙인다. 높이는 월드 유닛.
        /// 바닥(Floor)은 땅보다 아주 조금 아래(+Z)라 바닥 스프라이트(예고·함정)가 위에 보인다.
        /// </summary>
        public static GameObject Add(Transform owner, Shape shape, float height, Material material, bool castShadows)
        {
            var go = new GameObject(ShapeName);
            Mesh mesh = MeshFor(shape);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>();
            // 내장 메시 크기가 제각각이다 (상자·판 1, 원기둥·공 2) → 메시 크기로 나눠 지름 1로 맞춘다
            Vector3 ms = mesh != null ? mesh.bounds.size : Vector3.one;
            ms = new Vector3(Mathf.Max(0.0001f, ms.x), Mathf.Max(0.0001f, ms.y), Mathf.Max(0.0001f, ms.z));

            float ownerZ = Mathf.Max(0.0001f, Mathf.Abs(owner.lossyScale.z));
            go.transform.SetParent(owner, false);
            switch (shape)
            {
                case Shape.Box:
                    go.transform.localPosition = new Vector3(0f, 0f, -height * 0.5f / ownerZ);
                    go.transform.localScale = new Vector3(1f / ms.x, 1f / ms.y, height / ownerZ / ms.z);
                    break;
                case Shape.Pillar:
                    // 원기둥은 Y축이 높이 → X축으로 90° 눕혀 -Z(위)로 세운다
                    go.transform.localPosition = new Vector3(0f, 0f, -height * 0.5f / ownerZ);
                    go.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                    go.transform.localScale = new Vector3(1f / ms.x, height / ownerZ / ms.y, 1f / ms.z);
                    break;
                case Shape.Rock:
                    // 납작한 공을 땅에 반쯤 묻는다
                    go.transform.localPosition = new Vector3(0f, 0f, -height * 0.35f / ownerZ);
                    go.transform.localScale = new Vector3(1.05f / ms.x, 1.05f / ms.y, height / ownerZ / ms.z);
                    break;
                case Shape.Floor:
                    go.transform.localPosition = new Vector3(0f, 0f, 0.02f / ownerZ);
                    break;
            }

            var r = go.GetComponent<MeshRenderer>();
            r.sharedMaterial = material;
            r.shadowCastingMode = castShadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
            r.receiveShadows = true;
            return go;
        }

        /// <summary>모양별 기본 메시: 게임 중에는 장면의 Stage3D가 든 것, 에디터에서는 내장 메시</summary>
        public static Mesh MeshFor(Shape shape)
        {
            if (_instance != null)
            {
                Mesh m = shape == Shape.Pillar ? _instance.cylinder : shape == Shape.Rock ? _instance.sphere : shape == Shape.Floor ? _instance.quad : _instance.cube;
                if (m != null) return m;
            }

#if UNITY_EDITOR
            return Resources.GetBuiltinResource<Mesh>(BuiltinName(shape));
#else
            return null;
#endif
        }

        public static string BuiltinName(Shape shape) => shape == Shape.Pillar ? "Cylinder.fbx" : shape == Shape.Rock ? "Sphere.fbx" : shape == Shape.Floor ? "Quad.fbx" : "Cube.fbx";

        /// <summary>부서지는 상자가 깎인 정도 (1 = 새것, 0 = 곧 부서짐): 어둡게</summary>
        public static void ShowWear(Transform owner, float left)
        {
            Transform shape = owner != null ? owner.Find(ShapeName) : null;
            if (shape == null || !shape.TryGetComponent(out MeshRenderer r)) return;
            if (left >= 0.999f)
            {
                r.SetPropertyBlock(null);
                return;
            }

            _block ??= new MaterialPropertyBlock();
            Color baseColor = r.sharedMaterial != null && r.sharedMaterial.HasProperty(BaseColorId) ? r.sharedMaterial.GetColor(BaseColorId) : Color.white;
            _block.Clear();
            _block.SetColor(BaseColorId, baseColor * Mathf.Lerp(0.45f, 1f, left));
            r.SetPropertyBlock(_block);
        }
    }
}
