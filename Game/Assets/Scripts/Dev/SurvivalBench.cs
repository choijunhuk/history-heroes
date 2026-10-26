#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using HistoryHeroes.Combat;
using HistoryHeroes.Common;
using HistoryHeroes.Enemy;
using HistoryHeroes.Player;
using HistoryHeroes.Records;
using HistoryHeroes.Survival;
using HistoryHeroes.UI;
using UnityEngine;

namespace HistoryHeroes.Dev
{
    /// <summary>
    /// v0.5 서바이벌 벤치 (v0.5-Plan 14번): 영웅마다 서바이벌 한 판을 자동 조종(SurvivalPilot)으로 돌려
    /// 30초마다 적 수·투사체 수·보석 수·레벨·체력·프레임 시간을, 끝에 생존 시간·처치·기본 공격 비중·골드를 남긴다.
    /// "god"이면 체력을 계속 채워 10분 끝까지 (적 300·성능 확인용). 실제 저장 파일은 건드리지 않는다 (임시 저장소).
    /// </summary>
    public sealed class SurvivalBench : MonoBehaviour
    {
        private static SurvivalBench _instance;
        private static readonly List<string> _results = new List<string>();

        public static bool Running => _instance != null;
        public static string Status { get; private set; } = "대기";

        private List<string> _heroes;
        private float _speed;
        private bool _god;
        private Skills.SkillDatabase _database;

        public static string Begin(List<string> heroes, float speed, bool god, Skills.SkillDatabase database)
        {
            if (_instance != null) return "이미 도는 중";
            _results.Clear();
            var go = new GameObject("[SurvivalBench]");
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<SurvivalBench>();
            _instance._heroes = heroes;
            _instance._speed = speed;
            _instance._god = god;
            _instance._database = database;
            _instance.StartCoroutine(_instance.RunAll());
            return $"시작: {heroes.Count}판 ×{speed} {(god ? "무적" : "실전")}";
        }

        private IEnumerator RunAll()
        {
            Application.runInBackground = true;
            SaveStore real = SaveSystem.Store;
            var sandbox = new SaveStore(Path.Combine(Application.temporaryCachePath, "SurvivalBenchSave"));
            sandbox.Save(SaveSystem.Data);
            SaveSystem.Use(sandbox);

            for (int i = 0; i < _heroes.Count; i++)
            {
                Status = $"{i + 1}/{_heroes.Count} {_heroes[i]}";
                yield return One(_heroes[i]);
            }

            SaveSystem.Use(real);
            GamePause.DevSpeed = 1f;
            FloatingJoystick.DevOverride = null;
            Status = "끝";
            Debug.Log("[SurvivalBench]\n" + string.Join("\n", _results));
            _instance = null;
            Destroy(gameObject);
        }

