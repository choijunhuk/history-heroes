using System;
using System.Collections;
using System.Collections.Generic;
using HistoryHeroes.Combat;
using HistoryHeroes.Common;
using HistoryHeroes.Player;
using UnityEngine;

namespace HistoryHeroes.Enemy
{
    /// <summary>보스 HUD가 단계·빈틈을 읽는다 (균열의 수문장·패턴 보스 공통)</summary>
    public interface IPhasedBoss
    {
        int Phase { get; }
        bool IsOpen { get; }
    }

    /// <summary>
    /// 패턴 보스 바탕 (v0.6: 중간 보스 5 · 보스맵 보스 4). 패턴을 "예고 → 공격 → 빈틈" 순서의 짧은 루틴으로 쓴다.
    /// 루틴은 이 적의 Tick에서만 한 걸음씩 나아간다 → 멈춤(레벨업 창)·빙결 동안 패턴 시간도 멈춘다 (EnemyBase 규칙 그대로).
    /// - 체력 2/3 · 1/3에서 단계 전환 (Modes 2-9), 3단계는 예고 0.8배 (빈틈 길이는 그대로)
    /// - 빈틈(Recover) 동안 발밑에 노란 고리 = "지금 멈춰서 쏘세요"
    /// - 졸개 소환은 판이 넣어 준 EnemyBase.ChildSpawner로 (판이 처치 수·경험치를 센다)
    /// </summary>
    public abstract class PatternBoss : EnemyBase, IPhasedBoss
    {
        [Header("보스")]
        [SerializeField] protected float bossScale = 2f;
        [SerializeField] protected float walkTime = 1.1f;
        [SerializeField] protected float keepDistance = 2.6f;
        [Tooltip("중간 보스 (던전 15번 방): 단계는 2개, 손맛 효과 작게")]
        [SerializeField] protected bool midBoss;
        [SerializeField] protected LayerMask wallMask;
        [SerializeField] protected SpriteRenderer openingRing;

        private readonly Stack<IEnumerator> _stack = new Stack<IEnumerator>();
        private readonly List<SpriteRenderer> _shown = new List<SpriteRenderer>();
        protected float bossTime;
        protected int phase = 1;
        private bool _started;

        public int Phase => phase;
        public bool IsOpen => state == State.Recover;

        /// <summary>무한의 균열 버전: 2단계 패턴을 1단계부터 쓴다 (v0.6-Plan [제안] "패턴 1개 추가")</summary>
        public bool RiftVersion { get; set; }
        public override bool CanBeKnockedBack => false;
        public event Action<int> PhaseChanged;

        /// <summary>3단계 예고 0.8배 × 강화형</summary>
        protected float Tele => (phase >= 3 ? 0.8f : 1f) * TelegraphScale;
        protected Vector2 Pos => transform.position;
        protected Vector2 PlayerPos => player.position;

        protected override void Awake()
        {
            base.Awake();
            if (openingRing != null) openingRing.enabled = false;
            health.Damaged += (_, __) => UpdatePhase();
        }

        public override void Spawn(Transform target, float hpMultiplier, float atkMultiplier, bool elite)
        {
            base.Spawn(target, hpMultiplier, atkMultiplier, false);
            transform.localScale = Vector3.one * bossScale;
            phase = RiftVersion ? 2 : 1;
            bossTime = 0f;
            _stack.Clear();
            _started = false;
            HideAll();
            OnSpawned();
            Enter(State.Approach);
        }

        protected virtual void OnSpawned()
        {
        }

        private void UpdatePhase()
        {
            float ratio = health.Max > 0f ? health.Current / health.Max : 0f;
            int next = midBoss ? (ratio > 0.5f ? 1 : 2) : ratio > 2f / 3f ? 1 : ratio > 1f / 3f ? 2 : 3;
            if (next <= phase) return;
            phase = next;
            HitStop.Freeze(midBoss ? HitStop.EliteKill : HitStop.BossPhase);
            OnPhaseChanged(phase);
            PhaseChanged?.Invoke(phase);
        }

