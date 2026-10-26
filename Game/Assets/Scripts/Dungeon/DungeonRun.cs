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
    public sealed class DungeonRun : MonoBehaviour
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
        public int RoomCount => _plans != null ? _plans.Count : dungeon.plan.Count;
        public bool IsFinished => _finished;

        private void Start()
        {
            GamePause.ResetAll();
            DamageLog.Reset();
            _startTime = Time.time;
            PlanRun();
            _childSpawner = SpawnChild;
            EnemyBase.ChildSpawner = _childSpawner;
            player.Health.Died += _ => Finish(victory: false);
            player.Health.Damaged += (_, dealt) =>
            {
                if (dealt > 0f && !_finished) _hitsTaken++;
            };
            ShowHitCounter();
            skills.FusionCompleted += recipe => _fusions.Add(recipe.result.displayName);
            door.Entered += () => StartCoroutine(GoToNextRoom());
            if (fade != null) fade.alpha = 0f;
            EnterRoom(1);
        }

        /// <summary>판 시작: 시드(일일 도전은 날짜)로 방마다 레이아웃과 적 편성을 정한다</summary>
        private void PlanRun()
        {
            if (GameSession.RunSeed != 0) RunRandom.Begin(GameSession.RunSeed);
            else RunRandom.BeginRandom();
            _chapter = dungeon.Chapter(GameSession.Chapter);
            _plans = GameSession.CustomPlan != null ? new List<RoomPlan>(GameSession.CustomPlan) : new List<RoomPlan>(dungeon.plan);
            System.Random rooms = RunRandom.Rooms;
            _layouts = Encounter.PickLayouts(_plans, dungeon.layouts, dungeon.specialLayout, rooms);
            _encounters = new List<List<SpawnEntry>>(_plans.Count);
            for (int i = 0; i < _plans.Count; i++) _encounters.Add(Encounter.Build(_chapter, _plans[i], i + 1, rooms));
        }

        private void EnterRoom(int room)
        {
            _room = room;
            _hitsAtRoomStart = _hitsTaken;
            levelUp.Floor = room;
            RoomPlan plan = _plans[room - 1];
            string chapter = GameSession.Chapter > 1 ? $"{GameSession.Chapter}구간 " : string.Empty;
            string tag = plan.kind == RoomKind.Elite ? "  엘리트" : plan.kind == RoomKind.Reward ? "  보상" : plan.kind == RoomKind.MidBoss ? "  중간 보스" : string.Empty;
            hud.RoomText = $"{chapter}방 {room}/{RoomCount}{tag}";
            door.SetOpen(false);
            var kinds = new List<RoomKind>(RoomCount);
            foreach (RoomPlan r in _plans) kinds.Add(r.kind);
            hud.SetRoomProgress(room, kinds);
            ApplyLayout(_layouts[room - 1]);

            if (plan.kind == RoomKind.Reward) OpenRewardRoom();
            else StartCoroutine(SpawnRoom(plan, _encounters[room - 1]));
            RoomEntered?.Invoke(room);
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
        }

        private float RoomHpMultiplier()
        {
            int chapter = GameSession.Chapter;
            float enhanced = _chapter != null && _chapter.enhanced ? _chapter.enhancedHp : 1f;
            return dungeon.RoomHp(_room) * dungeon.ChapterHpMultiplier(chapter) * enhanced * GameSession.EnemyHpScale;
        }

        private float RoomAttackMultiplier() => dungeon.RoomAttack(_room) * dungeon.ChapterAttackMultiplier(GameSession.Chapter) * GameSession.EnemyAttackScale;

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
            int chapter = Mathf.Max(1, GameSession.Chapter);
            int gold = 50 + 25 * chapter;
            if (rewardRoom == null)
            {
                OnRoomCleared();
                return;
            }

            rewardRoom.Open(0.3f, gold, choice =>
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
            if (layout == null || layoutRoot == null) return;

            foreach (ObstacleDef o in layout.obstacles) _layoutObjects.Add(Obstacle(o));

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
            sr.sprite = o.circle ? PlaceholderSprites.Circle : PlaceholderSprites.Square;
            sr.sortingOrder = -6;
            sr.color = o.kind == ObstacleKind.High ? new Color(0.42f, 0.38f, 0.33f)
                : o.kind == ObstacleKind.Low ? new Color(0.12f, 0.14f, 0.2f, 0.95f)
                : new Color(0.62f, 0.45f, 0.25f);
            if (o.circle) go.AddComponent<CircleCollider2D>().radius = 0.5f;
            else go.AddComponent<BoxCollider2D>();
            if (o.kind == ObstacleKind.Breakable) go.AddComponent<Breakable>().Setup(o.hits, sr);
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
            experience.Register(enemy);
            enemy.Killed -= OnEnemyKilled;
            enemy.Killed += OnEnemyKilled;
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
            _alive--;
            if (_alive <= 0 && !_finished) OnRoomCleared();
        }

        private void OnRoomCleared()
        {
            _roomsCleared = Mathf.Max(_roomsCleared, _room);
            experience.AttractAll();
            if (_hitsTaken == _hitsAtRoomStart) Missions.MissionEvents.Raise(Missions.MissionEvents.NoHitRoom); // 피격 없이 방 클리어

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
            else door.SetOpen(true);
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
            summary.RewardLines = GrantRewards(victory, roomsCleared);

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
            bool challenge = !Stars.Has(SaveSystem.Data, chapter, Stars.Dungeon, 2);
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
