using System.Collections;
using System.Collections.Generic;
using HistoryHeroes.Combat;
using UnityEngine;

namespace HistoryHeroes.Enemy
{
    /// <summary>
    /// 2구간 보스 👻 망령 군주 (Content 3장: "잡몹 계속 소환 → 광역 빌드").
    /// - 망령 부르기: 마법진 → 졸개 3/4/5마리 (살아 있는 졸개는 최대 8) → 부른 뒤 빈틈
    /// - 저주 고리: 몸이 빛나다가 사방으로 탄 (2단계부터 두 겹)
    /// - 혼 빨아들이기 (3단계): 플레이어 자리 원 2번
    /// - 망령의 가호: 살아 있는 졸개 1마리마다 받는 피해 -12% (최대 -60%), 보스 둘레에 푸른 막으로 보인다
    /// 졸개를 빨리 치우지 못하면 보스가 단단해지고 경기장이 졸개로 가득 찬다 → 광역 기록이 유리하다 (v0.6 역할 벤치).
    /// </summary>
    public sealed class WraithLordBoss : PatternBoss
    {
        [SerializeField] private EnemyBase minion;
        [SerializeField] private int maxMinions = 8;
        [SerializeField] private int ringShards = 10;
        [SerializeField] private float ringSpeed = 4.5f;
        [Tooltip("살아 있는 졸개 1마리마다 받는 피해 감소")]
        [SerializeField] private float wardPerMinion = 0.12f;
        [SerializeField] private float wardMax = 0.6f;

        private static readonly Color WardColor = new Color(0.55f, 0.45f, 1f, 0.3f); // 푸른 막 (위험 예고와 다른 색)
        private SpriteRenderer _ward;

        private readonly List<EnemyBase> _minions = new List<EnemyBase>();
        private int _step;

        /// <summary>지금 받는 피해 감소 (졸개 수)</summary>
        public float WardReduction => Mathf.Min(wardMax, _minions.Count * wardPerMinion);

        protected override void OnSpawned()
        {
            _minions.Clear();
            _step = 0;
            _ward = null;
            health.Guard = Ward;
        }

        private DamageInfo Ward(DamageInfo info)
        {
            float cut = WardReduction;
            if (cut > 0f) info.Amount *= 1f - cut;
            return info;
        }

        protected override void OnTick()
        {
            _minions.RemoveAll(m => m == null || m.Health.IsDead || !m.gameObject.activeInHierarchy);
            float cut = WardReduction;
            if (cut > 0f)
            {
                if (_ward == null || !_ward.gameObject.activeSelf) _ward = Show(Telegraphs.Circle(Pos, transform.localScale.x * 0.8f, WardColor, 1));
                _ward.transform.position = Pos;
                Color c = WardColor;
                c.a = 0.12f + cut * 0.5f; // 졸개가 많을수록 진하게
                _ward.color = c;
            }
            else if (_ward != null)
            {
                HideShown(_ward);
                _ward = null;
            }
        }

        protected override IEnumerator Cycle()
        {
            _step++;
            if (_step % 2 == 1)
            {
                int want = Mathf.Min(2 + phase, maxMinions - _minions.Count);
                if (want > 0) yield return Summon(minion, want, 2.4f, 1f * Tele, 1f, _minions);

                Blink(Random.insideUnitCircle * 2.4f);
                yield return Open(1.8f);
                yield break;
            }

            yield return Glow(0.9f * Tele, new Color(0.6f, 0.4f, 1f, 0.45f));
            Ring(ringShards + phase * 2, ringSpeed, 0.8f, "boss_wraith_ring");
            if (phase >= 2)
            {
                yield return Wait(0.35f);
                Ring(ringShards + phase * 2, ringSpeed, 0.8f, "boss_wraith_ring", 0.5f);
            }

            if (phase >= 3)
            {
                yield return CircleStrike(PlayerPos, 1.3f, 0.9f * Tele, 1.2f, "boss_wraith_drain");
                yield return CircleStrike(PlayerPos, 1.3f, 0.7f * Tele, 1.2f, "boss_wraith_drain");
            }

            yield return Open(1.5f);
        }
    }
}
