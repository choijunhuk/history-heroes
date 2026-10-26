using HistoryHeroes.Common;
using HistoryHeroes.Enemy;
using UnityEngine;

namespace HistoryHeroes.Skills
{
    /// <summary>
    /// 부메랑: 4초마다 가장 가까운 적 쪽으로 왕복 부메랑 1개 (80%, 관통), 레벨마다 +1개 (Skills.md 2장).
    /// 쌍 부메랑 (진화, split): 부메랑 5개, 돌아올 때 2개로 갈라짐 (각 50%, 4-4).
    /// </summary>
    public sealed class BoomerangBehaviour : ActiveSkillBehaviour
    {
        [SerializeField] private float distance = 5f;
        [SerializeField] private float outTime = 0.45f;
        [SerializeField] private float spreadDegrees = 22f;
        [SerializeField] private float searchRange = 9f;
        [SerializeField] private Color color = new Color(0.85f, 0.7f, 0.4f);
        [SerializeField] private Vector2 size = new Vector2(0.55f, 0.18f);

        [Header("쌍 부메랑")]
        [SerializeField] private bool split;
        [Tooltip("조각 하나의 피해 (공격력 배수, Skills 4-4: 각 50%)")]
        [SerializeField] private float splitCoefficient = 0.5f;

        private float ReturnSpeed => distance / Mathf.Max(0.05f, outTime) * 1.3f;

        private ComponentPool<BoomerangMover> _pool;

        protected override bool Activate()
        {
            EnemyBase target = CombatQuery.Nearest(player.transform.position, searchRange);
            if (target == null) return false;

            _pool ??= BoomerangMover.CreatePool(EffectRoot, color, size);
            Vector2 origin = player.transform.position;
            Vector2 aim = ((Vector2)target.transform.position - origin).normalized;
            int count = data.CountAt(level);
            float start = -spreadDegrees * (count - 1) * 0.5f;
            float coefficient = data.DamageAt(level);

            for (int i = 0; i < count; i++)
            {
                Vector2 direction = Quaternion.Euler(0f, 0f, start + spreadDegrees * i) * aim;
                _pool.Get(origin, Quaternion.identity).Launch(player.transform, player.Stats, enemyMask, origin, direction, distance, outTime,
                    coefficient, false, split ? SplitAt : null, _pool.Release, ReturnSpeed, endAtApex: split, sourceId: data.id);
            }

            return true;
        }

        /// <summary>쌍 부메랑: 가장 멀리 간 자리에서 원본은 사라지고 양옆으로 조각 2개(각 50%)가 돌아온다</summary>
        private void SplitAt(Vector2 apex, Vector2 direction)
        {
            for (int side = -1; side <= 1; side += 2)
            {
                Vector2 offset = Quaternion.Euler(0f, 0f, 90f * side) * direction * 0.6f;
                _pool.Get(apex + offset, Quaternion.identity).Launch(player.transform, player.Stats, enemyMask, apex + offset, direction, 0f, outTime,
                    splitCoefficient, true, null, _pool.Release, ReturnSpeed, sourceId: data.id);
            }
        }
    }
}
