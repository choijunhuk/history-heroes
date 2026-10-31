using System.Collections.Generic;
using HistoryHeroes.Heroes;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

namespace HistoryHeroes.UI
{
    /// <summary>
    /// 영웅 화면의 3D 초상 (Art3D-Plan 7번): 영웅마다 작은 카메라가 3D 모델을 그림(RenderTexture)으로 그려 줄의 오른쪽에 보인다.
    /// 끌면 그 영웅이 돈다. 이 부품은 영웅 화면에 붙어 있어 화면이 열려 있을 때만 그린다.
    /// 필요할 때만 그린다 (Codex 3D 코드 리뷰 2차 2번): 보이는 칸은 처음 한 번씩, 그 뒤엔 고른 영웅만 초당 15번(서 있는 동작),
    /// 끄는 영웅은 바로. 카메라는 늘 꺼 두고 손으로 Render를 부른다 → 장비·펫 화면을 위에 열어도 카메라가 계속 돌지 않는다.
    /// 무대는 로비 UI와 멀리 떨어진 곳(위가 +Y인 보통 3D)에 둔다. 카메라는 3D 렌더러(1번), 배경은 투명.
    /// </summary>
    public sealed class HeroPortraits : MonoBehaviour
    {
        [SerializeField] private HeroRoster roster;
        [SerializeField] private RawImage[] images = new RawImage[0];
        [SerializeField] private int textureSize = 320;
        [Tooltip("모델 키 (초상 안)")]
        [SerializeField] private float modelHeight = 1.8f;
        [SerializeField] private float startYaw = 25f;
        [Tooltip("고른 영웅을 다시 그리는 빠르기 (초당)")]
        [SerializeField] private float liveFps = 15f;
        [Tooltip("무대 자리 (초상 묶음이 여럿이면 서로 겹치지 않게 다르게)")]
        [SerializeField] private Vector3 stageOrigin = new Vector3(0f, -5000f, 0f);
        private const float SlotGap = 30f;

        private readonly List<Slot> _slots = new List<Slot>();
        private Transform _stage;
        private ScrollRect _scroll;
        private Light[] _lights = new Light[0];
        private static readonly System.Collections.Generic.List<Light> _sceneLights = new System.Collections.Generic.List<Light>();
        private int _focus = -1;
        private float _nextLive;

        private sealed class Slot
        {
            public int Index;
            public Camera Camera;
            public Transform Model;
            public Animator Animator;
            public RenderTexture Texture;
            public float Yaw;
        }

        private void OnEnable()
        {
            if (_stage == null && roster != null && images.Length > 0) Build();
            if (_stage != null) _stage.gameObject.SetActive(true);
            EnsureVisible();
        }

        private void OnDisable()
        {
            if (_stage != null) _stage.gameObject.SetActive(false); // 모델 동작도 멈춘다
        }

        private void OnApplicationFocus(bool focused)
        {
            if (focused && isActiveAndEnabled) EnsureVisible();
        }

        private void OnDestroy()
        {
            if (_scroll != null) _scroll.onValueChanged.RemoveListener(OnScroll);
            foreach (Slot s in _slots)
            {
                if (s.Camera != null) s.Camera.targetTexture = null;
                if (images.Length > s.Index && images[s.Index] != null) images[s.Index].texture = null;
                if (s.Texture != null)
                {
                    s.Texture.Release();
                    Destroy(s.Texture); // Release만으로는 객체가 남는다 (Codex 3D 코드 리뷰 2차 4번)
                }
            }

            _slots.Clear();
            if (_stage != null) Destroy(_stage.gameObject);
        }

        /// <summary>영웅 한 명의 초상 그림 (영웅 화면의 큰 초상이 같은 그림을 쓴다). 없으면 null</summary>
        public Texture TextureOf(int index) => index >= 0 && index < images.Length && images[index] != null ? images[index].texture : null;

        /// <summary>지금 고른(포커스) 영웅: 이 초상만 계속 움직인다</summary>
        public void SetFocus(int index)
        {
            Slot next = FindSlot(index);
            if (_focus == index && next != null) return;
            Slot previous = FindSlot(_focus);
            if (previous != null && previous.Animator != null) previous.Animator.enabled = false;
            _focus = index;
            bool created = next == null;
            if (created) next = EnsureSlot(index);
            if (next != null)
            {
                if (next.Animator != null) next.Animator.enabled = true;
                if (!created) Draw(next);
            }
            if (created) EnsureVisible();
            _nextLive = Time.unscaledTime + 1f / Mathf.Max(1f, liveFps);
        }

