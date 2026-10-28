using System.Collections;
using System.Collections.Generic;
using HistoryHeroes.Combat;
using HistoryHeroes.Common;
using HistoryHeroes.Economy;
using HistoryHeroes.Enemy;
using HistoryHeroes.Player;
using HistoryHeroes.Progress;
using HistoryHeroes.Records;
using HistoryHeroes.Skills;
using HistoryHeroes.UI;
using UnityEngine;
using Func = System.Func<HistoryHeroes.Enemy.EnemyBase, UnityEngine.Vector2, float, float, HistoryHeroes.Enemy.EnemyBase>;

namespace HistoryHeroes.Dungeon
{
    /// <summary>
    /// 던전 한 판의 흐름: 방 입장 → 적 등장(예고 표시) → 전멸하면 문 열림 → 문으로 다음 방 → 마지막 방을 깨면 결과.
    /// 엘리트를 잡으면 확정 레벨업. 플레이어가 쓰러지면 바로 결과. (Content 2-2, 2-4)
    /// v0.6: 방 15개 (보상 방 6·11, 중간 보스 15). 판 시드(RunRandom.Rooms)로 방마다 레이아웃과 적 편성을 판 시작에 정한다
    /// → 같은 시드 = 같은 판 (일일 도전). 소환사·분열체의 졸개는 EnemyBase.ChildSpawner로 받아서 방 전멸 판정에 넣는다.
    /// </summary>
    public sealed partial class DungeonRun : MonoBehaviour
    {
        [SerializeField] private DungeonData dungeon;
        [SerializeField] private SkillDatabase database;

        [Header("장면")]
        [SerializeField] private PlayerController player;
        [Tooltip("장비 드랍 (v0.4)")]
        [SerializeField] private Meta.MetaTable metaTable;
        [SerializeField] private ExperienceSystem experience;
        [SerializeField] private SkillManager skills;
        [SerializeField] private Door door;
        [SerializeField] private Transform playerStart;

        [Header("UI")]
        [SerializeField] private LevelUpScreen levelUp;
        [SerializeField] private HudView hud;
        [SerializeField] private RunResultScreen result;
        [SerializeField] private RecordSaveScreen recordSave;
        [SerializeField] private CanvasGroup fade;
        [Tooltip("중간 보스 체력 막대 (v0.6)")]
        [SerializeField] private BossHud bossHud;
        [SerializeField] private RewardRoomPanel rewardRoom;
        [Tooltip("레이아웃 장애물·함정을 놓는 곳")]
        [SerializeField] private Transform layoutRoot;
        [Tooltip("무한의 균열 10층마다 나오는 보스맵 보스 (1~5구간 순서)")]
        [SerializeField] private List<Modes.BossData> riftBosses = new List<Modes.BossData>();

        [Header("등장")]
        [Tooltip("적이 나올 수 있는 영역 (월드 좌표)")]
        [SerializeField] private Rect spawnArea = new Rect(-3.5f, -1f, 7f, 7.5f);
        [SerializeField] private float minDistanceFromPlayer = 3.5f;
        [Tooltip("적이 나오기 전 바닥 표시 시간 (갑자기 옆에 생겨서 맞는 일이 없게)")]
        [SerializeField] private float spawnTelegraph = 0.6f;
        [SerializeField] private float spawnStagger = 0.08f;

        private readonly Dictionary<EnemyBase, List<EnemyBase>> _pools = new Dictionary<EnemyBase, List<EnemyBase>>();
        private readonly List<SpriteRenderer> _markers = new List<SpriteRenderer>();
        private readonly List<string> _fusions = new List<string>();
        private int _room;
        private int _roomsCleared;
        private int _hitsTaken;
        private int _hitsAtRoomStart;
        private int _alive;
        private bool _finished;
        private float _startTime;

        private List<RoomPlan> _plans;
        private List<RoomLayout> _layouts;
        private List<List<SpawnEntry>> _encounters;
        private ChapterData _chapter;
        private Func _childSpawner;
        private readonly List<GameObject> _layoutObjects = new List<GameObject>();
        private static int _obstacleMask = -1;

        // v0.6 무한의 균열 · 일일 도전
        private bool _rift;
        private bool _daily;
        private float _enemyHp = 1f;
        private float _enemyAttack = 1f;
        private float _budgetScale = 1f;
        private float _rewardHeal = 0.3f;
        private readonly object _modeOwner = new object();
        private readonly List<string> _modeLines = new List<string>();
        private LayoutKind? _lastRiftLayout;
        private GimmickKind _riftGimmick;
        private EnemyBase _riftBoss;
        private bool _riftFloor5;
        /// <summary>이 판이 속한 주 (월요일 05:00을 넘기면 이 판의 층 보상·체크포인트는 거기까지)</summary>
        private string _riftWeek;
        private bool _riftSeasonNoticed;
        /// <summary>이어한 방의 보상을 이미 받았나 (그 방에서는 보상 방을 다시 열지 않는다)</summary>
        private bool _resumeRewardTaken;
        private int _resumeRoom;

        public bool IsRift => _rift;
        public bool IsDaily => _daily;

        public int Room => _room;
        public ChapterData Chapter => _chapter;
        public RoomPlan CurrentPlan => _plans != null && _room >= 1 && _room <= _plans.Count ? _plans[_room - 1] : null;
        public RoomLayout CurrentLayout => _layouts != null && _room >= 1 && _room <= _layouts.Count ? _layouts[_room - 1] : null;
        public IReadOnlyList<SpawnEntry> EncounterOf(int room) => _encounters != null && room >= 1 && room <= _encounters.Count ? _encounters[room - 1] : null;
        public IReadOnlyList<RoomLayout> Layouts => _layouts;
        public RewardRoomPanel RewardRoom => rewardRoom;

        /// <summary>새 방에 들어갔다 (방 번호). 흰 비둘기 보물 찾기 등</summary>
        public event System.Action<int> RoomEntered;

        /// <summary>적이 나오는 영역 (방 안쪽)</summary>
        public Rect SpawnArea => spawnArea;
        /// <summary>던전 15, 일일 도전 7. 무한의 균열은 끝이 없다 (아주 큰 수)</summary>
        public int RoomCount => _rift ? 9999 : _plans != null ? _plans.Count : dungeon.plan.Count;
        public bool IsFinished => _finished;

        private void Start()
        {
            GamePause.ResetAll();
            DamageLog.Reset();
            _startTime = Time.time;
            _rift = GameSession.Mode == BattleMode.Rift;
            _daily = GameSession.Mode == BattleMode.Daily;
            ApplyModeRules();
            PlanRun();
            _childSpawner = SpawnChild;
            EnemyBase.ChildSpawner = _childSpawner;
            skills.RiftBoosts = _rift;
            player.Health.Died += _ => Finish(victory: false);
            player.Health.Damaged += (_, dealt) =>
            {
                if (dealt > 0f && !_finished) _hitsTaken++;
            };
            ShowHitCounter();
            skills.FusionCompleted += recipe => _fusions.Add(recipe.result.displayName);
            door.Entered += () => StartCoroutine(GoToNextRoom());
            WireSpecials(); // 갈림길 문 · 도전 방 피격 (확장 1차 3장)
            if (fade != null) fade.alpha = 0f;
            int first = _rift ? ResumeRift() : 1;
            RunSnapshot resume = GameSession.ConsumeResume();
            if (resume != null) first = RestoreSnapshot(resume);
            EnterRoom(first);
        }

        // ---------------------------------------------------------------- v1.0 판 이어하기

