using System;
using System.Collections.Generic;
using HistoryHeroes.Combat;
using HistoryHeroes.Common;
using HistoryHeroes.Enemy;
using HistoryHeroes.Progress;
using HistoryHeroes.Skills;
using HistoryHeroes.Stats;
using UnityEngine;

namespace HistoryHeroes.Pets
{
    /// <summary>
    /// 🦅 사냥매 — "멈춰서 버티기" (Pets 3장): 2초 멈추면 솟구쳤다가 화면 안 체력이 가장 높은 적에게 급강하
    /// (공격력 30% + 사냥 표식 5초: 그 적에게 기본 공격 치명타 확률 +25%). 재사용 6초, 움직이면 게이지가 처음부터.
    /// Lv5: 표식 대상이 죽으면 가장 가까운 적에게 옮겨 간다. Lv10: 4초 넘게 멈추면 한 번 더 급강하.
    /// </summary>
    public sealed class HawkBehaviour : IPetBehaviour
    {
        public const float StopNeed = 2f;
        public const float Cooldown = 6f;
        public const float DoubleAt = 4f;
        public const float Coefficient = 0.3f;
        public const float MarkDuration = 5f;
        public const float MarkCrit = 0.25f;
        private const float DiveSpeed = 16f;
        private const float ReturnSpeed = 11f;

        private enum Phase { Perch, Diving, Returning }

        private readonly PetController _pet;
        private readonly List<EnemyBase> _buffer = new List<EnemyBase>();
        private readonly Func<DamageInfo, DamageInfo> _markModifier;
        private Phase _phase;
        private float _gauge;
        private float _still;
        private bool _doubleDone;
        private float _cooldownUntil;
        private EnemyBase _target;
        private EnemyBase _marked;
        private float _markUntil;

        public HawkBehaviour(PetController pet)
        {
            _pet = pet;
            _markModifier = MarkModifier;
        }

        /// <summary>
        /// 급강하 게이지: 멈춰 있으면 채우고, 움직이면 0으로. 다 차고 재사용이 끝났으면 true를 돌려주고 비운다
        /// (그래서 다음 급강하는 "다시 2초" 멈춰야 한다).
        /// </summary>
        public static bool UpdateDiveGauge(ref float gauge, bool stopped, float deltaTime, bool cooldownReady, float need = StopNeed)
        {
            if (!stopped)
            {
                gauge = 0f;
                return false;
            }

            gauge += deltaTime;
            if (gauge < need || !cooldownReady) return false;
            gauge = 0f;
            return true;
        }

        public EnemyBase Marked => _marked;

        public void Tick(float dt)
        {
            bool stopped = _pet.Player.IsStopped;
            if (_marked != null && (Time.time >= _markUntil || _marked.Health.IsDead || !_marked.gameObject.activeInHierarchy)) Unmark();
            if (_marked != null) _pet.ShowMarker((Vector2)_marked.transform.position + Vector2.up * 0.75f, new Color(1f, 0.35f, 0.25f, 0.9f), 0.32f);

            switch (_phase)
            {
                case Phase.Perch:
                    if (!stopped)
                    {
                        _still = 0f;
                        _doubleDone = false;
                    }
                    else
                    {
                        _still += dt;
                    }

                    // 솟구침: 게이지만큼 높이 (게이지가 차오르는 게 보인다)
                    float g = Mathf.Clamp01(_gauge / StopNeed);
                    _pet.Follow(_pet.HeroPosition + Vector2.up * (0.55f + g * 1.3f) + Vector2.right * 0.3f, 10f, dt);
                    _pet.SetLook(1f, 1f + g * 0.35f);

                    bool dive = UpdateDiveGauge(ref _gauge, stopped, dt, Time.time >= _cooldownUntil);
                    if (dive)
                    {
                        _cooldownUntil = Time.time + Cooldown;
                        StartDive();
                    }
                    else if (_pet.AwakenedSecond && stopped && !_doubleDone && _still >= DoubleAt)
                    {
                        _doubleDone = true; // Lv10: 오래 버티면 한 번 더 (재사용과 상관없이)
                        StartDive();
                    }

                    break;

                case Phase.Diving:
                    if (_target == null || _target.Health.IsDead || !_target.gameObject.activeInHierarchy)
                    {
                        _phase = Phase.Returning;
                        break;
                    }

                    if (_pet.FlyTo(_target.transform.position, DiveSpeed, dt))
                    {
                        _target.Health.TryTakeDamage(_pet.Damage(Coefficient, "pet_hawk"));
                        AttackFx.Burst(_target.transform.position, 0.45f, new Color(1f, 0.75f, 0.35f, 0.8f));
                        if (!_target.Health.IsDead) Mark(_target, MarkDuration);
                        _phase = Phase.Returning;
                    }

                    break;

                case Phase.Returning:
                    _pet.SetLook(1f, 1f);
                    if (_pet.FlyTo(_pet.HeroPosition + Vector2.up * 0.55f + Vector2.right * 0.3f, ReturnSpeed, dt)) _phase = Phase.Perch;
                    break;
            }
        }

