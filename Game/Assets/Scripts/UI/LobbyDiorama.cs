using HistoryHeroes.Heroes;
using UnityEngine;

namespace HistoryHeroes.UI
{
    /// <summary>
    /// 로비 가운데 무대 (그래픽 평가 3번 + 조사 "아처로 2 로비": 구간 섬 위에 지금 영웅).
    /// 고른 구간의 섬 장식·바닥 색·해 색·배경색으로 바꾸고, 고른 영웅을 가운데 세운다 (가만히 숨 쉬는 동작).
    /// UI(화면 위 덮개) 뒤에서 로비 카메라가 직접 그린다.
    /// </summary>
    public sealed class LobbyDiorama : MonoBehaviour
    {
        [SerializeField] private Camera view;
        [SerializeField] private Light sun;
        [SerializeField] private Renderer floor;
        [SerializeField] private Transform heroSpot;
        [Tooltip("구간 1~5 장식 묶음")]
        [SerializeField] private GameObject[] chapterGroups;
        [SerializeField] private Color[] floorTints;
        [SerializeField] private Color[] sunColors;
        [SerializeField] private Color[] backgrounds;
        [Tooltip("영웅이 천천히 좌우로 돈다 (도)")]
        [SerializeField] private float swayDegrees = 14f;

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private MaterialPropertyBlock _block;
        private GameObject _hero;
        private string _heroId;
        private int _chapter = -1;

        public void Show(int chapter, HeroData hero)
        {
            if (chapter != _chapter)
            {
                _chapter = chapter;
                int i = Mathf.Clamp(chapter, 1, 5) - 1;
                if (chapterGroups != null)
                {
                    for (int g = 0; g < chapterGroups.Length; g++)
                    {
                        if (chapterGroups[g] != null) chapterGroups[g].SetActive(g == i);
                    }
                }

                if (floor != null && floorTints != null && i < floorTints.Length)
                {
                    _block ??= new MaterialPropertyBlock();
                    floor.GetPropertyBlock(_block);
                    Color baseColor = floor.sharedMaterial != null && floor.sharedMaterial.HasProperty(BaseColorId) ? floor.sharedMaterial.GetColor(BaseColorId) : Color.white;
                    _block.SetColor(BaseColorId, baseColor * floorTints[i]);
                    floor.SetPropertyBlock(_block);
                }

                if (sun != null && sunColors != null && i < sunColors.Length) sun.color = sunColors[i];
                if (view != null && backgrounds != null && i < backgrounds.Length) view.backgroundColor = backgrounds[i];
            }

            string id = hero != null ? hero.id : null;
            if (id == _heroId) return;
            _heroId = id;
            if (_hero != null) Destroy(_hero);
            _hero = null;
            if (hero == null || hero.model3D == null || heroSpot == null) return;
            // 영웅 모델은 위가 +Y, 앞이 -Z (카메라 쪽) → 그대로 세운다
            _hero = Instantiate(hero.model3D, heroSpot.position, Quaternion.identity, heroSpot);
            _hero.name = "Hero";
            foreach (Renderer r in _hero.GetComponentsInChildren<Renderer>()) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
        }

        private void Update()
        {
            if (_hero == null) return;
            // 가만히 서서 살짝 좌우로 몸을 돌린다 (정지 화면처럼 보이지 않게)
            float yaw = Mathf.Sin(Time.unscaledTime * 0.6f) * swayDegrees;
            _hero.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
        }
    }
}