        private IEnumerator One(string heroId)
        {
            SaveData save = SaveSystem.Data;
            if (!save.unlockedHeroes.Contains(heroId)) save.unlockedHeroes.Add(heroId);
            save.selectedHero = heroId;
            long goldBefore = save.gold;
            GamePause.DevSpeed = 1f;
            GameSession.StartSurvival();

            SurvivalRun run = null;
            PlayerController player = null;
            float waitUntil = Time.realtimeSinceStartup + 20f;
            while (Time.realtimeSinceStartup < waitUntil)
            {
                yield return null;
                run = FindAnyObjectByType<SurvivalRun>();
                player = FindAnyObjectByType<PlayerController>();
                if (run != null && player != null && player.Hero != null && player.Hero.id == heroId) break;
            }

            if (run == null || player == null || player.Hero == null || player.Hero.id != heroId)
            {
                _results.Add($"{heroId} | 서바이벌이 안 열림 (또는 다른 영웅)");
                yield break;
            }

            var pilot = run.gameObject.AddComponent<SurvivalPilot>();
            pilot.Begin(player, FindAnyObjectByType<LevelUpScreen>(), _god, heroId.GetHashCode());
            GamePause.DevSpeed = _speed;

            var samples = new StringBuilder();
            float nextSample = 30f;
            int maxAlive = 0, maxProjectiles = 0, maxGems = 0;
            var experience = FindAnyObjectByType<Progress.ExperienceSystem>();
            float frameSum = 0f;
            int frames = 0;
            float limit = run.Data.duration + 180f;
            while (!run.IsFinished && run.Elapsed < limit)
            {
                yield return null;
                frameSum += Time.unscaledDeltaTime;
                frames++;
                int alive = EnemyBase.Alive.Count;
                if (alive > maxAlive) maxAlive = alive;
                if (run.Elapsed < nextSample) continue;

                nextSample += 30f;
                int projectiles = FindObjectsByType<Projectile>(FindObjectsSortMode.None).Length;
                int gems = experience != null ? experience.ActiveGems : 0;
                if (projectiles > maxProjectiles) maxProjectiles = projectiles;
                if (gems > maxGems) maxGems = gems;
                float ms = frames > 0 ? frameSum / frames * 1000f : 0f;
                frameSum = 0f;
                frames = 0;
                samples.Append($"\n  {SurvivalRun.Format(run.Elapsed)} 적 {alive} 투사체 {projectiles} 보석 {gems} Lv{experience?.Level.Level} 체력 {player.Health.Current / Mathf.Max(1f, player.Health.Max) * 100f:0}% 처치 {experience?.Kills} 프레임 {ms:0.0}ms");
            }

            GamePause.DevSpeed = 1f;
            FloatingJoystick.DevOverride = null;
            yield return new WaitForSecondsRealtime(2f); // 결과·저장까지
            long gold = SaveSystem.Data.gold - goldBefore;
            float minutes = Mathf.Max(0.01f, run.Elapsed / 60f);
            bool win = run.IsFinished && !player.Health.IsDead;
            _results.Add($"{heroId} | {(win ? "생존·보스 격파" : run.IsFinished ? "쓰러짐" : "시간 초과")} {SurvivalRun.Format(run.Elapsed)} | 처치 {experience?.Kills} | Lv{experience?.Level.Level} | " +
                         $"기본 공격 {DamageLog.BasicShare * 100f:0}% | 골드 {gold} (분당 {gold / minutes:0}) | 최대 적 {maxAlive} 투사체 {maxProjectiles} 보석 {maxGems} | 멈춤 {pilot.StopShare * 100f:0}% | " +
                         $"{Economy.RunText.TopDamage(_database, player.Hero, 4)}{samples}");
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
    /// 서바이벌 조종 AI (벤치용, 보통 플레이어 흉내). 사람이 보는 것만 본다:
    /// 1) 돌진 예고선이 나를 향하면 옆으로 비킨다 (예고 → 회피, 던전과 같은 판단)
    /// 2) 너무 가깝거나(사거리의 절반, 최대 2.4) 무리가 몰려오면 반대쪽으로 돌며 빠진다
    /// 3) 여유가 있으면 **멈춰서 쏜다**. 맵 가장자리·바위·연못은 피한다. 카드는 무작위 (고정 씨앗).
    /// </summary>
    public sealed class SurvivalPilot : MonoBehaviour
    {
        private const float DangerRadius = 2.4f;
        private const float SenseRadius = 6f;
        private const float CrowdThreshold = 1.1f;
        private const float ChargeSense = 6.5f; // 돌진 거리 5.6 + 몸

        private PlayerController _player;
        private LevelUpScreen _levelUp;
        private bool _god;
        private System.Random _random;
        private float _stopped, _total;
        private Vector2 _lastMove = Vector2.up;
        private int _wallMask;

        /// <summary>판 전체에서 멈춰 있던 비율</summary>
        public float StopShare => _total > 0f ? _stopped / _total : 0f;

        public void Begin(PlayerController player, LevelUpScreen levelUp, bool god, int seed)
        {
            _player = player;
            _levelUp = levelUp;
            _god = god;
            _random = new System.Random(seed);
            _wallMask = LayerMask.GetMask("Wall") | 1; // 바위 + 연못(Default)
        }

        private void Update()
        {
            if (_player == null || _player.Health.IsDead) return;
            if (_levelUp != null && _levelUp.IsOpen)
            {
                _levelUp.ChooseIndex(_random.Next(3));
                if (_levelUp.IsOpen) _levelUp.ChooseIndex(0);
                return;
            }

            if (GamePause.IsPaused) return;
            if (_god) _player.Health.Heal(1e9f);

            Vector2 move = Decide();
            _total += Time.deltaTime;
            if (move == Vector2.zero) _stopped += Time.deltaTime;
            FloatingJoystick.DevOverride = move == Vector2.zero ? (Vector2?)null : move.normalized;
        }

        private Vector2 Decide()
        {
            Vector2 me = _player.transform.position;
            Vector2 push = Vector2.zero;
            Vector2 sidestep = Vector2.zero;
            float closest = float.MaxValue;
            IReadOnlyList<EnemyBase> alive = EnemyBase.Alive;
            for (int i = 0; i < alive.Count; i++)
            {
                EnemyBase e = alive[i];
                if (e == null || e.Health.IsDead) continue;
                Vector2 d = me - (Vector2)e.transform.position;
                float dist = d.magnitude;
                if (dist < 0.001f) continue;

                // 돌진 예고·돌진 중인 돌진병이 나를 향하면 그 선에서 옆으로 (예고선은 예고 시작 때 내 쪽을 향한다)
                if (e is ChargerEnemy && (e.IsTelegraphing || e.IsAttacking) && dist < ChargeSense)
                {
                    Vector2 line = -d / dist;
                    Vector2 perpendicular = new Vector2(-line.y, line.x);
                    sidestep += Vector2.Dot(perpendicular, _lastMove) >= 0f ? perpendicular : -perpendicular;
                }

                if (dist > SenseRadius) continue;
                if (dist < closest) closest = dist;
                push += d / (dist * dist);
            }

            Vector2 dir;
            if (sidestep.sqrMagnitude > 0.0001f)
            {
                dir = sidestep.normalized;
            }
            else
            {
                float range = _player.Stats.Get(Stats.StatType.Range);
                float tooClose = Mathf.Min(DangerRadius, range * 0.5f);
                bool danger = closest < tooClose || push.magnitude > CrowdThreshold;
                if (!danger) return Vector2.zero; // 여유 → 멈춰서 쏜다

                Vector2 away = push.sqrMagnitude > 0.0001f ? push.normalized : _lastMove;
                // 그냥 뒤로만 가면 구석에 몰린다 → 옆으로 조금 돌면서
                Vector2 side = new Vector2(-away.y, away.x);
                dir = (away + side * 0.6f).normalized;
            }

            // 맵 가장자리에서는 가운데 쪽으로
            const float edge = 33f;
            if (Mathf.Abs(me.x) > edge) dir.x -= Mathf.Sign(me.x) * 1.5f;
            if (Mathf.Abs(me.y) > edge) dir.y -= Mathf.Sign(me.y) * 1.5f;
            dir = dir.normalized;

            // 바위·연못이 앞에 있으면 옆으로 꺾는다
            if (Physics2D.CircleCast(me, 0.4f, dir, 1.2f, _wallMask).collider != null)
            {
                Vector2 left = new Vector2(-dir.y, dir.x);
                dir = Physics2D.CircleCast(me, 0.4f, left, 1.2f, _wallMask).collider == null ? left : -left;
            }

            _lastMove = dir;
            return dir;
        }

        private void OnDestroy() => FloatingJoystick.DevOverride = null;
    }
}
#endif