        private void StartDive()
        {
            _target = Strongest();
            if (_target != null) _phase = Phase.Diving;
        }

        /// <summary>화면 안에서 체력이 가장 높은 적 (보스·엘리트를 먼저 노린다)</summary>
        private EnemyBase Strongest()
        {
            CombatQuery.EnemiesOnScreen(_buffer);
            EnemyBase best = null;
            float hp = 0f;
            foreach (EnemyBase e in _buffer)
            {
                if (e.Health.Current > hp)
                {
                    hp = e.Health.Current;
                    best = e;
                }
            }

            return best;
        }

        private void Mark(EnemyBase enemy, float duration)
        {
            Unmark();
            _marked = enemy;
            _markUntil = Time.time + duration;
            enemy.Health.IncomingModifier = _markModifier;
            enemy.Killed += OnMarkedKilled;
        }

        private void Unmark()
        {
            if (_marked != null)
            {
                if (_marked.Health.IncomingModifier == _markModifier) _marked.Health.IncomingModifier = null;
                _marked.Killed -= OnMarkedKilled;
            }

            _marked = null;
            _pet.HideMarker();
        }

        /// <summary>Lv5: 표식 대상이 죽으면 남은 시간만큼 가장 가까운 적에게</summary>
        private void OnMarkedKilled(EnemyBase dead)
        {
            float left = _markUntil - Time.time;
            Vector2 at = dead.transform.position;
            Unmark();
            if (!_pet.AwakenedFirst || left <= 0f) return;
            EnemyBase next = CombatQuery.Nearest(at, 8f);
            if (next != null && next != dead) Mark(next, left);
        }

        /// <summary>사냥 표식: 이 적이 맞는 기본 공격이 치명타가 아니면 25% 확률로 치명타로 (영웅의 치명타 피해 배율)</summary>
        private DamageInfo MarkModifier(DamageInfo info)
        {
            if (info.Kind != DamageKind.BasicAttack || info.IsCrit) return info;
            if (UnityEngine.Random.value >= MarkCrit) return info;
            info.Amount *= Mathf.Max(1f, _pet.Player.Stats.Get(StatType.CritDamage));
            info.IsCrit = true;
            return info;
        }

        public void Stop() => Unmark();
    }

