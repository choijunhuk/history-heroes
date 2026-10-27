using System.Collections;
using System.Collections.Generic;
using HistoryHeroes.Combat;
using HistoryHeroes.Common;
using HistoryHeroes.Dungeon;
using HistoryHeroes.Enemy;
using HistoryHeroes.UI;
using UnityEngine;

namespace HistoryHeroes.Survival
{
    /// <summary>무리 모양 (확장 1차 6-3장)</summary>
    public enum SwarmKind
    {
        /// <summary>포위 고리: 졸개 24마리가 지름 12 원으로 나타나 1.5초 멈췄다 조여 온다 (틈 2곳)</summary>
        Ring,
        /// <summary>한 줄 돌격: 돌진병 10마리가 화면을 가로질러 일직선 (0.8초 빨간 화살표 줄 예고)</summary>
        Line,
        /// <summary>날쌘 떼: 빠르고 약한 졸개 7마리가 한쪽에서 반대쪽으로 지나간다 (쫓아오지 않음)</summary>
        Flock,
        /// <summary>큰 덩치 행진: 느리고 단단한 방패병 6마리 (광역보다 단일 화력 시험)</summary>
        March,
    }

    /// <summary>중간 보스 + 울타리 (확장 1차 6-1장 + 검토 10번) · 무리 모양 4가지 (6-3장)</summary>
    public sealed partial class SurvivalRun
    {
        private EnemyBase _midBoss;
        private bool _midBossDone;
        private GameObject _fence;
        private Vector2 _fenceCenter;
        private int _nextSwarm;
        private readonly List<(EnemyBase enemy, Transform goal, float until)> _passers = new List<(EnemyBase, Transform, float)>();
        private readonly List<Transform> _goals = new List<Transform>();

        public bool FenceUp => _fence != null;

        // ---------------------------------------------------------------- 중간 보스 + 울타리

        private void MidBossTick()
        {
            if (_midBossDone || data.midBossTime <= 0f || data.midBoss == null || _elapsed < data.midBossTime) return;
            _midBossDone = true;
            StartCoroutine(SpawnMidBoss());
        }

        private IEnumerator SpawnMidBoss()
        {
            hud.ShowNotice($"중간 보스 — {data.midBossName}!", 2f);
            Vector2 center = ClampToMap(player.transform.position, data.fenceRadius + 2f);
            SpriteRenderer warn = Telegraphs.Circle(center, data.fenceRadius, Telegraphs.Warning);
            for (float t = 0f; t < 1.5f; t += Time.deltaTime)
            {
                if (_finished) break;
                Telegraphs.Fill(warn, Telegraphs.Warning, t / 1.5f);
                yield return null;
            }

            Telegraphs.Hide(warn);
            if (_finished || _bossSpawned) yield break;

            RaiseFence(center);
            _midBoss = GetEnemy(data.midBoss);
            Vector2 at = center + Vector2.up * (data.fenceRadius * 0.55f);
            _midBoss.transform.position = at;
            _midBoss.gameObject.SetActive(true);
            if (_midBoss is PatternBoss pattern)
            {
                pattern.MinionHp = data.HpMultiplier(_elapsed);
                pattern.MinionAttack = data.AttackMultiplier(_elapsed);
            }

            _midBoss.Spawn(player.transform, data.midBossHp * data.HpMultiplier(_elapsed), data.midBossAttack * data.AttackMultiplier(_elapsed), false);
            experience.Register(_midBoss);
            _midBoss.Killed -= OnMidBossKilled;
            _midBoss.Killed += OnMidBossKilled;
            if (bossHud != null) bossHud.Bind(_midBoss, data.midBossName);
        }

        private void OnMidBossKilled(EnemyBase enemy)
        {
            enemy.Killed -= OnMidBossKilled;
            if (_finished) return;
            DropPickup(PickupKind.Chest, enemy.transform.position);
            DropFence();
            HypeFx.Banner("울타리가 열렸다!", new Color(1f, 0.82f, 0.3f));
        }

