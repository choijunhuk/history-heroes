using System.Collections.Generic;
using HistoryHeroes.Heroes;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

namespace HistoryHeroes.UI
{
    /// <summary>
    /// 영웅 화면의 3D 초상 (Art3D-Plan 7번): 영웅마다 작은 카메라가 3D 모델을 그림(RenderTexture)으로 그려 줄의 오른쪽에 보인다.
    /// 끌면 그 영웅이 돈다. 화면이 열려 있을 때만 카메라가 돈다 (이 부품이 영웅 화면에 붙어 있어 같이 켜지고 꺼진다).
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

        private static readonly Vector3 StageOrigin = new Vector3(0f, -5000f, 0f);
        private const float SlotGap = 30f;

        private readonly List<Slot> _slots = new List<Slot>();
        private Transform _stage;

        private sealed class Slot
        {
            public Camera Camera;
            public Transform Model;
            public RenderTexture Texture;
            public float Yaw;
        }

        private void OnEnable()
        {
            if (_stage == null) Build();
            foreach (Slot s in _slots) s.Camera.enabled = true;
        }

        private void OnDisable()
        {
            foreach (Slot s in _slots)
            {
                if (s.Camera != null) s.Camera.enabled = false;
            }
        }

        private void OnDestroy()
        {
            foreach (Slot s in _slots)
            {
                if (s.Texture != null) s.Texture.Release();
            }

            if (_stage != null) Destroy(_stage.gameObject);
        }

        private void Build()
        {
            _stage = new GameObject("[HeroPortraits]").transform;
            _stage.position = StageOrigin;
            var sun = new GameObject("Light").AddComponent<Light>();
            sun.transform.SetParent(_stage, false);
            sun.type = LightType.Directional;
            sun.intensity = 1.2f;
            sun.color = new Color(1f, 0.96f, 0.9f);
            sun.transform.rotation = Quaternion.Euler(35f, 30f, 0f);

            int count = Mathf.Min(images.Length, roster != null ? roster.heroes.Count : 0);
            for (int i = 0; i < count; i++)
            {
                HeroData hero = roster.heroes[i];
                if (hero == null || hero.model3D == null || images[i] == null) continue;

                Vector3 at = StageOrigin + new Vector3(i * SlotGap, 0f, 0f);
                Transform model = Instantiate(hero.model3D, at, Quaternion.Euler(0f, startYaw, 0f), _stage).transform;
                float raw = hero.model3D.TryGetComponent(out Combat.Model3DInfo info) && info.height > 0.01f ? info.height : 1.8f;
                model.localScale = Vector3.one * (modelHeight / raw);
                foreach (Renderer r in model.GetComponentsInChildren<Renderer>()) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

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
                cam.enabled = false;

                images[i].texture = texture;
                images[i].color = Color.white;
                var slot = new Slot { Camera = cam, Model = model, Texture = texture, Yaw = startYaw };
                var drag = images[i].GetComponent<PortraitDrag>();
                if (drag == null) drag = images[i].gameObject.AddComponent<PortraitDrag>();
                drag.Dragged = dx => Rotate(slot, dx);
                _slots.Add(slot);
            }
        }

        /// <summary>잠긴 영웅은 초상을 어둡게 (그림이 아직 없으면 그대로 투명)</summary>
        public void SetLocked(int index, bool locked)
        {
            if (index < 0 || index >= images.Length || images[index] == null || images[index].texture == null) return;
            images[index].color = locked ? new Color(0.32f, 0.32f, 0.38f, 1f) : Color.white;
        }

        private static void Rotate(Slot slot, float deltaX)
        {
            if (slot.Model == null) return;
            slot.Yaw -= deltaX * 0.5f;
            slot.Model.rotation = Quaternion.Euler(0f, slot.Yaw, 0f);
        }
    }
}