    /// <summary>
    /// 🐎 적토마 — "계속 움직이기" (Pets 3장): 1.5초 넘게 움직이면 질주 (이동속도 +15%, 앞의 적을 들이받아
    /// 공격력 10% + 밀치기, 적마다 1초 간격). 질주하다 멈추면 기세: 2초 동안 기본 공격 피해 +20% (4초에 한 번).
    /// Lv5: 질주한 길에 먼지 구름 (적 이동속도 -30%, 2초). Lv10: 기세 3초 + 기세 중 받는 피해 -15%.
    /// </summary>
    public sealed class RedHareBehaviour : IPetBehaviour
    {
        public const float MoveNeed = 1.5f;
        public const float SprintSpeed = 0.15f;
        public const float RamCoefficient = 0.1f;
        public const float RamInterval = 1f;
        public const float MomentumBonus = 0.2f;
        public const float MomentumCooldown = 4f;
        private const float RamRadius = 0.75f;
        private const float RamPush = 0.7f;
        private const float DustEvery = 0.2f;
        private const float DustLife = 2f;
        private const float DustRadius = 0.9f;
        /// <summary>들이받기는 초당 최대 3번 (무리 속·벽에 붙어서 계속 들이받으면 펫 피해가 10%를 넘는다 — Pets 2장)</summary>
        public const float RamsPerSecond = 3f;
        /// <summary>실제로 이만큼(칸/초) 넘게 움직여야 "움직이는 중" (벽에 막혀 제자리면 질주가 아니다)</summary>
        private const float MinSpeed = 1.2f;

        private readonly PetController _pet;
        private readonly object _sprintOwner = new object();
        private readonly object _momentumOwner = new object();
        private readonly Dictionary<EnemyBase, float> _lastRam = new Dictionary<EnemyBase, float>();
        private readonly Collider2D[] _hits = new Collider2D[16];
        private readonly List<Vector3> _dust = new List<Vector3>(); // x, y, 사라지는 시각
        private float _moving;
        private bool _sprinting;
        private float _momentumUntil;
        private float _momentumReadyAt;
        private float _nextDust;
        private float _nextSlow;
        private float _ramTokens = RamsPerSecond;
        private Vector2 _lastHero;
        private float _speed;

        public RedHareBehaviour(PetController pet)
        {
            _pet = pet;
            _lastHero = pet.HeroPosition;
        }

        public bool Sprinting => _sprinting;
        public bool MomentumActive => _momentumUntil > Time.time;

        public void Tick(float dt)
        {
            Vector2 now = _pet.HeroPosition;
            float instant = (now - _lastHero).magnitude / Mathf.Max(0.0001f, dt);
            _lastHero = now;
            _speed = Mathf.Lerp(_speed, instant, 1f - Mathf.Exp(-10f * dt));
            _ramTokens = Mathf.Min(RamsPerSecond, _ramTokens + RamsPerSecond * dt);

            bool moving = !_pet.Player.IsStopped && _speed > MinSpeed;
            if (moving)
            {
                _moving += dt;
                if (!_sprinting && _moving >= MoveNeed) StartSprint();
            }
            else
            {
                if (_sprinting)
                {
                    EndSprint();
                    TryMomentum();
                }

                _moving = 0f;
            }

            if (_momentumUntil > 0f && Time.time >= _momentumUntil) EndMomentum();

            Vector2 hero = _pet.HeroPosition;
            if (_sprinting)
            {
                Ram(hero);
                if (_pet.AwakenedFirst) Dust(hero);
                Vector2 dir = _pet.HeroDirection;
                _pet.Follow(hero + new Vector2(dir.y, -dir.x) * 0.55f + dir * 0.15f, 20f, dt); // 옆에서 나란히 질주
                _pet.SetLook(1f, 1.1f + Mathf.Sin(Time.time * 18f) * 0.05f);
            }
            else
            {
                _pet.Follow(_pet.RestPoint(), 8f, dt);
                _pet.SetLook(1f, MomentumActive ? 1.15f : 1f);
            }

            TickDust();
        }

        private void StartSprint()
        {
            _sprinting = true;
            _pet.Player.Stats.Add(new StatModifier(StatType.MoveSpeed, ModifierKind.Percent, SprintSpeed, ModifierSource.Pet, _sprintOwner));
        }

        private void EndSprint()
        {
            _sprinting = false;
            _pet.Player.Stats.RemoveAllFrom(_sprintOwner);
        }