        protected virtual void OnPhaseChanged(int newPhase)
        {
        }

        protected override void Tick()
        {
            bossTime += Time.deltaTime;
            if (openingRing != null) openingRing.enabled = state == State.Recover;
            OnTick();

            if (!_started)
            {
                _started = true;
                _stack.Push(Loop());
            }

            // 루틴 한 걸음: 하위 루틴을 따라 들어가고, 끝나면 같은 프레임에 부모로 돌아간다
            int guard = 0;
            while (_stack.Count > 0 && guard++ < 64)
            {
                IEnumerator top = _stack.Peek();
                if (!top.MoveNext())
                {
                    _stack.Pop();
                    continue;
                }

                if (top.Current is IEnumerator child)
                {
                    _stack.Push(child);
                    continue;
                }

                break;
            }
        }

        protected virtual void OnTick()
        {
        }

        private IEnumerator Loop()
        {
            while (true)
            {
                yield return Walk(walkTime);
                yield return Cycle();
            }
        }

        /// <summary>패턴 하나 (단계마다 고른다)</summary>
        protected abstract IEnumerator Cycle();

        // ---------------------------------------------------------------- 기본 루틴

        protected IEnumerator Wait(float seconds)
        {
            float end = bossTime + seconds;
            while (bossTime < end) yield return null;
        }

        /// <summary>다가가되 너무 붙지 않는다</summary>
        protected IEnumerator Walk(float seconds)
        {
            Enter(State.Approach);
            float end = bossTime + seconds;
            while (bossTime < end)
            {
                if (ToPlayer.magnitude > keepDistance) Walk(ToPlayer, data.moveSpeed);
                else body.linearVelocity = Vector2.zero;
                yield return null;
            }

            body.linearVelocity = Vector2.zero;
        }

        /// <summary>빈틈 (멈춰서 쏠 때)</summary>
        protected IEnumerator Open(float seconds)
        {
            Enter(State.Recover);
            body.linearVelocity = Vector2.zero;
            yield return Wait(seconds);
        }

        /// <summary>원 예고가 차오르다가 → 그 안의 플레이어에게 피해 (여러 곳 동시에)</summary>
        protected IEnumerator CircleStrikes(IReadOnlyList<Vector2> centers, float radius, float telegraph, float damage, string source, Color? color = null)
        {
            Enter(State.Telegraph);
            body.linearVelocity = Vector2.zero;
            Color c = color ?? Telegraphs.Danger;
            var markers = new List<SpriteRenderer>(centers.Count);
            foreach (Vector2 p in centers) markers.Add(Show(Telegraphs.Circle(p, radius, c)));
            float start = bossTime;
            while (bossTime < start + telegraph)
            {
                float t = (bossTime - start) / Mathf.Max(0.01f, telegraph);
                foreach (SpriteRenderer m in markers) Telegraphs.Fill(m, c, t);
                yield return null;
            }

            Enter(State.Attack);
            bool hit = false;
            foreach (Vector2 p in centers)
            {
                if (!hit && HitPlayerIn(p, radius, damage, source)) hit = true; // 겹쳐도 한 번만
                AttackFx.Burst(p, radius, new Color(1f, 0.8f, 0.55f, 0.55f), 0.15f);
            }

            foreach (SpriteRenderer m in markers) HideShown(m);
            Audio.AudioManager.Play("slam", 0.7f);
            CameraShake.Add(midBoss ? CameraShake.PlayerHit : CameraShake.BossImpact);
        }

        protected IEnumerator CircleStrike(Vector2 center, float radius, float telegraph, float damage, string source)
        {
            yield return CircleStrikes(new[] { center }, radius, telegraph, damage, source);
        }