        /// <summary>방에 들어갈 때 판 상태를 남긴다 (일일 도전 공식 판은 남기지 않는다 — 끄면 0점)</summary>
        private void SaveSnapshot(int room)
        {
            if (_daily && GameSession.DailyOfficial) return;
            if (GameSession.Tutorial) return; // 튜토리얼은 처음부터 다시
            var s = new RunSnapshot
            {
                valid = true,
                mode = (int)GameSession.Mode,
                chapter = GameSession.Chapter,
                seed = RunRandom.Seed,
                practice = GameSession.Practice,
                dayKey = _daily ? Modes.DailyRules.DayKey(Clock.Now()) : null,
                weekKey = _rift ? _riftWeek : null,
                room = room,
                heroId = skills.HeroId,
                level = experience.Level.Level,
                experience = experience.Level.Experience,
                hpRatio = player.Health.Current / Mathf.Max(1f, player.Health.Max),
                kills = experience.Kills,
                roomsCleared = _roomsCleared,
                hits = _hitsTaken,
                elapsed = Time.time - _startTime,
                offersMade = skills.OffersMade,
                choseLegendary = skills.ChoseLegendary,
                rerollsLeft = levelUp.RerollsLeft,
                rewardTaken = _resumeRewardTaken && room == _resumeRoom,
                heartSpent = GameSession.HeartSpent,
                hasMeta = true,
                meta = JsonUtility.FromJson<MetaSnapshot>(JsonUtility.ToJson(player.Meta)),
                savedAt = Clock.UnixSeconds,
            };
            skills.CaptureFusionHistory(s);
            foreach (SkillInstance k in skills.Loadout.Actives) s.skills.Add(new RecordSkill(k.Data.id, k.Level, k.IsFusionResult));
            foreach (SkillInstance k in skills.Loadout.Passives) s.skills.Add(new RecordSkill(k.Data.id, k.Level, k.IsFusionResult));
            s.boosts = skills.RiftBoostCounts();
            s.fusions.AddRange(_fusions);
            s.rng = RunRandom.Capture();
            CaptureSpecial(s, room);
            SaveSystem.Data.run = s;
            SaveSystem.Save();
        }

        /// <summary>보상을 받았다고 스냅샷에 적고 바로 저장 (골드와 "받음"이 같이 남는다 → 껐다 켜도 다시 못 받는다)</summary>
        private void MarkRewardTaken()
        {
            RunSnapshot run = SaveSystem.Data.run;
            if (run != null && run.valid && run.room == _room) run.rewardTaken = true;
            _resumeRewardTaken = true;
            _resumeRoom = _room;
            SaveSystem.Save();
        }

        /// <summary>저장해 둔 판으로 되돌린다: 빌드·레벨·체력·처치·피격·시간·난수. 돌아갈 방 번호를 돌려준다</summary>
        private int RestoreSnapshot(RunSnapshot s)
        {
            int room = Mathf.Max(1, s.room);
            if (_rift) EnsureRiftFloor(room + 9);
            else room = Mathf.Min(room, _plans.Count);
            skills.ApplyRecord(s.skills, database);
            skills.RestoreRiftBoosts(s.boosts);
            skills.RestoreRunState(s.offersMade, s.choseLegendary);
            skills.RestoreFusionHistory(s, database);
            _resumeRewardTaken = s.rewardTaken;
            _resumeRoom = room;
            experience.Level.Restore(s.level, s.experience);
            experience.RestoreKills(s.kills);
            player.Health.Revive(player.Health.Max * Mathf.Clamp(s.hpRatio, 0.05f, 1f));
            _hitsTaken = s.hits;
            _roomsCleared = s.roomsCleared;
            _startTime = Time.time - Mathf.Max(0f, s.elapsed);
            _fusions.AddRange(s.fusions);
            levelUp.RestoreRerolls(s.rerollsLeft);
            RunRandom.Restore(s.rng);
            RestoreSpecial(s, room); // 갈림길 선택 · 축복 (최대체력이 바뀌므로 체력은 아래에서 다시)
            player.Health.Revive(player.Health.Max * Mathf.Clamp(s.hpRatio, 0.05f, 1f));
            hud.ShowNotice(_rift ? $"균열 {room}층부터 이어서" : $"방 {room}부터 이어서", 2.5f);
            return room;
        }

        // ---------------------------------------------------------------- v0.6 모드 규칙

        /// <summary>무한 주간 변형 1개 · 일일 도전 변형 2개 (적 배수는 이 판 안에서만, 플레이어 보정은 이 판 출처로)</summary>
        private void ApplyModeRules()
        {
            _enemyHp = GameSession.EnemyHpScale;
            _enemyAttack = GameSession.EnemyAttackScale;
            if (GameSession.Tutorial)
            {
                // 튜토리얼: 지지 않는다 (쓰러질 만큼 맞으면 체력 50%로), 경험치 두 배, 카드는 정해 둔 순서부터
                experience.ExpMultiplier = Progress.Tutorial.ExpMultiplier;
                player.Health.PreventDeath = () =>
                {
                    player.Health.Revive(player.Health.Max * Progress.Tutorial.SaveHp);
                    return true;
                };
                skills.TutorialCards = Progress.Tutorial.CardOrder;
            }

            Stats.StatSheet stats = player.Stats;
            void Mod(Stats.StatType stat, Stats.ModifierKind kind, float value, Stats.StatTarget target = Stats.StatTarget.Both) =>
                stats.Add(new Stats.StatModifier(stat, kind, value, Stats.ModifierSource.Skill, _modeOwner, target));

            if (_rift)
            {
                SaveData save = SaveSystem.Data;
                Modes.RiftRules.EnsureWeek(save, Clock.Now());
                _riftWeek = save.rift.weekKey;
                Modes.RiftRules.Variant v = Modes.RiftRules.VariantFor(save.rift.weekKey);
                switch (v)
                {
                    case Modes.RiftRules.Variant.ToughEnemiesMoreExp:
                        _enemyHp *= 1.3f;
                        experience.ExpMultiplier = 1.3f;
                        break;
                    case Modes.RiftRules.Variant.FierceEnemiesStronger:
                        _enemyAttack *= 1.25f;
                        Mod(Stats.StatType.Attack, Stats.ModifierKind.Percent, 0.15f);
                        break;
                    case Modes.RiftRules.Variant.FastArrowsFastFeet:
                        EnemyProjectiles.SpeedScale = 1.2f;
                        Mod(Stats.StatType.MoveSpeed, Stats.ModifierKind.Percent, 0.1f);
                        break;
                    default:
                        Mod(Stats.StatType.CooldownReduction, Stats.ModifierKind.Flat, 0.2f);
                        Mod(Stats.StatType.Attack, Stats.ModifierKind.Percent, -0.15f, Stats.StatTarget.BasicAttack);
                        break;
                }

                hud.ShowNotice(Modes.RiftRules.VariantText(v), 3f);
            }

            if (_daily)
            {
                string day = Modes.DailyRules.DayKey(Clock.Now());
                foreach (Modes.DailyRules.Modifier m in Modes.DailyRules.ModifiersFor(day))
                {
                    switch (m)
                    {
                        case Modes.DailyRules.Modifier.DoubleEnemiesNoHeal:
                            _budgetScale = 2f;
                            _rewardHeal = 0f;
                            break;
                        case Modes.DailyRules.Modifier.FastSkillsWeakShots:
                            Mod(Stats.StatType.CooldownReduction, Stats.ModifierKind.Flat, 0.3f);
                            Mod(Stats.StatType.Attack, Stats.ModifierKind.Percent, -0.3f, Stats.StatTarget.BasicAttack);
                            break;
                        case Modes.DailyRules.Modifier.ToughEnemiesStronger:
                            _enemyHp *= 1.4f;
                            Mod(Stats.StatType.Attack, Stats.ModifierKind.Percent, 0.2f);
                            break;
                        case Modes.DailyRules.Modifier.FastArrows:
                            EnemyProjectiles.SpeedScale = 1.25f;
                            break;
                        case Modes.DailyRules.Modifier.StrongShotsWeakSkills:
                            Mod(Stats.StatType.Attack, Stats.ModifierKind.Percent, 0.4f, Stats.StatTarget.BasicAttack);
                            Mod(Stats.StatType.ActiveDamage, Stats.ModifierKind.Flat, -0.3f);
                            break;
                        default:
                            Mod(Stats.StatType.MoveSpeed, Stats.ModifierKind.Percent, 0.2f);
                            Mod(Stats.StatType.DamageTaken, Stats.ModifierKind.Flat, 0.2f); // 받는 피해 +20% (피해 감소 -0.2는 0에서 잘려 안 먹었다)
                            break;
                    }
                }
            }
        }