        /// <summary>달려와서 멈추고 몰아친다</summary>
        private void TryMomentum()
        {
            if (Time.time < _momentumReadyAt) return;
            float duration = _pet.AwakenedSecond ? 3f : 2f;
            _momentumUntil = Time.time + duration;
            _momentumReadyAt = Time.time + MomentumCooldown;
            _pet.Player.Stats.RemoveAllFrom(_momentumOwner);
            _pet.Player.Stats.Add(new StatModifier(StatType.Attack, ModifierKind.Percent, MomentumBonus, ModifierSource.Pet, _momentumOwner, StatTarget.BasicAttack));
            if (_pet.AwakenedSecond) _pet.Player.Stats.Add(new StatModifier(StatType.DamageReduction, ModifierKind.Flat, 0.15f, ModifierSource.Pet, _momentumOwner));
            AttackFx.Burst(_pet.HeroPosition, 0.9f, new Color(1f, 0.45f, 0.2f, 0.55f), 0.25f);
        }

        private void EndMomentum()
        {
            _momentumUntil = 0f;
            _pet.Player.Stats.RemoveAllFrom(_momentumOwner);
        }

        private void Ram(Vector2 hero)
        {
            Vector2 dir = _pet.HeroDirection;
            int count = Physics2D.OverlapCircleNonAlloc(hero + dir * 0.5f, RamRadius, _hits, _pet.EnemyMask);
            for (int i = 0; i < count && _ramTokens >= 1f; i++)
            {
                if (!_hits[i].TryGetComponent(out EnemyBase enemy) || enemy.Health.IsDead) continue;
                if (_lastRam.TryGetValue(enemy, out float last) && Time.time - last < RamInterval) continue;
                _lastRam[enemy] = Time.time;
                _ramTokens -= 1f;
                enemy.Health.TryTakeDamage(_pet.Damage(RamCoefficient, "pet_redhare"));
                if (enemy.CanBeKnockedBack) enemy.Push(dir * RamPush);
                AttackFx.Burst(enemy.transform.position, 0.35f, new Color(0.9f, 0.35f, 0.2f, 0.7f), 0.12f);
            }
        }

        /// <summary>Lv5: 지나간 길에 먼지 구름</summary>
        private void Dust(Vector2 hero)
        {
            if (Time.time < _nextDust) return;
            _nextDust = Time.time + DustEvery;
            _dust.Add(new Vector3(hero.x, hero.y, Time.time + DustLife));
            AttackFx.Burst(hero, DustRadius * 0.7f, new Color(0.65f, 0.55f, 0.4f, 0.35f), DustLife);
        }

        private void TickDust()
        {
            _dust.RemoveAll(d => d.z <= Time.time);
            if (_dust.Count == 0 || Time.time < _nextSlow) return;
            _nextSlow = Time.time + 0.25f;
            foreach (Vector3 d in _dust)
            {
                int count = Physics2D.OverlapCircleNonAlloc(d, DustRadius, _hits, _pet.EnemyMask);
                for (int i = 0; i < count; i++)
                {
                    if (_hits[i].TryGetComponent(out EnemyBase enemy)) enemy.Status.ApplySlow(0.3f, 2f);
                }
            }
        }

        public void Stop()
        {
            EndSprint();
            EndMomentum();
            _dust.Clear();
            _lastRam.Clear();
        }
    }

    /// <summary>
    /// 🐴 마렝고 — "위험한 순간" (Pets 3장): 적 투사체가 영웅에게 닿기 0.3초 전에 막아서서 1발을 몸으로 받는다.
    /// 재사용 7초 (체력 30% 아래면 4초). Lv5: 막은 투사체를 쏜 적에게 되돌려 보낸다 (그 피해 100%). Lv10: 한 번에 2발까지.
    /// 근접 공격(돌진)은 못 막는다.
    /// </summary>
    public sealed class MarengoBehaviour : IPetBehaviour
    {
        public const float Cooldown = 7f;
        public const float LowHpCooldown = 4f;
        public const float LowHp = 0.3f;
        public const float Warning = 0.3f;
        private const float BlockRadius = 0.7f;
        private const float SecondWindow = 0.5f;

