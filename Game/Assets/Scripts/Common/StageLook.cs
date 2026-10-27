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

        [SerializeField] private Light sun;
        [SerializeField] private Volume volume;
        [SerializeField] private Camera viewCamera;
        [SerializeField] private Material[] floorMaterials;
        [SerializeField] private Material[] wallMaterials;
        [SerializeField] private Look[] looks;

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

        private void Awake() => _instance = this;

        private void Start()
        {
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
            foreach (MeshRenderer r in FindObjectsByType<MeshRenderer>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (r.sharedMaterial != null && _copies.TryGetValue(r.sharedMaterial, out Material copy)) r.sharedMaterial = copy;
            }
        }
    }
}
