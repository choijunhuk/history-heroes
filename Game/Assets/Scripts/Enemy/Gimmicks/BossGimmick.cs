using System;
using System.Collections.Generic;
using HistoryHeroes.Combat;
using UnityEngine;

namespace HistoryHeroes.Enemy
{
    /// <summary>기믹 (확장 1차 5장 + 검토 9장 3·4번): "피하고 때리기"에 풀어야 하는 장치 하나</summary>
    public enum GimmickKind
    {
        None,
        /// <summary>수정 기둥 보호막: 네 귀퉁이 기둥(체력 = 보스 3%)이 보스 받는 피해 -75%, 기둥 하나에 25%씩 줄어든다</summary>
        PillarShield,
        /// <summary>무너지는 바닥: 바닥 칸이 1.2초 금 → 3초 사라짐, 안전한 칸이 줄어든다</summary>
        CrumblingFloor,
        /// <summary>그림자 표식: 15초마다 표식 3개 (지름 2.5, 4초 뒤 터짐) — 그 4초 안에 맞는 쌍둥이를 5번 맞히면 사라진다</summary>
        ShadowMarks,
        /// <summary>거울 분신: 진짜만 그림자가 있다. 가짜를 기본 공격하면 0.5초 기절 (2.5초에 한 번까지)</summary>
        MirrorStun,
        /// <summary>불길 반쪽: 12초마다 보스가 있던 반쪽이 불길 (1.5초 예고) → 보스는 반대편으로 건너간다</summary>
        HalfFire,
    }

    /// <summary>기믹이 쓰는 싸움터 모양 (보스맵 = 원, 던전·균열 방 = 네모, 서바이벌 울타리 = 원)</summary>
    public struct BossArena
    {
        public Vector2 Center;
        public Vector2 Half;
        public bool Round;
        public float Radius;

        public static BossArena Room(Vector2 center, Vector2 half) => new BossArena { Center = center, Half = half, Radius = Mathf.Min(half.x, half.y) };
        public static BossArena Circle(Vector2 center, float radius) => new BossArena { Center = center, Half = Vector2.one * radius, Round = true, Radius = radius };

        /// <summary>네 귀퉁이 (원이면 45°마다 반지름 0.72)</summary>
        public Vector2 Corner(int i)
        {
            float sx = i % 2 == 0 ? -1f : 1f, sy = i < 2 ? 1f : -1f;
            if (Round) return Center + new Vector2(sx, sy).normalized * Radius * 0.72f;
            return Center + new Vector2(sx * (Half.x - 0.9f), sy * (Half.y - 1.2f));
        }

        public bool Contains(Vector2 p) => Round ? (p - Center).sqrMagnitude <= Radius * Radius : Mathf.Abs(p.x - Center.x) <= Half.x && Mathf.Abs(p.y - Center.y) <= Half.y;
    }

    /// <summary>기믹 하나 (보스 오브젝트에 붙는다, 풀에서 다시 쓰이면 Begin으로 새로)</summary>
    public abstract class BossGimmick : MonoBehaviour
    {
        protected EnemyBase boss;
        protected BossArena arena;
        protected Transform hero;
        private bool _running;

        /// <summary>처음 알리는 한 줄 (보스 소개 아래 · 등장 알림)</summary>
        public abstract string Hint { get; }

        public bool Running => _running;

        public void Begin(EnemyBase owner, BossArena where)
        {
            End();
            boss = owner;
            arena = where;
            hero = owner.Target;
            _running = true;
            boss.Killed -= OnBossKilled;
            boss.Killed += OnBossKilled;
            OnBegin();
        }

        private void OnBossKilled(EnemyBase _) => End();

        public void End()
        {
            if (!_running) return;
            _running = false;
            if (boss != null) boss.Killed -= OnBossKilled;
            OnEnd();
        }

        protected abstract void OnBegin();
        protected abstract void OnEnd();

        protected virtual void OnDisable() => End();

        protected bool Paused => Common.GamePause.IsPaused || boss == null || boss.Health.IsDead;
    }

    /// <summary>
    /// 기믹 붙이기 (보스맵 · 던전 중간 보스 · 균열 보스 층 · 서바이벌 중간 보스). 보스 종류마다 맞는 기믹만 (검토 4번 표).
    /// </summary>
    public static class BossGimmicks
    {
        /// <summary>기믹이 화면 알림을 띄울 때 (판이 HUD 알림으로 연결한다)</summary>
        public static Action<string> Notice;