        private readonly PetController _pet;
        private float _readyAt;
        private float _windowUntil;
        private int _blocksLeft;
        private float _jumpUntil;

        public MarengoBehaviour(PetController pet) => _pet = pet;

        public static float CooldownFor(float hpRatio) => hpRatio < LowHp ? LowHpCooldown : Cooldown;

        public bool Ready => Time.time >= _readyAt;
        public int Blocked { get; private set; }

        public void Tick(float dt)
        {
            Vector2 hero = _pet.HeroPosition;
            bool open = Ready || (Time.time < _windowUntil && _blocksLeft > 0);
            if (open) TryBlock(hero);

            if (Time.time >= _jumpUntil) _pet.Follow(_pet.RestPoint(0.6f, 0.5f), 9f, dt);
            _pet.SetLook(Ready ? 1f : 0.45f, 1f); // 재사용 중에는 흐리게 (남은 시간이 보이게)
        }

        private void TryBlock(Vector2 hero)
        {
            IReadOnlyList<Projectile> arrows = EnemyProjectiles.Active;
            for (int i = arrows.Count - 1; i >= 0; i--)
            {
                Projectile p = arrows[i];
                if (p == null) continue;
                Vector2 toHero = hero - (Vector2)p.transform.position;
                float along = Vector2.Dot(toHero, p.Direction);
                if (along <= 0f) continue;
                if ((toHero - p.Direction * along).magnitude > BlockRadius) continue;
                if (along / Mathf.Max(0.01f, p.Speed) > Warning) continue;
                Block(p);
                if (Time.time >= _windowUntil || _blocksLeft <= 0) return;
            }
        }

        private void Block(Projectile p)
        {
            Vector2 at = (Vector2)p.transform.position + p.Direction * 0.2f;
            DamageInfo damage = p.Damage;
            Component owner = p.Owner;
            p.Cancel();
            Blocked++;

            _pet.Position = at; // 막아서기 (몸으로 받는다)
            _jumpUntil = Time.time + 0.25f;
            AttackFx.Burst(at, 0.45f, new Color(0.85f, 0.85f, 0.9f, 0.85f), 0.15f);

            // Lv5: 쏜 적에게 되돌려 보낸다
            if (_pet.AwakenedFirst && owner != null && owner.TryGetComponent(out EnemyBase shooter) && !shooter.Health.IsDead && shooter.gameObject.activeInHierarchy)
            {
                shooter.Health.TryTakeDamage(new DamageInfo { Amount = damage.Amount, Kind = DamageKind.Active, AttackerTeam = Team.Player, SourceId = "pet_marengo" });
                AttackFx.Line(at, shooter.transform.position, 0.1f, new Color(0.85f, 0.85f, 0.95f, 0.8f), 0.15f);
            }

            if (Ready)
            {
                // 이번 막기 시작: 재사용을 걸고, Lv10이면 잠깐 동안 1발 더
                Health hp = _pet.Player.Health;
                _readyAt = Time.time + CooldownFor(hp.Current / Mathf.Max(1f, hp.Max));
                _blocksLeft = _pet.AwakenedSecond ? 1 : 0;
                _windowUntil = Time.time + SecondWindow;
            }
            else
            {
                _blocksLeft--;
            }
        }

        public void Stop()
        {
        }
    }

