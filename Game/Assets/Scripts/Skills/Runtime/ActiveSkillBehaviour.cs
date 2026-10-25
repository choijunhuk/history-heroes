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
        /// <summary>표적이 없어서 못 쏘았을 때 다시 시도하기까지의 시간</summary>
        private const float RetryDelay = 0.25f;

        protected PlayerController player;
        protected ActiveSkillData data;
        protected int level;
        protected LayerMask enemyMask;
        private float _cooldownLeft;
        private Transform _effectRoot;

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

        /// <summary>조합 재료로 쓰여 사라질 때. 소환물·이펙트도 함께 정리한다 (GameDesign 12-2 조합).</summary>
        public virtual void Dispose()
        {
            Destroy(gameObject);
        }

        protected virtual void OnDestroy()
        {
            if (_effectRoot != null) Destroy(_effectRoot.gameObject);
        }

        /// <summary>
        /// 장판·번개처럼 **월드에 남는** 이펙트를 담는 곳. 플레이어 자식이면 같이 끌려다니므로 따로 둔다.
        /// 스킬이 사라지면 같이 지워진다.
        /// </summary>
        protected Transform EffectRoot
        {
            get
            {
                if (_effectRoot == null) _effectRoot = new GameObject($"{name} Effects").transform;
                return _effectRoot;
            }
        }

        protected virtual void Update()
        {
            if (player == null || data == null || data.cooldown <= 0f) return;

            _cooldownLeft -= Time.deltaTime;
            if (_cooldownLeft > 0f) return;

            if (!Activate())
            {
                _cooldownLeft = RetryDelay;
                return;
            }

            float cdr = player.Stats.Get(StatType.CooldownReduction, StatTarget.Active);
            _cooldownLeft = data.cooldown * (1f - cdr);
        }

        /// <returns>실제로 발동했으면 true. 표적이 없어 못 쏘았으면 false (쿨다운을 쓰지 않음).</returns>
        protected abstract bool Activate();

        protected DamageInfo RollDamage(float coefficient)
        {
            return DamageCalculator.Roll(player.Stats, coefficient, DamageKind.Active);
        }
    }
}
