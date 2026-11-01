using UnityEngine;

namespace HistoryHeroes.Skills
{
    /// <summary>
    /// 액티브: 쿨다운마다 자동 발동, 이동 중에도 나간다. (GameDesign 3-1)
    /// 실제 동작은 behaviourPrefab(ActiveSkillBehaviour)이 맡는다.
    /// </summary>
    [CreateAssetMenu(menuName = "History Heroes/Skill/Active", fileName = "Active_")]
    public class ActiveSkillData : SkillData
    {
        [Header("발동")]
        [Tooltip("0이면 상시 (회전 칼날 등)")]
        public float cooldown = 3f;
        public float damageFirstLevel = 1f;
        public float damagePerLevel = 0.2f;
        [Tooltip("자신의 최대체력 기준 회복 비율. 피해·치명타 배수와 분리한다")]
        public float healFractionFirstLevel;
        public float healFractionPerLevel;
        [Tooltip("레벨마다 늘어나는 개수 (칼날, 번개, 화살 수 등)")]
        public int countFirstLevel = 1;
        public int countPerLevel = 1;
        [Tooltip("범위 반지름 (장판·칼날 궤도 등)")]
        public float radius = 1.5f;
        [Tooltip("레벨마다 범위가 몇 배씩 늘어나나 (0.15 = +15%)")]
        public float radiusPerLevel = 0f;
        [Tooltip("지속 시간 (장판 등). 0이면 즉발")]
        public float duration = 0f;
        public float durationPerLevel = 0f;
        [Tooltip("둔화 비율. 0이면 둔화 없음")]
        [Range(0f, 1f)] public float slowFirstLevel;
        public float slowPerLevel;
        [Tooltip("보스에게 주는 둔화 세기 비율")]
        public float slowBossScale = 0.3f;
        [Tooltip("적이 범위에 처음 들어왔을 때 생기는 파동 반경. 0이면 파동 없음")]
        public float waveRadius;
        [Tooltip("귀환 망치가 돌아올 때 내리는 한 번의 낙뢰 피해 배수. 다른 기술은 0")]
        public float returnImpactCoefficient;

        [Header("직선 검기")]
        public float waveLength;
        public float waveSpeed;
        public float waveInterval;
        public float pushDistance;

        public ActiveSkillBehaviour behaviourPrefab;

        public override SkillKind Kind => SkillKind.Active;

        public float DamageAt(int level) => damageFirstLevel + damagePerLevel * (level - 1);
        public float HealFractionAt(int level) => healFractionFirstLevel + healFractionPerLevel * (level - 1);
        public int CountAt(int level) => countFirstLevel + countPerLevel * (level - 1);
        public float RadiusAt(int level) => radius * (1f + radiusPerLevel * (level - 1));
        public float DurationAt(int level) => duration + durationPerLevel * (level - 1);
        public float SlowAt(int level) => Mathf.Clamp01(slowFirstLevel + slowPerLevel * (level - 1));
    }
}
