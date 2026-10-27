using System;
using System.Collections.Generic;
using HistoryHeroes.Combat;
using HistoryHeroes.Common;
using UnityEngine;

namespace HistoryHeroes.Enemy
{
    /// <summary>엘리트 속성 8개 (확장 1차 4장). 여러 개를 함께 가질 수 있다</summary>
    [Flags]
    public enum EliteAffix
    {
        None = 0,
        Enraged = 1 << 0,   // 분노한: 체력 50% 아래에서 이동·공격 +30%
        Shielded = 1 << 1,  // 보호막: 체력 30% 보호막, 깨지면 2초 기절
        Summoning = 1 << 2, // 부르는: 10초마다 졸개 2 (동시에 4까지)
        Burning = 1 << 3,   // 불타는: 지나간 자리에 불 2초
        Splitting = 1 << 4, // 갈라지는: 죽으면 작은 적 2 (체력 25%, 속성 없음)
        Blinking = 1 << 5,  // 깜빡이는: 멈춰 쏘기 2초 뒤 옆으로 순간이동, 그 뒤 1.2초 제자리
        Volley = 1 << 6,    // 연사하는: 원거리 공격 3갈래
        Guardian = 1 << 7,  // 지키는: 반경 4 안 다른 적 받는 피해 -30%
    }

    /// <summary>
    /// 엘리트 속성 (확장 1차 4장 · 검토 반영 7·9번). 엘리트에게만 붙는다.
    /// - 1구간 0개 · 2~3구간 1개 · 4~5구간 1개(40%로 2개) · 무한의 균열 = 5층마다 1개 (최대 2)
    /// - 어떤 속성도 빈틈을 없애지 않는다: 깜빡임 뒤 1.2초 제자리, 보호막이 깨지면 2초 기절
    /// - 갈라진 작은 적·부른 졸개는 방을 끝내려면 잡아야 하고, 확정 레벨업은 원래 엘리트 1번만
    /// </summary>
    public sealed class EliteAffixes : MonoBehaviour
    {
        public static readonly EliteAffix[] All =
        {
            EliteAffix.Enraged, EliteAffix.Shielded, EliteAffix.Summoning, EliteAffix.Burning,
            EliteAffix.Splitting, EliteAffix.Blinking, EliteAffix.Volley, EliteAffix.Guardian,
        };

        private static readonly List<EliteAffixes> Guardians = new List<EliteAffixes>();

        /// <summary>부르는 속성이 내는 졸개 (판이 정한다)</summary>
        public static EnemyBase MinionPrefab;

        private EnemyBase _enemy;
        private EnemyBase _prefab;
        private ParticleSystem _aura;
        private bool _hooked;
        private bool _enraged;
        private float _nextSummon;
        private readonly List<EnemyBase> _summoned = new List<EnemyBase>();
        // 불타는
        private readonly List<Vector3> _trail = new List<Vector3>(); // x, y = 자리, z = 생긴 때
        private Vector2 _lastTrail;
        private float _nextBurnTick;
        // 깜빡이는
        private float _stoppedFor;
        private float _blinkAt = -1f;
        private float _blinkReady;

        public EliteAffix Affixes { get; private set; }
        public bool Has(EliteAffix a) => (Affixes & a) != 0;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Guardians.Clear();
            MinionPrefab = null;
            Health.EnemyIncoming = null;
        }

        /// <summary>몇 개 붙일까 (확장 1차 4장). riftFloor > 0 이면 무한의 균열 층 기준</summary>
        public static int CountFor(int chapter, int riftFloor, System.Random random)
        {
            if (riftFloor > 0) return Mathf.Clamp(riftFloor / 5, 0, 2);
            if (chapter <= 1) return 0;
            if (chapter <= 3) return 1;
            return random.NextDouble() < 0.4 ? 2 : 1;
        }

        /// <summary>서로 다른 속성 count개</summary>
        public static EliteAffix Roll(System.Random random, int count)
        {
            var pool = new List<EliteAffix>(All);
            EliteAffix result = EliteAffix.None;
            for (int i = 0; i < count && pool.Count > 0; i++)
            {
                int k = random.Next(pool.Count);
                result |= pool[k];
                pool.RemoveAt(k);
            }

            return result;
        }

        /// <summary>이름 앞말 ("분노한 · 보호막")</summary>
        public static string Names(EliteAffix affixes)
        {
            var names = new List<string>();
            foreach (EliteAffix a in All)
            {
                if ((affixes & a) != 0) names.Add(NameOf(a));
            }

            return string.Join(" · ", names);
        }

        public static string NameOf(EliteAffix a)
        {
            switch (a)
            {
                case EliteAffix.Enraged: return "분노한";
                case EliteAffix.Shielded: return "보호막";
                case EliteAffix.Summoning: return "부르는";
                case EliteAffix.Burning: return "불타는";
                case EliteAffix.Splitting: return "갈라지는";
                case EliteAffix.Blinking: return "깜빡이는";
                case EliteAffix.Volley: return "연사하는";
                case EliteAffix.Guardian: return "지키는";
                default: return string.Empty;
            }
        }

