using System.Collections.Generic;
using HistoryHeroes.Combat;
using HistoryHeroes.Enemy;
using HistoryHeroes.Player;
using HistoryHeroes.Stats;
using UnityEngine;

namespace HistoryHeroes.Dungeon
{
    /// <summary>
    /// 구간 함정 (Modes 2-5). 모두 예고가 있고, 적에게도 효과가 있다 (적을 함정 쪽으로 유인하는 플레이).
    /// 상자 모양 구역 하나. 종류는 구간이 정하고(같은 레이아웃이 구간마다 다른 함정), 3·4구간은 자리마다 두 가지를 번갈아 둔다.
    /// 플레이어 피해는 적 피해 종류(Enemy), 적에게 주는 피해도 Enemy 종류라 기본 공격 비중·미션 처치로 세지 않는다.
    /// </summary>
    public sealed class TrapZone : MonoBehaviour
    {
        public enum Effect
        {
            Thorns,
            FireVent,
            Conveyor,
            Steam,
            Rockfall,
            Vines,
            Oblivion,
        }

        [SerializeField] private SpriteRenderer area;

        private Effect _effect;
        private Vector2 _size;
        private float _angle;
        private float _damage;
        private float _clock;
        private float _phaseOffset;
        private readonly object _slowOwner = new object();
        private readonly object _rootOwner = new object();
        private readonly Collider2D[] _hits = new Collider2D[24];
        private readonly Dictionary<Health, float> _rootCooldown = new Dictionary<Health, float>();
        private PlayerController _slowed;
        private PlayerController _rooted;
        private float _rootUntil;
        private float _nextTick;
        private SpriteRenderer _marker;
        private ConveyorArrows _arrows;
        private Sprite _placeholder;
        private Vector2 _rockAt;
        private bool _rockPending;
        private static int _mask = -1;

        public Effect Kind => _effect;

        /// <summary>3구간 레버 (확장 1차 2장): 이때까지 컨베이어는 반대로, 증기는 멈춘다</summary>
        private float _disruptUntil;
        public bool Disrupted => Time.time < _disruptUntil;

        /// <summary>영웅만 맞는다 (기믹 보스의 무너지는 바닥: 보스·수정 기둥은 다치지 않게)</summary>
        public bool HeroOnly { get; set; }
        private static int _heroMask = -1;
        private int Mask => HeroOnly ? (_heroMask >= 0 ? _heroMask : (_heroMask = LayerMask.GetMask("Player"))) : _mask;

        public void Disrupt(float seconds) => _disruptUntil = Mathf.Max(_disruptUntil, Time.time + seconds);

        public void SetArea(SpriteRenderer sr) => area = sr;

        /// <summary>함정 종류 + 자리 번호 → 실제 효과 (3구간: 컨베이어·증기, 4구간: 낙석·덩굴 번갈아)</summary>
        public static Effect EffectFor(TrapKind kind, int index)
        {
            switch (kind)
            {
                case TrapKind.Thorns: return Effect.Thorns;
                case TrapKind.FireVent: return Effect.FireVent;
                case TrapKind.Conveyor: return index % 2 == 0 ? Effect.Conveyor : Effect.Steam;
                case TrapKind.Rockfall: return index % 2 == 0 ? Effect.Rockfall : Effect.Vines;
                default: return Effect.Oblivion;
            }
        }

