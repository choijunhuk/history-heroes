using System.Collections.Generic;
using HistoryHeroes.Combat;
using HistoryHeroes.Common;
using HistoryHeroes.Dungeon;
using HistoryHeroes.Enemy;
using HistoryHeroes.UI;
using UnityEngine;

namespace HistoryHeroes.Survival
{
    /// <summary>
    /// 서바이벌 큰 모드 (확장 1차 6장): 맵 고르기 결과 받기 · 짚더미/화로 · 줍는 물건 4종 · 엘리트 보물 상자 · 맵 임무(★2).
    /// </summary>
    public sealed partial class SurvivalRun
    {
        [System.Serializable]
        private sealed class SurvivalArt
        {
            public GameObject haystack, brazier, magnet, bomb, meat, goldBag, chest, fence;
        }

        [Header("확장 1차 6장")]
        [SerializeField] private SurvivalArt art = new SurvivalArt();

        private int _mapIndex;
        private readonly List<GameObject> _crates = new List<GameObject>();
        private readonly List<float> _crateRespawns = new List<float>();
        private int _cratesBroken;
        private int _elitesEarly;
        private int _bagGold;
        private float _crateTimer;

        public int MapIndex => _mapIndex;
        public int CratesBroken => _cratesBroken;
        public int BagGold => _bagGold;

        /// <summary>BattleBootstrap이 Start 전에: 고른 맵의 숫자 · 시작 자리</summary>
        public void Configure(SurvivalData map, Transform start, int index)
        {
            if (map != null) data = map;
            if (start != null) playerStart = start;
            _mapIndex = index;
        }

        /// <summary>★2 맵 임무를 해냈나</summary>
        public bool MissionDone
        {
            get
            {
                switch (data.mission)
                {
                    case SurvivalMission.ElitesBefore: return _elitesEarly >= data.missionTarget;
                    default: return _cratesBroken >= data.missionTarget;
                }
            }
        }

        public string MissionText
        {
            get
            {
                switch (data.mission)
                {
                    case SurvivalMission.Haystacks: return $"짚더미 {data.missionTarget}개 부수기";
                    case SurvivalMission.Braziers: return $"화로 {data.missionTarget}개 부수기";
                    default: return $"{Format(data.missionDeadline)} 전에 엘리트 {data.missionTarget}마리";
                }
            }
        }

        // ---------------------------------------------------------------- 짚더미 · 화로

        private void StartCrates()
        {
            for (int i = 0; i < data.crateKeep; i++) SpawnCrate(minFromPlayer: 5f);
        }

        /// <summary>부서진 자리를 90초 뒤 다른 곳에 다시 (6~8개 유지)</summary>
        private void CrateTick()
        {
            _crateTimer -= Time.deltaTime;
            if (_crateTimer > 0f) return;
            _crateTimer = 0.5f;
            for (int i = _crateRespawns.Count - 1; i >= 0; i--)
            {
                if (_elapsed < _crateRespawns[i]) continue;
                _crateRespawns.RemoveAt(i);
                SpawnCrate(minFromPlayer: 9f);
            }

            // 영웅이 몸으로 밀고 지나가도 부서진다 (일부러 부수러 갈 수 있게)
            Vector2 me = player.transform.position;
            for (int i = _crates.Count - 1; i >= 0; i--)
            {
                GameObject c = _crates[i];
                if (c == null || !c.activeSelf) continue;
                if (((Vector2)c.transform.position - me).sqrMagnitude < 1.1f * 1.1f && c.TryGetComponent(out Breakable b))
                {
                    while (!b.Broken) b.Hit();
                }
            }
        }

        private void SpawnCrate(float minFromPlayer)
        {
            Vector2 at = FreeMapSpot(minFromPlayer);
            var go = new GameObject(data.braziers ? "Brazier" : "Haystack") { layer = RoomDevices.WallLayer };
            go.transform.SetParent(transform, false);
            go.transform.position = at;
            go.transform.localScale = new Vector3(1f, 1f, 1f);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = PlaceholderSprites.Circle;
            sr.color = data.braziers ? new Color(0.8f, 0.4f, 0.2f) : new Color(0.8f, 0.7f, 0.3f);
            sr.sortingOrder = -6;
            go.AddComponent<CircleCollider2D>().radius = 0.5f;
            var breakable = go.AddComponent<Breakable>();
            breakable.Setup(2, sr);
            breakable.BrokenNow += OnCrateBroken;
            if (RoomDevices.FitModel(go.transform, data.braziers ? art.brazier : art.haystack, 1.1f) != null) sr.enabled = false;
            _crates.Add(go);
        }

        private void OnCrateBroken(Breakable b)
        {
            b.BrokenNow -= OnCrateBroken;
            _crates.Remove(b.gameObject);
            Destroy(b.gameObject, 0.05f);
            _crateRespawns.Add(_elapsed + data.crateRespawn);
            if (_finished) return;

            _cratesBroken++;
            if (data.mission != SurvivalMission.ElitesBefore && _cratesBroken <= data.missionTarget)
            {
                hud.ShowNotice(_cratesBroken >= data.missionTarget ? $"임무 완료! ({MissionText})" : $"임무 {_cratesBroken}/{data.missionTarget}", 1.5f);
            }

            // 자석 15% · 폭탄 10% · 고기 35% · 금화 주머니 40%
            float r = Random.value;
            PickupKind kind = r < 0.15f ? PickupKind.Magnet : r < 0.25f ? PickupKind.Bomb : r < 0.6f ? PickupKind.Meat : PickupKind.GoldBag;
            DropPickup(kind, b.transform.position);
        }

