using System.Collections.Generic;
using HistoryHeroes.Common;
using HistoryHeroes.Enemy;
using UnityEngine;

namespace HistoryHeroes.Skills
{
    /// <summary>
    /// 화살비 (전설): 6초마다 화면 안 모든 적에게 화살 (적마다 150%). (Skills.md 2장)
    /// 화살은 떨어지는 동안 적을 따라가서 꼭 맞는다. 도착 시간을 조금씩 흩어서 "비"처럼 보이게 한다.
    /// 천궁 (합체, returnCoefficient 0.75): 화살이 부메랑처럼 되돌아오며 한 번 더 75% (Skills.md 4-4).
    /// </summary>
    public sealed class ArrowRainBehaviour : ActiveSkillBehaviour
    {
        [SerializeField] private Color arrowColor = new Color(0.95f, 0.85f, 0.6f);
        [SerializeField] private Vector2 arrowSize = new Vector2(0.1f, 0.7f);
        [SerializeField] private float fallHeight = 6f;
        [SerializeField] private Vector2 fallTimeRange = new Vector2(0.25f, 0.55f);
        [Tooltip("천궁: 되돌아오는 화살의 피해 (0이면 없음)")]
        [SerializeField] private float returnCoefficient;
        [SerializeField] private float returnDelay = 0.35f;

        private readonly List<EnemyBase> _targets = new List<EnemyBase>();
        private ComponentPool<StrikeVisual> _arrows;

        protected override bool Activate()
        {
            CombatQuery.EnemiesOnScreen(_targets);
            if (_targets.Count == 0) return false;

            _arrows ??= StrikeVisual.CreatePool(EffectRoot);
            float coefficient = data.DamageAt(level);

            foreach (EnemyBase enemy in _targets)
            {
                EnemyBase target = enemy; // 람다가 반복 변수를 잡지 않게
                Vector2 point = target.transform.position;
                float fallTime = Random.Range(fallTimeRange.x, fallTimeRange.y);

                _arrows.Get(point, Quaternion.identity).Play(point, target.transform, fallHeight, fallTime, 0.2f,
                    arrowSize, Vector2.zero, arrowColor,
                    () =>
                    {
                        if (target != null && target.gameObject.activeInHierarchy && !target.Health.IsDead)
                            target.Health.TryTakeDamage(RollDamage(coefficient));
                        if (returnCoefficient > 0f) ReturnArrow(target, coefficient);
                    },
                    _arrows.Release);
            }

            return true;
        }

        /// <summary>천궁: 되돌아오는 화살 (합체 레벨 비율을 그대로)</summary>
        private void ReturnArrow(EnemyBase target, float coefficient)
        {
            if (target == null || !target.gameObject.activeInHierarchy || target.Health.IsDead) return;
            float levelScale = coefficient / Mathf.Max(0.01f, data.DamageAt(1));
            Vector2 point = target.transform.position;
            _arrows.Get(point, Quaternion.identity).Play(point, target.transform, fallHeight * 0.5f, returnDelay, 0.15f,
                arrowSize, Vector2.zero, new Color(1f, 0.95f, 0.75f), () =>
                {
                    if (target != null && target.gameObject.activeInHierarchy && !target.Health.IsDead)
                        target.Health.TryTakeDamage(RollDamage(returnCoefficient * levelScale)); // Lv1 75%, 합체 레벨만큼 커짐
                }, _arrows.Release);
        }
    }
}