        public void Setup(Effect effect, Vector2 center, Vector2 size, float angle, float damage, float phaseOffset)
        {
            _effect = effect;
            _size = size;
            _angle = angle;
            _damage = damage;
            _clock = 0f;
            _phaseOffset = phaseOffset;
            _rockPending = false;
            _disruptUntil = 0f;
            _rootCooldown.Clear();
            transform.position = center;
            transform.rotation = Quaternion.Euler(0f, 0f, angle);
            if (area != null)
            {
                area.transform.localScale = new Vector3(size.x, size.y, 1f);
                area.color = BaseColor(effect);
                // 2D 그림 시험판: 함정마다 코덱스 그림 (색은 그대로 곱해져 예고 · 깜빡임이 보인다). 컨베이어는 띠 그림을 이어 붙인다
                Sprite art2D = Common.Art2D.Enabled ? Art2DFor(effect) : null;
                if (_placeholder == null) _placeholder = area.sprite;
                area.drawMode = SpriteDrawMode.Simple;
                if (art2D != null)
                {
                    area.sprite = art2D;
                    Vector2 b = art2D.bounds.size;
                    if (effect == Effect.Conveyor)
                    {
                        float k = size.y / Mathf.Max(0.01f, b.y);
                        area.transform.localScale = new Vector3(k, k, 1f);
                        area.drawMode = SpriteDrawMode.Tiled;
                        area.size = new Vector2(size.x / k, b.y);
                    }
                    else
                    {
                        area.transform.localScale = new Vector3(size.x / Mathf.Max(0.01f, b.x), size.y / Mathf.Max(0.01f, b.y), 1f);
                    }

                    area.color = Color.white;
                }
                else if (_placeholder != null)
                {
                    area.sprite = _placeholder; // 풀에서 다른 함정으로 다시 쓰일 때
                }
            }

            // 컨베이어: 미는 쪽으로 흐르는 꺾쇠 화살표 (화면 점검 5번의 남은 것). 다른 함정으로 다시 쓰이면 숨긴다
            if (effect == Effect.Conveyor)
            {
                if (_arrows == null) _arrows = ConveyorArrows.Create(transform, area != null ? area.sortingOrder + 1 : 0);
                _arrows.Setup(size);
            }
            else if (_arrows != null)
            {
                _arrows.gameObject.SetActive(false);
            }

            if (_mask < 0) _mask = LayerMask.GetMask("Player", "Enemy");
            gameObject.SetActive(true);
        }

        private static Sprite Art2DFor(Effect e)
        {
            Common.Art2DLibrary lib = Common.Art2D.Library;
            switch (e)
            {
                case Effect.Thorns: return lib.thorns;
                case Effect.FireVent: return lib.fireVent;
                case Effect.Conveyor: return lib.conveyor;
                case Effect.Steam: return lib.steam;
                case Effect.Vines: return lib.vines;
                case Effect.Oblivion: return lib.oblivion;
                default: return null; // 낙석은 떨어질 자리 예고 원이 따로 있다
            }
        }

        private static Color BaseColor(Effect e)
        {
            switch (e)
            {
                case Effect.Thorns: return new Color(0.35f, 0.45f, 0.2f, 0.75f);
                case Effect.FireVent: return new Color(0.35f, 0.18f, 0.12f, 0.8f);
                case Effect.Conveyor: return new Color(0.45f, 0.45f, 0.5f, 0.7f);
                case Effect.Steam: return new Color(0.5f, 0.55f, 0.6f, 0.55f);
                case Effect.Rockfall: return new Color(0.4f, 0.32f, 0.25f, 0.45f);
                case Effect.Vines: return new Color(0.2f, 0.12f, 0.3f, 0.8f);
                default: return new Color(0.3f, 0.2f, 0.4f, 0.22f); // 화면 점검: 쉬는 칸은 옅게 → 금(밝게)·무너짐(검정)과 갈린다
            }
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f) return;
            _clock += dt;
            float t = _clock + _phaseOffset;

            switch (_effect)
            {
                case Effect.Thorns: TickThorns(); break;
                case Effect.FireVent: TickCycle(t, period: 3f, telegraph: 0.8f, active: 1f, new Color(1f, 0.35f, 0.15f, 0.85f), Effect.FireVent); break;
                case Effect.Conveyor: TickConveyor(dt); break;
                case Effect.Steam:
                    if (Disrupted)
                    {
                        if (area != null) area.color = new Color(0.35f, 0.5f, 0.75f, 0.45f); // 레버: 증기 멈춤 (파랗게 식는다)
                        break;
                    }

                    TickCycle(t, period: 4f, telegraph: 1f, active: 0.35f, new Color(0.95f, 0.95f, 1f, 0.8f), Effect.Steam);
                    break;
                case Effect.Rockfall: TickRockfall(t); break;
                case Effect.Vines: TickVines(); break;
                case Effect.Oblivion: TickCycle(t, period: 6f, telegraph: 1.2f, active: 3f, new Color(0.05f, 0.02f, 0.08f, 0.95f), Effect.Oblivion); break;
            }

