using HistoryHeroes.Common;
using HistoryHeroes.Economy;
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
        [Header("v0.5 펫")]
        [SerializeField] private Pets.PetController pet;
        [SerializeField] private Progress.ExperienceSystem experience;

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

            // 이어하기: 그 판의 영웅
            if (GameSession.Resuming != null && !string.IsNullOrEmpty(GameSession.Resuming.heroId))
            {
                HeroData resumed = roster.Find(GameSession.Resuming.heroId);
                if (resumed != null) return resumed;
            }

            // 일일 도전: 오늘의 영웅 (해금 안 했어도 빌려 쓴다, Modes 5장)
            if (GameSession.Mode == BattleMode.Daily)
            {
                var order = roster.heroes.ConvertAll(h => h.id);
                HeroData today = roster.Find(DailyRules.HeroFor(DailyRules.DayKey(Clock.Now()), order));
                if (today != null) return today;
            }

            // 균열 이어하기: 체크포인트의 영웅
            if (GameSession.Mode == BattleMode.Rift && GameSession.RiftContinue && save.rift.checkpoint.valid)
            {
                HeroData saved = roster.Find(save.rift.checkpoint.heroId);
                if (saved != null) return saved;
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

            if (GameSession.Mode == BattleMode.Daily) return DailyRules.FixedSet(); // 공정한 고정 세트 (펫·유물·수련 없음)
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (DevMeta != null) return DevMeta; // 벤치: 정해 둔 판 밖 성장
#endif
            return Meta.MetaStats.FromAccount(save, table, heroId);
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        /// <summary>벤치용: 던전·서바이벌·균열의 판 밖 성장을 이 값으로 (null = 계정 그대로)</summary>
        public static MetaSnapshot DevMeta;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetDev() => DevMeta = null;
#endif

        private void Awake()
        {
            if (metaTable != null) Missions.MissionEvents.Configure(metaTable.missions, roster, database != null ? database.recipes.Count : 0);
            if (roster != null && player != null)
            {
                HeroData hero = HeroForThisBattle(roster);
                player.SetHero(hero);
                if (metaTable != null && hero != null) player.ApplyMeta(MetaForThisBattle(metaTable, hero.id), metaTable);

                // 룬: 던전·서바이벌·균열만 (보스맵은 레벨업이 없다, 일일 도전은 고정 세트 — Runes 6장)
                if (skills != null && metaTable != null && metaTable.runes != null && hero != null && GameSession.Mode != BattleMode.Boss && GameSession.Mode != BattleMode.Daily)
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
                if (survive && survival != null)
                {
                    cameraFollow.BoundsHalfSize = survival.mapHalfSize;
                    CameraFit fit = cameraFollow.GetComponentInChildren<CameraFit>();
                    if (fit != null) fit.Area = survival.cameraArea;
                }
            }

            if (survive && skills != null && survival != null) skills.ConfigureSlots(survival.activeSlots, survival.passiveSlots); // 6·6

            // 펫: 이번 판 스냅샷의 펫 (던전·서바이벌 = 지금 장착, 보스 = 기록의 펫). 모드 묶음을 켠 뒤라야 던전을 찾는다
            if (pet != null)
            {
                Pets.PetData data = metaTable != null && metaTable.pets != null && player != null ? metaTable.pets.Find(player.Meta.petId) : null;
                if (data != null) pet.Setup(data, player.Meta.petLevel, player, experience);
                else pet.gameObject.SetActive(false);
            }
        }
    }
}
