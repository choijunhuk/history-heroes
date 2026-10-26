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

namespace HistoryHeroes.Survival
{
    /// <summary>
    /// 서바이벌 한 판 (GameDesign 8장): 열린 맵에서 10분 버티기 → 마지막 보스. 적은 화면 바로 밖 원형 띠에서 나오고
    /// 시간이 지날수록 많고 세진다 (동시 300 상한). 경험치 보석을 주워 레벨업 (슬롯 6·6, 전설 확률은 경과 시간 기준).
    /// 기본 공격은 던전과 똑같이 **멈춰야 나간다** → "지금 멈춰서 몰아칠까, 계속 피할까".
    /// 기록은 저장하지 않는다 (8-3). 보상은 조합 발견·생존 시간 중심, 재화는 던전보다 적게 (8-4).
    /// </summary>
    public sealed class SurvivalRun : MonoBehaviour
    {
        [SerializeField] private SurvivalData data;
        [SerializeField] private SkillDatabase database;

        [Header("장면")]
        [SerializeField] private PlayerController player;
        [SerializeField] private ExperienceSystem experience;
        [SerializeField] private SkillManager skills;
        [SerializeField] private Transform playerStart;
        [SerializeField] private Meta.MetaTable metaTable;

        [Header("UI")]
        [SerializeField] private LevelUpScreen levelUp;
        [SerializeField] private HudView hud;
        [SerializeField] private BossHud bossHud;
        [SerializeField] private RunResultScreen result;

        private readonly Dictionary<EnemyBase, List<EnemyBase>> _pools = new Dictionary<EnemyBase, List<EnemyBase>>();
        private readonly List<string> _fusions = new List<string>();
        private float _elapsed;
        private float _spawnTimer;
        private float _recycleTimer;
        private int _nextElite;
        private bool _bossSpawned;
        private EnemyBase _boss;
        private bool _finished;
        private int _lastShownSecond = -1;
        private Camera _camera;

        public float Elapsed => _elapsed;
        public bool IsFinished => _finished;
        public bool BossSpawned => _bossSpawned;
        public SurvivalData Data => data;

        private void Start()
        {
            GamePause.ResetAll();
            DamageLog.Reset();
            _camera = Camera.main;

            player.Teleport(playerStart != null ? (Vector2)playerStart.position : Vector2.zero);
            player.Health.Died += _ => Finish(victory: false);
            if (SaveSystem.Data.run.valid)
            {
                SaveSystem.Data.run = new RunSnapshot(); // 서바이벌은 이어하지 않는다 (10분짜리, 적 수백 마리)
                SaveSystem.Save();
            }
            skills.FusionCompleted += recipe => _fusions.Add(recipe.result.displayName);
            experience.MaxGems = data.maxGems;
            experience.ExpMultiplier = data.expMultiplier;

            hud.SetSurvivalMode();
            levelUp.Floor = 1;
        }

        private void Update()
        {
            if (_finished || GamePause.IsPaused) return;
            _elapsed += Time.deltaTime;
            levelUp.Floor = 1 + Mathf.FloorToInt(_elapsed / 60f * data.floorPerMinute); // 전설 확률: 경과 시간 기준 (8-1)
            UpdateClock();

            if (!_bossSpawned)
            {
                SpawnTick();
                RecycleTick();
                if (_nextElite < data.eliteTimes.Length && _elapsed >= data.eliteTimes[_nextElite])
                {
                    _nextElite++;
                    SpawnOne(_nextElite % 2 == 1 ? data.charger : data.archer, elite: true); // 돌진병·궁병 엘리트를 번갈아
                    hud.ShowNotice("★ 엘리트 등장!");
                }

                if (_elapsed >= data.duration) StartCoroutine(SpawnBoss());
            }
        }

