using UnityEngine;

namespace HistoryHeroes.Combat
{
    /// <summary>
    /// 맞을 때 · 쓰러질 때 · 터질 때 튀는 빛 조각 (그래픽 평가 2번: "공격이 맞는 순간의 빛·파편이 약하다").
    /// 입자 장치 몇 개를 장면에 하나씩 두고 Emit(위치)만 한다 → 적이 300마리여도 장치 수는 그대로라 가볍다.
    /// 한 프레임에 너무 많이 터지지 않게 막는다 (서바이벌).
    /// </summary>
    public sealed class FxBursts : MonoBehaviour
    {
        [SerializeField] private ParticleSystem sparks;
        [SerializeField] private ParticleSystem puffs;
        [SerializeField] private ParticleSystem rings;
        [Tooltip("한 프레임에 맞음 불꽃을 터뜨리는 최대 횟수")]
        [SerializeField] private int maxHitsPerFrame = 10;
        [SerializeField] private float bodyHeight = 0.8f;

        private static FxBursts _instance;
        private int _frame;
        private int _hitsThisFrame;

        private static readonly Color BasicSpark = new Color(1f, 0.92f, 0.65f);
        private static readonly Color SkillSpark = new Color(1f, 0.6f, 0.25f);
        private static readonly Color CritSpark = new Color(1f, 0.35f, 0.25f);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => _instance = null;

        private void OnEnable()
        {
            _instance = this;
            Health.AnyDamaged += OnAnyDamaged;
        }

        private void OnDisable()
        {
            Health.AnyDamaged -= OnAnyDamaged;
            if (_instance == this) _instance = null;
        }

        private void OnAnyDamaged(Health target, DamageInfo info, float dealt)
        {
            if (target == null || target.Team != Team.Enemy || dealt <= 0f) return;
            Vector3 p = target.transform.position;
            p.z = -bodyHeight;
            if (target.IsDead)
            {
                Puff(p, new Color(0.85f, 0.8f, 0.95f), 1f);
                return;
            }

            if (Time.frameCount != _frame)
            {
                _frame = Time.frameCount;
                _hitsThisFrame = 0;
            }

            if (++_hitsThisFrame > maxHitsPerFrame) return;
            Color c = info.IsCrit ? CritSpark : info.Kind == DamageKind.Active ? SkillSpark : BasicSpark;
            EmitSparks(p, c, info.IsCrit ? 9 : 5, info.IsCrit ? 1.4f : 1f);
        }

        private void EmitSparks(Vector3 position, Color color, int count, float speed)
        {
            if (sparks == null) return;
            var e = new ParticleSystem.EmitParams { applyShapeToPosition = true };
            for (int i = 0; i < count; i++)
            {
                Vector2 dir = Random.insideUnitCircle.normalized;
                e.position = position;
                e.velocity = new Vector3(dir.x, dir.y, -Random.Range(0.3f, 1.2f)) * Random.Range(2.5f, 5f) * speed;
                e.startColor = color;
                e.startSize = Random.Range(0.06f, 0.12f);
                e.startLifetime = Random.Range(0.18f, 0.32f);
                sparks.Emit(e, 1);
            }
        }

        /// <summary>쓰러짐 · 작은 터짐: 부드러운 연기 덩이 몇 개</summary>
        public static void Puff(Vector3 position, Color color, float size)
        {
            if (_instance == null || _instance.puffs == null) return;
            var e = new ParticleSystem.EmitParams();
            for (int i = 0; i < 5; i++)
            {
                Vector2 dir = Random.insideUnitCircle * 0.9f;
                e.position = position + new Vector3(dir.x, dir.y, 0f) * 0.25f * size;
                e.velocity = new Vector3(dir.x, dir.y, -0.6f) * size;
                e.startColor = color;
                e.startSize = Random.Range(0.45f, 0.75f) * size;
                e.startLifetime = Random.Range(0.35f, 0.5f);
                _instance.puffs.Emit(e, 1);
            }
        }

        /// <summary>폭발 (유성 · 포탄 · 폭탄병): 퍼지는 고리 + 불꽃 + 연기. radius = 판정 반지름</summary>
        public static void Explosion(Vector2 center, float radius, Color color)
        {
            if (_instance == null) return;
            var p = new Vector3(center.x, center.y, -0.05f);
            if (_instance.rings != null)
            {
                var e = new ParticleSystem.EmitParams { position = p, startColor = color, startSize = radius * 2.4f, startLifetime = 0.35f };
                _instance.rings.Emit(e, 1);
            }

            _instance.EmitSparks(p + new Vector3(0f, 0f, -0.3f), color, 14, 1.3f + radius * 0.3f);
            Puff(p + new Vector3(0f, 0f, -0.4f), Color.Lerp(new Color(0.4f, 0.37f, 0.36f), color, 0.35f), 0.6f + radius * 0.5f);
        }
    }
}