        /// <summary>균열 이어하기: 체크포인트의 빌드·레벨·체력으로 다음 층부터 (그 층까지 방 난수를 같은 순서로 돌려 같은 판을 잇는다)</summary>
        private int ResumeRift()
        {
            RiftCheckpoint cp = SaveSystem.Data.rift.checkpoint;
            if (!GameSession.RiftContinue || !cp.valid) return 1;
            EnsureRiftFloor(cp.floor + 1);
            skills.ApplyRecord(cp.skills, database);
            skills.RestoreRiftBoosts(cp.boosts);
            experience.Level.Restore(cp.level, cp.experience);
            player.Health.Revive(player.Health.Max * Mathf.Clamp(cp.hpRatio, 0.2f, 1f));
            _roomsCleared = cp.floor;
            hud.ShowNotice($"균열 {cp.floor + 1}층부터 이어서", 2.5f);
            return cp.floor + 1;
        }

        /// <summary>균열 층 계획을 floor까지 만든다 (방 난수를 층 순서대로 쓴다 → 같은 시드면 같은 층)</summary>
        private void EnsureRiftFloor(int floor)
        {
            System.Random rooms = RunRandom.Rooms;
            while (_plans.Count < floor)
            {
                int f = _plans.Count + 1;
                RoomPlan plan = Modes.RiftRules.PlanFor(f);
                ChapterData chapter = dungeon.Chapter(Modes.RiftRules.ChapterFor(f));
                RoomLayout layout = dungeon.specialLayout;
                int chapterNumber = chapter != null ? chapter.chapter : 0;
                if (plan.kind == RoomKind.Normal && dungeon.layouts.Count > 0)
                {
                    // 일반 용도 방만 (엘리트 투기장·보상 받침대는 그 층 종류일 때), 그 층 구간 전용 방도 섞는다
                    List<RoomLayout> options = Encounter.NormalOptions(dungeon.layouts, chapterNumber, null, _lastRiftLayout);
                    layout = options.Count > 0 ? Encounter.PickNormal(options, chapterNumber, rooms) : dungeon.specialLayout;
                    _lastRiftLayout = layout.kind;
                }
                else
                {
                    layout = Encounter.PickSpecial(plan.kind, dungeon.layouts, dungeon.specialLayout, chapterNumber, rooms);
                    _lastRiftLayout = null;
                }

                _plans.Add(plan);
                _layouts.Add(layout);
                // 새 적이 나오는 층 감각: 그 구간 안에서 몇 번째 층인가
                _encounters.Add(Encounter.Build(chapter, plan, (f - 1) % 10 + 1, rooms));
            }
        }

        /// <summary>판 시작: 시드(일일 도전은 날짜)로 방마다 레이아웃과 적 편성을 정한다</summary>
        private void PlanRun()
        {
            if (GameSession.RunSeed != 0) RunRandom.Begin(GameSession.RunSeed);
            else RunRandom.BeginRandom();
            _chapter = dungeon.Chapter(GameSession.Chapter);
            if (_rift)
            {
                _plans = new List<RoomPlan>();
                _layouts = new List<RoomLayout>();
                _encounters = new List<List<SpawnEntry>>();
                return; // 층은 들어갈 때 만든다 (끝이 없다)
            }

            _plans = GameSession.CustomPlan != null ? new List<RoomPlan>(GameSession.CustomPlan) : new List<RoomPlan>(dungeon.plan);
            if (_budgetScale != 1f)
            {
                for (int i = 0; i < _plans.Count; i++)
                {
                    RoomPlan copy = _plans[i].Copy();
                    copy.budget = Mathf.RoundToInt(copy.budget * _budgetScale);
                    _plans[i] = copy;
                }
            }

            System.Random rooms = RunRandom.Rooms;
            _layouts = Encounter.PickLayouts(_plans, dungeon.layouts, dungeon.specialLayout, rooms, _chapter != null ? _chapter.chapter : 0); // 구간 전용 방 · 전용 모양 (확장 1차 1장)
            if (GameSession.Tutorial)
            {
                for (int i = 0; i < _layouts.Count; i++) _layouts[i] = dungeon.specialLayout; // 튜토리얼은 장애물·함정 없는 방
            }

            _encounters = new List<List<SpawnEntry>>(_plans.Count);
            for (int i = 0; i < _plans.Count; i++) _encounters.Add(Encounter.Build(_chapter, _plans[i], i + 1, rooms));
            PlanSpecials(); // 갈림길·도전·석비·저주 (따로 떼어 둔 갈림길 난수, 15방 던전만)
        }

        private void EnterRoom(int room)
        {
            _room = room;
            _hitsAtRoomStart = _hitsTaken;
            levelUp.Floor = room;
            RunRandom.BeginRoom(room); // 방마다 적 난수 새로 (갈림길 · 플레이 차이가 뒤 방에 번지지 않게)
            BeginSpecialRoom(room);
            if (_rift)
            {
                EnsureRiftFloor(room + 9); // 진행 바에 보일 층까지
                _chapter = dungeon.Chapter(Modes.RiftRules.ChapterFor(room));
                if (_chapter != null) StageLook.Show(StageLook.KeyFor(GameSession.Mode, _chapter.chapter)); // 층의 구간 색
                if (room >= 5 && !_riftFloor5)
                {
                    _riftFloor5 = true;
                    Missions.MissionEvents.Raise(Missions.MissionEvents.RiftFloor5); // 일일 미션 "균열 5층 이상"
                }
            }

            RoomPlan plan = _plans[room - 1];
            string tag = plan.kind == RoomKind.Elite ? "  엘리트" : plan.kind == RoomKind.Reward ? "  보상" : plan.kind == RoomKind.MidBoss ? "  중간 보스" : plan.kind == RoomKind.Boss ? "  보스" : string.Empty;
            tag = SpecialTag() ?? tag;
            if (_rift)
            {
                hud.RoomText = $"무한의 균열 {room}층{tag}";
            }
            else
            {
                string chapter = _daily ? "일일 도전 " : GameSession.Chapter > 1 ? $"{GameSession.Chapter}구간 " : string.Empty;
                _roomTitle = $"{chapter}방 {room}/{RoomCount}{tag}";
                hud.RoomText = _roomTitle;
            }

            door.SetOpen(false);
            SaveSnapshot(room);
            var kinds = new List<RoomKind>();
            int first = _rift ? (room - 1) / 10 * 10 : 0; // 균열: 지금 10층 묶음만
            int count = _rift ? 10 : _plans.Count;
            for (int i = first; i < first + count && i < _plans.Count; i++) kinds.Add(_plans[i].kind);
            hud.SetRoomProgress(room - first, kinds);
            MarkSpecialCells(first);
            ApplyLayout(_layouts[room - 1]);

            if (plan.kind == RoomKind.Reward && _resumeRewardTaken && room == _resumeRoom)
            {
                _alive = 0; // 이미 받은 보상 방 → 바로 다음 방으로
                OnRoomCleared();
            }
            else if (plan.kind == RoomKind.Reward && CurrentSpecial == RoomSpecial.Stele) OpenStele();
            else if (plan.kind == RoomKind.Reward) OpenRewardRoom();
            else if (plan.kind == RoomKind.Boss) StartCoroutine(SpawnRiftBoss(room));
            else if (StartChallenge(plan)) { } // 도전 방 (갈림길 오른쪽)
            else StartCoroutine(SpawnRoom(plan, _encounters[room - 1]));
            RoomEntered?.Invoke(room);
        }

