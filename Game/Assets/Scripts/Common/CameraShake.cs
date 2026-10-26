using HistoryHeroes.Combat;
using HistoryHeroes.Records;
using UnityEngine;

namespace HistoryHeroes.Common
{
    /// <summary>
    /// 화면 흔들림 — 트라우마 방식 (조사 A9, Eiserloh). 트라우마 0~1을 더하고, 흔들림 크기 = 트라우마², 초당 1.5씩 줄어든다.
    /// 플레이어 피격 +0.3, 엘리트 처치 +0.4, 보스 강타·처치 +0.5. 설정(끄기/약하게/보통)을 따른다.
    /// 전투 카메라에 붙는다 (카메라는 고정이라 처음 자리를 기준으로 흔든다).
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public sealed class CameraShake : MonoBehaviour
    {
        public const float PlayerHit = 0.3f;
        public const float EliteKill = 0.4f;
        public const float BossImpact = 0.5f;

        [SerializeField] private float decayPerSecond = 1.5f;
        [Tooltip("최대 이동 (유닛)")]
        [SerializeField] private float maxOffset = 0.3f;
        [Tooltip("최대 회전 (도)")]
        [SerializeField] private float maxAngle = 2f;
        [Tooltip("노이즈 빠르기")]
        [SerializeField] private float frequency = 22f;

        private static CameraShake _instance;

        private Vector3 _basePosition;
        private Quaternion _baseRotation;
        private float _trauma;
        private float _seed;

        /// <summary>지금 트라우마 (QA·테스트용)</summary>
        public static float Trauma => _instance != null ? _instance._trauma : 0f;

        public static void Add(float trauma)
        {
            if (_instance == null || trauma <= 0f) return;
            _instance._trauma = Mathf.Min(1f, _instance._trauma + trauma);
        }

        /// <summary>설정의 흔들림 배수: 0 끄기, 1 약하게(50%), 2 보통</summary>
        public static float SettingMultiplier(int shakeSetting) => shakeSetting <= 0 ? 0f : shakeSetting == 1 ? 0.5f : 1f;

        private void Awake()
        {
            _basePosition = transform.localPosition;
            _baseRotation = transform.localRotation;
            _seed = Random.value * 100f;
        }

        private void OnEnable()
        {
            _instance = this;
            Health.AnyDamaged += OnAnyDamaged;
        }

        private void OnDisable()
        {
            Health.AnyDamaged -= OnAnyDamaged;
            if (_instance == this) _instance = null;
            transform.localPosition = _basePosition;
            transform.localRotation = _baseRotation;
        }

        private static void OnAnyDamaged(Health target, DamageInfo info, float dealt)
        {
            if (target.Team == Team.Player && dealt > 0f) Add(PlayerHit);
        }

        private void LateUpdate()
        {
            if (_trauma <= 0f)
            {
                transform.localPosition = _basePosition;
                transform.localRotation = _baseRotation;
                return;
            }

            float amount = _trauma * _trauma * SettingMultiplier(SaveSystem.Data.settings.shake);
            // 멈춘 동안(레벨업 창)에도 자연스럽게 가라앉게 실제 시간으로
            float t = Time.unscaledTime * frequency;
            float x = Mathf.PerlinNoise(_seed, t) * 2f - 1f;
            float y = Mathf.PerlinNoise(_seed + 10f, t) * 2f - 1f;
            float r = Mathf.PerlinNoise(_seed + 20f, t) * 2f - 1f;
            transform.localPosition = _basePosition + new Vector3(x, y, 0f) * (maxOffset * amount);
            transform.localRotation = _baseRotation * Quaternion.Euler(0f, 0f, r * maxAngle * amount);

            _trauma = Mathf.Max(0f, _trauma - decayPerSecond * Time.unscaledDeltaTime);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => _instance = null;
    }
}