        /// <summary>그 보스 고유 기믹 (보스맵 · 던전 · 서바이벌). 없으면 None</summary>
        public static GimmickKind For(EnemyBase boss)
        {
            switch (boss)
            {
                case OblivionKingBoss _: return GimmickKind.PillarShield; // 2단계에 무너지는 바닥이 더해진다
                case ShadowTwinsBoss _: return GimmickKind.ShadowMarks;
                case ShadowHunterBoss _: return GimmickKind.MirrorStun;
                case OblivionExecutorBoss _: return GimmickKind.HalfFire;
                default: return GimmickKind.None;
            }
        }

        /// <summary>무한의 균열 보스 층: 맞는 기믹 목록 (검토 4번: 수문장 = 기둥 · 강철 거상 = 불길 · 쌍둥이 = 표식 · 망각의 왕 = 기둥 · 바닥)</summary>
        public static GimmickKind[] RiftOptions(EnemyBase boss)
        {
            switch (boss)
            {
                case RiftWardenBoss _: return new[] { GimmickKind.PillarShield };
                case SteelColossusBoss _: return new[] { GimmickKind.HalfFire };
                case ShadowTwinsBoss _: return new[] { GimmickKind.ShadowMarks };
                case OblivionKingBoss _: return new[] { GimmickKind.PillarShield, GimmickKind.CrumblingFloor };
                default: return Array.Empty<GimmickKind>();
            }
        }

        /// <summary>균열 층 씨앗으로 하나 (같은 씨앗·층 = 같은 기믹)</summary>
        public static GimmickKind PickRift(EnemyBase boss, int seed, int floor)
        {
            GimmickKind[] options = RiftOptions(boss);
            if (options.Length == 0) return GimmickKind.None;
            var random = new System.Random(seed ^ (floor * 7919 + 0x51A3));
            return options[random.Next(options.Length)];
        }

        public static BossGimmick Attach(EnemyBase boss, GimmickKind kind, BossArena arena)
        {
            if (boss == null || kind == GimmickKind.None) return null;
            foreach (BossGimmick old in boss.GetComponents<BossGimmick>()) old.End();
            BossGimmick g;
            switch (kind)
            {
                case GimmickKind.PillarShield: g = GetOrAdd<PillarShieldGimmick>(boss); break;
                case GimmickKind.CrumblingFloor: g = GetOrAdd<CrumblingFloorGimmick>(boss); break;
                case GimmickKind.ShadowMarks: g = GetOrAdd<ShadowMarksGimmick>(boss); break;
                case GimmickKind.MirrorStun: g = GetOrAdd<MirrorStunGimmick>(boss); break;
                case GimmickKind.HalfFire: g = GetOrAdd<HalfFireGimmick>(boss); break;
                default: return null;
            }

            g.Begin(boss, arena);
            return g;
        }

        private static T GetOrAdd<T>(Component c) where T : BossGimmick => c.TryGetComponent(out T t) ? t : c.gameObject.AddComponent<T>();

        /// <summary>보스 소개 아래 한 줄 (로비 보스 시작 전 화면 · 등장 알림)</summary>
        public static string HintFor(GimmickKind kind)
        {
            switch (kind)
            {
                case GimmickKind.PillarShield: return "수정 기둥을 부술 때마다 보스 보호막이 약해져요";
                case GimmickKind.CrumblingFloor: return "금이 간 바닥은 곧 무너져요 — 다른 칸으로";
                case GimmickKind.ShadowMarks: return "그림자 표식이 뜨면 진한 쌍둥이를 사거리에 두고 멈춰 있어요 (1.2초)";
                case GimmickKind.MirrorStun: return "진짜만 그림자가 있어요 — 진짜를 맞히면 2초 동안 피해 +25%";
                case GimmickKind.HalfFire: return "불길이 오면 보스가 건너간 쪽으로 피해요";
                default: return string.Empty;
            }
        }

        /// <summary>기믹이 영웅에게 주는 피해 (적 종류)</summary>
        public static DamageInfo ToHero(float amount, string source) =>
            new DamageInfo { Amount = amount, Kind = DamageKind.Enemy, AttackerTeam = Team.Enemy, SourceId = source };

        /// <summary>장면에 남은 기믹 물건 (기둥 · 바닥 칸) 정리용</summary>
        public static void DestroyAll(List<GameObject> list)
        {
            foreach (GameObject go in list)
            {
                if (go != null) UnityEngine.Object.Destroy(go);
            }

            list.Clear();
        }
    }
}
