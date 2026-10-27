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

            bool on = player.Health.Shield > 0f;
            _bubble.enabled = on;

            // 그래픽 평가 5번: 영웅 둘레를 도는 3D 방패 2개 (평평한 원만으로는 막는 느낌이 약하다)
            SkillArt art = SkillArt.Get;
            if (_orbit == null && art != null && art.shield != null)
            {
                _orbit = new Transform[2];
                for (int i = 0; i < _orbit.Length; i++)
                {
                    _orbit[i] = Instantiate(art.shield, player.transform).transform;
                    _orbit[i].name = "OrbitShield";
                    _orbit[i].localScale = Vector3.one * 0.85f;
                }
            }

            if (_orbit == null) return;
            float spin = Time.time * 140f;
            for (int i = 0; i < _orbit.Length; i++)
            {
                Transform s = _orbit[i];
                if (s == null) continue;
                if (s.gameObject.activeSelf != on) s.gameObject.SetActive(on);
                if (!on) continue;
                float a = (spin + 180f * i) * Mathf.Deg2Rad;
                var offset = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f) * 0.95f;
                // 방패 면(모델 위 = 게임의 위)이 바깥을 보게 세운다
                s.SetPositionAndRotation(player.transform.position + offset + new Vector3(0f, 0f, -0.8f),
                    Quaternion.LookRotation(Vector3.back, offset) * Quaternion.Euler(-90f, 0f, 0f));
            }
        }

        private Transform[] _orbit;

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
            if (_orbit != null)
            {
                foreach (Transform s in _orbit)
                {
                    if (s != null) Destroy(s.gameObject);
                }
            }

            base.OnDestroy();
        }
    }
}
