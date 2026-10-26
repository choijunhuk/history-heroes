using HistoryHeroes.Combat;
using HistoryHeroes.Player;
using HistoryHeroes.Progress;
using HistoryHeroes.Stats;
using UnityEngine;

namespace HistoryHeroes.Pets
{
    /// <summary>펫 행동 하나 (트리거 × 행동, Pets 7장). PetController가 매 프레임 부른다</summary>
    public interface IPetBehaviour
    {
        void Tick(float deltaTime);
        /// <summary>판이 끝나거나 펫이 꺼질 때: 붙인 보정값·표식·구독을 모두 뗀다 (GameDesign 12-2 Apply 계약)</summary>
        void Stop();
    }

    /// <summary>
    /// 장면의 펫 (Pets 5·7장): 영웅 뒤를 따라다니고(충돌 없음, 적이 공격하지 않음), 펫 종류에 맞는 행동을 돌린다.
    /// 판 밖 능력치(+0.3%/Lv, 인연 +2%)는 MetaStats가 따로 붙인다 — 여기는 화면에서 보이는 "행동"만.
    /// BattleBootstrap이 장착한 펫(보스맵은 기록의 펫)으로 Setup한다. 없으면 꺼 둔다.
    /// </summary>
    public sealed class PetController : MonoBehaviour
    {
        [SerializeField] private SpriteRenderer body;
        [Tooltip("사냥 표식 · 보물 자리 표시 (펫마다 하나만 쓴다)")]
        [SerializeField] private SpriteRenderer marker;
        [SerializeField] private LayerMask enemyMask;

        private IPetBehaviour _behaviour;
        private Vector2 _lastHero;
        private Vector2 _heroDirection = Vector2.up;
        private Color _baseColor;
        private float _baseSize;

        public PetData Data { get; private set; }
        public int Level { get; private set; }
        public PlayerController Player { get; private set; }
        public ExperienceSystem Experience { get; private set; }
        public LayerMask EnemyMask => enemyMask;
        public SpriteRenderer Marker => marker;
        public IPetBehaviour Behaviour => _behaviour;

        public bool AwakenedFirst => Level >= 5;
        public bool AwakenedSecond => Level >= 10;

        public Vector2 Position
        {
            get => transform.position;
            set => transform.position = new Vector3(value.x, value.y, transform.position.z);
        }

        public Vector2 HeroPosition => Player.transform.position;
        /// <summary>영웅이 마지막으로 움직인 방향</summary>
        public Vector2 HeroDirection => _heroDirection;

        public void Setup(PetData data, int level, PlayerController player, ExperienceSystem experience)
        {
            _behaviour?.Stop();
            Data = data;
            Level = Mathf.Max(1, level);
            Player = player;
            Experience = experience;
            _lastHero = player.transform.position;
            Position = RestPoint();
            if (body != null)
            {
                body.color = data.color;
                _baseColor = data.color;
                _baseSize = body.transform.localScale.x;
            }

            if (marker != null) marker.gameObject.SetActive(false);
            _behaviour = Create(data.kind);
            gameObject.SetActive(true);
        }

        private IPetBehaviour Create(PetKind kind)
        {
            switch (kind)
            {
                case PetKind.Hawk: return new HawkBehaviour(this);
                case PetKind.RedHare: return new RedHareBehaviour(this);
                case PetKind.Marengo: return new MarengoBehaviour(this);
                case PetKind.Dove: return new DoveBehaviour(this);
                default: return null;
            }
        }

        private void Update()
        {
            if (_behaviour == null || Player == null) return;
            float dt = Time.deltaTime;
            if (dt <= 0f) return; // 멈춤 (레벨업 창)

            Vector2 hero = Player.transform.position;
            Vector2 delta = hero - _lastHero;
            if (delta.sqrMagnitude > 0.000025f) _heroDirection = delta.normalized;
            _lastHero = hero;

            if (Player.Health.IsDead)
            {
                Stop();
                return;
            }

            _behaviour.Tick(dt);
        }

        private void OnDisable() => Stop();

        private void Stop()
        {
            _behaviour?.Stop();
            _behaviour = null;
        }

        // ---------------------------------------------------------------- 행동 도우미

        /// <summary>쉬는 자리: 영웅 뒤 옆</summary>
        public Vector2 RestPoint(float side = 0.55f, float back = 0.6f)
        {
            Vector2 dir = _heroDirection;
            Vector2 right = new Vector2(dir.y, -dir.x);
            return HeroPosition - dir * back + right * side;
        }

        /// <summary>목표 쪽으로 부드럽게 (따라다니기)</summary>
        public void Follow(Vector2 goal, float sharpness, float deltaTime)
        {
            float k = 1f - Mathf.Exp(-sharpness * deltaTime);
            Position = Vector2.Lerp(Position, goal, k);
        }

        /// <summary>일정한 빠르기로 날아간다. 닿으면 true</summary>
        public bool FlyTo(Vector2 goal, float speed, float deltaTime)
        {
            Position = Vector2.MoveTowards(Position, goal, speed * deltaTime);
            return (Position - goal).sqrMagnitude < 0.0225f;
        }

        /// <summary>펫 피해: 영웅 공격력 × 계수. 액티브 피해로 세지만 판 안 액티브 증가·치명타는 받지 않는다 (Pets 2장 "약하게")</summary>
        public DamageInfo Damage(float coefficient, string source)
        {
            return new DamageInfo
            {
                Amount = Player.Stats.Get(StatType.Attack) * coefficient,
                Kind = DamageKind.Active,
                AttackerTeam = Team.Player,
                SourceId = source,
            };
        }

        public void SetLook(float alpha, float sizeMultiplier)
        {
            if (body == null) return;
            Color c = _baseColor;
            c.a = alpha;
            body.color = c;
            body.transform.localScale = Vector3.one * _baseSize * sizeMultiplier;
        }

        public void ShowMarker(Vector2 at, Color color, float size)
        {
            if (marker == null) return;
            marker.gameObject.SetActive(true);
            marker.transform.SetParent(null, true);
            marker.transform.position = new Vector3(at.x, at.y, 0f);
            marker.transform.localScale = Vector3.one * size;
            marker.color = color;
        }

        public void HideMarker()
        {
            if (marker != null) marker.gameObject.SetActive(false);
        }

        private void OnDestroy()
        {
            if (marker != null && marker.transform.parent == null) Destroy(marker.gameObject);
        }
    }
}