        /// <summary>
        /// 직선 돌진: 선 예고 (끝 0.25초는 방향 고정) → 돌진 → 벽에 부딪히면 기절 (가장 큰 빈틈), 빗나가면 짧은 빈틈.
        /// 돌아오는 값 없음. 기절했으면 stunned = true (다음 패턴에서 쓴다)
        /// </summary>
        protected IEnumerator Dash(float telegraph, float speed, float maxTime, float damage, string source, float stun, float missRecover, float hitRadius = 0.5f)
        {
            Enter(State.Telegraph);
            body.linearVelocity = Vector2.zero;
            Vector2 dir = ToPlayer.normalized;
            SpriteRenderer line = Show(Telegraphs.Line(Pos, Pos + dir * speed * maxTime, 0.5f, Telegraphs.Danger));
            float start = bossTime;
            while (bossTime < start + telegraph)
            {
                if (bossTime < start + telegraph - 0.25f) dir = ToPlayer.normalized;
                Telegraphs.SetLine(line, Pos, DashEnd(dir, speed * maxTime, hitRadius), 0.5f);
                Telegraphs.Fill(line, Telegraphs.Danger, (bossTime - start) / Mathf.Max(0.01f, telegraph));
                yield return null;
            }

            HideShown(line);
            DashDirection = dir;
            Enter(State.Attack);
            bool hit = false;
            float end = bossTime + maxTime;
            float radius = hitRadius * transform.localScale.x;
            while (bossTime < end)
            {
                float step = speed * Time.deltaTime;
                RaycastHit2D wall = Physics2D.CircleCast(Pos, radius, dir, step + 0.05f, wallMask);
                if (wall.collider != null)
                {
                    body.linearVelocity = Vector2.zero;
                    CameraShake.Add(CameraShake.EliteKill);
                    LastDashStunned = true;
                    yield return Open(stun);
                    yield break;
                }

                MoveToward(dir, speed);
                if (!hit) hit = HitPlayerIn(Pos, radius, damage, source);
                yield return null;
            }

            body.linearVelocity = Vector2.zero;
            LastDashStunned = false;
            yield return Open(missRecover);
        }

        public Vector2 DashDirection { get; private set; }
        protected bool LastDashStunned { get; private set; }

        private Vector2 DashEnd(Vector2 dir, float distance, float hitRadius)
        {
            RaycastHit2D wall = Physics2D.CircleCast(Pos, hitRadius * transform.localScale.x, dir, distance, wallMask);
            return wall.collider != null ? wall.centroid : Pos + dir * distance;
        }

        /// <summary>부채꼴 휘두르기: 앞쪽 부채꼴 예고 (끝 0.2초 방향 고정) → 그 안의 플레이어에게 피해</summary>
        protected IEnumerator ArcStrike(float radius, float degrees, float telegraph, float damage, string source)
        {
            Enter(State.Telegraph);
            body.linearVelocity = Vector2.zero;
            Vector2 aim = ToPlayer.normalized;
            SpriteRenderer fan = Show(Telegraphs.Circle(Pos, radius, Telegraphs.Danger, -2));
            fan.sprite = PlaceholderSprites.Fan(Mathf.RoundToInt(degrees));
            float start = bossTime;
            while (bossTime < start + telegraph)
            {
                if (bossTime < start + telegraph - 0.2f) aim = ToPlayer.normalized;
                fan.transform.position = Pos;
                fan.transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(aim.y, aim.x) * Mathf.Rad2Deg);
                Telegraphs.Fill(fan, Telegraphs.Danger, (bossTime - start) / Mathf.Max(0.01f, telegraph));
                yield return null;
            }

            HideShown(fan);
            Enter(State.Attack);
            AttackFx.Arc(Pos, aim, radius, degrees, new Color(1f, 0.6f, 0.25f, 0.7f), 0.15f);
            Collider2D hit = Physics2D.OverlapCircle(Pos, radius, playerMask);
            if (hit != null && Vector2.Angle(aim, (Vector2)hit.transform.position - Pos) <= degrees * 0.5f && hit.TryGetComponent(out Health target))
            {
                target.TryTakeDamage(DamageCalculator.Enemy(Damage * damage, source));
            }
        }