        /// <summary>한 영웅 초상만 보인다 (로비 "영웅" 버튼: 지금 영웅)</summary>
        public void ShowOnly(int index)
        {
            for (int i = 0; i < images.Length; i++)
            {
                if (images[i] != null) images[i].gameObject.SetActive(i == index);
            }

            SetFocus(index);
        }

        /// <summary>잠긴 영웅은 초상을 어둡게 (그림이 아직 없으면 그대로 투명)</summary>
        public void SetLocked(int index, bool locked)
        {
            if (index < 0 || index >= images.Length || images[index] == null) return;
            float alpha = images[index].texture == null ? 0f : 1f;
            images[index].color = locked ? new Color(0.6f, 0.6f, 0.68f, alpha) : new Color(1f, 1f, 1f, alpha); // 그림이 늦게 생겨도 빈 칸은 투명하게 둔다
            Transform badge = images[index].transform.Find("LockBack") ?? images[index].transform.Find("Lock"); // "잠김" 글 + 받침 띠 (어두운 옷이 아니라 잠긴 것으로 보이게)
            if (badge != null) badge.gameObject.SetActive(locked);
        }

        private void Update()
        {
            if (_focus < 0 || Time.unscaledTime < _nextLive) return;
            _nextLive = Time.unscaledTime + 1f / Mathf.Max(1f, liveFps);
            Draw(FindSlot(_focus));
        }

        /// <summary>
        /// 초상 한 장 그리기. 초상 빛은 그릴 때만 켜고, 장면의 다른 해(로비 무대 등)는 그동안 끈다
        /// → 로비 무대가 초상 빛을 받지 않고, 초상도 무대 해를 받지 않는다 (해는 위치와 상관없이 모든 것을 비춘다)
        /// </summary>
        private void Draw(Slot s)
        {
            if (s == null || s.Camera == null) return;
            _sceneLights.Clear();
            foreach (Light l in FindObjectsByType<Light>(FindObjectsSortMode.None))
            {
                if (l.enabled && l.type == LightType.Directional && System.Array.IndexOf(_lights, l) < 0)
                {
                    _sceneLights.Add(l);
                    l.enabled = false;
                }
            }

            foreach (Light l in _lights) l.enabled = true;
            s.Camera.Render();
            foreach (Light l in _lights) l.enabled = false;
            foreach (Light l in _sceneLights) l.enabled = true;
        }

        private void Build()
        {
            _stage = new GameObject("[HeroPortraits]").transform;
            _stage.position = stageOrigin;
            var sun = new GameObject("Light").AddComponent<Light>();
            sun.transform.SetParent(_stage, false);
            sun.type = LightType.Directional;
            sun.intensity = 1.2f;
            sun.color = new Color(1f, 0.96f, 0.9f);
            sun.transform.rotation = Quaternion.Euler(35f, 30f, 0f);
            // 뒤에서 비추는 차가운 테두리 빛: 어두운 옷(테슬라)도 배경과 갈린다 (화면 점검 2차)
            var rim = new GameObject("Rim").AddComponent<Light>();
            rim.transform.SetParent(_stage, false);
            rim.type = LightType.Directional;
            rim.intensity = 0.9f;
            rim.color = new Color(0.65f, 0.8f, 1f);
            rim.transform.rotation = Quaternion.Euler(20f, 200f, 0f);
            _lights = new[] { sun, rim };
            sun.enabled = false; // 그릴 때만 켠다 (Draw)
            rim.enabled = false;

            if (images.Length > 0 && images[0] != null)
            {
                _scroll = images[0].GetComponentInParent<ScrollRect>();
                if (_scroll != null) _scroll.onValueChanged.AddListener(OnScroll);
            }
        }

        private void OnScroll(Vector2 position) => EnsureVisible();