        /// <summary>균열 10층마다: 보스맵 보스 (균열 버전 — 2단계 패턴부터)</summary>
        private IEnumerator SpawnRiftBoss(int floor)
        {
            _alive = 1;
            int index = Modes.RiftRules.BossIndex(floor);
            Modes.BossData info = index < riftBosses.Count ? riftBosses[index] : null;
            Vector2 at = new Vector2(spawnArea.center.x, spawnArea.yMax - 1.5f);
            SpriteRenderer marker = GetMarker(at, true);
            marker.transform.localScale = Vector3.one * 3f;
            hud.ShowNotice(info != null ? $"균열의 {info.displayName}!" : "균열 보스!", 1.5f);
            yield return new WaitForSeconds(1.5f);
            marker.gameObject.SetActive(false);
            if (_finished) yield break;
            if (info == null || info.prefab == null)
            {
                _alive = 0;
                OnRoomCleared();
                yield break;
            }

            if (_riftBoss != null) Destroy(_riftBoss.gameObject);
            _riftBoss = Instantiate(info.prefab, at, Quaternion.identity, transform);
            if (_riftBoss is PatternBoss pattern)
            {
                pattern.RiftVersion = true;
                pattern.MinionHp = RoomHpMultiplier() * Modes.RiftRules.BossMinionHpFactor;
                pattern.MinionAttack = RoomAttackMultiplier() * Modes.RiftRules.BossMinionAttackFactor;
            }

            if (_riftBoss is RiftWardenBoss warden) warden.RiftVersion = true;
            _riftGimmick = BossGimmicks.PickRift(_riftBoss, RunRandom.Seed, floor); // 검토 4번: 보스마다 맞는 기믹만, 층 씨앗으로
            float hp = info.hpMultiplier * Modes.RiftRules.BossHpFactor * Modes.RiftRules.HpScale(floor) * _enemyHp;
            float atk = info.attackMultiplier * Modes.RiftRules.BossAttackFactor * Modes.RiftRules.AttackScale(floor) * _enemyAttack;
            _riftBoss.Spawn(player.transform, hp, atk, false);
            _riftBoss.Killed -= OnEnemyKilled;
            _riftBoss.Killed += OnEnemyKilled;
            if (bossHud != null) bossHud.Bind(_riftBoss, info.displayName + " (균열)");
            BossGimmick riftGimmick = BossGimmicks.Attach(_riftBoss, _riftGimmick, RoomArena);
            if (riftGimmick != null) hud.ShowNotice("균열 기믹: " + riftGimmick.Hint, 3f);
        }

        private IEnumerator SpawnRoom(RoomPlan plan, List<SpawnEntry> spawns)
        {
            _alive = 0;
            foreach (SpawnEntry entry in spawns) _alive += entry.prefab != null ? entry.count : 0;
            bool midBoss = plan.kind == RoomKind.MidBoss && _chapter != null && _chapter.midBoss != null;
            if (midBoss) _alive++;

            if (midBoss) StartCoroutine(SpawnMidBoss());
            foreach (SpawnEntry entry in spawns)
            {
                if (entry.prefab == null) continue;
                for (int i = 0; i < entry.count; i++)
                {
                    StartCoroutine(SpawnOne(entry, PickSpawnPoint()));
                    yield return new WaitForSeconds(spawnStagger);
                }
            }

            if (_alive <= 0 && !_finished) OnRoomCleared(); // 적이 없는 방 (데이터가 비었을 때)
        }

        /// <summary>중간 보스 (Modes 2-8): 위쪽 가운데에 1.2초 예고 → 등장, 체력 막대</summary>
        private IEnumerator SpawnMidBoss()
        {
            Vector2 at = new Vector2(spawnArea.center.x, spawnArea.yMax - 1.5f);
            SpriteRenderer marker = GetMarker(at, true);
            marker.transform.localScale = Vector3.one * 2.6f;
            yield return new WaitForSeconds(1.2f);
            marker.gameObject.SetActive(false);
            if (_finished) yield break;

            EnemyBase boss = GetEnemy(_chapter.midBoss);
            boss.transform.position = at;
            boss.gameObject.SetActive(true);
            float hp = RoomHpMultiplier() * _chapter.midBossHp;
            float atk = RoomAttackMultiplier() * _chapter.midBossAttack;
            if (boss is PatternBoss pattern)
            {
                pattern.MinionHp = RoomHpMultiplier();
                pattern.MinionAttack = RoomAttackMultiplier();
            }

            boss.Spawn(player.transform, hp, atk, false);
            if (_chapter.enhanced) boss.TelegraphScale = _chapter.enhancedTelegraph;
            experience.Register(boss);
            boss.Killed -= OnEnemyKilled;
            boss.Killed += OnEnemyKilled;
            if (bossHud != null) bossHud.Bind(boss, string.IsNullOrEmpty(_chapter.midBossName) ? "중간 보스" : _chapter.midBossName);
            // 기믹 (확장 1차 5장): 4구간 그림자 사냥꾼 = 거울 분신, 5구간 망각의 집행자 = 불길 반쪽
            BossGimmick gimmick = BossGimmicks.Attach(boss, BossGimmicks.For(boss), RoomArena);
            if (gimmick != null) hud.ShowNotice(gimmick.Hint, 3f);
        }

        /// <summary>방 싸움터 (기믹 자리 계산: 9×14 방, 벽 안쪽)</summary>
        private static BossArena RoomArena => BossArena.Room(new Vector2(0f, 0.2f), new Vector2(3.6f, 6.3f));

        private float RoomHpMultiplier()
        {
            float enhanced = _chapter != null && _chapter.enhanced ? _chapter.enhancedHp : 1f;
            if (_rift) return Modes.RiftRules.HpScale(_room) * enhanced * _enemyHp; // 1.06^층 (Modes 4-3)
            return dungeon.RoomHp(_room) * dungeon.ChapterHpMultiplier(GameSession.Chapter) * enhanced * _enemyHp;
        }

        private float RoomAttackMultiplier()
        {
            if (_rift) return Modes.RiftRules.AttackScale(_room) * _enemyAttack; // 1.04^층
            return dungeon.RoomAttack(_room) * dungeon.ChapterAttackMultiplier(GameSession.Chapter) * _enemyAttack;
        }

        /// <summary>소환사·분열체·보스 졸개: 바로 나오고, 방 전멸 판정에 들어간다</summary>
        private EnemyBase SpawnChild(EnemyBase prefab, Vector2 at, float hp, float atk)
        {
            if (_finished || prefab == null) return null;
            EnemyBase enemy = GetEnemy(prefab);
            enemy.transform.position = at;
            enemy.gameObject.SetActive(true);
            enemy.Spawn(player.transform, hp, atk, false);
            if (_chapter != null && _chapter.enhanced) enemy.TelegraphScale = _chapter.enhancedTelegraph;
            experience.Register(enemy);
            enemy.Killed -= OnEnemyKilled;
            enemy.Killed += OnEnemyKilled;
            _alive++;
            return enemy;
        }

        // ---------------------------------------------------------------- 보상 방

        private void OpenRewardRoom()
        {
            _alive = 0;
            PlacePedestalModel(specialArt.chestModel, 1.15f); // 받침대 자리에 금 상자
            int gold = SpecialRooms.RewardGold(GameSession.Chapter);
            if (rewardRoom == null)
            {
                OnRoomCleared();
                return;
            }

            rewardRoom.Open(_rewardHeal, gold, choice =>
            {
                switch (choice)
                {
                    case RewardRoomPanel.Choice.Heal:
                        player.Health.Heal(player.Health.Max * 0.3f);
                        hud.ShowNotice("체력 회복 +30%");
                        break;
                    case RewardRoomPanel.Choice.Card:
                        experience.GrantLevelUp();
                        break;
                    case RewardRoomPanel.Choice.Gold:
                        Wallet.Grant(SaveSystem.Data, new Reward { Gold = gold });
                        SaveSystem.MarkDirty();
                        hud.ShowNotice($"골드 +{gold}");
                        break;
                }

                MarkRewardTaken();
                SaveAfterRewards();
                if (!_finished) OnRoomCleared();
            });
        }

        // ---------------------------------------------------------------- 레이아웃

