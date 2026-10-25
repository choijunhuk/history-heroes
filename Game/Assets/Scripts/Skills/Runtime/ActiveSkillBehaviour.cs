using HistoryHeroes.Combat;
using HistoryHeroes.Player;
using HistoryHeroes.Stats;
using UnityEngine;

namespace HistoryHeroes.Skills
{
    /// <summary>
    /// 액티브 스킬 동작의 공통 부분. 쿨다운마다 Activate()를 부른다 (이동 중에도).
    /// 쿨다운 감소는 액티브에만 적용된다. 레벨이 바뀌면 OnLevelChanged가 불린다.
    /// </summary>
    public abstract class ActiveSkillBehaviour : MonoBehaviour
    {
        protected PlayerController player;
        protected ActiveSkillData data;
        protected int level;
        protected LayerMask enemyMask;
        private float _cooldownLeft;

        public void Bind(PlayerController owner, ActiveSkillData skill, int startLevel, LayerMask enemies)
        {
            player = owner;
            data = skill;
            enemyMask = enemies;
            SetLevel(startLevel);
            _cooldownLeft = 0.5f;
        }

        public void SetLevel(int newLevel)
        {
            level = newLevel;
            OnLevelChanged();
        }

        protected virtual void OnLevelChanged() { }

        /// <summary>조합 재료로 쓰여 사라질 때. 소환물·이벤트를 정리한다 (GameDesign 12-2 조합).</summary>
        public virtual void Dispose()
        {
            Destroy(gameObject);
        }

        protected virtual void Update()
        {
            if (player == null || data == null || data.cooldown <= 0f) return;

            _cooldownLeft -= Time.deltaTime;
            if (_cooldownLeft > 0f) return;

            float cdr = player.Stats.Get(StatType.CooldownReduction, StatTarget.Active);
            _cooldownLeft = data.cooldown * (1f - cdr);
            Activate();
        }

        protected abstract void Activate();

        protected DamageInfo RollDamage(float coefficient)
        {
            return DamageCalculator.Roll(player.Stats, coefficient, DamageKind.Active);
        }
    }
}
