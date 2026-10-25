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
        [Tooltip("레벨마다 늘어나는 개수 (칼날, 번개, 화살 수 등)")]
        public int countFirstLevel = 1;
        public int countPerLevel = 1;

        public ActiveSkillBehaviour behaviourPrefab;

        public override SkillKind Kind => SkillKind.Active;

        public float DamageAt(int level) => damageFirstLevel + damagePerLevel * (level - 1);
        public int CountAt(int level) => countFirstLevel + countPerLevel * (level - 1);
    }
}