        /// <summary>장애물·함정을 새로 놓는다 (방마다 30초쯤이라 다시 만들어도 싸다)</summary>
        private void ApplyLayout(RoomLayout layout)
        {
            foreach (GameObject go in _layoutObjects)
            {
                if (go != null) Destroy(go);
            }

            _layoutObjects.Clear();
            _deviceRoom?.Clear();
            _deviceRoom = null;
            if (layout == null || layoutRoot == null) return;

            foreach (ObstacleDef o in layout.obstacles) _layoutObjects.Add(Obstacle(o));
            SpawnDevices(layout); // 구간 장치 (확장 1차 2장) — 받침대 자리(장애물 0번)는 그대로 첫째
            StageLook.Scatter(layoutRoot, _room * 7919 + GameSession.Chapter * 104729 + layout.obstacles.Count, _layoutObjects, DeviceAvoidRects(layout)); // 방마다 바닥 장식 (맵이 단조롭다), 장치는 피해서

            TrapKind trap = _chapter != null ? _chapter.trap : TrapKind.None;
            if (trap == TrapKind.None || layout.traps.Count == 0) return;
            float damage = 45f * dungeon.ChapterAttackMultiplier(GameSession.Chapter) * GameSession.EnemyAttackScale;
            for (int i = 0; i < layout.traps.Count; i++)
            {
                TrapSpot spot = layout.traps[i];
                var go = new GameObject($"Trap {i + 1}");
                go.transform.SetParent(layoutRoot, false);
                var areaObject = new GameObject("Area");
                areaObject.transform.SetParent(go.transform, false);
                var sr = areaObject.AddComponent<SpriteRenderer>();
                sr.sprite = PlaceholderSprites.Square;
                sr.sortingOrder = -9;
                var zone = go.AddComponent<TrapZone>();
                zone.SetArea(sr);
                zone.Setup(TrapZone.EffectFor(trap, i), spot.position, spot.size, spot.angle, damage, i * 0.9f);
                _layoutObjects.Add(go);
                _deviceRoom?.Zones.Add(zone); // 레버가 컨베이어·증기를 바꾼다
            }
        }

        private GameObject Obstacle(ObstacleDef o)
        {
            var go = new GameObject(o.kind.ToString());
            go.transform.SetParent(layoutRoot, false);
            go.transform.position = o.position;
            go.transform.localScale = new Vector3(o.size.x, o.size.y, 1f);
            go.layer = o.kind == ObstacleKind.Low ? 0 : LayerMask.NameToLayer("Wall");
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = o.kind == ObstacleKind.Low && !o.circle ? PlaceholderSprites.Pit : o.circle ? PlaceholderSprites.Circle : PlaceholderSprites.Square; // 구덩이는 밝은 테가 있는 구멍
            sr.sortingOrder = -6;
            sr.color = o.kind == ObstacleKind.High ? new Color(0.42f, 0.38f, 0.33f)
                : o.kind == ObstacleKind.Low ? new Color(0.34f, 0.36f, 0.45f, 1f) // 구덩이 무늬(Pit)가 가운데를 어둡게 한다
                : new Color(0.62f, 0.45f, 0.25f);
            if (o.circle) go.AddComponent<CircleCollider2D>().radius = 0.5f;
            else go.AddComponent<BoxCollider2D>();
            if (o.kind == ObstacleKind.Breakable) go.AddComponent<Breakable>().Setup(o.hits, sr);
            // 3D (Art3D-Plan 3번): 높은 장애물 = 돌덩이·돌기둥, 부서지는 = 나무 상자. 낮은 장애물(구덩이)은 바닥 그림 그대로
            if (o.kind == ObstacleKind.High) Stage3D.Dress(sr, Stage3D.Kind.High, o.circle);
            else if (o.kind == ObstacleKind.Breakable) Stage3D.Dress(sr, Stage3D.Kind.Crate, o.circle);
            return go;
        }

        private IEnumerator SpawnOne(SpawnEntry entry, Vector2 point)
        {
            SpriteRenderer marker = GetMarker(point, entry.elite);
            yield return new WaitForSeconds(spawnTelegraph);
            marker.gameObject.SetActive(false);

            if (_finished) yield break;
            EnemyBase enemy = GetEnemy(entry.prefab);
            enemy.transform.position = point;
            enemy.gameObject.SetActive(true);
            enemy.Spawn(player.transform, RoomHpMultiplier(), RoomAttackMultiplier(), entry.elite);
            if (_chapter != null && _chapter.enhanced) enemy.TelegraphScale = _chapter.enhancedTelegraph;
            if (entry.elite) GiveAffixes(enemy, entry.prefab);
            experience.Register(enemy);
            enemy.Killed -= OnEnemyKilled;
            enemy.Killed += OnEnemyKilled;
        }

        /// <summary>엘리트 속성 (확장 1차 4장): 구간·층에 따라 0~2개, 적 난수로 (같은 씨앗이면 같은 속성)</summary>
        private void GiveAffixes(EnemyBase enemy, EnemyBase prefab)
        {
            int chapter = _chapter != null ? _chapter.chapter : 1;
            bool cursed = TryCursedAffixes(enemy, out EliteAffix affixes); // 저주받은 엘리트: 미리 뽑아 둔 2개 (확장 1차 3-4장)
            if (!cursed)
            {
                System.Random random = RunRandom.Enemies;
                int count = EliteAffixes.CountFor(chapter, _rift ? _room : 0, random);
                affixes = EliteAffixes.Roll(random, Mathf.Min(count, 2));
            }

            if (affixes == EliteAffix.None) return;
            if (!enemy.TryGetComponent(out EliteAffixes holder)) holder = enemy.gameObject.AddComponent<EliteAffixes>();
            if (EliteAffixes.MinionPrefab == null) EliteAffixes.MinionPrefab = MinionFor(_chapter);
            holder.Apply(affixes, prefab);
            hud.ShowNotice(cursed ? $"저주받은 {EliteAffixes.Names(affixes)} {enemy.Data.displayName}!" : $"{EliteAffixes.Names(affixes)} {enemy.Data.displayName}!", 2f);
        }

        /// <summary>부르는 속성이 낼 졸개: 그 구간의 졸개, 없으면 돌진병, 없으면 첫 적</summary>
        private static EnemyBase MinionFor(ChapterData chapter)
        {
            if (chapter == null || chapter.enemies == null || chapter.enemies.Count == 0) return null;
            EnemyBase fallback = chapter.enemies[0].prefab;
            foreach (EnemyCost e in chapter.enemies)
            {
                if (e.prefab == null) continue;
                if (e.prefab.Data != null && e.prefab.Data.id == "minion") return e.prefab;
                if (e.prefab.Data != null && e.prefab.Data.id == "charger") fallback = e.prefab;
            }

            return fallback;
        }

        private Vector2 PickSpawnPoint()
        {
            Vector2 from = player.transform.position;
            Vector2 best = spawnArea.center;
            float bestDistance = -1f;

            // 플레이어와 충분히 떨어지고 장애물 위가 아닌 자리 (근접 도달 검사, Modes 2-2). 못 찾으면 그중 가장 먼 빈자리
            if (_obstacleMask < 0) _obstacleMask = LayerMask.GetMask("Wall") | 1;
            System.Random random = RunRandom.Enemies;
            for (int i = 0; i < 24; i++)
            {
                var p = new Vector2(RunRandom.Range(random, spawnArea.xMin, spawnArea.xMax), RunRandom.Range(random, spawnArea.yMin, spawnArea.yMax));
                if (Physics2D.OverlapCircle(p, 0.55f, _obstacleMask) != null) continue;
                float d = Vector2.Distance(p, from);
                if (d >= minDistanceFromPlayer) return p;
                if (d > bestDistance) { bestDistance = d; best = p; }
            }

            return best;
        }

        private void OnEnemyKilled(EnemyBase enemy)
        {
            if (enemy.IsElite) experience.GrantLevelUp();
            OnCursedKilled(enemy);
            _alive--;
            if (_alive <= 0 && !_finished && !_holdClear) OnRoomCleared(); // "20초 버티기"는 시간이 끝낸다
        }