        /// <summary>속성마다 보이는 색 (발밑 빛 알갱이)</summary>
        public static Color ColorOf(EliteAffix a)
        {
            switch (a)
            {
                case EliteAffix.Enraged: return new Color(1f, 0.25f, 0.2f);
                case EliteAffix.Shielded: return new Color(0.35f, 0.65f, 1f);
                case EliteAffix.Summoning: return new Color(0.7f, 0.35f, 1f);
                case EliteAffix.Burning: return new Color(1f, 0.55f, 0.15f);
                case EliteAffix.Splitting: return new Color(0.35f, 1f, 0.5f);
                case EliteAffix.Blinking: return new Color(0.55f, 0.9f, 1f);
                case EliteAffix.Volley: return new Color(1f, 0.9f, 0.3f);
                case EliteAffix.Guardian: return new Color(1f, 0.8f, 0.35f);
                default: return Color.white;
            }
        }

        /// <param name="prefab">갈라지는 속성이 같은 적을 내려고 쓴다</param>
        public void Apply(EliteAffix affixes, EnemyBase prefab)
        {
            _enemy = GetComponent<EnemyBase>();
            _prefab = prefab;
            Affixes = affixes;
            _enraged = false;
            _summoned.Clear();
            _trail.Clear();
            _stoppedFor = 0f;
            _blinkAt = -1f;
            _blinkReady = Time.time + 3f;
            _nextSummon = Time.time + 4f;
            _lastTrail = transform.position;
            if (!_hooked)
            {
                _hooked = true;
                _enemy.Killed += OnKilled;
                _enemy.Health.ShieldBroken += OnShieldBroken;
            }

            if (Has(EliteAffix.Shielded)) _enemy.Health.SetShieldAtLeast(_enemy.Health.Max * 0.3f);
            Guardians.Remove(this);
            if (Has(EliteAffix.Guardian)) Guardians.Add(this);
            Health.EnemyIncoming = GuardianReduce;
            ShowAura();
        }

        private void ShowAura()
        {
            EliteAffix first = EliteAffix.None;
            foreach (EliteAffix a in All)
            {
                if (Has(a))
                {
                    first = a;
                    break;
                }
            }

            if (first == EliteAffix.None)
            {
                if (_aura != null) _aura.gameObject.SetActive(false);
                return;
            }

            // 불타는 속성은 자리에 남는 불씨가 길게 (지나간 자리 = 위험)
            if (_aura == null) _aura = Skills.SkillArt.Embers(transform, ColorOf(first), 14f, 0.9f, 0.4f);
            if (_aura == null) return;
            _aura.gameObject.SetActive(true);
            ParticleSystem.MainModule main = _aura.main;
            main.startColor = ColorOf(first);
            main.startLifetime = Has(EliteAffix.Burning) ? new ParticleSystem.MinMaxCurve(1.6f, 2f) : new ParticleSystem.MinMaxCurve(0.6f, 1f);
            ParticleSystem.EmissionModule emission = _aura.emission;
            emission.rateOverTime = Has(EliteAffix.Burning) ? 30f : 14f;
            _aura.Play();
        }

        private void OnDisable()
        {
            Guardians.Remove(this);
            Affixes = EliteAffix.None;
            if (_aura != null) _aura.gameObject.SetActive(false);
        }

        private void Update()
        {
            if (Affixes == EliteAffix.None || _enemy == null || _enemy.Health.IsDead || GamePause.IsPaused) return;
            float now = Time.time;

            if (Has(EliteAffix.Enraged) && !_enraged && _enemy.Health.Current < _enemy.Health.Max * 0.5f)
            {
                _enraged = true;
                _enemy.Status.Haste = 1.3f;
                FxBursts.Puff(transform.position + Vector3.back * 0.8f, new Color(1f, 0.3f, 0.25f), 1.4f);
            }

            if (Has(EliteAffix.Summoning) && now >= _nextSummon) Summon(now);
            if (Has(EliteAffix.Burning)) Burn(now);
            if (Has(EliteAffix.Blinking)) Blink(now);
        }

        private void Summon(float now)
        {
            _nextSummon = now + 10f;
            _summoned.RemoveAll(e => e == null || !e.isActiveAndEnabled || e.Health.IsDead);
            if (MinionPrefab == null || EnemyBase.ChildSpawner == null) return;
            for (int i = 0; i < 2 && _summoned.Count < 4; i++)
            {
                Vector2 at = (Vector2)transform.position + UnityEngine.Random.insideUnitCircle.normalized * 1.3f;
                EnemyBase minion = EnemyBase.ChildSpawner(MinionPrefab, at, _enemy.HpMultiplierUsed, _enemy.AttackMultiplierUsed / 1.5f);
                if (minion != null) _summoned.Add(minion);
            }

            FxBursts.Puff(transform.position + Vector3.back * 0.3f, new Color(0.7f, 0.35f, 1f), 1.6f);
        }