    /// <summary>
    /// 🕊️ 흰 비둘기 — "돌아다니기" (Pets 3장): 자석 범위 밖에 떨어진 경험치 보석을 하나씩 날아가 물어 온다 (경험치 양은 같다).
    /// 던전 방마다 숨은 보물 1개를 찾아 표시한다 (골드, 10%는 체력 회복 열매) → 영웅이 가서 줍는다.
    /// 영웅이 멈춰 있으면 가까운 적 1명에게 약한 전기 (공격력 15%, 2초마다).
    /// Lv5: 보물 골드 5% 확률로 2배. Lv10: 회복 열매 25%, 서바이벌에서 30초마다 화면 보석 전부 흡수.
    /// </summary>
    public sealed class DoveBehaviour : IPetBehaviour
    {
        public const float ZapInterval = 2f;
        public const float ZapCoefficient = 0.15f;
        public const float FruitChance = 0.1f;
        public const float FruitChanceAwakened = 0.25f;
        public const float FruitHeal = 0.15f;
        private const float FetchRange = 7f;
        private const float MagnetRadius = 2.4f;
        private const float FlySpeed = 9f;
        private const float ZapRange = 5f;
        private const float MagnetEvery = 30f;

        private enum Phase { Idle, ToGem, ToTreasure, Returning }

        private readonly PetController _pet;
        private readonly Dungeon.DungeonRun _dungeon;
        private Phase _phase;
        private ExperienceGem _gem;
        private float _zapTimer = ZapInterval;
        private float _magnetTimer;
        private float _orbit;
        private bool _treasurePending;
        private bool _treasureShown;
        private Vector2 _treasure;

        public DoveBehaviour(PetController pet)
        {
            _pet = pet;
            _dungeon = UnityEngine.Object.FindAnyObjectByType<Dungeon.DungeonRun>();
            if (_dungeon != null) _dungeon.RoomEntered += OnRoomEntered;
        }

        public int Fetched { get; private set; }

        /// <summary>보물 한 개: 열매(회복)인가, 골드면 얼마인가 (roll·doubleRoll = 0~1 무작위 값)</summary>
        public static (bool fruit, int gold) RollTreasure(int chapter, bool awakenedFirst, bool awakenedSecond, float roll, float doubleRoll, bool practice = false)
        {
            float fruitChance = awakenedSecond ? FruitChanceAwakened : FruitChance;
            if (roll < fruitChance) return (true, 0);
            int gold = 10 + 5 * Mathf.Max(1, chapter);
            if (awakenedFirst && doubleRoll < 0.05f) gold *= 2;
            return (false, practice ? 0 : gold);
        }

        private void OnRoomEntered(int room)
        {
            // 방마다 숨은 보물 1개: 플레이어와 떨어진 자리
            Rect area = _dungeon.SpawnArea;
            _treasure = new Vector2(UnityEngine.Random.Range(area.xMin, area.xMax), UnityEngine.Random.Range(area.yMin, area.yMax));
            _treasurePending = true;
            _treasureShown = false;
            _pet.HideMarker();
        }

        public void Tick(float dt)
        {
            Vector2 hero = _pet.HeroPosition;
            Zap(hero, dt);
            if (_pet.AwakenedSecond && GameSession.Mode == BattleMode.Survival && _pet.Experience != null)
            {
                _magnetTimer += dt;
                if (_magnetTimer >= MagnetEvery)
                {
                    _magnetTimer = 0f;
                    _pet.Experience.AttractAll();
                    AttackFx.Burst(hero, 1.6f, new Color(0.7f, 1f, 1f, 0.4f), 0.3f);
                }
            }

            if (_treasureShown && (hero - _treasure).sqrMagnitude < 0.5f) Collect();

            switch (_phase)
            {
                case Phase.Idle:
                    _orbit += dt * 2.2f;
                    _pet.Follow(hero + new Vector2(Mathf.Cos(_orbit), Mathf.Sin(_orbit)) * 0.9f + Vector2.up * 0.3f, 6f, dt);
                    _gem = FarGem(hero);
                    if (_gem != null) _phase = Phase.ToGem;
                    else if (_treasurePending) _phase = Phase.ToTreasure;
                    break;

                case Phase.ToGem:
                    if (_gem == null || !_gem.gameObject.activeInHierarchy)
                    {
                        _phase = Phase.Returning;
                        break;
                    }

                    if (_pet.FlyTo(_gem.transform.position, FlySpeed, dt))
                    {
                        _gem.Attract(); // 물어 온다 (영웅에게 날아간다)
                        Fetched++;
                        _phase = Phase.Returning;
                    }

                    break;

                case Phase.ToTreasure:
                    if (_pet.FlyTo(_treasure, FlySpeed, dt))
                    {
                        _treasurePending = false;
                        _treasureShown = true;
                        _pet.ShowMarker(_treasure, new Color(1f, 0.9f, 0.35f, 0.95f), 0.45f); // 반짝이는 보물 자리
                        _phase = Phase.Returning;
                    }

                    break;

                case Phase.Returning:
                    if (_pet.FlyTo(hero + Vector2.up * 0.6f, FlySpeed, dt)) _phase = Phase.Idle;
                    break;
            }
        }

