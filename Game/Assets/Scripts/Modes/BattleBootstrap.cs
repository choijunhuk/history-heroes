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
        [Header("v0.5 서바이벌")]
        [SerializeField] private GameObject survivalRoot;
        [Tooltip("던전·보스가 같이 쓰는 방 벽 (서바이벌에서는 끈다)")]
        [SerializeField] private GameObject arenaRoot;
        [SerializeField] private CameraFollow cameraFollow;
        [SerializeField] private Survival.SurvivalData survival;
        [SerializeField] private HeroRoster roster;
        [SerializeField] private PlayerController player;
        [Tooltip("판 밖 성장 표 (v0.4). 비어 있으면 판 밖 성장 없이 싸운다")]
        [SerializeField] private Meta.MetaTable metaTable;
        [Tooltip("룬 (던전만): 카드 뽑기 ②단계 배율")]
        [SerializeField] private Skills.SkillManager skills;
        [SerializeField] private Skills.SkillDatabase database;

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

        /// <summary>
        /// 이번 판의 판 밖 성장: 던전 = 지금 계정, 보스 = 기록에 고정된 것 (GameDesign 7-3 "현재 장비를 덧붙이지 않는다").
        /// </summary>
        public static MetaSnapshot MetaForThisBattle(Meta.MetaTable table, string heroId)
        {
            SaveData save = SaveSystem.Data;
            if (GameSession.Mode == BattleMode.Boss)
            {
                RunRecord record = save.FindRecord(GameSession.BossRecordId);
                if (record == null && save.records.Count > 0) record = save.records[save.records.Count - 1];
                return record != null && record.meta != null ? record.meta : new MetaSnapshot();
            }

            return Meta.MetaStats.FromAccount(save, table, heroId);
        }

        private void Awake()
        {
            if (roster != null && player != null)
            {
                HeroData hero = HeroForThisBattle(roster);
                player.SetHero(hero);
                if (metaTable != null && hero != null) player.ApplyMeta(MetaForThisBattle(metaTable, hero.id), metaTable);

                // 룬: 던전·서바이벌만 (보스맵은 레벨업이 없다, 기록에도 저장하지 않는다 — Runes 6장)
                if (skills != null && metaTable != null && metaTable.runes != null && hero != null && GameSession.Mode != BattleMode.Boss)
                {
                    skills.WeightModifier = Runes.RuneService.ModifierFor(SaveSystem.Data, metaTable.runes, hero.id, database != null ? database.recipes : null);
                }
            }

            bool boss = GameSession.Mode == BattleMode.Boss;
            bool survive = GameSession.Mode == BattleMode.Survival;
            if (dungeonRoot != null) dungeonRoot.SetActive(!boss && !survive);
            if (bossRoot != null) bossRoot.SetActive(boss);
            if (survivalRoot != null) survivalRoot.SetActive(survive);
            if (arenaRoot != null) arenaRoot.SetActive(!survive);
            if (cameraFollow != null)
            {
                cameraFollow.enabled = survive;
                if (survive && survival != null) cameraFollow.BoundsHalfSize = survival.mapHalfSize;
            }

            if (survive && skills != null && survival != null) skills.ConfigureSlots(survival.activeSlots, survival.passiveSlots); // 6·6
        }
    }
}