        private void OnRoomCleared()
        {
            _roomsCleared = Mathf.Max(_roomsCleared, _room);
            experience.AttractAll();
            if (!_rift) ResolveChallengeOnClear();
            if (_hitsTaken == _hitsAtRoomStart) Missions.MissionEvents.Raise(Missions.MissionEvents.NoHitRoom); // 피격 없이 방 클리어
            if (_rift)
            {
                OnRiftFloorCleared();
                return;
            }

            // 지금 끝나면 남길 수 있는 기록 용량 / 클리어하면 (조사 A7: "한 방만 더"의 동기)
            if (_room < RoomCount)
            {
                int now = RecordCapacity.For(_roomsCleared, RoomCount, cleared: false);
                int full = RecordCapacity.For(RoomCount, RoomCount, cleared: true);
                hud.ShowNotice($"기록 용량  지금 {now}pt · 클리어하면 {full}pt");
            }
            SaveSystem.SaveIfDirty(); // 안전한 시점: 새로 발견한 조합 등 (GameDesign 12-2)
            if (_room >= RoomCount)
            {
                // 마지막 방을 깨면 그 순간 클리어. 남은 보석이 날아오는 동안 맞아도 지지 않는다
                player.Health.Invulnerable = true;
                StartCoroutine(FinishAfter(1.5f));
            }
            else if (!TryOpenFork()) door.SetOpen(true); // 3·9번 방 뒤는 문 2개 (확장 1차 3-2장)
            if (_pendingNotice != null) hud.ShowNotice(_pendingNotice, 2.5f);
            _pendingNotice = null;
        }

        /// <summary>판이 시작된 주가 끝났나 (진행 중에 월요일 05:00을 넘겼다)</summary>
        private bool RiftSeasonOver => _riftWeek != Modes.RiftRules.WeekKey(Clock.Now());

        private void OnRiftFloorCleared() => StartCoroutine(RiftFloorCleared());

        /// <summary>
        /// 균열 층을 깼다: 보석을 다 줍고 레벨업 카드를 고른 뒤 (체크포인트에 그 레벨·카드가 들어가게)
        /// 이번 주 최고 층·층 보상, 보스 층이면 체크포인트, 엘리트 층이면 보상 방 선택 → 문.
        /// </summary>
        private IEnumerator RiftFloorCleared()
        {
            int floor = _room;
            float until = Time.realtimeSinceStartup + 6f;
            while (Time.realtimeSinceStartup < until && !_finished && (experience.ActiveGems > 0 || levelUp.IsOpen)) yield return null;
            if (_finished) yield break;

            SaveData save = SaveSystem.Data;
            if (RiftSeasonOver)
            {
                // 새 주가 시작됐다: 지난주 판으로 새 주 보상·체크포인트를 채우지 않는다 (판은 계속 할 수 있다)
                if (!_riftSeasonNoticed) hud.ShowNotice("새 주가 시작돼서 이 판의 층 보상·체크포인트는 여기까지예요", 3f);
                _riftSeasonNoticed = true;
            }
            else
            {
                foreach (string line in Modes.RiftRules.ReachFloor(save, floor))
                {
                    _modeLines.Add(line);
                    hud.ShowNotice(line, 2.5f);
                }

                if (Modes.RiftRules.IsBossFloor(floor)) SaveCheckpoint();
            }

            SaveSystem.SaveIfDirty();

            if (Modes.RiftRules.IsEliteFloor(_room) && rewardRoom != null && !(_resumeRewardTaken && _room == _resumeRoom))
            {
                int gold = 50 + 25 * Modes.RiftRules.ChapterFor(_room);
                rewardRoom.Open(_rewardHeal, gold, choice =>
                {
                    if (choice == RewardRoomPanel.Choice.Heal) player.Health.Heal(player.Health.Max * _rewardHeal);
                    else if (choice == RewardRoomPanel.Choice.Card) experience.GrantLevelUp();
                    else
                    {
                        Wallet.Grant(SaveSystem.Data, new Reward { Gold = gold });
                        SaveSystem.MarkDirty();
                    }

                    MarkRewardTaken();
                    door.SetOpen(true);
                });
                yield break;
            }

            door.SetOpen(true);
        }

        /// <summary>10층 보스를 깰 때마다: 빌드·레벨·체력·시드를 남긴다 (나갔다가 여기서 이어하기, 주간 시즌 안에서)</summary>
        private void SaveCheckpoint()
        {
            var cp = new RiftCheckpoint
            {
                valid = true,
                floor = _room,
                seed = RunRandom.Seed,
                heroId = skills.HeroId,
                hpRatio = player.Health.Current / Mathf.Max(1f, player.Health.Max),
                level = experience.Level.Level,
                experience = experience.Level.Experience,
            };
            foreach (SkillInstance s in skills.Loadout.Actives) cp.skills.Add(new RecordSkill(s.Data.id, s.Level, s.IsFusionResult));
            foreach (SkillInstance s in skills.Loadout.Passives) cp.skills.Add(new RecordSkill(s.Data.id, s.Level, s.IsFusionResult));
            cp.boosts = skills.RiftBoostCounts();
            SaveSystem.Data.rift.checkpoint = cp;
            hud.ShowNotice($"체크포인트 저장 ({_room}층)", 2f);
            SaveSystem.Save();
        }

        private IEnumerator GoToNextRoom()
        {
            yield return Fade(1f);
            player.Teleport(playerStart.position);
            EnterRoom(_room + 1);
            yield return Fade(0f);
        }

        private IEnumerator Fade(float target)
        {
            if (fade == null) yield break;
            float start = fade.alpha;
            for (float t = 0f; t < 0.25f; t += Time.deltaTime)
            {
                fade.alpha = Mathf.Lerp(start, target, t / 0.25f);
                yield return null;
            }

            fade.alpha = target;
        }

        private IEnumerator FinishAfter(float seconds)
        {
            yield return new WaitForSeconds(seconds); // 남은 보석이 날아올 시간
            Finish(victory: true);
        }

        private void Finish(bool victory)
        {
            if (_finished) return;
            _finished = true;
            SaveSystem.Data.run = new RunSnapshot(); // 판이 끝났다 → 이어하기 없음
            SaveSystem.MarkDirty();
            if (_rift)
            {
                FinishRift();
                return;
            }

            if (_daily)
            {
                FinishDaily(victory);
                return;
            }

            if (GameSession.Tutorial)
            {
                FinishTutorial(victory);
                return;
            }

            var summary = new RunSummary
            {
                Victory = victory,
                Seconds = Time.time - _startTime,
                Kills = experience.Kills,
                Level = experience.Level.Level,
                RoomReached = _room,
                RoomCount = RoomCount,
                Fusions = new List<string>(_fusions),
                BasicAttackShare = DamageLog.BasicShare,
                Skills = new List<string>(),
            };

            foreach (SkillInstance s in skills.Loadout.Actives) summary.Skills.Add($"{s.Data.displayName} Lv{s.Level}");
            foreach (SkillInstance s in skills.Loadout.Passives) summary.Skills.Add($"{s.Data.displayName} Lv{s.Level}");

            // 판 끝: 보상·계정 경험치·별 → 저장 (기록은 다음 화면에서 고른 뒤에 저장한다)
            int roomsCleared = victory ? RoomCount : _roomsCleared;
            summary.HitsTaken = _hitsTaken;
            summary.TopDamage = RunText.TopDamage(database, player.Hero);
            Missions.MissionEvents.DungeonFinished(skills.HeroId, GameSession.Chapter, victory, skills.ChoseLegendary);
            if (!string.IsNullOrEmpty(skills.HeroId)) SaveSystem.Data.Hero(skills.HeroId).runs++; // 첫 판 전용 액티브 확정용
            if (victory && _hitsTaken <= 10) Missions.MissionEvents.Raise(Missions.Newcomer.LowHitEvent); // 신규 7일 미션 "피격 10회 이하로 클리어"
            summary.RewardLines = GrantRewards(victory, roomsCleared);
            if (!victory && Economy.Hearts.TryRefundEarlyDeath(SaveSystem.Data, GameSession.Chapter, _room, GameSession.HeartSpent, Clock.UnixSeconds, Clock.Today))
                summary.RewardLines.Add("<color=#FF8FA3>♥</color> 아직 1구간을 못 깨서 하트 1개를 돌려받았어요");

            // 기록 용량 = 기준 × 0.8 × (깬 방 ÷ 전체 방), 클리어 = 1 (GameDesign 7-2)
            int capacity = RecordCapacity.For(roomsCleared, RoomCount, victory);
            summary.Capacity = capacity;
            RecordDraft draft = RecordDraft.FromLoadout(skills.Loadout, database, capacity);

            System.Action saveRecord = null;
            if (recordSave != null && capacity > 0 && draft.Entries.Count > 0)
            {
                int room = _room;
                int chapter = GameSession.Chapter;
                int balance = database != null ? database.balanceVersion : 0;
                string subtitle = victory ? "클리어! 용량 안에서 남길 스킬을 고르세요" : $"방 {room}에서 쓰러졌어요. 깬 방만큼 용량이 줄었어요";
                string heroName = player.Hero != null ? player.Hero.displayName : string.Empty;
                recordSave.DamageShare = DamageLog.ShareOf;
                saveRecord = () => recordSave.Open(draft, d =>
                {
                    RunRecord r = d.ToRecord(skills.HeroId, chapter, room, RoomCount, victory, balance);
                    r.heroName = heroName;
                    r.meta = JsonUtility.FromJson<MetaSnapshot>(JsonUtility.ToJson(player.Meta)); // 이번 판 판 밖 성장을 고정 저장 (복사본, 7-5)
                    return r;
                }, subtitle, GameSession.ToLobby);
            }

            result.Show(summary, saveRecord);
        }