        private void UpdateClock()
        {
            int second = Mathf.FloorToInt(_elapsed);
            if (second == _lastShownSecond) return;
            _lastShownSecond = second;
            if (_bossSpawned)
            {
                hud.RoomText = $"마지막 보스  {Format(_elapsed)}";
                return;
            }

            float left = Mathf.Max(0f, data.duration - _elapsed);
            hud.RoomText = $"서바이벌  {Format(left)} 남음";
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        /// <summary>개발용: 경과 시간을 건너뛴다 (후반 밀도·성능을 바로 보려고). 지난 엘리트는 건너뛴다</summary>
        public void DevSkipTo(float seconds)
        {
            _elapsed = Mathf.Clamp(seconds, 0f, data.duration - 1f);
            while (_nextElite < data.eliteTimes.Length && data.eliteTimes[_nextElite] <= _elapsed) _nextElite++;
        }
#endif

        // ---------------------------------------------------------------- 적 생성

        /// <summary>
        /// 뱀서식 생성: 분마다 정한 마리 수(aliveCurve)를 유지한다. 모자란 만큼 0.25초마다 최대 5마리씩 채운다.
        /// → 약한 빌드도 같은 밀도를 상대하고, 빨리 잡는 빌드는 그만큼 더 많이 잡아 더 빨리 큰다.
        /// </summary>
        private void SpawnTick()
        {
            _spawnTimer -= Time.deltaTime;
            if (_spawnTimer > 0f) return;
            _spawnTimer = data.spawnInterval;

            int target = data.TargetAlive(_elapsed);
            int missing = Mathf.Min(target - EnemyBase.Alive.Count, data.spawnBatchMax);
            for (int i = 0; i < missing && EnemyBase.Alive.Count < data.maxAlive; i++) SpawnOne(PickEnemy(), elite: false);
        }

        /// <summary>너무 멀리 뒤처진 적(엘리트 빼고)을 화면 바로 밖으로 옮긴다 — 달려서 무리를 떼어 내도 압박이 따라온다</summary>
        private void RecycleTick()
        {
            _recycleTimer -= Time.deltaTime;
            if (_recycleTimer > 0f) return;
            _recycleTimer = 1f;

            float far = SpawnRadius() * data.recycleDistanceRatio;
            float farSqr = far * far;
            Vector2 me = player.transform.position;
            IReadOnlyList<EnemyBase> alive = EnemyBase.Alive;
            for (int i = 0; i < alive.Count; i++)
            {
                EnemyBase e = alive[i];
                if (e == null || e.IsElite || e == _boss || e.Health.IsDead) continue;
                if (e.IsTelegraphing || e.IsAttacking) continue; // 공격 도중 옮기면 새 자리에서 옛 조준으로 쏜다 (Codex v0.5 3번)
                if (((Vector2)e.transform.position - me).sqrMagnitude > farSqr) e.transform.position = RingPosition();
            }
        }

        private float SpawnRadius()
        {
            float halfH = _camera != null ? _camera.orthographicSize : 9f;
            float halfW = halfH * (_camera != null ? _camera.aspect : 0.56f);
            return Mathf.Sqrt(halfW * halfW + halfH * halfH) + data.spawnMargin;
        }

        private EnemyBase PickEnemy()
        {
            float r = Random.value;
            if (_elapsed >= data.bomberFrom && r < data.bomberShare) return data.bomber;
            if (_elapsed >= data.archerFrom && r < data.bomberShare + data.archerShare) return data.archer;
            return data.charger;
        }

        private void SpawnOne(EnemyBase prefab, bool elite)
        {
            if (prefab == null) return;
            EnemyBase enemy = GetEnemy(prefab);
            enemy.transform.position = RingPosition();
            enemy.gameObject.SetActive(true);
            enemy.Spawn(player.transform, data.HpMultiplier(_elapsed), data.AttackMultiplier(_elapsed), elite);
            experience.Register(enemy);
        }

        /// <summary>
        /// 화면 바로 밖 원형 띠 (Modes 2-10). 맵 안이고 바위·연못 위가 아닌 자리만 (Codex v0.5 2번).
        /// 구석이라 못 찾으면 맵 가운데 쪽 방향으로 같은 거리 — 플레이어 바로 옆에 생기지 않는다.
        /// </summary>
        public Vector2 RingPosition()
        {
            Vector2 center = player.transform.position;
            float radius = SpawnRadius();
            float limit = data.mapHalfSize - 1f;
            for (int attempt = 0; attempt < 12; attempt++)
            {
                float angle = Random.value * Mathf.PI * 2f;
                Vector2 p = center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
                if (Mathf.Abs(p.x) <= limit && Mathf.Abs(p.y) <= limit && IsFree(p)) return p;
            }

            Vector2 inward = center.sqrMagnitude > 0.01f ? -center.normalized : Vector2.up;
            for (int attempt = 0; attempt < 8; attempt++)
            {
                Vector2 dir = Quaternion.Euler(0f, 0f, Random.Range(-60f, 60f)) * inward;
                Vector2 q = center + dir * radius;
                q = new Vector2(Mathf.Clamp(q.x, -limit, limit), Mathf.Clamp(q.y, -limit, limit));
                if (IsFree(q) && (q - center).sqrMagnitude > radius * radius * 0.5f) return q;
            }

            return center + inward * radius;
        }

        private static readonly int ObstacleMask = 1 << 10 | 1 << 0; // 바위(Wall) + 연못(Default)

        private static bool IsFree(Vector2 p) => Physics2D.OverlapCircle(p, 0.6f, ObstacleMask) == null;

        private EnemyBase GetEnemy(EnemyBase prefab)
        {
            if (!_pools.TryGetValue(prefab, out List<EnemyBase> pool))
            {
                pool = new List<EnemyBase>();
                _pools[prefab] = pool;
            }

            foreach (EnemyBase e in pool)
            {
                if (!e.gameObject.activeSelf) return e;
            }

            EnemyBase created = Instantiate(prefab, transform);
            created.gameObject.SetActive(false);
            pool.Add(created);
            return created;
        }

        // ---------------------------------------------------------------- 마지막 보스

        private IEnumerator SpawnBoss()
        {
            _bossSpawned = true;
            hud.ShowNotice("마지막 보스가 와요!", data.bossWarning);
            ClearCrowd(); // 무리가 한꺼번에 무너진다 (보석 잔치) → 보스와 1:1, "멈출까 피할까"가 다시 또렷해진다
            yield return new WaitForSeconds(data.bossWarning);
            if (_finished || data.bossPrefab == null) yield break;

            Vector2 at = (Vector2)player.transform.position + Random.insideUnitCircle.normalized * 5f;
            float limit = data.mapHalfSize - 3f;
            at = new Vector2(Mathf.Clamp(at.x, -limit, limit), Mathf.Clamp(at.y, -limit, limit));
            _boss = Instantiate(data.bossPrefab, at, Quaternion.identity, transform);
            _boss.Spawn(player.transform, data.bossHpMultiplier, data.bossAttackMultiplier, false);
            _boss.Killed += _ => Finish(victory: true);
            if (bossHud != null) bossHud.Bind(_boss, data.bossName);
        }

        /// <summary>살아 있는 적을 모두 쓰러뜨린다 (처치·경험치는 그대로, 피해 기록에는 안 들어간다)</summary>
        private void ClearCrowd()
        {
            var alive = new List<EnemyBase>(EnemyBase.Alive);
            foreach (EnemyBase e in alive)
            {
                if (e == null || e.Health.IsDead) continue;
                e.Health.TryTakeDamage(new DamageInfo { Amount = 1e9f, Kind = DamageKind.Enemy, AttackerTeam = Team.Player, SourceId = "survival_clear" });
            }

            experience.AttractAll();
            CameraShake.Add(CameraShake.EliteKill);
        }

        // ---------------------------------------------------------------- 끝

        private void Finish(bool victory)
        {
            if (_finished) return;
            _finished = true;
            SaveSystem.RotateRunBackups();
            player.Health.Invulnerable = true;
            experience.AttractAll();

            SaveData save = SaveSystem.Data;
            string heroId = skills.HeroId;
            Missions.MissionEvents.SurvivalFinished(_elapsed);
            int minutes = Mathf.FloorToInt(_elapsed / 60f);
            var lines = new List<string>();

            var reward = new Reward { Gold = minutes * data.goldPerMinute };
            Wallet.Grant(save, reward);
            if (!reward.IsEmpty) lines.Add(reward.ToString());
            int shards = Mathf.Min(data.shardsMax, minutes / 2 * data.shardsPerTwoMinutes);
            if (shards > 0)
            {
                save.AddItem(Relics.RelicTable.AncientShard, shards);
                lines.Add($"고대 파편 +{shards}");
            }

            lines.Add(RunText.Level(AccountLevel.AddExp(save, minutes * data.accountExpPerMinute, Clock.UnixSeconds)));

            // 생존 기록 (영웅별 최고), 클리어한 영웅 (유물 "영웅 2명으로 클리어")
            HeroBest best = save.survival.BestOf(heroId);
            bool newBest = _elapsed > best.seconds;
            if (newBest) best.seconds = _elapsed;
            save.survival.longestSeconds = Mathf.Max(save.survival.longestSeconds, _elapsed);
            if (victory)
            {
                save.survival.clears++;
                if (!save.survival.clearHeroes.Contains(heroId)) save.survival.clearHeroes.Add(heroId);
            }

            if (metaTable != null && metaTable.relics != null) lines.AddRange(Relics.RelicService.CheckAll(save, metaTable.relics));
            if (metaTable != null && metaTable.runes != null) lines.AddRange(Runes.RuneService.GrantSurvivalRunes(save, metaTable.runes));
            if (metaTable != null && metaTable.pets != null) lines.AddRange(Pets.PetService.CheckAll(save, metaTable.pets)); // 흰 비둘기
            SaveSystem.Save();

            var summary = new SurvivalSummary
            {
                Victory = victory,
                Seconds = _elapsed,
                Duration = data.duration,
                Kills = experience.Kills,
                Level = experience.Level.Level,
                BestSeconds = best.seconds,
                NewBest = newBest,
                BasicAttackShare = DamageLog.BasicShare,
                TopDamage = RunText.TopDamage(database, player.Hero),
                Fusions = new List<string>(_fusions),
                RewardLines = lines,
            };
            StartCoroutine(ShowResultAfter(victory ? 1f : 0.6f, summary));
        }

        private IEnumerator ShowResultAfter(float seconds, SurvivalSummary summary)
        {
            yield return new WaitForSeconds(seconds);
            result.ShowSurvival(summary);
        }

        public static string Format(float seconds)
        {
            int m = Mathf.FloorToInt(seconds / 60f);
            int s = Mathf.FloorToInt(seconds % 60f);
            return $"{m:00}:{s:00}";
        }
    }

    /// <summary>서바이벌 결과 화면 값</summary>
    public sealed class SurvivalSummary
    {
        public bool Victory;
        public float Seconds;
        public float Duration;
        public int Kills;
        public int Level;
        public float BestSeconds;
        public bool NewBest;
        public float BasicAttackShare;
        public string TopDamage;
        public List<string> Fusions;
        public List<string> RewardLines;
    }
}
