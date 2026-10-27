using System.Collections.Generic;
using HistoryHeroes.Common;
using HistoryHeroes.Dungeon;
using UnityEngine;

namespace HistoryHeroes.Enemy
{
    /// <summary>
    /// 무너지는 바닥 (확장 1차 5장, 망각의 왕 2단계 · 균열): 바닥 칸이 1.2초 금 → 3초 사라짐 (망각 균열 함정 그대로).
    /// 처음엔 3칸, 6초마다 한 칸씩 늘어 싸움터의 45%까지 → 안전한 칸이 줄어든다. 사라진 칸에 서 있으면 피해 + 가까운 곳으로 밀려난다
    /// </summary>
    public sealed class CrumblingFloorGimmick : BossGimmick
    {
        public const float Cell = 1.8f;
        public const int StartCells = 3;
        public const float AddEvery = 6f;
        public const float MaxShare = 0.45f;

        private readonly List<Vector2> _cells = new List<Vector2>();
        private readonly List<GameObject> _zones = new List<GameObject>();
        private float _clock;
        private float _nextAdd;

        public override string Hint => BossGimmicks.HintFor(GimmickKind.CrumblingFloor);
        public int ActiveCells => _zones.Count;

        protected override void OnBegin()
        {
            _cells.Clear();
            int n = Mathf.CeilToInt(arena.Half.x * 2f / Cell), m = Mathf.CeilToInt(arena.Half.y * 2f / Cell);
            for (int x = 0; x < n; x++)
            {
                for (int y = 0; y < m; y++)
                {
                    var p = new Vector2(arena.Center.x - arena.Half.x + Cell * (x + 0.5f), arena.Center.y - arena.Half.y + Cell * (y + 0.5f));
                    if (arena.Round && (p - arena.Center).magnitude > arena.Radius - Cell * 0.5f) continue;
                    _cells.Add(p);
                }
            }

            // 무작위 순서 (보스 자리에서 시드 — 같은 판이면 같은 순서일 필요는 없다)
            for (int i = _cells.Count - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                (_cells[i], _cells[j]) = (_cells[j], _cells[i]);
            }

            _clock = 0f;
            _nextAdd = 0f;
            for (int i = 0; i < StartCells; i++) AddZone();
            BossGimmicks.Notice?.Invoke(Hint);
        }

        protected override void OnEnd() => BossGimmicks.DestroyAll(_zones);

        private void Update()
        {
            if (!Running || Paused) return;
            _clock += Time.deltaTime;
            if (_clock < _nextAdd) return;
            _nextAdd = _clock + AddEvery;
            if (_zones.Count < Mathf.FloorToInt(_cells.Count * MaxShare)) AddZone();
        }

        private void AddZone()
        {
            if (_zones.Count >= _cells.Count) return;
            Vector2 at = _cells[_zones.Count];
            var go = new GameObject("CrumbleCell");
            go.transform.position = at;
            var areaObject = new GameObject("Area");
            areaObject.transform.SetParent(go.transform, false);
            var sr = areaObject.AddComponent<SpriteRenderer>();
            sr.sprite = PlaceholderSprites.Square;
            sr.sortingOrder = -9;
            var zone = go.AddComponent<TrapZone>();
            zone.SetArea(sr);
            float damage = boss != null ? boss.Damage * 0.9f : 60f;
            zone.Setup(TrapZone.Effect.Oblivion, at, Vector2.one * (Cell - 0.1f), 0f, damage, Random.Range(0f, 6f));
            _zones.Add(go);
        }
    }
}