        /// <summary>from에서 플레이어 쪽 부채꼴 탄 (분신·쌍둥이가 쏠 때)</summary>
        protected void FanFrom(Vector2 from, int count, float spreadDegrees, float speed, float damage, string source)
        {
            Vector2 aim = ((Vector2)player.position - from).normalized;
            float start = -spreadDegrees * 0.5f;
            float step = count > 1 ? spreadDegrees / (count - 1) : 0f;
            for (int i = 0; i < count; i++)
            {
                Vector2 d = Quaternion.Euler(0f, 0f, count > 1 ? start + step * i : 0f) * aim;
                EnemyProjectiles.Fire(from + d * 0.6f, d, speed, DamageCalculator.Enemy(Damage * damage, source), playerMask, this);
            }
        }

        /// <summary>플레이어를 한쪽으로 민다 (증기 골렘의 컨베이어). 바닥 띠가 깜빡인다</summary>
        protected IEnumerator PushPlayer(Vector2 direction, float speed, float seconds, Color color)
        {
            SpriteRenderer band = Show(Telegraphs.Box(Vector2.zero, new Vector2(30f, 30f), Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg, color, -9));
            Rigidbody2D rb = player.GetComponent<Rigidbody2D>();
            PlayerController pushedPlayer = rb != null ? rb.GetComponent<PlayerController>() : null;
            float end = bossTime + seconds;
            while (bossTime < end)
            {
                Color c = color;
                c.a = color.a * (0.6f + 0.4f * Mathf.Sin(bossTime * 10f));
                band.color = c;
                if (rb != null)
                {
                    Vector2 delta = direction.normalized * speed * Time.deltaTime;
                    rb.position += delta;
                    if (pushedPlayer != null) pushedPlayer.NoteExternalDisplacement(delta);
                }
                yield return null;
            }

            HideShown(band);
        }

        /// <summary>순간 이동 (보랏빛 깜빡임). 경기장 안 자리로</summary>
        protected void Blink(Vector2 to)
        {
            AttackFx.Burst(Pos, 0.8f, new Color(0.6f, 0.3f, 0.9f, 0.6f), 0.2f);
            body.position = Inside(to);
            transform.position = body.position;
            AttackFx.Burst(body.position, 0.8f, new Color(0.6f, 0.3f, 0.9f, 0.6f), 0.2f);
        }

        /// <summary>몸이 빛나는 예고 (사방 탄 등)</summary>
        protected IEnumerator Glow(float seconds, Color color)
        {
            Enter(State.Telegraph);
            body.linearVelocity = Vector2.zero;
            SpriteRenderer glow = Show(Telegraphs.Circle(Pos, transform.localScale.x * 0.75f, color, 2));
            float start = bossTime;
            while (bossTime < start + seconds)
            {
                glow.transform.position = Pos;
                Color c = color;
                c.a = color.a * (0.6f + 0.4f * Mathf.Sin(bossTime * 18f));
                glow.color = c;
                yield return null;
            }

            HideShown(glow);
        }

        /// <summary>사방으로 탄 (offset 0~1: 칸 사이로 비틀기)</summary>
        protected void Ring(int count, float speed, float damage, string source, float offset = 0f)
        {
            float step = 360f / Mathf.Max(1, count);
            for (int i = 0; i < count; i++)
            {
                float angle = (i + offset) * step * Mathf.Deg2Rad;
                var d = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                EnemyProjectiles.Fire(Pos + d * 0.9f, d, speed, DamageCalculator.Enemy(Damage * damage, source), playerMask, this);
            }
        }