        private void EnsureVisible()
        {
            if (_scroll == null || _scroll.viewport == null) return;
            Rect view = _scroll.viewport.rect;
            int count = Mathf.Min(images.Length, roster != null ? roster.heroes.Count : 0);
            for (int i = 0; i < count; i++)
            {
                if (IsVisible(i, view)) EnsureSlot(i);
            }
        }

        private bool IsVisible(int index, Rect view)
        {
            if (images[index] == null || !images[index].gameObject.activeInHierarchy) return false;
            Bounds bounds = RectTransformUtility.CalculateRelativeRectTransformBounds(_scroll.viewport, images[index].rectTransform);
            return bounds.max.x >= view.xMin && bounds.min.x <= view.xMax;
        }

        private Slot FindSlot(int index)
        {
            foreach (Slot slot in _slots) if (slot.Index == index) return slot;
            return null;
        }

        private Slot EnsureSlot(int index)
        {
            if (index < 0 || index >= images.Length || roster == null || index >= roster.heroes.Count) return null;
            Slot existing = FindSlot(index);
            if (existing != null)
            {
                // 모바일에서 렌더 표면이 사라진 뒤에도 보이는 칸은 복구한다.
                if (existing.Texture != null && !existing.Texture.IsCreated()) Draw(existing);
                return existing;
            }
            HeroData hero = roster.heroes[index];
            if (hero == null || hero.model3D == null || images[index] == null) return null;
            if (_stage == null) Build();

            Vector3 at = stageOrigin + new Vector3(index * SlotGap, 0f, 0f);
            Transform model = Instantiate(hero.model3D, at, Quaternion.Euler(0f, startYaw, 0f), _stage).transform;
            float raw = hero.model3D.TryGetComponent(out Combat.Model3DInfo info) && info.height > 0.01f ? info.height : 1.8f;
            model.localScale = Vector3.one * (modelHeight / raw);
            foreach (Renderer r in model.GetComponentsInChildren<Renderer>()) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            var animator = model.GetComponent<Animator>();
            if (animator != null)
            {
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate; // 고른 영웅은 수동 렌더 사이에도 움직인다
                animator.Update(0f); // 첫 장에 T자세가 찍히지 않게
            }

            var texture = new RenderTexture(textureSize, Mathf.RoundToInt(textureSize * 1.15f), 16, RenderTextureFormat.ARGB32) { name = $"Portrait_{hero.id}", antiAliasing = 2 };
            var cameraObject = new GameObject($"Camera {hero.id}");
            cameraObject.transform.SetParent(_stage, false);
            cameraObject.transform.position = at + new Vector3(0f, modelHeight * 0.55f, -modelHeight * 1.9f);
            cameraObject.transform.LookAt(at + new Vector3(0f, modelHeight * 0.52f, 0f));
            var cam = cameraObject.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0f, 0f, 0f, 0f);
            cam.fieldOfView = 30f;
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 12f; // 옆 칸 영웅(30 유닛 떨어짐)이 안 보이게
            cam.targetTexture = texture;
            cam.GetUniversalAdditionalCameraData().SetRenderer(1); // 3D 렌더러 (로비 기본은 2D)
            cam.enabled = false; // 늘 꺼 두고 Draw에서 손으로 그린다

            images[index].texture = texture;
            Color tint = images[index].color;
            images[index].color = new Color(tint.r, tint.g, tint.b, 1f);
            var slot = new Slot { Index = index, Camera = cam, Model = model, Animator = animator, Texture = texture, Yaw = startYaw };
            // 가로 영웅 줄에서는 손가락 끌기를 ScrollRect가 받아야 목록을 넘길 수 있다.
            if (_scroll == null)
            {
                var drag = images[index].GetComponent<PortraitDrag>();
                if (drag == null) drag = images[index].gameObject.AddComponent<PortraitDrag>();
                drag.Dragged = dx => Rotate(slot, dx);
            }
            _slots.Add(slot);
            Draw(slot);
            if (animator != null && index != _focus) animator.enabled = false;
            return slot;
        }

        private void Rotate(Slot slot, float deltaX)
        {
            if (slot.Model == null) return;
            slot.Yaw -= deltaX * 0.5f;
            slot.Model.rotation = Quaternion.Euler(0f, slot.Yaw, 0f);
            Draw(slot);
        }
    }
}