            if (_rooted != null && Time.time >= _rootUntil) Unroot();
        }

        // ---------------------------------------------------------------- 종류별

        private void TickThorns()
        {
            bool playerInside = false;
            int n = Overlap(transform.position, _size, _angle);
            for (int i = 0; i < n; i++)
            {
                if (_hits[i].TryGetComponent(out PlayerController p))
                {
                    playerInside = true;
                    if (_slowed == null)
                    {
                        _slowed = p;
                        p.Stats.Add(new StatModifier(StatType.MoveSpeed, ModifierKind.Percent, -0.4f, ModifierSource.Hero, _slowOwner));
                    }
                }
                else if (_hits[i].TryGetComponent(out EnemyBase e))
                {
                    e.Status.ApplySlow(0.4f, 0.2f);
                }
            }

            if (!playerInside && _slowed != null) Unslow();
        }

        /// <summary>쉬다가 → 예고 → 켜짐 (화염·증기·균열). 켜진 동안 안에 있으면 효과</summary>
        private void TickCycle(float t, float period, float telegraph, float active, Color activeColor, Effect effect)
        {
            float local = t % period;
            float telegraphStart = period - telegraph - active;
            Color baseColor = BaseColor(effect);
            if (local < telegraphStart)
            {
                if (area != null) area.color = baseColor;
                return;
            }

            if (local < telegraphStart + telegraph)
            {
                // 예고: 바닥이 깜빡인다 (화염 = 빨갛게, 증기 = 흰 연기, 균열 = 금이 감)
                float blink = 0.5f + 0.5f * Mathf.Sin(local * 20f);
                Color warn = effect == Effect.Oblivion ? new Color(0.92f, 0.78f, 1f, 0.85f) : effect == Effect.Steam ? new Color(1f, 1f, 1f, 0.5f) : new Color(1f, 0.25f, 0.2f, 0.6f);
                if (area != null) area.color = Color.Lerp(baseColor, warn, blink);
                return;
            }

            if (area != null) area.color = activeColor;
            if (Time.time < _nextTick) return;
            _nextTick = Time.time + (effect == Effect.Oblivion ? 0.4f : 0.5f);

            int n = Overlap(transform.position, _size, _angle);
            for (int i = 0; i < n; i++)
            {
                if (!_hits[i].TryGetComponent(out Health h)) continue;
                bool isPlayer = h.Team == Team.Player;
                float amount = effect == Effect.Oblivion && isPlayer ? Mathf.Max(_damage, h.Max * 0.1f) : _damage;
                h.TryTakeDamage(TrapDamage(amount, isPlayer, effect == Effect.FireVent ? "trap_fire" : effect == Effect.Steam ? "trap_steam" : "trap_oblivion"));
                if (effect != Effect.FireVent) PushOut(_hits[i]); // 증기: 밀어냄, 균열: 떨어졌다가 가까운 곳으로 복귀
            }
        }

        private void TickConveyor(float dt)
        {
            bool reversed = Disrupted; // 레버: 반대로 돈다
            Vector2 dir = reversed ? -(Vector2)transform.right : (Vector2)transform.right;
            if (area != null)
            {
                Color c = reversed ? new Color(0.85f, 0.55f, 0.25f, 0.7f) : BaseColor(Effect.Conveyor);
                c.a = 0.55f + 0.15f * Mathf.Sin(Time.time * 8f); // 움직이는 띠
                area.color = c;
            }

            if (_arrows != null) _arrows.Tick(reversed, ConveyorSpeed, dt);

            int n = Overlap(transform.position, _size, _angle);
            for (int i = 0; i < n; i++)
            {
                if (_hits[i].attachedRigidbody == null) continue;
                // 적은 약하게 민다: 느린 적(방패병 1.3 · 증기 골렘 1.1)도 거슬러 건널 수 있게 (2.2로 밀면 띠 끝에 영영 붙는다)
                bool enemy = _hits[i].TryGetComponent(out Health h) && h.Team == Team.Enemy;
                _hits[i].attachedRigidbody.position += dir * ConveyorSpeed * (enemy ? EnemyConveyorScale : 1f) * dt;
            }
        }