        /// <summary>플레이어 쪽 부채꼴 탄</summary>
        protected void Fan(int count, float spreadDegrees, float speed, float damage, string source)
        {
            Vector2 aim = ToPlayer.normalized;
            float start = -spreadDegrees * 0.5f;
            float step = count > 1 ? spreadDegrees / (count - 1) : 0f;
            for (int i = 0; i < count; i++)
            {
                Vector2 d = Quaternion.Euler(0f, 0f, count > 1 ? start + step * i : 0f) * aim;
                EnemyProjectiles.Fire(Pos + d * 0.9f, d, speed, DamageCalculator.Enemy(Damage * damage, source), playerMask, this);
            }
        }

        /// <summary>마법진 예고 → 졸개 소환 (ChildSpawner가 없으면 아무것도 안 함)</summary>
        /// <param name="into">소환된 졸개를 넣을 목록 (졸개 상한을 세는 보스)</param>
        protected IEnumerator Summon(EnemyBase prefab, int count, float radius, float telegraph, float hpScale = 0.6f, List<EnemyBase> into = null, float attackScale = 1f)
        {
            if (prefab == null || count <= 0) yield break;
            Enter(State.Telegraph);
            body.linearVelocity = Vector2.zero;
            var points = new List<Vector2>(count);
            for (int i = 0; i < count; i++)
            {
                float a = (i + 0.5f) / count * Mathf.PI * 2f + bossTime;
                points.Add(Inside(Pos + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * radius));
            }

            var marks = new List<SpriteRenderer>();
            foreach (Vector2 p in points) marks.Add(Show(Telegraphs.Circle(p, 0.55f, Telegraphs.Summon)));
            yield return Wait(telegraph);
            foreach (SpriteRenderer m in marks) HideShown(m);
            if (ChildSpawner == null) yield break;
            foreach (Vector2 p in points)
            {
                EnemyBase spawned = ChildSpawner(prefab, p, MinionHp * hpScale, MinionAttack * attackScale);
                if (spawned != null) into?.Add(spawned);
            }
        }

        /// <summary>졸개 강도 = 그 구간·방의 일반 적 배수 (보스 체력 배수가 졸개에게 가지 않게). 판이 소환 전에 넣는다</summary>
        public float MinionHp { get; set; } = 1f;
        public float MinionAttack { get; set; } = 1f;

        /// <summary>벽 안쪽으로 (경기장·방 밖으로 나가지 않게)</summary>
        protected Vector2 Inside(Vector2 target)
        {
            Vector2 d = target - Pos;
            float dist = d.magnitude;
            if (dist < 0.01f) return target;
            RaycastHit2D wall = Physics2D.CircleCast(Pos, 0.5f, d / dist, dist, wallMask);
            return wall.collider != null ? wall.centroid - d / dist * 0.4f : target;
        }

        protected bool HitPlayerIn(Vector2 center, float radius, float damage, string source)
        {
            Collider2D hit = Physics2D.OverlapCircle(center, radius, playerMask);
            return hit != null && hit.TryGetComponent(out Health target) && target.TryTakeDamage(DamageCalculator.Enemy(Damage * damage, source));
        }

        protected SpriteRenderer Show(SpriteRenderer sr)
        {
            _shown.Add(sr);
            return sr;
        }

        protected void HideShown(SpriteRenderer sr)
        {
            Telegraphs.Hide(sr);
            _shown.Remove(sr);
        }

        private void HideAll()
        {
            foreach (SpriteRenderer sr in _shown) Telegraphs.Hide(sr);
            _shown.Clear();
        }

        protected override void OnDisable()
        {
            HideAll();
            base.OnDisable();
        }

        protected override void OnDied(Health h)
        {
            HideAll();
            _stack.Clear();
            if (openingRing != null) openingRing.enabled = false;
            if (midBoss) HitStop.Freeze(HitStop.EliteKill);
            else HitStop.OnBossKilled();
            CameraShake.Add(CameraShake.BossImpact);
            base.OnDied(h);
        }
    }
}
