using System.Collections.Generic;
using HistoryHeroes.Combat;
using HistoryHeroes.Common;
using HistoryHeroes.Enemy;
using UnityEngine;

namespace HistoryHeroes.Skills
{
    /// <summary>
    /// 수호 방패: 8초마다 최대체력 10% 보호막 (레벨마다 +4%). 피해를 체력보다 먼저 막는다 (Skills.md 2장).
    /// 철벽 (진화, explode): 보호막 26%, 깨질 때 주변 폭발 200% (반지름 3, 4-4).
    /// 이 스킬의 "피해" 값 = 보호막 비율 (0.10 = 최대체력 10%).
    /// </summary>
    public sealed class GuardShieldBehaviour : ActiveSkillBehaviour
    {
        [SerializeField] private Color shieldColor = new Color(0.5f, 0.75f, 1f, 0.28f);
        [SerializeField] private bool explodeOnBreak;
        [SerializeField] private float explodeCoefficient = 2f;
        [SerializeField] private float explodeRadius = 3f;

        private SpriteRenderer _bubble;
        private bool _subscribed;
        private readonly List<EnemyBase> _hits = new List<EnemyBase>();

        protected override bool Activate()
        {
            if (!_subscribed)
            {
                player.Health.ShieldBroken += OnBroken;
                _subscribed = true;
            }

            player.Health.SetShieldAtLeast(player.Health.Max * data.DamageAt(level));
            return true;
        }

        protected override void Update()
        {
            base.Update();
            if (player == null) return;

            if (_bubble == null)
            {
                var go = new GameObject("ShieldBubble");
                go.transform.SetParent(player.transform, false);
                go.transform.localScale = Vector3.one * 1.5f;
                _bubble = go.AddComponent<SpriteRenderer>();
                _bubble.sprite = PlaceholderSprites.Circle;
                _bubble.color = shieldColor;
                _bubble.sortingOrder = 4;
            }

            _bubble.enabled = player.Health.Shield > 0f;
        }

        private void OnBroken()
        {
            if (!explodeOnBreak || player == null) return;
            Vector2 center = player.transform.position;
            AttackFx.Burst(center, explodeRadius, new Color(0.6f, 0.85f, 1f, 0.6f), 0.25f);
            CombatQuery.EnemiesWithin(center, explodeRadius, _hits);
            foreach (EnemyBase e in _hits) e.Health.TryTakeDamage(RollDamage(explodeCoefficient));
        }

        protected override void OnDestroy()
        {
            if (player != null && _subscribed) player.Health.ShieldBroken -= OnBroken;
            if (_bubble != null) Destroy(_bubble.gameObject);
            base.OnDestroy();
        }
    }
}