        /// <summary>자석 범위 밖, 7칸 안의 가장 가까운 보석</summary>
        private ExperienceGem FarGem(Vector2 hero)
        {
            if (_pet.Experience == null) return null;
            ExperienceGem best = null;
            float bestSqr = FetchRange * FetchRange;
            IReadOnlyList<ExperienceGem> gems = _pet.Experience.Gems;
            for (int i = 0; i < gems.Count; i++)
            {
                ExperienceGem g = gems[i];
                float sqr = ((Vector2)g.transform.position - hero).sqrMagnitude;
                if (sqr < MagnetRadius * MagnetRadius || sqr >= bestSqr) continue;
                bestSqr = sqr;
                best = g;
            }

            return best;
        }

        private void Zap(Vector2 hero, float dt)
        {
            if (!_pet.Player.IsStopped) return;
            _zapTimer -= dt;
            if (_zapTimer > 0f) return;
            EnemyBase target = CombatQuery.Nearest(hero, ZapRange);
            if (target == null)
            {
                _zapTimer = 0.3f;
                return;
            }

            _zapTimer = ZapInterval;
            target.Health.TryTakeDamage(_pet.Damage(ZapCoefficient, "pet_dove"));
            AttackFx.Line(_pet.Position, target.transform.position, 0.08f, new Color(0.6f, 0.95f, 1f, 0.9f), 0.12f);
        }

        private void Collect()
        {
            _treasureShown = false;
            _pet.HideMarker();
            (bool fruit, int gold) = RollTreasure(GameSession.Chapter, _pet.AwakenedFirst, _pet.AwakenedSecond, UnityEngine.Random.value, UnityEngine.Random.value, GameSession.Practice);
            UI.HudView hud = UnityEngine.Object.FindAnyObjectByType<UI.HudView>();
            if (fruit)
            {
                _pet.Player.Health.Heal(_pet.Player.Health.Max * FruitHeal);
                if (hud != null) hud.ShowNotice("흰 비둘기: 회복 열매 (체력 +15%)");
            }
            else if (gold > 0)
            {
                Economy.Wallet.Grant(Records.SaveSystem.Data, new Economy.Reward { Gold = gold });
                Records.SaveSystem.MarkDirty(); // 앱이 바로 꺼져도 다음 안전한 저장에서 남는다 (Codex v0.5 5번)
                if (hud != null) hud.ShowNotice($"흰 비둘기: 보물 골드 +{gold}");
            }
            else if (hud != null) hud.ShowNotice("흰 비둘기: 연습은 골드 보상이 없어요");

            AttackFx.Burst(_treasure, 0.6f, new Color(1f, 0.9f, 0.35f, 0.8f), 0.2f);
        }

        public void Stop()
        {
            if (_dungeon != null) _dungeon.RoomEntered -= OnRoomEntered;
            _pet.HideMarker();
        }
    }
}
