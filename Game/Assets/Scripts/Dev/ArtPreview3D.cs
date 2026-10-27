#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Collections.Generic;
using System.Reflection;
using HistoryHeroes.Combat;
using HistoryHeroes.Enemy;
using HistoryHeroes.Player;
using HistoryHeroes.Skills;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace HistoryHeroes.Dev
{
    /// <summary>
    /// 3D 그래픽 샘플 (개발자 2026-10-27 "2d 말고 3d는"): 게임 규칙(2D 물리, XY 평면)은 그대로 두고 **보이는 것만 3D**.
    /// 영웅·적 위에 3D 모델(휴머노이드 동작)을 얹어 움직이는 방향을 보게 돌리고, 카메라를 비스듬히 눕힌다 (아처로처럼).
    /// 땅은 XY 평면, "위"는 -Z (카메라 쪽). 바닥·벽·장애물은 그림자를 받는 3D 판·상자로 바꾼다. End()로 모두 원래대로.
    /// </summary>
    [DefaultExecutionOrder(10000)]
    public sealed class ArtPreview3D : MonoBehaviour
    {
        public sealed class Set
        {
            public GameObject hero, charger, archer, other;
            public Material ground, stone, crate;
            public Sprite blade;
            public int rendererIndex = 1;
            public float tilt = 38f, distance = 15f, fov = 34f;
            public float heroHeight = 1.5f, enemyHeight = 1.35f;
        }

        private sealed class Actor
        {
            public Transform Source;
            public GameObject Model;
            public Animator Animator;
            public Health Health;
            public Vector3 Last;
            public Vector2 Facing = Vector2.up;
            public float LastHp;
            public bool IsPlayer;
            public bool Dead;
            public float RemoveAt;
        }

        private static readonly FieldInfo PlayerBody = typeof(PlayerController).GetField("bodyRenderer", BindingFlags.NonPublic | BindingFlags.Instance);
        private static readonly FieldInfo CameraRenderer = typeof(UniversalAdditionalCameraData).GetField("m_RendererIndex", BindingFlags.NonPublic | BindingFlags.Instance);
        private static readonly int SpeedId = Animator.StringToHash("Speed");
        private static readonly int AttackId = Animator.StringToHash("Attack");
        private static readonly int HitId = Animator.StringToHash("Hit");
        private static readonly int DeadId = Animator.StringToHash("Dead");

        private static ArtPreview3D _instance;
        private Set _set;
        private Camera _camera;
        private bool _wasOrtho;
        private Vector3 _camPos;
        private Quaternion _camRot;
        private float _camFov, _camNear, _camFar;
        private int _camRenderer = -1;
        private AmbientMode _ambientMode;
        private Color _ambient;
        private Light _sun;
        private readonly Dictionary<Transform, Actor> _actors = new Dictionary<Transform, Actor>();
        private readonly Dictionary<GameObject, float> _scales = new Dictionary<GameObject, float>();
        private readonly List<GameObject> _props = new List<GameObject>();
        private readonly HashSet<Renderer> _hidden = new HashSet<Renderer>();
        private readonly HashSet<int> _seenArrows = new HashSet<int>();
        private readonly HashSet<int> _propped = new HashSet<int>();
        private readonly HashSet<int> _arrowed = new HashSet<int>();
        private Material _shaft, _head, _fletch, _enemyShaft;

        public static string Begin(Set set)
        {
            End();
            var go = new GameObject("[ArtPreview3D]");
            _instance = go.AddComponent<ArtPreview3D>();
            _instance._set = set;
            return _instance.Setup();
        }

        public static void End()
        {
            if (_instance == null) return;
            _instance.Restore();
            Destroy(_instance.gameObject);
            _instance = null;
        }

        private string Setup()
        {
            _camera = Camera.main;
            if (_camera == null) return "카메라 없음";
            _wasOrtho = _camera.orthographic;
            _camPos = _camera.transform.position;
            _camRot = _camera.transform.rotation;
            _camFov = _camera.fieldOfView;
            _camNear = _camera.nearClipPlane;
            _camFar = _camera.farClipPlane;
            UniversalAdditionalCameraData data = _camera.GetUniversalAdditionalCameraData();
            if (CameraRenderer != null) _camRenderer = (int)CameraRenderer.GetValue(data);
            data.SetRenderer(_set.rendererIndex);
            _camera.orthographic = false;
            _camera.fieldOfView = _set.fov;
            _camera.nearClipPlane = 0.3f;
            _camera.farClipPlane = 100f;

            _ambientMode = RenderSettings.ambientMode;
            _ambient = RenderSettings.ambientLight;
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.5f, 0.52f, 0.58f);

            var sunObject = new GameObject("Sun");
            sunObject.transform.SetParent(transform, false);
            _sun = sunObject.AddComponent<Light>();
            _sun.type = LightType.Directional;
            _sun.intensity = 1.25f;
            _sun.color = new Color(1f, 0.96f, 0.88f);
            _sun.shadows = LightShadows.Soft;
            _sun.shadowStrength = 0.75f;
            sunObject.transform.rotation = Quaternion.LookRotation(new Vector3(0.35f, -0.45f, 1f).normalized, Vector3.up); // 그림자는 오른쪽 아래로

            _shaft = Flat(new Color(0.55f, 0.38f, 0.2f));
            _head = Flat(new Color(0.82f, 0.84f, 0.88f));
            _fletch = Flat(new Color(0.95f, 0.95f, 0.92f));
            _enemyShaft = Flat(new Color(0.85f, 0.2f, 0.45f));
            BuildEnvironment();
            LateUpdate();
            return "3D 켬";
        }

        // ── 바닥 · 벽 · 장애물 ──

        private void BuildEnvironment()
        {
            foreach (SpriteRenderer sr in FindObjectsByType<SpriteRenderer>(FindObjectsSortMode.None)) Prop(sr);
        }

        private void Prop(SpriteRenderer sr)
        {
            if (sr == null || !sr.enabled || _propped.Contains(sr.GetInstanceID())) return;
            Vector3 p = sr.transform.position;
            Vector3 s = sr.transform.lossyScale;
            switch (sr.name)
            {
                case "Floor":
                {
                    GameObject quad = Primitive(PrimitiveType.Quad, Tiled(_set.ground, s.x / 2f, s.y / 2f));
                    quad.transform.position = new Vector3(p.x, p.y, 0.01f);
                    quad.transform.localScale = new Vector3(s.x, s.y, 1f);
                    break;
                }
                case "Wall Left":
                case "Wall Right":
                case "Wall Top":
                case "Wall Bottom":
                    Box(p, new Vector3(s.x, s.y, 1.4f), Tiled(_set.stone, Mathf.Max(s.x, s.y), 1.4f));
                    break;
                case "High":
                    if (sr.sprite == Common.PlaceholderSprites.Circle)
                    {
                        GameObject pillar = Primitive(PrimitiveType.Cylinder, Tiled(_set.stone, 2f, 1.5f));
                        pillar.transform.position = new Vector3(p.x, p.y, -0.9f);
                        pillar.transform.rotation = Quaternion.Euler(90f, 0f, 0f); // 기둥 축을 -Z(위)로
                        pillar.transform.localScale = new Vector3(s.x, 0.9f, s.y);
                    }
                    else
                    {
                        Box(p, new Vector3(s.x, s.y, 1.6f), Tiled(_set.stone, Mathf.Max(s.x, s.y), 1.6f));
                    }

                    break;
                case "Breakable":
                    Box(p, new Vector3(s.x, s.y, s.x), _set.crate);
                    break;
                default:
                    return; // 낮은 장애물(구덩이) · 문 · 예고는 2D 그대로
            }

            _propped.Add(sr.GetInstanceID());
            Hide(sr);
        }

        private void Box(Vector3 p, Vector3 size, Material mat)
        {
            GameObject box = Primitive(PrimitiveType.Cube, mat);
            box.transform.position = new Vector3(p.x, p.y, -size.z / 2f);
            box.transform.localScale = size;
        }

        private GameObject Primitive(PrimitiveType type, Material mat)
        {
            GameObject go = GameObject.CreatePrimitive(type);
            Destroy(go.GetComponent<Collider>()); // 판정은 2D 그대로
            go.transform.SetParent(transform, true);
            var r = go.GetComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = ShadowCastingMode.On;
            r.receiveShadows = true;
            _props.Add(go);
            return go;
        }

        private static Material Tiled(Material source, float x, float y)
        {
            if (source == null) return null;
            var m = new Material(source) { mainTextureScale = new Vector2(x, y) };
            return m;
        }

        private static Material Flat(Color c)
        {
            var m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            m.SetColor("_BaseColor", c);
            m.SetFloat("_Smoothness", 0.3f);
            return m;
        }

        /// <summary>3D 화살 (자루 · 촉 · 깃): 투사체의 오른쪽(+X)이 날아가는 쪽. 투사체가 늘려 그린 배율은 되돌린다</summary>
        private void Arrow(Transform projectile, bool enemy)
        {
            if (projectile.TryGetComponent(out SpriteRenderer sr)) Hide(sr);
            var root = new GameObject("Arrow3D");
            root.transform.SetParent(projectile, false);
            Vector3 s = projectile.localScale;
            root.transform.localScale = new Vector3(1f / Mathf.Max(0.01f, s.x), 1f / Mathf.Max(0.01f, s.y), 1f);
            Part(root.transform, PrimitiveType.Cylinder, enemy ? _enemyShaft : _shaft, new Vector3(0f, 0f, 0f), Quaternion.Euler(0f, 0f, 90f), new Vector3(0.035f, 0.3f, 0.035f));
            Part(root.transform, PrimitiveType.Cube, _head, new Vector3(0.33f, 0f, 0f), Quaternion.Euler(0f, 0f, 45f), new Vector3(0.09f, 0.09f, 0.03f));
            Part(root.transform, PrimitiveType.Cube, _fletch, new Vector3(-0.27f, 0f, 0f), Quaternion.identity, new Vector3(0.12f, 0.012f, 0.09f));
            Part(root.transform, PrimitiveType.Cube, _fletch, new Vector3(-0.27f, 0f, 0f), Quaternion.Euler(90f, 0f, 0f), new Vector3(0.12f, 0.012f, 0.09f));
            _props.Add(root);
        }

        private static void Part(Transform parent, PrimitiveType type, Material mat, Vector3 pos, Quaternion rot, Vector3 scale)
        {
            GameObject go = GameObject.CreatePrimitive(type);
            Destroy(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localRotation = rot;
            go.transform.localScale = scale;
            var r = go.GetComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = ShadowCastingMode.On;
        }

        private void Hide(Renderer r)
        {
            if (r == null || !r.enabled) return;
            r.enabled = false;
            _hidden.Add(r);
        }

        // ── 캐릭터 ──

        private void LateUpdate()
        {
            if (_set == null || _camera == null) return;
            float dt = Mathf.Max(Time.deltaTime, 0f);
            Transform player = null;

            foreach (PlayerController p in FindObjectsByType<PlayerController>(FindObjectsSortMode.None))
            {
                player = p.transform;
                Track(p.transform, _set.hero, true, p.Health, _set.heroHeight);
                if (PlayerBody?.GetValue(p) is SpriteRenderer body) Hide(body);
            }

            foreach (EnemyBase e in EnemyBase.Alive)
            {
                if (e == null) continue;
                GameObject prefab = e is ArcherEnemy ? _set.archer : e is ChargerEnemy ? _set.charger : _set.other;
                Track(e.transform, prefab, false, e.Health, _set.enemyHeight * Mathf.Max(0.5f, e.transform.localScale.x));
                if (e.TryGetComponent(out SpriteRenderer sr)) Hide(sr);
            }

            // 새로 생긴 장애물 (방이 바뀌면)
            foreach (SpriteRenderer sr in FindObjectsByType<SpriteRenderer>(FindObjectsSortMode.None))
            {
                if (sr.name == "High" || sr.name == "Breakable") Prop(sr);
            }

            // 화살·칼날은 가슴 높이로 (2D 판정은 XY라 z는 상관없다), 영웅이 새로 쏘면 쏘는 동작
            bool heroShot = false;
            foreach (Projectile projectile in FindObjectsByType<Projectile>(FindObjectsSortMode.None))
            {
                Vector3 pos = projectile.transform.position;
                projectile.transform.position = new Vector3(pos.x, pos.y, -0.9f);
                bool mine = projectile.name.StartsWith("Arrow_Player");
                if (mine && _seenArrows.Add(projectile.GetInstanceID())) heroShot = true;
                if ((mine || projectile.name.StartsWith("Arrow_Enemy")) && _arrowed.Add(projectile.GetInstanceID())) Arrow(projectile.transform, !mine);
            }

            foreach (OrbitBladesBehaviour blades in FindObjectsByType<OrbitBladesBehaviour>(FindObjectsSortMode.None))
            {
                foreach (SpriteRenderer sr in blades.GetComponentsInChildren<SpriteRenderer>())
                {
                    if (!sr.name.StartsWith("Blade ") || _set.blade == null) continue;
                    sr.sprite = _set.blade;
                    sr.color = Color.white;
                    sr.transform.localScale = Vector3.one * 1.2f;
                    Vector3 bp = sr.transform.position;
                    sr.transform.position = new Vector3(bp.x, bp.y, -0.7f);
                }
            }

            if (heroShot && player != null && _actors.TryGetValue(player, out Actor hero) && !hero.Dead) hero.Animator.SetTrigger(AttackId);

            var remove = new List<Transform>();
            foreach (KeyValuePair<Transform, Actor> pair in _actors)
            {
                Actor a = pair.Value;
                bool gone = a.Source == null || !a.Source.gameObject.activeInHierarchy || (a.Health != null && a.Health.IsDead);
                if (gone && !a.Dead)
                {
                    a.Dead = true;
                    a.RemoveAt = Time.time + 2.2f;
                    a.Animator.SetBool(DeadId, true);
                }

                if (a.Dead)
                {
                    if (Time.time >= a.RemoveAt)
                    {
                        Destroy(a.Model);
                        remove.Add(pair.Key);
                    }

                    continue;
                }

                Vector3 now = a.Source.position;
                Vector3 delta = now - a.Last;
                a.Last = now;
                float speed = dt > 0f ? delta.magnitude / dt : 0f;
                if (speed > 0.3f) a.Facing = new Vector2(delta.x, delta.y).normalized;
                else if (a.IsPlayer) FaceNearestEnemy(a);
                else if (player != null) a.Facing = ((Vector2)(player.position - now)).normalized;

                a.Model.transform.position = new Vector3(now.x, now.y, 0f);
                Quaternion goal = Quaternion.LookRotation(new Vector3(-a.Facing.x, -a.Facing.y, 0f), Vector3.back); // Quaternius 모델은 -Z를 본다
                a.Model.transform.rotation = Quaternion.Slerp(a.Model.transform.rotation, goal, 1f - Mathf.Exp(-14f * dt));
                if (dt > 0f) a.Animator.SetFloat(SpeedId, Mathf.Clamp01(speed / 5f), 0.08f, dt);

                if (a.Health != null)
                {
                    float hp = a.Health.Current;
                    if (!a.IsPlayer && hp < a.LastHp - 0.5f) a.Animator.SetTrigger(HitId);
                    a.LastHp = hp;
                }
            }

            foreach (Transform t in remove) _actors.Remove(t);
            FollowCamera(player);
        }

        private void FaceNearestEnemy(Actor a)
        {
            float best = float.MaxValue;
            foreach (EnemyBase e in EnemyBase.Alive)
            {
                if (e == null) continue;
                float d = (e.transform.position - a.Source.position).sqrMagnitude;
                if (d < best)
                {
                    best = d;
                    a.Facing = ((Vector2)(e.transform.position - a.Source.position)).normalized;
                }
            }
        }

        private void Track(Transform source, GameObject prefab, bool isPlayer, Health health, float height)
        {
            if (source == null || prefab == null || _actors.ContainsKey(source)) return;
            if (health != null && health.IsDead) return;
            GameObject model = Instantiate(prefab, transform);
            model.transform.localScale = Vector3.one * (height / Height(prefab));
            model.transform.position = new Vector3(source.position.x, source.position.y, 0f);
            var animator = model.GetComponent<Animator>();
            _actors[source] = new Actor
            {
                Source = source,
                Model = model,
                Animator = animator,
                Health = health,
                Last = source.position,
                LastHp = health != null ? health.Current : 0f,
                IsPlayer = isPlayer,
            };
        }

        /// <summary>모델 키 (한 번 재서 기억)</summary>
        private float Height(GameObject prefab)
        {
            if (_scales.TryGetValue(prefab, out float h)) return h;
            GameObject probe = Instantiate(prefab);
            var bounds = new Bounds(probe.transform.position, Vector3.zero);
            foreach (Renderer r in probe.GetComponentsInChildren<Renderer>()) bounds.Encapsulate(r.bounds);
            DestroyImmediate(probe);
            h = Mathf.Max(0.1f, bounds.size.y);
            _scales[prefab] = h;
            return h;
        }

        /// <summary>비스듬히 눕힌 카메라: 앞(+Y, 적이 오는 쪽)을 내려다본다. 던전 방은 영웅을 살짝 따라간다</summary>
        private void FollowCamera(Transform player)
        {
            float t = _set.tilt * Mathf.Deg2Rad;
            var forward = new Vector3(0f, Mathf.Sin(t), Mathf.Cos(t));
            var up = new Vector3(0f, Mathf.Cos(t), -Mathf.Sin(t));
            Vector3 focus = player != null ? new Vector3(player.position.x * 0.35f, player.position.y + 2.2f, 0f) : new Vector3(0f, 2f, 0f);
            _camera.transform.SetPositionAndRotation(focus - forward * _set.distance, Quaternion.LookRotation(forward, up));
        }

        private void Restore()
        {
            foreach (Actor a in _actors.Values)
            {
                if (a.Model != null) Destroy(a.Model);
            }

            _actors.Clear();
            foreach (GameObject go in _props)
            {
                if (go != null) Destroy(go);
            }

            _props.Clear();
            foreach (Renderer r in _hidden)
            {
                if (r != null) r.enabled = true;
            }

            _hidden.Clear();
            _propped.Clear();
            _arrowed.Clear();
            RenderSettings.ambientMode = _ambientMode;
            RenderSettings.ambientLight = _ambient;
            if (_camera != null)
            {
                _camera.orthographic = _wasOrtho;
                _camera.fieldOfView = _camFov;
                _camera.nearClipPlane = _camNear;
                _camera.farClipPlane = _camFar;
                _camera.transform.SetPositionAndRotation(_camPos, _camRot);
                _camera.GetUniversalAdditionalCameraData().SetRenderer(_camRenderer);
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => _instance = null;
    }
}
#endif