        /// <summary>지름 14 원 울타리: 안의 일반 적은 밖으로 밀려나고(그대로 산다), 영웅은 나갈 수 없다</summary>
        private void RaiseFence(Vector2 center)
        {
            _fenceCenter = center;
            float radius = data.fenceRadius;
            _fence = new GameObject("Fence") { layer = RoomDevices.WallLayer };
            _fence.transform.SetParent(transform, false);
            _fence.transform.position = center;
            var edge = _fence.AddComponent<EdgeCollider2D>();
            const int points = 48;
            var ring = new Vector2[points + 1];
            for (int i = 0; i <= points; i++)
            {
                float a = i * Mathf.PI * 2f / points;
                ring[i] = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * radius;
            }

            edge.points = ring;
            edge.edgeRadius = 0.15f;

            // 보이기: 쇠창살 조각을 원 둘레에 (바닥에서 솟는다) + 바닥 빛 고리
            const int segments = 24;
            float step = radius * Mathf.PI * 2f / segments;
            for (int i = 0; i < segments; i++)
            {
                float a = (i + 0.5f) * Mathf.PI * 2f / segments;
                var holder = new GameObject($"Bar{i}").transform;
                holder.SetParent(_fence.transform, false);
                holder.localPosition = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f) * radius;
                holder.localRotation = Quaternion.Euler(0f, 0f, a * Mathf.Rad2Deg + 90f);
                if (RoomDevices.FitModel(holder, art.fence, step * 1.02f) == null)
                {
                    var sr = holder.gameObject.AddComponent<SpriteRenderer>();
                    sr.sprite = PlaceholderSprites.Square;
                    sr.color = new Color(0.4f, 0.4f, 0.45f);
                    holder.localScale = new Vector3(step, 0.3f, 1f);
                }
            }

            var glow = new GameObject("Glow").AddComponent<SpriteRenderer>();
            glow.transform.SetParent(_fence.transform, false);
            glow.transform.localScale = Vector3.one * radius * 2f;
            glow.sprite = PlaceholderSprites.RimCircle;
            glow.color = new Color(1f, 0.75f, 0.3f, 0.45f);
            glow.sortingOrder = -7;
            StartCoroutine(RiseFence(_fence.transform));

            // 안의 일반 적은 밖으로 (경험치 없이 사라지지 않는다 — 그대로 산다)
            var alive = new List<EnemyBase>(EnemyBase.Alive);
            foreach (EnemyBase e in alive)
            {
                if (e == null || e.Health.IsDead || e == _boss) continue;
                Vector2 d = (Vector2)e.transform.position - center;
                if (d.sqrMagnitude >= radius * radius) continue;
                Vector2 dir = d.sqrMagnitude > 0.01f ? d.normalized : Random.insideUnitCircle.normalized;
                Vector2 to = center + dir * (radius + 1.2f);
                if (e.TryGetComponent(out Rigidbody2D rb)) rb.position = to;
                e.transform.position = new Vector3(to.x, to.y, e.transform.position.z);
            }

            // 영웅이 울타리 위에 서 있으면 안쪽으로
            Vector2 me = (Vector2)player.transform.position - center;
            if (me.magnitude > radius - 0.8f) player.Teleport(center + me.normalized * (radius - 1.2f));
            CameraShake.Add(0.25f);
            Audio.AudioManager.Play("slam", 0.8f);
        }

        private static IEnumerator RiseFence(Transform fence)
        {
            for (float t = 0f; t < 0.35f && fence != null; t += Time.deltaTime)
            {
                foreach (Transform bar in fence)
                {
                    if (bar.name.StartsWith("Bar")) bar.localPosition = new Vector3(bar.localPosition.x, bar.localPosition.y, Mathf.Lerp(2f, 0f, t / 0.35f));
                }

                yield return null;
            }

            if (fence == null) yield break;
            foreach (Transform bar in fence)
            {
                if (bar.name.StartsWith("Bar")) bar.localPosition = new Vector3(bar.localPosition.x, bar.localPosition.y, 0f);
            }
        }

        private void DropFence()
        {
            if (_fence == null) return;
            FxBursts.Explosion(_fenceCenter, data.fenceRadius * 0.5f, new Color(1f, 0.8f, 0.4f));
            Destroy(_fence);
            _fence = null;
        }

        /// <summary>울타리가 있으면 그 밖에서만 낸다 (검토 10번)</summary>
        private bool InsideFence(Vector2 p) => _fence != null && (p - _fenceCenter).sqrMagnitude < (data.fenceRadius + 1f) * (data.fenceRadius + 1f);

        private Vector2 ClampToMap(Vector2 p, float margin)
        {
            Vector2 half = data.mapHalf - Vector2.one * margin;
            return new Vector2(Mathf.Clamp(p.x, -half.x, half.x), Mathf.Clamp(p.y, -half.y, half.y));
        }

        // ---------------------------------------------------------------- 무리 모양

