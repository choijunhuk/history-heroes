#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using HistoryHeroes.Combat;
using HistoryHeroes.Common;
using HistoryHeroes.Dungeon;
using HistoryHeroes.Enemy;
using HistoryHeroes.Modes;
using HistoryHeroes.Player;
using HistoryHeroes.Records;
using HistoryHeroes.UI;
using UnityEngine;

namespace HistoryHeroes.Dev
{
    /// <summary>
    /// v0.6 무한의 균열 벤치 (Modes 4-3 "보통 빌드가 20~30층 근처"): 영웅마다 1층부터 자동 조종으로 쓰러질 때까지.
    /// 일반 층 = 서바이벌 조종 AI(다가가기 켬), 보스 층 = 예고 보고 피하는 AI. 카드는 무작위(고정 씨앗), 보상 방은 체력이 낮으면 회복·아니면 카드.
    /// 판 밖 성장은 정해 둔 값 (BattleBootstrap.DevMeta). 저장은 임시 폴더 (실제 저장은 그대로).
    /// </summary>
    public sealed class RiftBench : MonoBehaviour
    {
        private static RiftBench _instance;
        private static readonly List<string> _results = new List<string>();

        public static string Status { get; private set; } = "대기";

        private List<string> _heroes;
        private float _speed;
        private int _maxFloor;
        private MetaSnapshot _meta;
        private Skills.SkillDatabase _database;
        private int _dungeonChapter;

        /// <param name="dungeonChapter">0 = 무한의 균열, 1~5 = 그 구간 던전 15방 (같은 조종으로 시간·레벨 보기)</param>
        public static string Begin(List<string> heroes, MetaSnapshot meta, float speed, int maxFloor, Skills.SkillDatabase database, int dungeonChapter = 0)
        {
            if (_instance != null) return "이미 도는 중";
            _results.Clear();
            var go = new GameObject("[RiftBench]");
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<RiftBench>();
            _instance._heroes = heroes;
            _instance._meta = meta;
            _instance._speed = speed;
            _instance._maxFloor = maxFloor;
            _instance._database = database;
            _instance._dungeonChapter = dungeonChapter;
            _instance.StartCoroutine(_instance.RunAll());
            return $"시작: {heroes.Count}판 ×{speed} " + (dungeonChapter > 0 ? $"({dungeonChapter}구간 던전)" : $"(최대 {maxFloor}층)");
        }

        private IEnumerator RunAll()
        {
            Application.runInBackground = true;
            SaveStore real = SaveSystem.Store;
            var sandbox = new SaveStore(Path.Combine(Application.temporaryCachePath, "RiftBenchSave"));
            sandbox.Save(SaveSystem.Data);
            SaveSystem.Use(sandbox);

            for (int i = 0; i < _heroes.Count; i++)
            {
                Status = $"{i + 1}/{_heroes.Count} {_heroes[i]}";
                yield return One(_heroes[i]);
            }

            SaveSystem.Use(real);
            BattleBootstrap.DevMeta = null;
            GamePause.DevSpeed = 1f;
            FloatingJoystick.DevOverride = null;
            Status = "끝";
            Debug.Log("[RiftBench]\n" + string.Join("\n", _results));
            _instance = null;
            Destroy(gameObject);
        }

