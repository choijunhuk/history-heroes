using HistoryHeroes.Combat;
using UnityEngine;

namespace HistoryHeroes.Enemy
{
    /// <summary>
    /// 투척병 (Content.md 4장): 플레이어가 서 있던 자리에 1초 동안 원으로 예고 → 폭발 → 2초 재투척(빈틈).
    /// 노리는 플레이: 범위 밖으로 이동.
    /// </summary>
    public sealed class BomberEnemy : EnemyBase
    {
        [SerializeField] private float throwDistance = 6f;
        [SerializeField] private float blastRadius = 1.2f;
        [SerializeField] private SpriteRenderer blastMarker;

        [Tooltip("폭탄이 손을 떠나 터질 자리에 닿기까지 (초). 폭발 시점은 그대로 — 던지는 동작을 그만큼 더 일찍 시작한다")]
        [SerializeField] private float flightTime = 0.35f;

        private Vector2 _blastCenter;
        private SpriteRenderer _bomb;
        private Vector2 _bombFrom;

        /// <summary>던지는 동작이 타격(손 놓음)에 이르는 시간 + 폭탄이 날아가는 시간 만큼 일찍 시작해야, 폭탄이 터질 자리에 닿는 순간 터진다</summary>
        protected override float StrikeLead(float impact) => impact + flightTime;

        protected override void Awake()
        {
            base.Awake();
            if (blastMarker != null) blastMarker.enabled = false;
        }

        protected override void Tick()
        {
            switch (state)
            {
                case State.Approach:
                    if (ToPlayer.magnitude > throwDistance)
                    {
                        Walk(ToPlayer, data.moveSpeed);
                    }
                    else
                    {
                        _blastCenter = player.position;
                        ShowMarker(true);
                        Enter(State.Telegraph, data.telegraphTime * TelegraphScale);
                    }
                    break;

                case State.Telegraph:
                    TickBomb();
                    if (stateTimer <= 0f)
                    {
                        HideBomb();
                        Explode();
                        ShowMarker(false);
                        Enter(State.Recover, data.recoverTime);
                    }
                    break;

                case State.Recover:
                    if (stateTimer <= 0f) Enter(State.Approach);
                    break;
            }
        }

        /// <summary>
        /// 날아가는 폭탄 (그림만, 피해·폭발 시점·자리는 그대로): 남은 예고 시간이 flightTime 이하일 때 손에서 떠나
        /// 폭발 자리까지 포물선으로 날아가 터지는 순간 닿는다. 시간을 남은 예고에서 바로 읽으므로 빙결·분노에도 폭발과 같이 간다
        /// </summary>
        private void TickBomb()
        {
            if (stateTimer > flightTime) return;
            float k = 1f - Mathf.Clamp01(stateTimer / Mathf.Max(0.05f, flightTime));
            if (_bomb == null)
            {
                Vector2 toBlast = _blastCenter - (Vector2)transform.position;
                _bombFrom = (Vector2)transform.position + (toBlast.sqrMagnitude > 0.01f ? toBlast.normalized * 0.3f : Vector2.zero);
                _bomb = Telegraphs.Circle(_bombFrom, 0.2f, new Color(0.12f, 0.1f, 0.1f, 1f), 3);
            }

            float arc = 4f * k * (1f - k) * 1.8f; // 땅 위로 솟는 높이 (위 = -Z)
            float hand = (1f - k) * 0.9f; // 처음엔 손 높이
            Vector2 p = Vector2.Lerp(_bombFrom, _blastCenter, k);
            _bomb.transform.position = new Vector3(p.x, p.y, -(arc + hand));
        }

        private void HideBomb()
        {
            if (_bomb == null) return;
            Telegraphs.Hide(_bomb);
            _bomb = null;
        }

        private void ShowMarker(bool show)
        {
            if (blastMarker == null) return;
            blastMarker.enabled = show;
            if (!show) return;
            blastMarker.transform.SetParent(null);
            blastMarker.transform.position = _blastCenter;
            blastMarker.transform.localScale = Vector3.one * blastRadius * 2f;
        }

        private void Explode()
        {
            Audio.AudioManager.Play("explode", 0.6f);
            Collider2D hit = Physics2D.OverlapCircle(_blastCenter, blastRadius, playerMask);
            if (hit != null && hit.TryGetComponent(out Health target))
            {
                target.TryTakeDamage(DamageCalculator.Enemy(Damage, data.id));
            }
        }

        protected override void OnDisable()
        {
            HideBomb();
            base.OnDisable();
        }

        protected override void OnDied(Health h)
        {
            HideBomb();
            ShowMarker(false);
            base.OnDied(h);
        }
    }
}
