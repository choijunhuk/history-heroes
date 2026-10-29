using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace HistoryHeroes.Combat
{
    /// <summary>
    /// 3D 그림 (Art3D-Plan 4번): 2D 판정 오브젝트(영웅·적·보스) 위에 3D 모델을 얹는다. **판정은 2D 그대로.**
    /// - 땅은 XY 평면, 위는 -Z. 모델은 겨눈 쪽(공격·예고 중) → 움직이는 쪽 → 바라볼 대상 순으로 몸을 돌린다 (Quaternius 모델은 -Z를 본다).
    /// - 동작: Speed(이동 빠르기) · Attack(공격 사건) · Hit(맞음) · Dead(쓰러짐). 공격 동작은 캐릭터마다 Override 컨트롤러.
    /// - 모델은 돌려 쓴다 (Codex 3D 리뷰 5번): 적이 쓰러지면 오브젝트가 바로 꺼지므로 모델만 떼어 "시체"로 쓰러지는 동작을 보이고,
    ///   끝나면 예비로 넣어 둔다. 다음에 나오는 적은 예비 모델을 다시 쓴다 → 서바이벌에서 계속 만들고 지우지 않는다.
    /// - 맞으면 잠깐 밝게, 상태 이상(빙결·독·둔화)은 숨긴 2D 몸 색을 따라 물든다. 재질 원래 색에 곱한다 (Codex 3D 리뷰 9번).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class Visual3D : MonoBehaviour
    {
        [SerializeField] private GameObject modelPrefab;
        [Tooltip("모델 키 (월드, 오브젝트 배율 1일 때). 엘리트처럼 오브젝트가 커지면 같이 커진다")]
        [SerializeField] private float height = 1.5f;
        [Tooltip("이 빠르기에서 뛰기 동작 100%")]
        [SerializeField] private float runSpeed = 4.5f;
        [Tooltip("숨길 2D 그림 (임시 도형 몸 · 방향 표시)")]
        [SerializeField] private Renderer[] hideRenderers = new Renderer[0];
        [Tooltip("상태 이상 색을 읽을 2D 몸 (숨긴 채로 EnemyStatus가 색칠한다)")]
        [SerializeField] private SpriteRenderer tintSource;
        [SerializeField] private float turnSharpness = 16f;
        [Tooltip("영웅: 쓰러져도 모델을 남긴다 (부활하면 다시 일어난다)")]
        [SerializeField] private bool keepOnDeath;

        private static readonly int SpeedId = Animator.StringToHash("Speed");
        private static readonly int AttackId = Animator.StringToHash("Attack");
        private static readonly int HitId = Animator.StringToHash("Hit");
        private static readonly int DeadId = Animator.StringToHash("Dead");
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        private const float FlashTime = 0.08f;
        private const float CorpseTime = 1.6f;
        private const float SinkTime = 0.5f;
        private const float MinAttackGap = 0.2f;
        private static readonly Color FlashColor = new Color(2.2f, 2.2f, 2.2f, 1f);

        private struct Slot
        {
            public Renderer Renderer;
            public int Index;
            public Color Base;
        }

        private Transform _model;
        private GameObject _modelSource;
        private Material _outline;
        private const string OutlineShader = "HistoryHeroes/HeroOutline";
        private Animator _animator;
        private Slot[] _slots = new Slot[0];
        private MaterialPropertyBlock _block;
        private Rigidbody2D _body;
        private Health _health;
        private Vector3 _lastPosition;
        private Vector2 _facing = Vector2.down; // 처음엔 카메라 쪽
        private Vector2 _aim;
        private float _aimUntil;
        private float _flashUntil;
        private float _lastAttack = -10f;
        private Color _tint = Color.white;
        private Color _tintBase = Color.white;
        private bool _dead;

        /// <summary>움직이지 않을 때 바라볼 대상 (적 → 영웅)</summary>
        public Transform LookTarget { get; set; }

        public bool HasModel => _model != null;
        public Transform Model => _model;

        private void Awake()
        {
            _body = GetComponent<Rigidbody2D>();
            _health = GetComponent<Health>();
            _block = new MaterialPropertyBlock();
            if (tintSource != null) _tintBase = tintSource.color;
            if (_health != null)
            {
                _health.VisualDamaged += OnDamaged;
                _health.Died += OnDied;
            }
        }

        private void OnDestroy()
        {
            if (_health != null)
            {
                _health.VisualDamaged -= OnDamaged;
                _health.Died -= OnDied;
            }
        }

        private void OnEnable()
        {
            _dead = false;
            _aimUntil = 0f;
            _flashUntil = 0f;
            if (_model == null && modelPrefab != null) Take();
            else ResetAnimator();
            _lastPosition = transform.position;
            ApplyTint(Color.white, force: true);
            SetHidden(_model != null);
        }

        /// <summary>영웅을 바꿀 때 (같은 플레이어 오브젝트)</summary>
        public void SetModel(GameObject prefab, float modelHeight)
        {
            if (modelHeight > 0f) height = modelHeight;
            if (prefab == modelPrefab && _model != null)
            {
                Rescale();
                return;
            }

            if (_model != null) Pool.Release(_model, _modelSource);
            _model = null;
            _animator = null;
            _slots = new Slot[0];
            modelPrefab = prefab;
            if (modelPrefab != null && isActiveAndEnabled) Take();
            SetHidden(_model != null);
        }

        private void Take()
        {
            Transform model = Pool.Take(modelPrefab);
            if (model == null)
            {
                model = Instantiate(modelPrefab).transform;
                model.name = modelPrefab.name;
                ApplyShadows(model, modelPrefab);
            }

            _model = model;
            _modelSource = modelPrefab;
            _model.SetParent(transform, false);
            _model.localPosition = Vector3.zero;
            _model.rotation = FacingRotation(_facing);
            _model.gameObject.SetActive(true);
            Rescale();
            _animator = _model.GetComponent<Animator>();
            CollectSlots();
            ApplyOutline();
            ResetAnimator();
        }

        /// <summary>
        /// 영웅 외곽선 (새 영웅 화면 점검 1번: "영웅부터 찾기 쉽게"). 모델 조각마다 외곽선 재질을 하나 더 붙인다.
        /// 머리카락 카드(구멍 뚫린 재질)는 테가 지저분해서 뺀다. 모델이 바뀌면 Take에서 다시 붙인다. null이면 뗀다
        /// </summary>
        public void SetOutline(Material outline)
        {
            _outline = outline;
            ApplyOutline();
        }

        private void ApplyOutline()
        {
            if (_model != null) Outline(_model, _outline);
        }

        /// <summary>root 밑 모델 조각마다 외곽선 재질을 붙인다 (이미 있으면 바꾸고, outline이 null이면 뗀다). 거북선 같은 스킬 모델에도 쓴다</summary>
        public static void Outline(Transform root, Material outline)
        {
            if (root == null) return;
            foreach (Renderer r in root.GetComponentsInChildren<Renderer>(true))
            {
                if (!(r is SkinnedMeshRenderer) && !(r is MeshRenderer)) continue;
                var mats = new List<Material>(r.sharedMaterials);
                bool cutout = false;
                for (int i = mats.Count - 1; i >= 0; i--)
                {
                    if (mats[i] != null && mats[i].shader != null && mats[i].shader.name == OutlineShader) mats.RemoveAt(i);
                    else if (mats[i] != null && mats[i].IsKeywordEnabled("_ALPHATEST_ON")) cutout = true;
                }

                if (outline != null && !cutout) mats.Add(outline);
                r.sharedMaterials = mats.ToArray();
            }
        }

        private void Rescale()
        {
            if (_model == null) return;
            float raw = _modelSource != null && _modelSource.TryGetComponent(out Model3DInfo info) && info.height > 0.01f ? info.height : 1.8f;
            _model.localScale = Vector3.one * (height / raw);
        }

        private static void ApplyShadows(Transform model, GameObject prefab)
        {
            bool cast = !prefab.TryGetComponent(out Model3DInfo info) || info.castShadows;
            foreach (Renderer r in model.GetComponentsInChildren<Renderer>(true))
            {
                r.shadowCastingMode = cast ? ShadowCastingMode.On : ShadowCastingMode.Off;
            }
        }

        private void CollectSlots()
        {
            var slots = new List<Slot>();
            foreach (Renderer r in _model.GetComponentsInChildren<Renderer>(true))
            {
                Material[] mats = r.sharedMaterials;
                for (int i = 0; i < mats.Length; i++)
                {
                    Color c = mats[i] != null && mats[i].HasProperty(BaseColorId) ? mats[i].GetColor(BaseColorId) : Color.white;
                    slots.Add(new Slot { Renderer = r, Index = i, Base = c });
                }
            }

            _slots = slots.ToArray();
        }

        private void ResetAnimator()
        {
            if (_animator == null) return;
            _animator.Rebind();
            _animator.ResetTrigger(AttackId);
            _animator.ResetTrigger(HitId);
            _animator.SetBool(DeadId, false);
            _animator.SetFloat(SpeedId, 0f);
        }

        private void SetHidden(bool hidden)
        {
            foreach (Renderer r in hideRenderers)
            {
                if (r != null) r.enabled = !hidden;
            }
        }

        // ── 사건 ──

        /// <summary>
        /// 공격했다: 그쪽을 잠깐 보고 공격 동작. 0.2초 안에 또 부르면(탄막 · 쏜 직후의 상태 바뀜) 처음 방향을 지킨다
        /// → 모델이 실제로 날아간 화살 쪽을 본다 (Codex 3D 코드 리뷰 1번)
        /// </summary>
        public void Attack(Vector2 direction, float holdFacing = 0.45f)
        {
            if (_dead) return;
            if (Time.time - _lastAttack < MinAttackGap) return;
            Aim(direction, holdFacing);
            _lastAttack = Time.time;
            if (_animator != null && _animator.isActiveAndEnabled) _animator.SetTrigger(AttackId);
        }

        /// <summary>겨누기만 (예고 중 · 쏘기 전 조준): hold초 동안 그쪽을 본다 (움직여도)</summary>
        public void Aim(Vector2 direction, float hold = 0.3f)
        {
            if (direction.sqrMagnitude < 0.0001f) return;
            _aim = direction.normalized;
            _aimUntil = Mathf.Max(_aimUntil, Time.time + hold);
        }

        private void OnDamaged(DamageInfo info, float dealt)
        {
            if (dealt <= 0f || _dead) return;
            _flashUntil = Time.unscaledTime + FlashTime; // 멈춘 화면(카드·결과)에서도 돌아오게
            bool still = _body == null || _body.linearVelocity.sqrMagnitude < 0.25f;
            bool attacking = Time.time - _lastAttack < 0.35f;
            if (_animator != null && _animator.isActiveAndEnabled && still && !attacking) _animator.SetTrigger(HitId);
        }

        private void OnDied(Health h)
        {
            if (_dead || _model == null) return;
            _dead = true;
            ApplyTint(Color.white, force: true);
            if (_animator != null) _animator.SetBool(DeadId, true);
            if (keepOnDeath) return; // 영웅은 그 자리에서 쓰러진다 (부활하면 다시 일어난다)

            // 적: 오브젝트가 곧 꺼진다 → 모델만 떼어 쓰러지는 동작을 보이고, 끝나면 예비로
            Pool.AddCorpse(_model, _modelSource, Time.time + CorpseTime);
            _model = null;
            _animator = null;
            _slots = new Slot[0];
        }

        // ── 매 프레임 ──

        private void LateUpdate()
        {
            Pool.Tick();
            if (_model == null) return;
            float dt = Time.deltaTime;

            if (_dead && _health != null && !_health.IsDead)
            {
                _dead = false; // 부활
                if (_animator != null) _animator.SetBool(DeadId, false);
            }

            Vector3 now = transform.position;
            Vector2 velocity = _body != null ? _body.linearVelocity : (dt > 0f ? (Vector2)(now - _lastPosition) / dt : Vector2.zero);
            _lastPosition = now;
            float speed = velocity.magnitude;

            if (!_dead && dt > 0f)
            {
                if (Time.time < _aimUntil) _facing = _aim;
                else if (speed > 0.25f) _facing = velocity / speed;
                else if (LookTarget != null && LookTarget.gameObject.activeInHierarchy)
                {
                    Vector2 to = LookTarget.position - now;
                    if (to.sqrMagnitude > 0.01f) _facing = to.normalized;
                }

                _model.rotation = Quaternion.Slerp(_model.rotation, FacingRotation(_facing), 1f - Mathf.Exp(-turnSharpness * dt));
                if (_animator != null) _animator.SetFloat(SpeedId, Mathf.Clamp01(speed / runSpeed), 0.08f, dt);
            }

            ApplyTint(CurrentTint(), force: false);
        }

        private Color CurrentTint()
        {
            if (Time.unscaledTime < _flashUntil) return FlashColor;
            if (tintSource == null) return Color.white;
            Color c = tintSource.color;
            if (Near(c, _tintBase) || Near(c, Color.white)) return Color.white; // 보통 · 번쩍임(HitFlash가 2D 몸을 희게 칠함)
            return StatusTint(c, _tintBase);
        }

        /// <summary>2D 몸 색이 원래 색에서 얼마나 바뀌었는지 → 3D 모델에 곱할 색 (가장 큰 칸이 1이 되게 맞춰 연하게)</summary>
        public static Color StatusTint(Color now, Color baseColor)
        {
            float r = now.r / Mathf.Max(0.2f, baseColor.r), g = now.g / Mathf.Max(0.2f, baseColor.g), b = now.b / Mathf.Max(0.2f, baseColor.b);
            float max = Mathf.Max(0.0001f, Mathf.Max(r, Mathf.Max(g, b)));
            var ratio = new Color(r / max, g / max, b / max, 1f);
            return Color.Lerp(Color.white, ratio, 0.65f);
        }

        private static bool Near(Color a, Color b) => Mathf.Abs(a.r - b.r) + Mathf.Abs(a.g - b.g) + Mathf.Abs(a.b - b.b) < 0.03f;

        private void ApplyTint(Color tint, bool force)
        {
            if (!force && tint == _tint) return;
            _tint = tint;
            bool plain = tint == Color.white;
            foreach (Slot s in _slots)
            {
                if (s.Renderer == null) continue;
                if (plain)
                {
                    s.Renderer.SetPropertyBlock(null, s.Index);
                    continue;
                }

                _block.Clear();
                _block.SetColor(BaseColorId, s.Base * tint);
                s.Renderer.SetPropertyBlock(_block, s.Index);
            }
        }

        private static Quaternion FacingRotation(Vector2 facing)
        {
            if (facing.sqrMagnitude < 0.0001f) facing = Vector2.down;
            return Quaternion.LookRotation(new Vector3(-facing.x, -facing.y, 0f), Vector3.back);
        }

        // ── 모델 돌려쓰기 (시체 → 예비 → 다음 적) ──

        private static class Pool
        {
            private struct Corpse
            {
                public Transform Model;
                public GameObject Source;
                public float Until;
            }

            private static readonly Dictionary<GameObject, Stack<Transform>> Free = new Dictionary<GameObject, Stack<Transform>>();
            private static readonly List<Corpse> Corpses = new List<Corpse>();
            private static Transform _root;
            private static int _tickedFrame = -1;

            private static Transform Root
            {
                get
                {
                    if (_root == null) _root = new GameObject("[Model3D Pool]").transform; // 전투 장면과 같이 사라진다
                    return _root;
                }
            }

            public static Transform Take(GameObject source)
            {
                if (source == null || !Free.TryGetValue(source, out Stack<Transform> stack)) return null;
                while (stack.Count > 0)
                {
                    Transform t = stack.Pop();
                    if (t != null) return t; // 장면이 바뀌면 예비가 지워져 있다
                }

                return null;
            }

            public static void Release(Transform model, GameObject source)
            {
                if (model == null) return;
                if (source == null)
                {
                    Object.Destroy(model.gameObject);
                    return;
                }

                model.gameObject.SetActive(false);
                model.SetParent(Root, false);
                if (!Free.TryGetValue(source, out Stack<Transform> stack)) Free[source] = stack = new Stack<Transform>();
                stack.Push(model);
            }

            public static void AddCorpse(Transform model, GameObject source, float until)
            {
                model.SetParent(Root, true);
                Corpses.Add(new Corpse { Model = model, Source = source, Until = until });
            }

            /// <summary>한 프레임에 한 번: 끝난 시체를 예비로, 끝나기 직전엔 땅으로 가라앉힌다 (+Z)</summary>
            public static void Tick()
            {
                if (_tickedFrame == Time.frameCount) return;
                _tickedFrame = Time.frameCount;
                float now = Time.time, dt = Time.deltaTime;
                for (int i = Corpses.Count - 1; i >= 0; i--)
                {
                    Corpse c = Corpses[i];
                    if (c.Model == null)
                    {
                        Corpses.RemoveAt(i);
                        continue;
                    }

                    if (now >= c.Until)
                    {
                        Corpses.RemoveAt(i);
                        Release(c.Model, c.Source);
                        continue;
                    }

                    if (c.Until - now < SinkTime) c.Model.position += new Vector3(0f, 0f, 1.4f * dt);
                }
            }

            public static void Clear()
            {
                Free.Clear();
                Corpses.Clear();
                _root = null;
                _tickedFrame = -1;
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => Pool.Clear();
    }
}