        private void Burn(float now)
        {
            Vector2 p = transform.position;
            if ((p - _lastTrail).sqrMagnitude > 0.09f)
            {
                _trail.Add(new Vector3(p.x, p.y, now));
                _lastTrail = p;
            }

            _trail.RemoveAll(t => now - t.z > 2f);
            if (now < _nextBurnTick || _enemy.Target == null) return;
            Vector2 hero = _enemy.Target.position;
            foreach (Vector3 t in _trail)
            {
                if (((Vector2)t - hero).sqrMagnitude > 0.45f * 0.45f) continue;
                _nextBurnTick = now + 0.5f;
                if (_enemy.Target.TryGetComponent(out Player.PlayerController hit))
                {
                    hit.Health.TryTakeDamage(new DamageInfo { Amount = _enemy.Damage * 0.35f, Kind = DamageKind.Enemy, AttackerTeam = Team.Enemy, SourceId = "elite_burning" });
                }

                break;
            }
        }

        private void Blink(float now)
        {
            if (_enemy.Target == null || !_enemy.Target.TryGetComponent(out Player.PlayerController hero)) return;
            if (_blinkAt > 0f)
            {
                if (now < _blinkAt) return;
                _blinkAt = -1f;
                // 영웅을 잇는 선에 수직으로 3칸 (벽이면 반대쪽, 둘 다 막히면 그대로)
                Vector2 me = transform.position;
                Vector2 to = ((Vector2)_enemy.Target.position - me).normalized;
                var side = new Vector2(-to.y, to.x);
                int walls = LayerMask.GetMask("Wall");
                float first = UnityEngine.Random.value < 0.5f ? 1f : -1f;
                foreach (float dir in new[] { first, -first })
                {
                    Vector2 target = me + side * (3f * dir);
                    if (Physics2D.OverlapCircle(target, 0.6f, walls) != null) continue;
                    FxBursts.Puff(transform.position + Vector3.back * 0.6f, new Color(0.55f, 0.9f, 1f), 1.1f);
                    if (TryGetComponent(out Rigidbody2D body)) body.position = target;
                    transform.position = new Vector3(target.x, target.y, transform.position.z);
                    FxBursts.Puff(new Vector3(target.x, target.y, -0.6f), new Color(0.55f, 0.9f, 1f), 1.1f);
                    _enemy.Status.ApplyFreeze(1.2f); // 다시 조준할 빈틈 (검토 반영 9번)
                    break;
                }

                _blinkReady = now + 6f;
                _stoppedFor = 0f;
                return;
            }

            _stoppedFor = hero.IsStopped ? _stoppedFor + Time.deltaTime : 0f;
            if (_stoppedFor >= 2f && now >= _blinkReady)
            {
                _blinkAt = now + 0.5f; // 0.5초 전 반짝 (예고)
                FxBursts.Puff(transform.position + Vector3.back * 0.9f, new Color(0.7f, 0.95f, 1f), 0.8f);
            }
        }

        private void OnShieldBroken()
        {
            if (!Has(EliteAffix.Shielded) || _enemy == null) return;
            _enemy.Status.ApplyFreeze(2f); // 보호막이 깨지면 2초 기절 = 빈틈
            FxBursts.Explosion(transform.position, 0.9f, new Color(0.45f, 0.7f, 1f));
        }

        private void OnKilled(EnemyBase enemy)
        {
            if (!Has(EliteAffix.Splitting) || _prefab == null || EnemyBase.ChildSpawner == null) return;
            Vector2 at = transform.position;
            float hp = enemy.HpMultiplierUsed * 5f * 0.25f; // 엘리트 체력(×5)의 25%
            for (int i = 0; i < 2; i++)
            {
                var offset = new Vector2(i == 0 ? -0.8f : 0.8f, 0.2f);
                EnemyBase child = EnemyBase.ChildSpawner(_prefab, at + offset, hp, enemy.AttackMultiplierUsed / 1.5f);
                if (child != null) child.transform.localScale *= 1.15f;
            }

            FxBursts.Puff(transform.position + Vector3.back * 0.5f, new Color(0.35f, 1f, 0.5f), 1.5f);
        }

        /// <summary>원거리 공격 3갈래 (EnemyProjectiles가 부른다)</summary>
        public static bool IsVolley(Component owner) => owner != null && owner.TryGetComponent(out EliteAffixes a) && a.Has(EliteAffix.Volley);

        /// <summary>지키는 속성: 반경 4 안 다른 적 받는 피해 -30%</summary>
        private static DamageInfo GuardianReduce(Health target, DamageInfo info)
        {
            if (Guardians.Count == 0) return info;
            Vector2 p = target.transform.position;
            foreach (EliteAffixes g in Guardians)
            {
                if (g == null || g._enemy == null || g._enemy.Health == target || g._enemy.Health.IsDead) continue;
                if (((Vector2)g.transform.position - p).sqrMagnitude > 16f) continue;
                info.Amount *= 0.7f;
                break;
            }

            return info;
        }
    }
}