        // ---------------------------------------------------------------- v1.0 튜토리얼 끝 (UX 4장)

        /// <summary>
        /// 튜토리얼 던전이 끝났다: 별·구간·미션은 없다. 기록 저장 튜토리얼 (용량이 꼭 넘친다 → 하나 빼기) → 저장하면 튜토리얼 보스로.
        /// 다시 보기(연습)면 보상·기록 없이 결과만.
        /// </summary>
        private void FinishTutorial(bool victory)
        {
            var summary = new RunSummary
            {
                Victory = victory,
                Seconds = Time.time - _startTime,
                Kills = experience.Kills,
                Level = experience.Level.Level,
                RoomReached = _room,
                RoomCount = RoomCount,
                Fusions = new List<string>(_fusions),
                BasicAttackShare = DamageLog.BasicShare,
                Skills = new List<string>(),
                HitsTaken = _hitsTaken,
                TopDamage = RunText.TopDamage(database, player.Hero),
                RewardLines = new List<string>(),
            };
            foreach (SkillInstance s in skills.Loadout.Actives) summary.Skills.Add($"{s.Data.displayName} Lv{s.Level}");
            foreach (SkillInstance s in skills.Loadout.Passives) summary.Skills.Add($"{s.Data.displayName} Lv{s.Level}");

            if (GameSession.Practice)
            {
                summary.RewardLines.Add("튜토리얼 다시 보기라 보상·기록은 없어요");
                result.Show(summary, null);
                return;
            }

            if (!victory)
            {
                summary.RewardLines.Add("튜토리얼을 그만뒀어요 — 다시 하면 처음부터예요");
                result.Show(summary, null);
                return;
            }

            // 골드는 기록을 저장할 때 같이 준다 (저장 전에 앱이 꺼지면 튜토리얼 던전을 다시 하니까 두 번 받지 않게)
            var reward = new Reward { Gold = 300 };
            summary.RewardLines.Add(reward + " (기록을 저장하면)");

            // 기록 용량이 꼭 한 칸 넘치게 (지금 빌드 비용 - 1) → 패시브 한 레벨만 빼면 된다 (핵심 스킬은 남는다)
            RecordDraft all = RecordDraft.FromLoadout(skills.Loadout, database, 999);
            int capacity = Mathf.Max(3, Mathf.Min(RecordCapacity.For(RoomCount, RoomCount, true), all.TotalCost - 1));
            summary.Capacity = capacity;
            RecordDraft draft = RecordDraft.FromLoadout(skills.Loadout, database, capacity);
            System.Action saveRecord = null;
            if (recordSave != null && draft.Entries.Count > 0)
            {
                int balance = database != null ? database.balanceVersion : 0;
                string heroName = player.Hero != null ? player.Hero.displayName : string.Empty;
                RunRecord made = null;
                recordSave.DamageShare = DamageLog.ShareOf;
                saveRecord = () =>
                {
                    recordSave.AllowSkip = false;
                    recordSave.Open(draft, d =>
                    {
                        RunRecord r = d.ToRecord(skills.HeroId, 1, RoomCount, RoomCount, true, balance);
                        r.heroName = heroName;
                        r.meta = JsonUtility.FromJson<MetaSnapshot>(JsonUtility.ToJson(player.Meta));
                        made = r;
                        SaveSystem.Data.tutorialRecordId = r.id; // 보스 전에 앱이 꺼져도 로비가 튜토리얼 보스로 보낸다
                        Wallet.Grant(SaveSystem.Data, reward); // 기록과 같은 저장에 들어간다
                        return r;
                    }, "용량이 넘쳤어요! 뺄 스킬을 눌러 용량 안으로 맞춘 뒤 저장해요 → 이 기록으로 보스와 싸워요",
                    () => GameSession.StartTutorialBoss(made != null ? made.id : null));
                };
            }

            result.Show(summary, saveRecord);
        }

        // ---------------------------------------------------------------- 균열·일일 도전 결과 (기록은 남기지 않는다)

        private void FinishRift()
        {
            SaveData save = SaveSystem.Data;
            float seconds = Time.time - _startTime;
            int floor = _room;
            Missions.MissionEvents.Raise(Missions.MissionEvents.Run);
            Missions.MissionEvents.Raise(Missions.MissionEvents.SurvivalSeconds, Mathf.FloorToInt(seconds)); // 주간 "서바이벌 또는 균열 20분"
            AccountLevel.Result level = AccountLevel.AddExp(save, Mathf.Min(120, _roomsCleared * 4), Clock.UnixSeconds);
            SaveSystem.Save();

            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"도달한 층  {floor}  (깬 층 {_roomsCleared})");
            sb.AppendLine($"이번 주 최고  {save.rift.weeklyBest}층   ·   역대 최고  {save.rift.bestEver}층");
            sb.AppendLine($"처치 {experience.Kills}   ·   레벨 {experience.Level.Level}   ·   {Mathf.FloorToInt(seconds / 60f)}분 {Mathf.FloorToInt(seconds % 60f)}초");
            if (skills.RiftBoostCount > 0) sb.AppendLine($"균열 강화 {skills.RiftBoostCount}번");
            sb.AppendLine($"<size=85%>피해  {RunText.TopDamage(database, player.Hero)}</size>");
            sb.AppendLine();
            sb.AppendLine("<color=#FFD966>받은 것</color>");
            foreach (string line in _modeLines) sb.AppendLine("  " + line);
            sb.AppendLine("  " + RunText.Level(level));
            sb.AppendLine("<size=85%><color=#A9B4C2>무한의 균열은 기록을 남기지 않아요 (빌드는 매번 1층부터)</color></size>");

            RiftCheckpoint cp = save.rift.checkpoint;
            string primary = cp.valid ? $"{cp.floor + 1}층부터 이어하기" : "1층부터 다시";
            System.Action onPrimary = cp.valid ? () => GameSession.StartRift(true) : (System.Action)(() => GameSession.StartRift(false));
            StartCoroutine(ShowTextAfter(0.8f, $"균열 {floor}층", sb.ToString(), primary, onPrimary));
        }