        private const float ConveyorSpeed = 2.2f;
        private const float EnemyConveyorScale = 0.35f;

        private void TickRockfall(float t)
        {
            float local = t % 2.6f;
            if (!_rockPending && local < 1.5f)
            {
                // 다음 낙석 자리 (1초 전 원 표시)
                _rockAt = (Vector2)transform.position + (Vector2)(transform.rotation * new Vector2(Random.Range(-0.5f, 0.5f) * _size.x, Random.Range(-0.5f, 0.5f) * _size.y));
                _rockPending = true;
                _marker = Telegraphs.Circle(_rockAt, 0.9f, Telegraphs.Danger);
            }

            if (_rockPending && local >= 1.5f && local < 2.5f) Telegraphs.Fill(_marker, Telegraphs.Danger, (local - 1.5f));
            if (_rockPending && local >= 2.5f)
            {
                _rockPending = false;
                Telegraphs.Hide(_marker);
                _marker = null;
                AttackFx.Burst(_rockAt, 0.9f, new Color(0.6f, 0.5f, 0.4f, 0.8f), 0.18f);
                int n = Physics2D.OverlapCircleNonAlloc(_rockAt, 0.9f, _hits, Mask);
                for (int i = 0; i < n; i++)
                {
                    if (_hits[i].TryGetComponent(out Health h)) h.TryTakeDamage(TrapDamage(_damage * 2.2f, h.Team == Team.Player, "trap_rock"));
                }
            }
        }

        private void TickVines()
        {
            int n = Overlap(transform.position, _size, _angle);
            for (int i = 0; i < n; i++)
            {
                if (!_hits[i].TryGetComponent(out Health h)) continue;
                if (_rootCooldown.TryGetValue(h, out float ready) && Time.time < ready) continue;
                _rootCooldown[h] = Time.time + 2f;
                if (_hits[i].TryGetComponent(out PlayerController p) && _rooted == null)
                {
                    _rooted = p;
                    _rootUntil = Time.time + 0.5f;
                    p.Root(0.5f); // 이동 속도 -100%는 하한 0.5에서 잘려서 묶이지 않았다
                    AttackFx.Burst(p.transform.position, 0.5f, new Color(0.4f, 0.2f, 0.6f, 0.7f), 0.5f);
                }
                else if (_hits[i].TryGetComponent(out EnemyBase e))
                {
                    e.Status.ApplySlow(1f, 0.5f);
                }
            }
        }

        // ---------------------------------------------------------------- 도우미

        private int Overlap(Vector2 center, Vector2 size, float angle) => Physics2D.OverlapBoxNonAlloc(center, size, angle, _hits, Mask);

        private static DamageInfo TrapDamage(float amount, bool toPlayer, string source)
        {
            return new DamageInfo { Amount = amount, Kind = DamageKind.Enemy, AttackerTeam = toPlayer ? Team.Enemy : Team.Player, SourceId = source };
        }

        /// <summary>구역 밖 가장 가까운 쪽으로 밀어낸다</summary>
        private void PushOut(Collider2D c)
        {
            Rigidbody2D rb = c.attachedRigidbody;
            if (rb == null) return;
            Vector2 local = Quaternion.Inverse(transform.rotation) * (rb.position - (Vector2)transform.position);
            float dx = _size.x * 0.5f - Mathf.Abs(local.x);
            float dy = _size.y * 0.5f - Mathf.Abs(local.y);
            Vector2 push = dx < dy ? new Vector2(Mathf.Sign(local.x) * (dx + 0.5f), 0f) : new Vector2(0f, Mathf.Sign(local.y == 0f ? 1f : local.y) * (dy + 0.5f));
            rb.position += (Vector2)(transform.rotation * push);
        }

        private void Unslow()
        {
            if (_slowed != null) _slowed.Stats.RemoveAllFrom(_slowOwner);
            _slowed = null;
        }

        private void Unroot()
        {
            if (_rooted != null) _rooted.Stats.RemoveAllFrom(_rootOwner);
            _rooted = null;
        }

        private void OnDisable()
        {
            Unslow();
            Unroot();
            if (_marker != null) Telegraphs.Hide(_marker);
            _marker = null;
            _rockPending = false;
        }
    }
}
