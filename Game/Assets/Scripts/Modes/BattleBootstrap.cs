using HistoryHeroes.Common;
using UnityEngine;

namespace HistoryHeroes.Modes
{
    /// <summary>
    /// 전투 장면 하나로 던전과 보스맵을 둘 다 돌린다. 장면이 열릴 때(모든 Start 전에) 모드에 맞는 쪽만 켠다.
    /// 던전: 방·문 + DungeonRun / 보스: 원형 경기장 + BossRun
    /// </summary>
    [DefaultExecutionOrder(-1000)]
    public sealed class BattleBootstrap : MonoBehaviour
    {
        [SerializeField] private GameObject dungeonRoot;
        [SerializeField] private GameObject bossRoot;

        private void Awake()
        {
            bool boss = GameSession.Mode == BattleMode.Boss;
            if (dungeonRoot != null) dungeonRoot.SetActive(!boss);
            if (bossRoot != null) bossRoot.SetActive(boss);
        }
    }
}