        private void FinishDaily(bool victory)
        {
            SaveData save = SaveSystem.Data;
            float seconds = Time.time - _startTime;
            float hp = player.Health.Current / Mathf.Max(1f, player.Health.Max);
            int rooms = victory ? RoomCount : _roomsCleared;
            int score = Modes.DailyRules.Score(rooms, experience.Kills, victory ? hp : 0f, victory, seconds);
            var lines = new List<string>();
            bool official = GameSession.DailyOfficial; // 날짜는 시작한 날 기준 (자정·05:00을 넘겨도 그날 기록)
            if (official)
            {
                lines.AddRange(Modes.DailyRules.RecordOfficial(save, skills.HeroId, score, victory));
                Missions.MissionEvents.Raise(Missions.MissionEvents.Run);
                Missions.MissionEvents.Raise(Missions.MissionEvents.DailyChallenge);
            }
            else
            {
                save.dailyChallenge.bestPractice = Mathf.Max(save.dailyChallenge.bestPractice, score);
            }

            SaveSystem.Save();

            var sb = new System.Text.StringBuilder();
            sb.AppendLine(official ? "<color=#FFD966>공식 도전 기록</color>" : "<color=#A9B4C2>연습 (기록·보상 없음)</color>");
            sb.AppendLine($"점수  <b>{score}</b>");
            sb.AppendLine($"<size=85%>깬 방 {rooms} × 100 · 처치 {experience.Kills} · 남은 체력 {Mathf.RoundToInt(hp * 100f)}% × 2{(victory ? $" · 클리어 500 · 시간 보너스 {Mathf.Max(0, 600 - Mathf.CeilToInt(seconds)) * 2}" : string.Empty)}</size>");
            if (save.dailyChallenge.officialDone) sb.AppendLine($"오늘의 공식 기록  {save.dailyChallenge.officialScore}점");
            if (save.dailyChallenge.bestPractice > 0) sb.AppendLine($"<size=85%>연습 최고  {save.dailyChallenge.bestPractice}점</size>");
            sb.AppendLine($"<size=85%>오늘의 변형: {string.Join(" / ", Modes.DailyRules.ModifiersFor(save.dailyChallenge.dayKey).ConvertAll(Modes.DailyRules.ModifierText))}</size>");
            if (lines.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine("<color=#FFD966>받은 것</color>");
                foreach (string line in lines) sb.AppendLine("  " + line);
            }

            StartCoroutine(ShowTextAfter(victory ? 1.2f : 0.8f, victory ? "일일 도전 클리어!" : "일일 도전 끝", sb.ToString(), "연습 한 번 더", () => GameSession.StartDaily(false)));
        }

        private IEnumerator ShowTextAfter(float seconds, string title, string body, string primary, System.Action onPrimary)
        {
            yield return new WaitForSeconds(seconds);
            result.ShowText(title, body, primary, onPrimary, "로비로", GameSession.ToLobby);
        }

        /// <summary>골드 (Economy 4장), 계정 경험치 (Stats 7장), 별 (Modes 3-2), 별 상자</summary>
        private List<string> GrantRewards(bool victory, int roomsCleared)
        {
            SaveData save = SaveSystem.Data;
            save.dungeonRuns++;
            int chapter = GameSession.Chapter;
            var lines = new List<string>();

            Reward gold = RunRewards.Dungeon(chapter, roomsCleared, RoomCount, victory);
            Wallet.Grant(save, gold);
            if (!gold.IsEmpty) lines.Add(gold.ToString());

            AccountLevel.Result level = AccountLevel.AddExp(save, AccountLevel.DungeonExp(roomsCleared), Clock.UnixSeconds);
            lines.Add(RunText.Level(level));

            // 장비 드랍 (Equipment 8장): 클리어하면 30%, 1구간 보스 격파 뒤부터
            if (victory && metaTable != null && metaTable.equipment != null)
            {
                Equipment.EquipmentData drop = Equipment.EquipmentInventory.RollDungeonDrop(save, metaTable.equipment);
                if (drop != null) lines.Add(Equipment.EquipmentInventory.AddLine(Equipment.EquipmentInventory.Add(save, metaTable.equipment, drop), drop));
            }

            foreach (string star in Stars.EvaluateDungeon(save, chapter, victory, _hitsTaken, skills.ChoseLegendary)) lines.Add("새 별  " + star);
            Reward box = Stars.ClaimBoxes(save);
            if (!box.IsEmpty) lines.Add("별 상자  " + box);

            // 유물 (Relics 4장): 그 영웅으로 던전 첫 클리어, 구간 별 6개 등
            if (metaTable != null && metaTable.relics != null)
            {
                if (victory) Relics.RelicService.MarkHeroClear(save, skills.HeroId);
                lines.AddRange(Relics.RelicService.CheckAll(save, metaTable.relics));
            }

            // 펫 (Pets 3장): 첫 던전 클리어 = 사냥매 + 펫 칸
            if (metaTable != null && metaTable.pets != null) lines.AddRange(Pets.PetService.CheckAll(save, metaTable.pets));

            SaveSystem.Save();
            return lines;
        }

        /// <summary>★2를 아직 안 땄으면 HUD에 "맞은 횟수 n/한도"를 보여준다 (Modes 3-2)</summary>
        private void ShowHitCounter()
        {
            int chapter = GameSession.Chapter;
            bool challenge = !_rift && !_daily && !GameSession.Tutorial && !Stars.Has(SaveSystem.Data, chapter, Stars.Dungeon, 2); // 균열·일일 도전·튜토리얼은 별이 없다
            hud.SetHitCounter(challenge, () => _hitsTaken, Stars.DungeonHitLimit(chapter));
        }

        private void OnDestroy()
        {
            if (EnemyBase.ChildSpawner == _childSpawner) EnemyBase.ChildSpawner = null;
        }

        private EnemyBase GetEnemy(EnemyBase prefab)
        {
            if (!_pools.TryGetValue(prefab, out List<EnemyBase> pool))
            {
                pool = new List<EnemyBase>();
                _pools[prefab] = pool;
            }

            // 죽은 적은 스스로 꺼지므로, 꺼진 것을 다시 쓴다
            foreach (EnemyBase e in pool)
            {
                if (!e.gameObject.activeSelf) return e;
            }

            EnemyBase created = Instantiate(prefab, transform);
            pool.Add(created);
            return created;
        }

        private SpriteRenderer GetMarker(Vector2 point, bool elite)
        {
            SpriteRenderer marker = _markers.Find(m => !m.gameObject.activeSelf);
            if (marker == null)
            {
                var go = new GameObject("SpawnMarker");
                go.transform.SetParent(transform, false);
                marker = go.AddComponent<SpriteRenderer>();
                marker.sprite = PlaceholderSprites.Circle;
                marker.sortingOrder = -2;
                _markers.Add(marker);
            }

            marker.transform.position = point;
            marker.transform.localScale = Vector3.one * (elite ? 1.6f : 1f);
            marker.color = elite ? new Color(1f, 0.75f, 0.1f, 0.5f) : new Color(1f, 0.2f, 0.2f, 0.4f);
            marker.gameObject.SetActive(true);
            return marker;
        }
    }

    /// <summary>결과 화면에 보여줄 한 판 요약 (Content.md 2-4).</summary>
    public struct RunSummary
    {
        public bool Victory;
        public float Seconds;
        public int Kills;
        public int Level;
        public int RoomReached;
        public int RoomCount;
        public List<string> Fusions;
        /// <summary>준 피해 중 기본 공격 비율 (목표 40% 이상, GameDesign 3-3)</summary>
        public float BasicAttackShare;
        public List<string> Skills;
        /// <summary>남길 수 있는 기록 용량 (pt)</summary>
        public int Capacity;
        public int HitsTaken;
        /// <summary>"기본 공격 41% · 낙뢰 32% …" (조사 A5)</summary>
        public string TopDamage;
        /// <summary>받은 것: 골드, 계정 경험치, 새 별, 별 상자</summary>
        public List<string> RewardLines;
    }
}