        /// <summary>맵 안, 장애물 아닌 곳, 영웅에서 minFromPlayer 이상</summary>
        private Vector2 FreeMapSpot(float minFromPlayer)
        {
            Vector2 half = data.mapHalf - Vector2.one * 3f;
            Vector2 me = player != null ? (Vector2)player.transform.position : Vector2.zero;
            for (int attempt = 0; attempt < 40; attempt++)
            {
                var p = new Vector2(Random.Range(-half.x, half.x), Random.Range(-half.y, half.y));
                if ((p - me).sqrMagnitude < minFromPlayer * minFromPlayer) continue;
                if (Physics2D.OverlapCircle(p, 1.3f, ObstacleMask) == null) return p;
            }

            return me + Random.insideUnitCircle.normalized * minFromPlayer;
        }

        // ---------------------------------------------------------------- 줍는 물건

        private void DropPickup(PickupKind kind, Vector2 at)
        {
            GameObject prefab = kind == PickupKind.Magnet ? art.magnet : kind == PickupKind.Bomb ? art.bomb : kind == PickupKind.Meat ? art.meat
                : kind == PickupKind.GoldBag ? art.goldBag : art.chest;
            SurvivalPickup.Create(kind, at, transform, prefab, OnPicked);
        }

        private void OnPicked(SurvivalPickup pickup)
        {
            if (_finished) return;
            switch (pickup.Kind)
            {
                case PickupKind.Magnet:
                    StartCoroutine(MagnetSoon());
                    hud.ShowNotice("자석! 보석이 모여요", 1.2f);
                    break;
                case PickupKind.Bomb:
                    BombScreen();
                    break;
                case PickupKind.Meat:
                    player.Health.Heal(player.Health.Max * 0.2f);
                    hud.ShowNotice("고기: 체력 +20%", 1.2f);
                    break;
                case PickupKind.GoldBag:
                    int cap = data.bagGoldPerMinute * Mathf.Max(1, Mathf.CeilToInt(_elapsed / 60f));
                    int gold = Mathf.Clamp(cap - _bagGold, 0, data.bagGold);
                    _bagGold += gold;
                    hud.ShowNotice(gold > 0 ? $"금화 주머니 +{gold}" : "금화 주머니 (이번 분은 가득)", 1.2f);
                    Audio.AudioManager.Play("coin", 0.7f);
                    break;
                case PickupKind.Chest:
                    OpenChest(pickup.transform.position);
                    break;
            }
        }

        private System.Collections.IEnumerator MagnetSoon()
        {
            yield return new WaitForSeconds(0.3f);
            if (!_finished) experience.AttractAll();
        }

        /// <summary>폭탄: 보이는 범위의 적에게 공격력 300% (보스·중간 보스 빼고)</summary>
        private void BombScreen()
        {
            Rect view = WorldView();
            float attack = player.Stats.Get(Stats.StatType.Attack);
            var alive = new List<EnemyBase>(EnemyBase.Alive);
            foreach (EnemyBase e in alive)
            {
                if (e == null || e.Health.IsDead || e is IPhasedBoss || e == _boss || e == _midBoss) continue;
                if (!view.Contains(e.transform.position)) continue;
                e.Health.TryTakeDamage(RoomDevices.FromPlayer(attack * 3f, "pickup_bomb"));
            }

            FxBursts.Explosion(player.transform.position, 3f, new Color(1f, 0.55f, 0.2f));
            HypeFx.Banner("펑!", new Color(1f, 0.6f, 0.25f));
            CameraShake.Add(0.4f);
            Audio.AudioManager.Play("explode", 1f);
        }

        // ---------------------------------------------------------------- 엘리트 보물 상자

        private void HookElite(EnemyBase enemy)
        {
            enemy.Killed -= OnEliteKilled;
            enemy.Killed += OnEliteKilled;
        }

        private void OnEliteKilled(EnemyBase enemy)
        {
            enemy.Killed -= OnEliteKilled;
            if (_finished) return;
            if (_elapsed <= data.missionDeadline) _elitesEarly++;
            if (data.mission == SurvivalMission.ElitesBefore && _elitesEarly <= data.missionTarget && _elapsed <= data.missionDeadline)
                hud.ShowNotice(_elitesEarly >= data.missionTarget ? $"임무 완료! ({MissionText})" : $"임무 엘리트 {_elitesEarly}/{data.missionTarget}", 1.5f);
            DropPickup(PickupKind.Chest, enemy.transform.position);
        }

        /// <summary>
        /// 보물 상자 (검토 9장 6번): 레벨업 1회 70% · 2회 25% · 3회 5%. 한 번씩 차례로 레벨업 창을 열고,
        /// 매번 조합 후보를 다시 확인한다 (레벨업 화면이 묶음마다 CreateOffer → 조합 먼저)
        /// </summary>
        private void OpenChest(Vector2 at)
        {
            float r = Random.value;
            int levels = r < 0.7f ? 1 : r < 0.95f ? 2 : 3;
            for (int i = 0; i < levels; i++) experience.GrantLevelUp();
            FxBursts.Explosion(at, 1.4f, new Color(1f, 0.85f, 0.3f));
            HypeFx.Banner(levels == 1 ? "보물 상자!" : $"보물 상자 ×{levels}!", new Color(1f, 0.82f, 0.3f));
            Audio.AudioManager.Play(levels >= 3 ? "legendary" : "levelup", 0.9f);
        }
    }
}
