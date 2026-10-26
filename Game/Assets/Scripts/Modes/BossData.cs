using HistoryHeroes.Enemy;
using UnityEngine;

namespace HistoryHeroes.Modes
{
    /// <summary>보스맵 보스 정의 (Content.md 3장, Modes.md 2-9 · 3-3).</summary>
    [CreateAssetMenu(menuName = "History Heroes/Boss", fileName = "Boss_")]
    public sealed class BossData : ScriptableObject
    {
        [Tooltip("저장용 ID. 한 번 정하면 바꾸지 않는다")]
        public string id = "rift_warden";
        public string displayName = "균열의 수문장";
        [Min(1)] public int chapter = 1;

        [Tooltip("시작 전 화면: 이 보스가 시험하는 것")]
        [TextArea] public string tests = "큰 빈틈이 있다 → 빈틈마다 멈춰서 쏘기";
        [Tooltip("시작 전 화면: 추천 빌드")]
        [TextArea] public string recommend = "기본 공격이 강한 빌드";
        [Tooltip("이 강점의 기록에 \"추천\" 배지 (조사 A8). 고르는 건 플레이어 자유")]
        public Skills.SkillRole recommendedRole = Skills.SkillRole.Single;

        public EnemyBase prefab;
        [Tooltip("적 데이터 체력 × 이 값")]
        public float hpMultiplier = 1f;
        public float attackMultiplier = 1f;

        [Tooltip("★2 조건: 피격 이 횟수 이하로 격파 (Modes 3-3)")]
        public int star2MaxHits = 8;
    }
}
