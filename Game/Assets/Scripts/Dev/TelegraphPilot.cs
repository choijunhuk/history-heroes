#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Collections.Generic;
using HistoryHeroes.Combat;
using HistoryHeroes.Enemy;
using HistoryHeroes.Player;
using HistoryHeroes.Stats;
using UnityEngine;

namespace HistoryHeroes.Dev
{
    /// <summary>
    /// v0.6 보스 벤치용 일반 조종 AI. 사람이 화면에서 보는 것만 본다:
    /// 원 예고(내려찍기·분사구·균열) 안이면 원 밖으로, 돌진 예고선 위면 옆으로, 부채꼴 예고 안이면 바깥으로, 날아오는 탄은 비킨다.
    /// 위험이 없으면 보스(또는 가까운 졸개)를 사거리 안에 두고 **멈춰서 쏜다**. "dps"는 피하지 않고 체력을 계속 채운다.
    /// 소환 마법진(보라)은 위험이 아니라서 무시한다.
    /// </summary>
    public sealed class TelegraphPilot : MonoBehaviour
    {
        private PlayerController _player;
        private EnemyBase _boss;
        private bool _dpsOnly;
        private readonly List<SpriteRenderer> _telegraphs = new List<SpriteRenderer>();
        private Vector2 _lastSide = Vector2.right;

        public void Begin(PlayerController player, EnemyBase boss, bool dpsOnly)
        {
            _player = player;
            _boss = boss;
            _dpsOnly = dpsOnly;
        }

        private void Update()
        {
            if (_player == null) return;
            if (_boss == null || _boss.Health.IsDead)
            {
                FloatingJoystick.DevOverride = null;
                return;
            }

            if (_dpsOnly)
            {
                _player.Health.Heal(1e9f);
                FloatingJoystick.DevOverride = null;
                return;
            }

            Vector2 move = Dodge();
            if (move == Vector2.zero) move = Position();
            FloatingJoystick.DevOverride = move == Vector2.zero ? (Vector2?)null : move.normalized;
        }

        private Vector2 Me => _player.transform.position;

        private Vector2 Dodge()
        {
            Vector2 me = Me;
            Vector2 push = Vector2.zero;
            Telegraphs.CollectActive(_telegraphs);
            foreach (SpriteRenderer sr in _telegraphs)
            {
                Color c = sr.color;
                if (c.b > 0.8f && c.r < 0.85f && c.g < 0.6f) continue; // 소환 마법진
                Vector2 center = sr.transform.position;
                Vector3 scale = sr.transform.lossyScale;

                if (Telegraphs.IsCircle(sr) || Telegraphs.IsFan(sr))
                {
                    float radius = scale.x * 0.5f + 0.55f;
                    Vector2 d = me - center;
                    if (d.magnitude < radius) push += d.sqrMagnitude > 0.01f ? d.normalized * (radius - d.magnitude + 0.3f) : _lastSide;
                    continue;
                }

                // 선 (돌진 예고): 길쭉한 네모
                if (scale.x > scale.y * 3f && scale.x < 20f)
                {
                    Vector2 dir = sr.transform.right;
                    Vector2 from = center - dir * scale.x * 0.5f;
                    Vector2 rel = me - from;
                    float along = Mathf.Clamp(Vector2.Dot(rel, dir), 0f, scale.x);
                    Vector2 off = rel - dir * along;
                    if (off.magnitude < scale.y * 0.5f + 1f)
                    {
                        Vector2 side = off.sqrMagnitude > 0.0004f ? off.normalized : new Vector2(-dir.y, dir.x);
                        push += side * 1.5f;
                    }
                }
            }

            // 날아오는 탄
            IReadOnlyList<Projectile> arrows = EnemyProjectiles.Active;
            for (int i = 0; i < arrows.Count; i++)
            {
                Projectile a = arrows[i];
                if (a == null) continue;
                Vector2 toMe = me - (Vector2)a.transform.position;
                if (toMe.sqrMagnitude > 16f) continue;
                Vector2 flight = a.Direction;
                float along = Vector2.Dot(toMe, flight);
                if (along <= 0f) continue;
                Vector2 off = toMe - flight * along;
                if (off.magnitude < 0.8f) push += (off.sqrMagnitude > 0.0004f ? off.normalized : new Vector2(-flight.y, flight.x)) * 0.8f;
            }

            // 바로 옆 졸개
            IReadOnlyList<EnemyBase> alive = EnemyBase.Alive;
            for (int i = 0; i < alive.Count; i++)
            {
                EnemyBase e = alive[i];
                if (e == null || e == _boss || e.Health.IsDead) continue;
                Vector2 d = me - (Vector2)e.transform.position;
                if (d.magnitude < 1.1f) push += d.normalized * 0.6f;
            }

            if (push.sqrMagnitude < 0.0001f) return Vector2.zero;
            _lastSide = push.normalized;
            return push;
        }

        /// <summary>위험이 없을 때: 표적(보스, 졸개가 많으면 가까운 졸개)을 사거리 안에 두고 멈춘다</summary>
        private Vector2 Position()
        {
            float range = _player.Stats.Get(StatType.Range);
            Vector2 me = Me;
            Vector2 target = _boss.transform.position;
            float dist = Vector2.Distance(me, target);
            if (dist > range * 0.9f) return target - me;
            if (dist < 1.4f) return me - target; // 너무 붙지 않게
            return Vector2.zero;
        }

        private void OnDestroy() => FloatingJoystick.DevOverride = null;
    }
}
#endif
