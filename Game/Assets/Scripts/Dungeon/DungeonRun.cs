using System.Collections;
using System.Collections.Generic;
using HistoryHeroes.Common;
using HistoryHeroes.Enemy;
using HistoryHeroes.Player;
using HistoryHeroes.Progress;
using HistoryHeroes.Skills;
using HistoryHeroes.UI;
using UnityEngine;

namespace HistoryHeroes.Dungeon
{
    /// <summary>
    /// 던전 한 판의 흐름: 방 입장 → 적 등장(예고 표시) → 전멸하면 문 열림 → 문으로 다음 방 → 마지막 방을 깨면 결과.
    /// 엘리트를 잡으면 확정 레벨업. 플레이어가 쓰러지면 바로 결과. (Content.md 2-2, 2-4)
    /// </summary>
    public sealed class DungeonRun : MonoBehaviour
    {
        [SerializeField] private DungeonData dungeon;

        [Header("장면")]
        [SerializeField] private PlayerController player;
        [SerializeField] private ExperienceSystem experience;
        [SerializeField] private SkillManager skills;
        [SerializeField] private Door door;
        [SerializeField] private Transform playerStart;

        [Header("UI")]
        [SerializeField] private LevelUpScreen levelUp;
        [SerializeField] private HudView hud;
        [SerializeField] private RunResultScreen result;
        [SerializeField] private CanvasGroup fade;

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
        private int _alive;
        private bool _finished;
        private float _startTime;

        public int Room => _room;
        public int RoomCount => dungeon.rooms.Count;

        private void Start()
        {
            GamePause.ResetAll();
            _startTime = Time.time;
            player.Health.Died += _ => Finish(victory: false);
            skills.FusionCompleted += recipe => _fusions.Add(recipe.result.displayName);
            door.Entered += () => StartCoroutine(GoToNextRoom());
            if (fade != null) fade.alpha = 0f;
            EnterRoom(1);
        }

        private void EnterRoom(int room)
        {
            _room = room;
            levelUp.Floor = room;
            RoomData data = dungeon.rooms[room - 1];
            hud.RoomText = data.kind == RoomKind.Elite ? $"방 {room}/{RoomCount}  엘리트" : $"방 {room}/{RoomCount}";
            door.SetOpen(false);
            StartCoroutine(SpawnRoom(data));
        }

        private IEnumerator SpawnRoom(RoomData data)
        {
            _alive = 0;
            foreach (SpawnEntry entry in data.spawns) _alive += entry.prefab != null ? entry.count : 0;

            foreach (SpawnEntry entry in data.spawns)
            {
                if (entry.prefab == null) continue;
                for (int i = 0; i < entry.count; i++)
                {
                    StartCoroutine(SpawnOne(entry, PickSpawnPoint()));
                    yield return new WaitForSeconds(spawnStagger);
                }
            }
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
            enemy.Spawn(player.transform, dungeon.HpMultiplier(_room), dungeon.AttackMultiplier(_room), entry.elite);
            experience.Register(enemy);
            enemy.Killed -= OnEnemyKilled;
            enemy.Killed += OnEnemyKilled;
        }

        private Vector2 PickSpawnPoint()
        {
            Vector2 from = player.transform.position;
            Vector2 best = spawnArea.center;
            float bestDistance = -1f;

            // 플레이어와 충분히 떨어진 자리를 몇 번 찾아보고, 못 찾으면 그중 가장 먼 자리
            for (int i = 0; i < 12; i++)
            {
                var p = new Vector2(Random.Range(spawnArea.xMin, spawnArea.xMax), Random.Range(spawnArea.yMin, spawnArea.yMax));
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
            experience.AttractAll();
            if (_room >= RoomCount) StartCoroutine(FinishAfter(1.5f));
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
                Skills = new List<string>(),
            };

            foreach (SkillInstance s in skills.Loadout.Actives) summary.Skills.Add($"{s.Data.displayName} Lv{s.Level}");
            foreach (SkillInstance s in skills.Loadout.Passives) summary.Skills.Add($"{s.Data.displayName} Lv{s.Level}");

            result.Show(summary);
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
        public List<string> Skills;
    }
}