        private void SwarmTick()
        {
            if (data.swarmTimes == null || _nextSwarm >= data.swarmTimes.Length || _elapsed < data.swarmTimes[_nextSwarm]) return;
            SwarmKind kind = data.swarmKinds != null && _nextSwarm < data.swarmKinds.Length ? data.swarmKinds[_nextSwarm] : SwarmKind.Ring;
            _nextSwarm++;
            if (_fence != null && kind == SwarmKind.Ring) return; // 울타리 안에서는 포위 고리를 쉬게
            StartCoroutine(Swarm(kind));
        }

        private IEnumerator Swarm(SwarmKind kind)
        {
            Vector2 me = player.transform.position;
            Rect view = WorldView();
            Vector2 dir = Random.insideUnitCircle.normalized;
            if (dir.sqrMagnitude < 0.01f) dir = Vector2.right;

            // 위험 2초 전 화면 가장자리 경고 띠 + 화살표 (포위 고리는 원 예고)
            var marks = new List<SpriteRenderer>();
            if (kind == SwarmKind.Ring)
            {
                marks.Add(Telegraphs.Circle(me, 6f, Telegraphs.Danger));
                hud.ShowNotice("포위 고리! 틈으로 빠져나가요", 2f);
            }
            else
            {
                Vector2 edge = EdgePoint(view, -dir);
                bool vertical = Mathf.Abs(dir.x) > Mathf.Abs(dir.y);
                Vector2 bandSize = vertical ? new Vector2(1.2f, view.height) : new Vector2(view.width, 1.2f);
                marks.Add(Telegraphs.Box(edge + dir * 0.6f, bandSize, 0f, Telegraphs.Danger));
                marks.Add(Telegraphs.Line(edge + dir * 0.8f, edge + dir * 3.2f, 0.5f, Telegraphs.Danger));
                hud.ShowNotice(kind == SwarmKind.Line ? "한 줄 돌격!" : kind == SwarmKind.Flock ? "날쌘 떼가 지나가요!" : "큰 덩치 행진!", 2f);
            }

            Audio.AudioManager.Play("telegraph", 0.8f);
            for (float t = 0f; t < 2f; t += Time.deltaTime)
            {
                if (_finished) break;
                foreach (SpriteRenderer m in marks) Telegraphs.Fill(m, Telegraphs.Danger, t / 2f);
                yield return null;
            }

            foreach (SpriteRenderer m in marks) Telegraphs.Hide(m);
            if (_finished || _bossSpawned) yield break;

            switch (kind)
            {
                case SwarmKind.Ring: SpawnRing(player.transform.position); break;
                case SwarmKind.Line: yield return SpawnLine(view, dir); break;
                case SwarmKind.Flock: SpawnPassers(data.minion != null ? data.minion : data.charger, 7, view, dir, 1.8f, 0.8f); break;
                case SwarmKind.March: SpawnMarch(view, dir); break;
            }
        }

        /// <summary>보이는 범위 가장자리에서 dir 쪽 끝점</summary>
        private static Vector2 EdgePoint(Rect view, Vector2 dir)
        {
            Vector2 c = view.center;
            float tx = Mathf.Abs(dir.x) > 0.001f ? (view.width * 0.5f) / Mathf.Abs(dir.x) : float.MaxValue;
            float ty = Mathf.Abs(dir.y) > 0.001f ? (view.height * 0.5f) / Mathf.Abs(dir.y) : float.MaxValue;
            return c + dir * Mathf.Min(tx, ty);
        }

        private void SpawnRing(Vector2 center)
        {
            EnemyBase prefab = data.minion != null ? data.minion : data.charger;
            const int count = 24;
            int gapA = Random.Range(0, count);
            int gapB = (gapA + count / 2) % count;
            for (int i = 0; i < count; i++)
            {
                // 틈 2곳 (마주 보는 자리, 3칸씩)
                if (Mathf.Abs(i - gapA) <= 1 || Mathf.Abs(i - gapB) <= 1 || (gapA == 0 && i == count - 1) || (gapB == 0 && i == count - 1)) continue;
                float a = i * Mathf.PI * 2f / count;
                Vector2 p = ClampToMap(center + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 6f, 1f);
                if (!IsFree(p) || InsideFence(p) && _fence != null) continue;
                EnemyBase e = SpawnAt(prefab, p, 0.8f);
                if (e != null) e.Status.ApplyFreeze(1.5f); // 1.5초 멈췄다 조여 온다
            }
        }

