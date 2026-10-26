using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace HistoryHeroes.Enemy
{
    /// <summary>
    /// 2구간 보스 👻 망령 군주 (Content 3장: "잡몹 계속 소환 → 광역 빌드").
    /// - 망령 부르기: 마법진 → 졸개 3/4/5마리 (살아 있는 졸개는 최대 8) → 부른 뒤 빈틈
    /// - 저주 고리: 몸이 빛나다가 사방으로 탄 (2단계부터 두 겹)
    /// - 혼 빨아들이기 (3단계): 플레이어 자리 원 2번
    /// 졸개를 빨리 치우지 못하면 경기장이 졸개로 가득 찬다 → 광역 기록이 유리하다.
    /// </summary>
    public sealed class WraithLordBoss : PatternBoss
    {
        [SerializeField] private EnemyBase minion;
        [SerializeField] private int maxMinions = 8;
        [SerializeField] private int ringShards = 10;
        [SerializeField] private float ringSpeed = 4.5f;

        private readonly List<EnemyBase> _minions = new List<EnemyBase>();
        private int _step;

        protected override void OnTick() => _minions.RemoveAll(m => m == null || m.Health.IsDead || !m.gameObject.activeInHierarchy);

        protected override IEnumerator Cycle()
        {
            _step++;
            if (_step % 2 == 1)
            {
                int want = Mathf.Min(2 + phase, maxMinions - _minions.Count);
                if (want > 0)
                {
                    int before = EnemyBase.Alive.Count;
                    yield return Summon(minion, want, 2.4f, 1f * Tele, 1f);
                    // 새로 나온 졸개를 센다 (살아 있는 적 목록 끝에 붙는다)
                    IReadOnlyList<EnemyBase> alive = EnemyBase.Alive;
                    for (int i = before; i < alive.Count; i++)
                    {
                        if (alive[i] != this) _minions.Add(alive[i]);
                    }
                }

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
