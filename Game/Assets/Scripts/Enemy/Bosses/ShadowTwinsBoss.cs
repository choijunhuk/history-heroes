using System.Collections;
using HistoryHeroes.Combat;
using UnityEngine;

namespace HistoryHeroes.Enemy
{
    /// <summary>
    /// 4구간 보스 🌑 그림자 쌍둥이 (Content 3장: "둘이 따로 움직임 → 단일 순간 화력").
    /// 둘이 따로 움직이며 같이 공격하지만, **맞는 것은 한 번에 하나**(진한 쪽)다. 잠깐 서는 빈틈 동안 몰아쳐야 하고,
    /// 공격이 끝나면 둘이 자리를 바꾼다 (표적을 다시 잡아야 한다). 체력은 하나 (진한 쪽이 맞는다).
    /// - 엇갈려 돌진: 둘이 양쪽에서 동시에
    /// - 그림자 단검: 둘 다 부채꼴 (3단계: 더 많이)
    /// </summary>
    public sealed class ShadowTwinsBoss : PatternBoss
    {
        [SerializeField] private SpriteRenderer twin;
        [SerializeField] private float twinSpeed = 2.6f;
        [SerializeField] private float knifeSpeed = 6f;
        [SerializeField] private float knifeDamage = 0.7f;

        private int _step;
        private Vector2 _twinPos;
        private Vector2 _twinDash;
        private float _twinDashUntil;
        private bool _twinHit;

        protected override void OnSpawned()
        {
            if (twin == null) return;
            twin.transform.SetParent(null, true);
            _twinPos = Pos + new Vector2(2.5f, 0f);
            twin.enabled = true;
        }

        protected override void OnTick()
        {
            if (twin == null || player == null) return;
            if (bossTime < _twinDashUntil)
            {
                _twinPos += _twinDash * 11f * Time.deltaTime;
                if (!_twinHit) _twinHit = HitPlayerIn(_twinPos, 0.6f, 1.2f, "boss_twins_dash");
            }
            else
            {
                Vector2 to = (Vector2)player.position - _twinPos;
                if (to.magnitude > 2.4f) _twinPos += to.normalized * twinSpeed * Time.deltaTime;
            }

            _twinPos = Vector2.ClampMagnitude(_twinPos, 3.6f); // 경기장 안
            twin.transform.position = _twinPos;
            twin.transform.localScale = transform.localScale;
        }

        protected override IEnumerator Cycle()
        {
            _step++;
            if (_step % 2 == 1)
            {
                // 엇갈려 돌진: 그림자 쪽도 예고선을 보여 준 뒤 둘이 동시에
                Vector2 twinDir = ((Vector2)player.position - _twinPos).normalized;
                SpriteRenderer line = Show(Telegraphs.Line(_twinPos, _twinPos + twinDir * 9f, 0.5f, Telegraphs.Danger));
                float telegraph = 0.85f * Tele;
                float start = bossTime;
                Enter(State.Telegraph);
                while (bossTime < start + telegraph)
                {
                    Telegraphs.Fill(line, Telegraphs.Danger, (bossTime - start) / telegraph);
                    yield return null;
                }

                HideShown(line);
                _twinDash = twinDir;
                _twinDashUntil = bossTime + 0.7f;
                _twinHit = false;
                yield return Dash(0.25f, 10.5f, 0.9f, 1.2f, "boss_twins_dash", 1.6f, 1.2f);
            }
            else
            {
                yield return Glow(0.7f * Tele, new Color(0.4f, 0.2f, 0.6f, 0.5f));
                int knives = phase >= 3 ? 7 : 5;
                Fan(knives, 60f, knifeSpeed, knifeDamage, "boss_twins_knife");
                FanFrom(_twinPos, knives, 60f, knifeSpeed, knifeDamage, "boss_twins_knife");
                yield return Open(phase >= 2 ? 1.4f : 1.8f);
            }

            Swap();
        }

        /// <summary>자리 바꾸기: 진한 쪽(맞는 쪽)과 그림자가 순간 이동으로 바뀐다</summary>
        private void Swap()
        {
            if (twin == null) return;
            Vector2 main = Pos;
            Blink(_twinPos);
            _twinPos = main;
            twin.transform.position = main;
        }

        protected override void OnDied(Health h)
        {
            if (twin != null) twin.enabled = false;
            base.OnDied(h);
        }

        private void OnDestroy()
        {
            if (twin != null && twin.transform.parent == null) Destroy(twin.gameObject);
        }
    }
}