        private IEnumerator SpawnLine(Rect view, Vector2 dir)
        {
            Vector2 side = new Vector2(-dir.y, dir.x);
            Vector2 start = EdgePoint(view, -dir) - dir * 1.5f;
            for (int i = 0; i < 10; i++)
            {
                Vector2 p = start + side * ((i - 4.5f) * 1.1f);
                SpawnPasser(data.charger, ClampToMap(p, 1f), dir, view, 1.25f, 1f);
            }

            yield break;
        }

        private void SpawnPassers(EnemyBase prefab, int count, Rect view, Vector2 dir, float haste, float scale)
        {
            Vector2 side = new Vector2(-dir.y, dir.x);
            Vector2 start = EdgePoint(view, -dir) - dir * 1.5f;
            for (int i = 0; i < count; i++)
            {
                Vector2 p = start + side * Random.Range(-3f, 3f) - dir * Random.Range(0f, 2f);
                SpawnPasser(prefab, ClampToMap(p, 1f), dir, view, haste, scale);
            }
        }

        /// <summary>쫓아오지 않고 반대편 끝으로 지나간다 (목표 = 화면 밖 반대쪽 점). 닿거나 8초가 지나면 조용히 사라진다</summary>
        private void SpawnPasser(EnemyBase prefab, Vector2 at, Vector2 dir, Rect view, float haste, float scale)
        {
            if (prefab == null || EnemyBase.Alive.Count >= data.maxAlive) return;
            EnemyBase e = SpawnAt(prefab, at, 0.7f);
            if (e == null) return;
            Transform goal = GoalAt(ClampToMap(EdgePoint(view, dir) + dir * 4f + (at - EdgePoint(view, -dir)), 0.5f));
            e.Spawn(goal, data.HpMultiplier(_elapsed) * 0.7f, data.AttackMultiplier(_elapsed), false);
            e.Status.Haste = haste;
            e.transform.localScale *= scale;
            _passers.Add((e, goal, _elapsed + 8f));
        }

        private void SpawnMarch(Rect view, Vector2 dir)
        {
            EnemyBase prefab = data.shield != null ? data.shield : data.charger;
            Vector2 side = new Vector2(-dir.y, dir.x);
            Vector2 start = EdgePoint(view, -dir) - dir * 1.5f;
            for (int i = 0; i < 6; i++)
            {
                Vector2 p = ClampToMap(start + side * ((i - 2.5f) * 1.6f), 1f);
                EnemyBase e = SpawnAt(prefab, p, 2.2f); // 느리고 단단하게 (체력 ×2.2)
                if (e != null) e.transform.localScale *= 1.2f;
            }
        }

        /// <summary>정한 자리에 적 하나 (체력 배수 hpScale × 지금 시간 배수)</summary>
        private EnemyBase SpawnAt(EnemyBase prefab, Vector2 at, float hpScale)
        {
            if (prefab == null || EnemyBase.Alive.Count >= data.maxAlive) return null;
            EnemyBase enemy = GetEnemy(prefab);
            enemy.transform.position = at;
            enemy.gameObject.SetActive(true);
            enemy.Spawn(player.transform, data.HpMultiplier(_elapsed) * hpScale, data.AttackMultiplier(_elapsed), false);
            experience.Register(enemy);
            return enemy;
        }

        private Transform GoalAt(Vector2 p)
        {
            Transform goal = _goals.Find(g => !g.gameObject.activeSelf);
            if (goal == null)
            {
                goal = new GameObject("SwarmGoal").transform;
                goal.SetParent(transform, false);
                _goals.Add(goal);
            }

            goal.position = p;
            goal.gameObject.SetActive(true);
            return goal;
        }

        /// <summary>지나가는 무리: 목표에 닿았거나 시간이 지나면 사라진다 (경험치 없음)</summary>
        private void PasserTick()
        {
            for (int i = _passers.Count - 1; i >= 0; i--)
            {
                (EnemyBase e, Transform goal, float until) = _passers[i];
                bool gone = e == null || !e.gameObject.activeSelf || e.Health.IsDead;
                bool arrived = !gone && ((Vector2)e.transform.position - (Vector2)goal.position).sqrMagnitude < 2f * 2f;
                if (!gone && !arrived && _elapsed < until) continue;
                if (!gone)
                {
                    e.Status.Haste = 1f;
                    e.gameObject.SetActive(false);
                }

                goal.gameObject.SetActive(false);
                _passers.RemoveAt(i);
            }
        }
    }
}
