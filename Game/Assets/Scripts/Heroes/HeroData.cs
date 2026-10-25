using UnityEngine;

namespace HistoryHeroes.Heroes
{
    /// <summary>영웅 정의 (변하지 않는 값만). 기본값은 Docs/Design/Heroes.md 2장 표.</summary>
    [CreateAssetMenu(menuName = "History Heroes/Hero", fileName = "Hero_")]
    public sealed class HeroData : ScriptableObject
    {
        [Header("이름")]
        public string id = "khan";
        public string displayName = "칭기즈 칸";

        [Header("기본 능력치")]
        public float maxHp = 1000f;
        public float attack = 100f;
        public float attackSpeed = 1.0f;
        public float moveSpeed = 5.0f;
        public float range = 6.0f;
        [Range(0f, 1f)] public float critRate = 0.05f;
        public float critDamage = 1.5f;

        [Header("기본 공격")]
        [Tooltip("멈춘 뒤 첫 공격까지 기다리는 시간. 기본 0.2초, 칭기즈 칸은 대표 효과로 0초 (Stats.md 1-1)")]
        public float firstShotDelay = 0.2f;
        [Tooltip("멈춘 뒤 첫 발 피해 보너스. 칭기즈 칸 대표 효과 +50% (Heroes.md 3장)")]
        public float firstShotBonus = 0f;
        public float projectileSpeed = 14f;

        [Header("화면 (임시 도형)")]
        public Color color = new Color(0.95f, 0.75f, 0.2f);
    }
}
