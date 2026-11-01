using HistoryHeroes.Dungeon;
using UnityEngine;

namespace HistoryHeroes.Survival
{
    /// <summary>
    /// 서바이벌 맵에 고정으로 깔린 함정 (맵 3 증기 공장: 컨베이어 띠 · 증기 구역). 장면 빌더가 놓고, 판이 시작될 때 TrapZone을 켠다
    /// </summary>
    [RequireComponent(typeof(TrapZone))]
    public sealed class SurvivalHazard : MonoBehaviour
    {
        [SerializeField] private TrapZone.Effect effect = TrapZone.Effect.Conveyor;
        [SerializeField] private Vector2 size = new Vector2(10f, 3f);
        [SerializeField] private float angle;
        [SerializeField] private float damage = 40f;
        [SerializeField] private float phase;

        private void Start()
        {
            GetComponent<TrapZone>().Setup(effect, transform.position, size, angle, damage * Common.GameSession.EnemyAttackScale, phase);
        }
    }
}
