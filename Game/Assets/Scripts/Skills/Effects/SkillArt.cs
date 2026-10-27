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
        [Tooltip("장판·불씨 입자 (더하기 섞기, 둥근 빛)")]
        public Material glow;
        [Tooltip("영웅 외곽선 (HistoryHeroes/HeroOutline, 새 영웅 화면 점검 1번)")]
        public Material heroOutline;
        [Header("머리 위 대표 효과 아이콘 (화면 점검 2차 4번: 뜻이 읽히게)")]
        public Sprite hudIron;
        public Sprite hudCharge;
        public Sprite hudParts;

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

        /// <summary>
        /// 장판 위로 피어오르는 빛 알갱이 (불꽃 장판 = 불씨, 독 구름 = 초록 거품, 눈보라 = 눈송이 …). 그래픽 평가 5번 "평평한 스킬 효과".
        /// owner의 크기(지름 1 기준)에 맞춰 동그랗게 흩어진다. 알갱이 크기는 owner 크기와 상관없다
        /// </summary>
        public static ParticleSystem Embers(Transform owner, Color color, float rate, float rise, float size)
        {
            SkillArt art = Get;
            if (art == null || art.glow == null || owner == null) return null;
            var go = new GameObject("Embers");
            go.transform.SetParent(owner, false);
            go.transform.localRotation = Quaternion.LookRotation(Vector3.back); // 입자의 +Z → 게임의 위(-Z)
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            ParticleSystem.MainModule main = ps.main;
            main.loop = true;
            main.playOnAwake = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.6f, 1f);
            main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(size * 0.6f, size);
            main.startColor = new Color(color.r, color.g, color.b, 1f);
            main.maxParticles = 80;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.scalingMode = ParticleSystemScalingMode.Shape; // 장판 크기만큼 넓게, 알갱이는 그대로
            ParticleSystem.EmissionModule emission = ps.emission;
            emission.rateOverTime = rate;
            ParticleSystem.ShapeModule shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = 0.5f;
            shape.radiusThickness = 1f;
            ParticleSystem.VelocityOverLifetimeModule velocity = ps.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.Local;
            velocity.x = new ParticleSystem.MinMaxCurve(-0.12f, 0.12f);
            velocity.y = new ParticleSystem.MinMaxCurve(-0.12f, 0.12f);
            velocity.z = new ParticleSystem.MinMaxCurve(rise * 0.6f, rise);
            ParticleSystem.ColorOverLifetimeModule col = ps.colorOverLifetime;
            col.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(0.9f, 0.2f), new GradientAlphaKey(0f, 1f) });
            col.color = gradient;
            ParticleSystem.SizeOverLifetimeModule sol = ps.sizeOverLifetime;
            sol.enabled = true;
            sol.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 0.6f, 1f, 0.15f));
            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = art.glow;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            ps.Play();
            return ps;
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
