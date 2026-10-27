using UnityEngine;

namespace HistoryHeroes.Skills
{
    /// <summary>
    /// 스킬 3D 모양 모음 (그래픽 평가 2번, 개발자: "스킬들 이거 다 너무 그냥 html 그런 느낌").
    /// 블렌더 창이 만든 칼날·부메랑·얼음 파편·유성·방패 + 꼬리 빛 재질. Resources/SkillArt (장면 빌더가 만든다).
    /// 없으면 예전 납작한 도형을 그대로 쓴다.
    /// </summary>
    [CreateAssetMenu(menuName = "History Heroes/Skill Art", fileName = "SkillArt")]
    public sealed class SkillArt : ScriptableObject
    {
        public GameObject blade;
        public GameObject boomerang;
        public GameObject iceShard;
        public GameObject meteor;
        public GameObject shield;
        [Tooltip("꼬리 빛 (더하기 섞기, 1 넘는 색이면 빛 번짐)")]
        public Material trail;

        private static SkillArt _cached;
        private static bool _loaded;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            _cached = null;
            _loaded = false;
        }

        public static SkillArt Get
        {
            get
            {
                if (_loaded) return _cached;
                _loaded = true;
                _cached = Resources.Load<SkillArt>("SkillArt");
                return _cached;
            }
        }

        /// <summary>
        /// owner 밑에 모델을 붙인다. owner의 배율(스프라이트 크기)을 되돌려서 모델은 제 크기 × size로 보인다.
        /// 모델 프리팹은 이미 게임 방향(XY 바닥, -Z 위, 끝이 +X)으로 세워져 있다
        /// </summary>
        public static GameObject Attach(GameObject prefab, Transform owner, float size)
        {
            if (prefab == null || owner == null) return null;
            GameObject go = Instantiate(prefab, owner, false);
            go.name = "Model3D";
            Vector3 s = owner.lossyScale;
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale = new Vector3(size / Mathf.Max(0.0001f, Mathf.Abs(s.x)), size / Mathf.Max(0.0001f, Mathf.Abs(s.y)), size / Mathf.Max(0.0001f, Mathf.Abs(s.z)));
            return go;
        }

        /// <summary>움직이는 것 뒤에 꼬리 빛 (회전 칼날의 원 · 부메랑 · 얼음 파편)</summary>
        public static TrailRenderer Trail(GameObject go, Color color, float width, float time)
        {
            SkillArt art = Get;
            if (art == null || art.trail == null || go == null) return null;
            var trail = go.AddComponent<TrailRenderer>();
            trail.sharedMaterial = art.trail;
            trail.time = time;
            trail.minVertexDistance = 0.05f;
            trail.widthMultiplier = width;
            trail.widthCurve = AnimationCurve.Linear(0f, 1f, 1f, 0f);
            var gradient = new Gradient();
            gradient.SetKeys(new[] { new GradientColorKey(color, 0f), new GradientColorKey(color, 1f) },
                new[] { new GradientAlphaKey(0.9f, 0f), new GradientAlphaKey(0f, 1f) });
            trail.colorGradient = gradient;
            trail.alignment = LineAlignment.View;
            trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            trail.receiveShadows = false;
            trail.numCapVertices = 2;
            return trail;
        }
    }
}
