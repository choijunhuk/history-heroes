using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace HistoryHeroes.Common
{
    /// <summary>
    /// 구간마다 다른 빛·색 (그래픽 평가 1번 묶음, Docs/Reviews/2026-10-27-graphics-eval.md).
    /// 해 색·밝기, 주변광, 바닥·벽 색, 화면 후처리(색 보정)를 구간(1~5)·서바이벌마다 바꾼다.
    /// 바닥·벽 재질은 게임 중에만 사본을 만들어 색을 입힌다 → 에셋 파일은 그대로.
    /// 그래픽 "낮음"은 후처리를 끈다 (SettingsApplier).
    /// </summary>
    public sealed class StageLook : MonoBehaviour
    {
        [Serializable]
        public struct Look
        {
            public string key;
            [Tooltip("바닥 재질 색에 곱한다")] public Color floor;
            [Tooltip("벽·돌기둥 재질 색에 곱한다")] public Color wall;
            public Color sun;
            public float sunIntensity;
            public Color ambient;
            [Tooltip("화면 전체 색 (후처리)")] public Color filter;
            public float exposure;
            public float contrast;
            public float saturation;
            [Tooltip("방 밖 빈 곳 (카메라 배경)")] public Color background;
        }

        /// <summary>방마다 흩뿌리는 바닥 장식 (그래픽 평가 4번): flat = 방 안 아무 데나 (아주 납작하게), edge = 벽 가장자리</summary>
        [Serializable]
        public struct FloorSet
        {
            public string key;
            public GameObject[] flat;
            public GameObject[] edge;
        }

        [SerializeField] private Light sun;
        [SerializeField] private Volume volume;
        [SerializeField] private Camera viewCamera;
        [SerializeField] private Material[] floorMaterials;
        [SerializeField] private Material[] wallMaterials;
        [SerializeField] private Look[] looks;
        [Header("구간 장식 (이름 = 모습 이름, 그 구간에서만 켠다)")]
        [SerializeField] private GameObject[] decoGroups;
        [SerializeField] private FloorSet[] floorSets;
        [SerializeField] private Vector2 roomHalf = new Vector2(4f, 7f);

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static StageLook _instance;
        private static string _pending;
        private static bool _post = true;

        private readonly Dictionary<Material, Material> _copies = new Dictionary<Material, Material>();
        private string _current;
        private bool _started;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            _instance = null;
            _pending = null;
            _post = true;
        }

        /// <summary>모드·구간 → 모습 이름 (서바이벌은 풀밭 하나)</summary>
        public static string KeyFor(BattleMode mode, int chapter) => mode == BattleMode.Survival ? "survival" : "ch" + Mathf.Clamp(chapter, 1, 5);

        /// <summary>이 모습으로 바꾼다 (균열은 층마다 구간이 바뀐다). 장면이 아직 준비 전이면 Start에서 건다</summary>
        public static void Show(string key)
        {
            if (_instance != null && _instance._started) _instance.Apply(key);
            else _pending = key;
        }

        /// <summary>그래픽 품질: 낮음이면 후처리를 끈다</summary>
        public static void SetPost(bool on)
        {
            _post = on;
            if (_instance != null) _instance.ApplyPost();
        }

        /// <summary>게임 중 새로 만드는 장애물(Stage3D)이 쓸 재질: 이 구간 색을 입힌 사본</summary>
        public static Material Resolve(Material source) =>
            _instance != null && source != null && _instance._copies.TryGetValue(source, out Material copy) ? copy : source;

        /// <summary>게임 중 새로 놓은 모델의 재질을 구간 색 사본으로 바꿔 끼운다 (돌덩이·돌기둥이 벽과 같은 색을 입게)</summary>
        public static void Retint(GameObject go)
        {
            if (_instance == null || go == null || _instance._copies.Count == 0) return;
            foreach (Renderer r in go.GetComponentsInChildren<Renderer>(true))
            {
                Material[] mats = r.sharedMaterials;
                bool changed = false;
                for (int i = 0; i < mats.Length; i++)
                {
                    if (mats[i] == null || !_instance._copies.TryGetValue(mats[i], out Material copy)) continue;
                    mats[i] = copy;
                    changed = true;
                }

                if (changed) r.sharedMaterials = mats;
            }
        }

        /// <summary>
        /// 방마다 바닥 장식을 흩뿌린다 (금 간 바닥 · 깔개 · 쇠창살 · 물웅덩이 · 뼈 · 잡초). 판정 없음, 보기만.
        /// 납작한 것은 예고 원·함정 그림보다 아래에 깔리게 아주 얇게 누른다. 같은 방(같은 seed)은 늘 같은 모습
        /// </summary>
        public static void Scatter(Transform parent, int seed, List<GameObject> into)
        {
            if (_instance == null) return;
            _instance.EnsureStarted(); // 첫 방이 StageLook.Start보다 먼저 만들어질 수 있다
            _instance.ScatterFloor(parent, seed, into);
        }

        private void ScatterFloor(Transform parent, int seed, List<GameObject> into)
        {
            if (floorSets == null) return;
            int index = Array.FindIndex(floorSets, f => f.key == _current);
            if (index < 0) return;
            FloorSet set = floorSets[index];
            var rng = new System.Random(seed);
            float Range(float a, float b) => a + (float)rng.NextDouble() * (b - a);

            int flat = set.flat != null && set.flat.Length > 0 ? 2 + rng.Next(3) : 0;
            for (int i = 0; i < flat; i++)
            {
                GameObject prefab = set.flat[rng.Next(set.flat.Length)];
                if (prefab == null) continue;
                var pos = new Vector3(Range(-roomHalf.x + 0.9f, roomHalf.x - 0.9f), Range(-roomHalf.y + 1.5f, roomHalf.y - 1.8f), 0.019f); // 바닥(+0.02) 바로 위
                float size = Range(0.8f, 1.25f);
                into.Add(Place(prefab, parent, pos, Range(0f, 360f), new Vector3(size, size, 0.35f)));
            }

            int edge = set.edge != null && set.edge.Length > 0 ? 2 + rng.Next(3) : 0;
            for (int i = 0; i < edge; i++)
            {
                GameObject prefab = set.edge[rng.Next(set.edge.Length)];
                if (prefab == null) continue;
                // 벽 쪽(등)이 벽을 보게: 위 벽 0°, 왼쪽 90°, 오른쪽 -90°, 아래 180°
                int side = rng.Next(4);
                Vector3 pos;
                float turn;
                switch (side)
                {
                    case 0: pos = new Vector3(Range(-roomHalf.x + 0.6f, roomHalf.x - 0.6f), roomHalf.y - 0.3f, 0f); turn = 0f; break;
                    case 1: pos = new Vector3(-roomHalf.x + 0.3f, Range(-roomHalf.y + 1f, roomHalf.y - 1f), 0f); turn = 90f; break;
                    case 2: pos = new Vector3(roomHalf.x - 0.3f, Range(-roomHalf.y + 1f, roomHalf.y - 1f), 0f); turn = -90f; break;
                    default: pos = new Vector3(Range(-roomHalf.x + 0.6f, roomHalf.x - 0.6f), -roomHalf.y + 0.3f, 0f); turn = 180f; break;
                }

                into.Add(Place(prefab, parent, pos, turn + Range(-20f, 20f), Vector3.one * Range(0.85f, 1.15f)));
            }
        }

        private static GameObject Place(GameObject prefab, Transform parent, Vector3 position, float turn, Vector3 scale)
        {
            GameObject go = Instantiate(prefab, parent, false);
            go.transform.position = position;
            go.transform.rotation = Quaternion.Euler(0f, 0f, turn);
            go.transform.localScale = scale;
            Retint(go);
            return go;
        }

        private void Awake() => _instance = this;

        private void Start() => EnsureStarted();

        private void EnsureStarted()
        {
            if (_started) return;
            _started = true;
            ApplyPost();
            Apply(_pending ?? KeyFor(GameSession.Mode, GameSession.Chapter));
            _pending = null;
        }

        private void OnDestroy()
        {
            if (_instance == this) _instance = null;
            foreach (Material copy in _copies.Values) Destroy(copy);
            _copies.Clear();
        }

        private void ApplyPost()
        {
            if (viewCamera != null && viewCamera.TryGetComponent(out UniversalAdditionalCameraData data)) data.renderPostProcessing = _post;
        }

        public void Apply(string key)
        {
            if (looks == null || key == _current) return;
            int index = Array.FindIndex(looks, l => l.key == key);
            if (index < 0) return;
            _current = key;
            Look look = looks[index];

            if (sun != null)
            {
                sun.color = look.sun;
                sun.intensity = look.sunIntensity;
            }

            RenderSettings.ambientLight = look.ambient;
            if (viewCamera != null) viewCamera.backgroundColor = look.background;
            if (decoGroups != null)
            {
                foreach (GameObject group in decoGroups)
                {
                    if (group != null) group.SetActive(group.name == key); // 이 구간 장식만
                }
            }

            bool created = Tint(floorMaterials, look.floor) | Tint(wallMaterials, look.wall);
            if (created) SwapRenderers();

            // volume.profile = 게임 중 사본 (에셋은 그대로)
            if (volume != null && volume.profile != null && volume.profile.TryGet(out ColorAdjustments color))
            {
                color.colorFilter.Override(look.filter);
                color.postExposure.Override(look.exposure);
                color.contrast.Override(look.contrast);
                color.saturation.Override(look.saturation);
            }
        }

        /// <summary>재질 사본에 색을 곱한다. 새 사본을 만들었으면 true (장면의 렌더러를 바꿔 끼워야 한다)</summary>
        private bool Tint(Material[] sources, Color tint)
        {
            if (sources == null) return false;
            bool created = false;
            foreach (Material source in sources)
            {
                if (source == null || !source.HasProperty(BaseColorId)) continue;
                if (!_copies.TryGetValue(source, out Material copy))
                {
                    copy = new Material(source) { name = source.name + " (구간 색)" };
                    _copies[source] = copy;
                    created = true;
                }

                Color baseColor = source.GetColor(BaseColorId);
                copy.SetColor(BaseColorId, new Color(baseColor.r * tint.r, baseColor.g * tint.g, baseColor.b * tint.b, baseColor.a));
            }

            return created;
        }

        private void SwapRenderers()
        {
            // 재질이 여러 개인 블렌더 소품도 있어서 칸마다 본다
            foreach (MeshRenderer r in FindObjectsByType<MeshRenderer>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                Material[] mats = r.sharedMaterials;
                bool changed = false;
                for (int i = 0; i < mats.Length; i++)
                {
                    if (mats[i] == null || !_copies.TryGetValue(mats[i], out Material copy)) continue;
                    mats[i] = copy;
                    changed = true;
                }

                if (changed) r.sharedMaterials = mats;
            }
        }
    }
}
