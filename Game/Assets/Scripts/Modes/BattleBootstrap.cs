using HistoryHeroes.Common;
using HistoryHeroes.Heroes;
using HistoryHeroes.Player;
using HistoryHeroes.Records;
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
        [SerializeField] private HeroRoster roster;
        [SerializeField] private PlayerController player;

        /// <summary>
        /// 이번 판 영웅: 던전 = 로비에서 고른 영웅, 보스 = 기록에 저장된 영웅 (GameDesign 7-1 "사용한 영웅").
        /// 플레이어의 Awake보다 먼저 정해야 능력치가 그 영웅으로 만들어진다.
        /// </summary>
        public static HeroData HeroForThisBattle(HeroRoster roster)
        {
            SaveData save = SaveSystem.Data;
            if (GameSession.Mode == BattleMode.Boss)
            {
                RunRecord record = save.FindRecord(GameSession.BossRecordId);
                if (record == null && save.records.Count > 0) record = save.records[save.records.Count - 1];
                if (record != null) return roster.Find(record.heroId);
            }

            return HeroUnlock.Selected(save, roster);
        }

        private void Awake()
        {
            if (roster != null && player != null) player.SetHero(HeroForThisBattle(roster));

            bool boss = GameSession.Mode == BattleMode.Boss;
            if (dungeonRoot != null) dungeonRoot.SetActive(!boss);
            if (bossRoot != null) bossRoot.SetActive(boss);
        }
    }
}
