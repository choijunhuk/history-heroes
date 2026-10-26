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

        private Vector2 _blastCenter;

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
                    if (stateTimer <= 0f)
                    {
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
            Collider2D hit = Physics2D.OverlapCircle(_blastCenter, blastRadius, playerMask);
            if (hit != null && hit.TryGetComponent(out Health target))
            {
                target.TryTakeDamage(DamageCalculator.Enemy(Damage, data.id));
            }
        }

        protected override void OnDied(Health h)
        {
            ShowMarker(false);
            base.OnDied(h);
        }
    }
}