        private IEnumerator One(string heroId)
        {
            SaveData save = SaveSystem.Data;
            if (!save.unlockedHeroes.Contains(heroId)) save.unlockedHeroes.Add(heroId);
            save.selectedHero = heroId;
            save.unlockedChapter = Mathf.Max(save.unlockedChapter, 4);
            save.rift.checkpoint = new RiftCheckpoint();
            BattleBootstrap.DevMeta = _meta;
            GamePause.DevSpeed = 1f;
            bool dungeon = _dungeonChapter != 0; // -1 = 튜토리얼 던전 (v1.0)
            if (_dungeonChapter < 0) GameSession.StartTutorial(false);
            else if (dungeon) GameSession.StartDungeon(_dungeonChapter);
            else GameSession.StartRift(false);
            float lowestHp = 1f;

            DungeonRun run = null;
            PlayerController player = null;
            float waitUntil = Time.realtimeSinceStartup + 20f;
            while (Time.realtimeSinceStartup < waitUntil)
            {
                yield return null;
                run = FindAnyObjectByType<DungeonRun>();
                player = FindAnyObjectByType<PlayerController>();
                if (run != null && run.IsRift != dungeon && player != null && player.Hero != null && player.Hero.id == heroId) break;
            }

            if (run == null || run.IsRift == dungeon || player == null || player.Hero == null || player.Hero.id != heroId)
            {
                _results.Add($"{heroId} | 판이 안 열림 (또는 다른 영웅)");
                yield break;
            }

            // 무엇에 맞았나 (적 ID·보스 패턴별 받은 피해)
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
            var levelUp = FindAnyObjectByType<LevelUpScreen>();
            var experience = FindAnyObjectByType<Progress.ExperienceSystem>();
            var skills = FindAnyObjectByType<Skills.SkillManager>();
            var door = FindAnyObjectByType<Door>();
            var walker = run.gameObject.AddComponent<SurvivalPilot>();
            walker.Begin(player, levelUp, experience, false, heroId.GetHashCode());
            walker.Approach = true;
            var dodger = run.gameObject.AddComponent<TelegraphPilot>();
            dodger.enabled = false;
            var random = new System.Random(heroId.GetHashCode() ^ 0x2f);
            GamePause.DevSpeed = _speed;

            float start = Time.time;
            int lastFloor = run.Room;
            float floorStart = Time.time;
            float doorWait = 0f;
            var samples = new StringBuilder();
            int stuckCount = 0;
            while (!run.IsFinished && run.Room <= _maxFloor && Time.time - start < 3600f)
            {
                yield return null;
                if (run.Room != lastFloor)
                {
                    if (lastFloor % 5 == 0)
                    {
                        samples.Append($"\n  {lastFloor}층까지 {Format(Time.time - start)} Lv{experience.Level.Level} 체력 {player.Health.Current / Mathf.Max(1f, player.Health.Max) * 100f:0}% (이 층 {Time.time - floorStart:0}초)");
                    }

                    lastFloor = run.Room;
                    floorStart = Time.time;
                }

                // 한 방에 2분 넘게 = 못 잡는 적이 있나 본다 (자리·장애물 겹침을 적고 치운 뒤 계속)
                if (Time.time - floorStart > 120f && EnemyBase.Alive.Count > 0 && FindBoss() == null)
                {
                    stuckCount++;
                    samples.Append($"\n  ⚠️ 방 {run.Room}에서 2분 ({(run.CurrentLayout != null ? run.CurrentLayout.name : "?")}) 나 {(Vector2)player.transform.position}");
                    int obstacles = LayerMask.GetMask("Wall") | 1;
                    foreach (EnemyBase e in EnemyBase.Alive)
                    {
                        Vector2 at = e.transform.position;
                        Collider2D inside = Physics2D.OverlapCircle(at, 0.2f, obstacles);
                        samples.Append($" | {e.name.Replace("(Clone)", "")} {at} 체력 {e.Health.Current:0}/{e.Health.Max:0}{(inside != null ? " 장애물 겹침:" + inside.name : "")}{(e.gameObject.activeInHierarchy ? "" : " 꺼짐")}");
                    }

                    Dev.DevCheats.KillAll();
                    floorStart = Time.time;
                }

                lowestHp = Mathf.Min(lowestHp, player.Health.Current / Mathf.Max(1f, player.Health.Max));

                // 보상 방: 체력이 낮으면 회복, 아니면 카드
                if (run.RewardRoom != null && run.RewardRoom.IsOpen)
                {
                    float hp = player.Health.Current / Mathf.Max(1f, player.Health.Max);
                    run.RewardRoom.Choose(hp < 0.55f ? RewardRoomPanel.Choice.Heal : RewardRoomPanel.Choice.Card);
                    continue;
                }

                // 보스 층: 예고 보고 피하는 AI로 바꾼다
                EnemyBase boss = FindBoss();
                if (boss != null && !dodger.enabled)
                {
                    dodger.Begin(player, boss, false);
                    dodger.enabled = true;
                    walker.enabled = false;
                }
                else if (boss == null && dodger.enabled)
                {
                    dodger.enabled = false;
                    walker.enabled = true;
                    FloatingJoystick.DevOverride = null;
                }

                if (!walker.enabled && levelUp.IsOpen)
                {
                    levelUp.ChooseIndex(random.Next(3));
                    if (levelUp.IsOpen) levelUp.ChooseIndex(0);
                }

                // 다 깼으면 보석을 줍고 문으로 (걸어가는 시간은 생략)
                if (door.IsOpen && EnemyBase.Alive.Count == 0 && !levelUp.IsOpen)
                {
                    doorWait += Time.deltaTime;
                    if ((doorWait > 1f && experience.ActiveGems == 0) || doorWait > 4f)
                    {
                        player.Teleport(door.transform.position);
                        doorWait = 0f;
                    }
                }
                else
                {
                    doorWait = 0f;
                }
            }

            float seconds = Time.time - start;
            int floor = run.Room;
            bool died = run.IsFinished && player.Health.IsDead;
            GamePause.DevSpeed = 1f;
            FloatingJoystick.DevOverride = null;
            Health.AnyDamaged -= OnDamaged;
            float takenTotal = 0f;
            foreach (float v in taken.Values) takenTotal += v;
            var parts = new List<KeyValuePair<string, float>>(taken);
            parts.Sort((a, b) => b.Value.CompareTo(a.Value));
            var takenParts = new List<string>();
            for (int i = 0; i < parts.Count && i < 5; i++) takenParts.Add($"{parts[i].Key} {parts[i].Value / Mathf.Max(1f, takenTotal) * 100f:0}%");
            string outcome = dungeon ? (died ? $"쓰러짐 방 {floor}" : run.IsFinished ? "클리어" : $"시간 초과 방 {floor}") : $"{(died ? "쓰러짐" : "상한")} {floor}층";
            if (stuckCount > 0) outcome += $" (막힌 방 {stuckCount}번)";
            if (_dungeonChapter < 0) outcome += $" · 가장 낮은 체력 {lowestHp * 100f:0}%";
            _results.Add($"{heroId} | {outcome} | {Format(seconds)} | Lv{experience.Level.Level} | 균열 강화 {skills.RiftBoostCount} | 처치 {experience.Kills} | " +
                         $"{Economy.RunText.TopDamage(_database, player.Hero, 4)} | 받은 피해 ({string.Join(" · ", takenParts)}){samples}");
            Destroy(walker);
            Destroy(dodger);
            yield return new WaitForSecondsRealtime(2f); // 결과·저장까지
        }

        /// <summary>균열 보스 (균열에는 중간 보스가 없다 → 단계 보스면 보스 층)</summary>
        private static EnemyBase FindBoss()
        {
            IReadOnlyList<EnemyBase> alive = EnemyBase.Alive;
            for (int i = 0; i < alive.Count; i++)
            {
                if (alive[i] is IPhasedBoss && !alive[i].Health.IsDead) return alive[i];
            }

            return null;
        }

        private static string Format(float seconds) => $"{Mathf.FloorToInt(seconds / 60f)}:{Mathf.FloorToInt(seconds % 60f):00}";

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
}
#endif
