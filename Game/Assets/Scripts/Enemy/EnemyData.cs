using UnityEngine;

namespace HistoryHeroes.Enemy
{
    /// <summary>적 정의. 기준값은 Docs/Design/Stats.md 8장 (1구간 일반 적 체력 120, 공격 60).</summary>
    [CreateAssetMenu(menuName = "History Heroes/Enemy", fileName = "Enemy_")]
    public sealed class EnemyData : ScriptableObject
    {
        public string id = "charger";
        public string displayName = "돌진병";
        public float maxHp = 120f;
        public float damage = 60f;
        public float moveSpeed = 2.5f;
        public int experience = 3;

        [Header("패턴 시간 (Content.md 4장)")]
        [Tooltip("공격 예고 시간")] public float telegraphTime = 0.8f;
        [Tooltip("공격 뒤 빈틈")] public float recoverTime = 1.2f;

        [Header("화면 (임시 도형)")]
        public Color color = new Color(0.85f, 0.25f, 0.25f);
        [Tooltip("몸 크기 (작은 분열체·졸개는 작게)")]
        public float scale = 1f;
    }
}
