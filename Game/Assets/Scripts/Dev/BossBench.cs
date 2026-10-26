#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using HistoryHeroes.Combat;
using HistoryHeroes.Common;
using HistoryHeroes.Enemy;
using HistoryHeroes.Modes;
using HistoryHeroes.Player;
using HistoryHeroes.Records;
using HistoryHeroes.Stats;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HistoryHeroes.Dev
{
    /// <summary>
    /// v0.3 8단계 검증 벤치 (v0.3-Plan, Heroes.md 8장 완료 기준):
    /// 영웅 4명이 **같은 기록**으로 실제 1구간 보스(연습 모드)와 싸워 처치 시간·피격·승패를 잰다.
    /// - "dps": 체력을 계속 채우고 피하지 않는다 → 순수 처치 속도 (관우 ≤ 원거리 × 1.5)
    /// - "play": 회복 없음, 예고를 보고 피하는 AI → 전설 없이 이길 수 있는가
    /// - "swarm" / "barrage": 던전 방에서 같은 빌드로 돌진병 떼 / 궁병·폭탄병 (영웅별 잡몹 처리 비교, 체력 채움)
    /// 장면을 여러 번 다시 여니 장면 밖(DontDestroyOnLoad)에서 돈다. 임시 기록은 끝나면 지운다.
    /// </summary>
    public sealed class BossBench : MonoBehaviour
    {
        public const string RecordPrefix = "bench_";

        public struct Run
        {
            public string HeroId;
            public string Mode;
            /// <summary>이 판만의 빌드 (비면 Begin의 빌드)</summary>
            public List<RecordSkill> Build;
            /// <summary>이 판의 판 밖 성장 (기록 스냅샷, 비면 없음) — v0.4 판 밖 성장 벤치</summary>
            public MetaSnapshot Meta;
            /// <summary>결과 줄 앞에 붙일 이름 ("기본 성장 + 연사형")</summary>
            public string Label;
            /// <summary>보스 구간 (v0.6, 0이면 1구간)</summary>
            public int Chapter;
        }

        /// <summary>잡몹 시나리오용 적 (에디터 QaTools가 넣어 준다)</summary>
        public static EnemyBase ChargerPrefab, ArcherPrefab, BomberPrefab;

        private static BossBench _instance;
        private static readonly List<string> _results = new List<string>();

        public static bool Running => _instance != null;
        public static string Status { get; private set; } = "대기";
        public static IReadOnlyList<string> Results => _results;

        private List<Run> _runs;
        private List<RecordSkill> _build;
        private float _speed;
        private Skills.SkillDatabase _database;

        /// <summary>순서대로 싸운다. 전설·영웅 전용 스킬은 넣지 않는 것을 권장 (같은 빌드 비교)</summary>
        public static string Begin(List<Run> runs, List<RecordSkill> build, float speed, Skills.SkillDatabase database)
        {
            if (_instance != null) return "이미 도는 중";
            _results.Clear();
            var go = new GameObject("[BossBench]");
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<BossBench>();
            _instance._runs = runs;
            _instance._build = build;
            _instance._speed = speed;
            _instance._database = database;
            _instance.StartCoroutine(_instance.RunAll());
            return $"시작: {runs.Count}판";
        }

        private IEnumerator RunAll()
        {
            Application.runInBackground = true;

            // 실제 저장 파일은 건드리지 않는다: 지금 데이터를 임시 폴더 저장소로 복사해 그쪽에서 돈다 (Codex v0.3b 1번).
            // 중간에 Play를 꺼도 SaveSystem이 다음 Play에서 기기 저장소를 다시 읽으므로 실제 파일은 그대로다.
            SaveStore real = SaveSystem.Store;
            var sandbox = new SaveStore(Path.Combine(Application.temporaryCachePath, "BossBenchSave"));
            sandbox.Save(SaveSystem.Data);
            SaveSystem.Use(sandbox);

            for (int i = 0; i < _runs.Count; i++)
            {
                Run run = _runs[i];
                Status = $"{i + 1}/{_runs.Count} {run.HeroId} {run.Mode}";
                yield return run.Mode == "swarm" || run.Mode == "barrage" ? Crowd(run) : Fight(run);
            }

            SaveSystem.Use(real); // 임시 기록·잠금 해제·영웅 선택은 임시 저장소와 함께 버려진다
            GamePause.DevSpeed = 1f;
            Status = "끝";
            Debug.Log("[BossBench]\n" + string.Join("\n", _results));
            _instance = null;
            Destroy(gameObject);
        }

        private IEnumerator Fight(Run run)
        {
            SaveData save = SaveSystem.Data;
            string id = RecordPrefix + run.HeroId;
            save.records.RemoveAll(r => r.id == id);
            save.records.Add(new RunRecord
            {
                id = id,
                slot = 90, // 보관함 칸(0~2) 밖: 화면에 안 보인다
                heroId = run.HeroId,
                heroName = run.HeroId,
                skills = new List<RecordSkill>(run.Build ?? _build),
                meta = run.Meta ?? new MetaSnapshot(),
                capacity = 999,
                chapter = Mathf.Max(1, run.Chapter),
                cleared = true,
                roomReached = 10,
                roomCount = 10,
            });

            GamePause.DevSpeed = 1f;
            GameSession.StartBossPractice(Mathf.Max(1, run.Chapter), id);

            // 장면이 다시 열리고 보스가 나올 때까지
            BossRun boss = null;
            float waitUntil = Time.realtimeSinceStartup + 20f;
            while (Time.realtimeSinceStartup < waitUntil)
            {
                yield return null;
                boss = FindAnyObjectByType<BossRun>();
                if (boss != null && boss.Boss != null) break;
            }

            if (boss == null || boss.Boss == null)
            {
                _results.Add($"{run.HeroId} | {run.Mode} | 보스가 안 나옴");
                yield break;
            }

            var player = FindAnyObjectByType<PlayerController>();
            if (boss.Boss is RiftWardenBoss warden)
            {
                var pilot = boss.gameObject.AddComponent<BossPilot>();
                pilot.Begin(player, warden, run.Mode == "dps");
            }
            else
            {
                // v0.6 패턴 보스: 화면 예고(원·선·부채꼴)와 탄을 보고 피하는 일반 조종 AI
                var pilot = boss.gameObject.AddComponent<TelegraphPilot>();
                pilot.Begin(player, boss.Boss, run.Mode == "dps");
            }
            // 무엇에 맞았나 (패턴·졸개별 받은 피해 — 보스 손볼 때 본다)
            var taken = new Dictionary<string, float>();
            Health playerHealth = player.Health;
            void OnDamaged(Health target, DamageInfo info, float dealt)
            {
                if (target != playerHealth || dealt <= 0f) return;
                string key = info.SourceId ?? "?";
                taken.TryGetValue(key, out float sum);
                taken[key] = sum + dealt;
            }

            Health.AnyDamaged += OnDamaged;
            GamePause.DevSpeed = _speed;
            float spawnedAt = Time.time;

            while (!boss.IsFinished && Time.time - spawnedAt < 400f) yield return null;
            Health.AnyDamaged -= OnDamaged;
            float takenTotal = 0f;
            foreach (float v in taken.Values) takenTotal += v;
            var parts = new List<KeyValuePair<string, float>>(taken);
            parts.Sort((a, b) => b.Value.CompareTo(a.Value));
            var takenParts = new List<string>();
            for (int i = 0; i < parts.Count && i < 5; i++) takenParts.Add($"{parts[i].Key} {parts[i].Value / Mathf.Max(1f, takenTotal) * 100f:0}%");

            GamePause.DevSpeed = 1f;
            float seconds = Time.time - spawnedAt;
            Health bossHealth = boss.Boss != null ? boss.Boss.Health : null;
            bool win = bossHealth != null && bossHealth.IsDead;
            float bossLeft = bossHealth != null ? bossHealth.Current / Mathf.Max(1f, bossHealth.Max) : 1f;
            float hpLeft = player.Health.Current / Mathf.Max(1f, player.Health.Max);
            string outcome = win ? "승" : boss.IsFinished ? $"패 (보스 {bossLeft * 100f:0}% 남음)" : "시간 초과";
            _results.Add($"{(run.Label != null ? run.Label + " | " : string.Empty)}{(run.Chapter > 1 ? run.Chapter + "구간 | " : string.Empty)}{run.HeroId} | {run.Mode} | {outcome} | {seconds:0.0}초 | 피격 {boss.HitsTaken} | 남은 체력 {hpLeft * 100f:0}% | 기본 공격 {DamageLog.BasicShare * 100f:0}% | {Economy.RunText.TopDamage(_database, player.Hero, 4)} | 받은 피해 {takenTotal / Mathf.Max(1f, player.Health.Max) * 100f:0}% ({string.Join(" · ", takenParts)})");
            yield return new WaitForSecondsRealtime(0.5f);
        }

        /// <summary>던전 방에서 같은 빌드로 잡몹 처리 (ScenarioMonitor: 체력 채움, 사거리 안에서 멈춰 쏘기)</summary>
        private IEnumerator Crowd(Run run)
        {
            // 잠긴 영웅은 고르면 칭기즈 칸으로 바뀌어 버린다 → 벤치 동안만 풀어 둔다 (끝나면 되돌림)
            if (!SaveSystem.Data.unlockedHeroes.Contains(run.HeroId)) SaveSystem.Data.unlockedHeroes.Add(run.HeroId);
            SaveSystem.Data.selectedHero = run.HeroId;
            GamePause.DevSpeed = 1f;
            GameSession.StartDungeon(1);

            Dungeon.DungeonRun dungeon = null;
            PlayerController player = null;
            float waitUntil = Time.realtimeSinceStartup + 20f;
            while (Time.realtimeSinceStartup < waitUntil)
            {
                yield return null;
                dungeon = FindAnyObjectByType<Dungeon.DungeonRun>();
                player = FindAnyObjectByType<PlayerController>();
                if (dungeon != null && player != null && player.Hero != null && player.Hero.id == run.HeroId) break;
            }

            yield return new WaitForSecondsRealtime(0.3f);
            if (dungeon == null || player == null || player.Hero == null || player.Hero.id != run.HeroId)
            {
                _results.Add($"{run.HeroId} | {run.Mode} | 던전이 안 열림 (또는 다른 영웅)");
                yield break;
            }

            var levelUp = FindAnyObjectByType<UI.LevelUpScreen>();
            if (levelUp != null)
            {
                levelUp.DevCancel();
                levelUp.enabled = false;
            }

            dungeon.gameObject.SetActive(false);
            DevCheats.KillAll();
            var skills = FindAnyObjectByType<Skills.SkillManager>();
            skills.ClearAll();
            foreach (RecordSkill s in _build) skills.DevSetLevel(_database.Find(s.id), s.level, s.fusion);
            player.Teleport(new Vector2(0f, -5.5f));
            player.Health.Heal(1e9f);

            var root = new GameObject("BenchCrowd").transform;
            if (run.Mode == "swarm")
            {
                Spawn(root, ChargerPrefab, 14, player);
            }
            else
            {
                Spawn(root, ArcherPrefab, 6, player);
                Spawn(root, BomberPrefab, 2, player);
            }

            DamageLog.Reset();
            var monitor = root.gameObject.AddComponent<ScenarioMonitor>();
            monitor.Begin($"{run.HeroId} {run.Mode}", false, player);
            GamePause.DevSpeed = _speed;
            while (monitor != null && !monitor.Finished) yield return null;
            GamePause.DevSpeed = 1f;

            string time = monitor != null && monitor.Cleared ? $"{monitor.Elapsed:0.0}초" : "못 잡음";
            _results.Add($"{run.HeroId} | {run.Mode} | {time} | 기본 공격 {DamageLog.BasicShare * 100f:0}% | {Economy.RunText.TopDamage(_database, player.Hero, 4)}");
            yield return new WaitForSecondsRealtime(0.3f);
        }

        private static void Spawn(Transform root, EnemyBase prefab, int count, PlayerController player)
        {
            if (prefab == null) return;
            // 같은 자리에서 비교하려고 고정 씨앗
            var random = new System.Random(count * 7919 + prefab.name.Length);
            for (int i = 0; i < count; i++)
            {
                var position = new Vector2((float)(random.NextDouble() * 6.4 - 3.2), (float)(random.NextDouble() * 5.0 + 1.0));
                EnemyBase enemy = Instantiate(prefab, position, Quaternion.identity, root);
                enemy.Spawn(player.transform, 1.4f, 1f, false);
            }
        }

        public static string Report()
        {
            var sb = new StringBuilder();
            sb.Append(Status);
            foreach (string r in _results) sb.Append("\n").Append(r);
            return sb.ToString();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            _instance = null;
            Status = "대기";
            _results.Clear();
        }
    }

    /// <summary>
    /// 보스전 조종 AI (검증 벤치용). 사람이 보는 예고(강타 원, 돌진 선, 날아오는 파편)만 보고 움직인다.
    /// - 위험이 없으면: 사거리 밖이면 다가가고, 안이면 **멈춰서 쏜다** (몸에 붙지는 않는다)
    /// - 강타 예고 원 안이면 원 밖으로, 돌진 선 위면 옆으로, 가까운 파편이 오면 옆으로 비킨다
    /// - "dps"는 피하지 않고 체력을 계속 채운다 (순수 처치 속도)
    /// </summary>
    public sealed class BossPilot : MonoBehaviour
    {
        private const float ArenaSafeRadius = 3.2f; // 경기장 반지름 3.9 - 몸
        private const float ApproachRangeRatio = 0.85f;

        private PlayerController _player;
        private RiftWardenBoss _boss;
        private bool _dpsOnly;

        public void Begin(PlayerController player, RiftWardenBoss boss, bool dpsOnly)
        {
            _player = player;
            _boss = boss;
            _dpsOnly = dpsOnly;
        }

        private void Update()
        {
            if (_player == null) return;
            if (_boss == null || _boss.Health.IsDead)
            {
                FloatingJoystick.DevOverride = null;
                return;
            }

            if (_dpsOnly) _player.Health.Heal(1e9f);
            Vector2 move = _dpsOnly ? Vector2.zero : Dodge();
            if (move == Vector2.zero) move = Position();
            FloatingJoystick.DevOverride = move == Vector2.zero ? (Vector2?)null : move.normalized;
        }

        private Vector2 Me => _player.transform.position;
        private Vector2 BossPos => _boss.transform.position;

        private Vector2 Dodge()
        {
            Vector2 me = Me;

            // 강타: 예고 원 안이면 바깥으로
            if (_boss.CurrentPattern == RiftWardenBoss.Pattern.Slam && _boss.IsTelegraphing)
            {
                Vector2 d = me - _boss.SlamCenter;
                if (d.magnitude < _boss.SlamRadius + 0.5f) return d.sqrMagnitude > 0.01f ? d : Perpendicular(BossPos - me);
            }

            // 돌진: 선 위(앞쪽)면 옆으로
            if (_boss.CurrentPattern == RiftWardenBoss.Pattern.Charge && (_boss.IsTelegraphing || _boss.IsAttacking))
            {
                Vector2 dir = _boss.ChargeDirection;
                Vector2 rel = me - BossPos;
                float along = Vector2.Dot(rel, dir);
                Vector2 side = rel - dir * along;
                if (along > -0.5f && side.magnitude < _boss.ChargeHitRadius + 0.9f)
                {
                    Vector2 away = side.sqrMagnitude > 0.01f ? side : Perpendicular(dir);
                    return away;
                }
            }

            // 파편: 가장 먼저 닿을 것을 옆으로 비킨다
            Vector2 best = Vector2.zero;
            float soonest = 0.7f; // 이 시간(초) 안에 닿을 것만
            foreach (Projectile p in EnemyProjectiles.Active)
            {
                if (p == null || !p.isActiveAndEnabled) continue;
                Vector2 pos = p.transform.position;
                Vector2 dir = p.transform.right;
                Vector2 rel = me - pos;
                float along = Vector2.Dot(rel, dir);
                if (along <= 0f) continue; // 이미 지나감
                Vector2 side = rel - dir * along;
                if (side.magnitude > 0.75f) continue; // 비켜 감
                float t = along / 5.5f;
                if (t >= soonest) continue;
                soonest = t;
                best = side.sqrMagnitude > 0.0001f ? side : Perpendicular(dir);
            }

            return best;
        }

        /// <summary>위험이 없을 때: 사거리 안에서 멈춘다. 너무 붙었으면 조금 물러난다</summary>
        private Vector2 Position()
        {
            Vector2 toBoss = BossPos - Me;
            float distance = toBoss.magnitude;
            float range = _player.Stats.Get(StatType.Range) * ApproachRangeRatio;
            float bodyGap = 0.55f * _boss.transform.localScale.x + 0.4f;
            if (distance > range) return toBoss;
            if (distance < bodyGap && range > bodyGap + 0.3f) return -toBoss;
            return Vector2.zero;
        }

        private static Vector2 Perpendicular(Vector2 v) => new Vector2(-v.y, v.x);

        private void LateUpdate()
        {
            // 경기장 가장자리에서 바깥으로 밀면 둘레를 따라 돈다 (벽에 끼지 않게)
            if (FloatingJoystick.DevOverride is Vector2 move && _player != null)
            {
                Vector2 me = Me;
                if (me.magnitude > ArenaSafeRadius && Vector2.Dot(move, me) > 0f)
                {
                    Vector2 tangent = Perpendicular(me.normalized);
                    if (Vector2.Dot(tangent, move) < 0f) tangent = -tangent;
                    FloatingJoystick.DevOverride = tangent;
                }
            }
        }

        private void OnDestroy() => FloatingJoystick.DevOverride = null;
    }
}
#endif
