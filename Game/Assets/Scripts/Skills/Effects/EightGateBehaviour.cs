using System.Collections;
using System.Collections.Generic;
using HistoryHeroes.Combat;
using HistoryHeroes.Common;
using HistoryHeroes.Enemy;
using UnityEngine;

namespace HistoryHeroes.Skills
{
    /// <summary>
    /// 제갈량 전용 액티브 "팔진도" (확장: 새 영웅): 10초마다 영웅 둘레에 돌기둥 8개를 원으로 4초 세운다 (레벨마다 지속 +0.5초).
    /// 돌기둥에 닿는 적 탄은 사라지고, 진 안으로 들어온 적은 1초 헤맨다 (70% 느려짐 + 비틀거림, 적마다 한 번).
    /// 전용 조합 "칠성단의 바람" (합체: 팔진도 + 회오리): 진 안에 회오리 3개가 돌며 적을 가운데로 모으고 초당 40%.
    /// 돌기둥은 판정이 없다 (영웅·적·내 탄은 지나간다) — 막는 건 적 탄만.
    /// </summary>
    public sealed class EightGateBehaviour : ActiveSkillBehaviour
    {
        [SerializeField] private GameObject stoneModel;
        [SerializeField] private int stones = 8;
        [SerializeField] private float radius = 2.6f;
        [SerializeField] private float blockRadius = 0.6f;
        [SerializeField] private float confuseSeconds = 1f;
        [Header("칠성단의 바람")]
        [SerializeField] private bool whirlwinds;
        [SerializeField] private float pullSpeed = 2.5f;
        [SerializeField] private float windDamagePerSecond = 0.4f;

        /// <summary>회오리의 초당 피해 (공격력 비율, 1레벨). 레벨업 카드가 "초당 피해 40% → 50%"를 보여 줄 때 쓴다</summary>
        public float WindDamagePerSecond => windDamagePerSecond;

        private readonly List<EnemyBase> _inside = new List<EnemyBase>();
        private readonly Dictionary<EnemyBase, int> _confused = new Dictionary<EnemyBase, int>(); // 적 → 헤매게 한 삶 (풀에서 다시 나오면 새 적)

        protected override bool Activate()
        {
            if (CombatQuery.Nearest(player.transform.position, 8f) == null) return false;
            StartCoroutine(Run(player.transform.position));
            return true;
        }

        private IEnumerator Run(Vector2 center)
        {
            _confused.Clear();
            var root = new GameObject(whirlwinds ? "SevenStarWind" : "EightGate").transform;
            root.SetParent(EffectRoot, false);
            root.position = center;
            var stoneList = new List<Transform>();
            var corners = new Vector2[stones];
            for (int i = 0; i < stones; i++)
            {
                float a = i * Mathf.PI * 2f / stones;
                var dir = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                // 벽 너머에 서지 않게: 벽 0.35 앞에서 멈춘다 (벽 가까이에서 쓰면 진이 벽 쪽으로 납작해진다)
                float reach = radius;
                RaycastHit2D wall = Physics2D.Raycast(center, dir, radius + 0.35f, player.WallMask);
                if (wall.collider != null) reach = Mathf.Max(0.5f, wall.distance - 0.35f);
                corners[i] = center + dir * reach;
                var s = new GameObject($"Stone{i}").transform;
                s.SetParent(root, false);
                s.position = corners[i];
                if (stoneModel != null) SkillArt.Attach(stoneModel, s, 1f);
                else
                {
                    var sr = s.gameObject.AddComponent<SpriteRenderer>();
                    sr.sprite = PlaceholderSprites.Circle;
                    sr.color = new Color(0.6f, 0.58f, 0.55f);
                    s.localScale = Vector3.one * 0.6f;
                }

                stoneList.Add(s);
            }

            // 바닥 팔각 문양 (돌기둥을 잇는 금빛 선 + 안쪽 옅은 선): 장애물이 아니라 "진"으로 읽히게 (화면 점검 4번)
            LineRenderer outer = Polygon(root, "Octagon", center, corners, 1f, new Color(1f, 0.85f, 0.4f, 0.75f), 0.09f);
            LineRenderer inner = Polygon(root, "OctagonInner", center, corners, 0.55f, new Color(1f, 0.9f, 0.6f, 0.4f), 0.05f);
            // 가운데 표식 (금빛 작은 고리, 두근): 진의 한가운데가 읽히게 (화면 점검 2차 3번)
            var heart = new GameObject("Center").transform;
            heart.SetParent(root, false);
            heart.position = new Vector3(center.x, center.y, -0.035f);
            var heartSprite = heart.gameObject.AddComponent<SpriteRenderer>();
            heartSprite.sprite = PlaceholderSprites.RimCircle;
            heartSprite.color = new Color(1f, 0.88f, 0.45f, 0.7f);
            heartSprite.sortingOrder = -3;
            // 칠성단의 바람: 가운데로 휘어 들어가는 바람 띠 3줄 (돈다)
            var arms = new List<LineRenderer>();
            if (whirlwinds)
            {
                for (int i = 0; i < 3; i++) arms.Add(Line(root, $"WindArm{i}", 14, new Color(0.85f, 0.98f, 1f, 0.6f), 0.12f));
            }

            var winds = new List<ParticleSystem>();
            if (whirlwinds)
            {
                for (int i = 0; i < 3; i++)
                {
                    var w = new GameObject($"Wind{i}").transform;
                    w.SetParent(root, false);
                    winds.Add(SkillArt.Embers(w, new Color(0.75f, 0.95f, 1f, 0.8f), 40f, 1.4f, 0.4f));
                }
            }

            Audio.AudioManager.Play("slam", 0.5f);
            float duration = data.DurationAt(level) > 0f ? data.DurationAt(level) : 4f;
            float coefficient = data.DamageAt(level);
            float t = 0f, nextTick = 0f;
            while (t < duration && player != null)
            {
                if (IsPaused)
                {
                    yield return null;
                    continue;
                }

                t += Time.deltaTime;
                // 솟아오름 (0.25초) — 위 = -Z, 바닥 밑에서 올라온다
                float rise = Mathf.Clamp01(t / 0.25f);
                for (int i = 0; i < stoneList.Count; i++)
                {
                    Transform s = stoneList[i];
                    Vector3 p = s.position;
                    s.position = new Vector3(p.x, p.y, Mathf.Lerp(1.2f, 0f, rise));
                    // 막으면 돌기둥 위에서 금빛으로 번쩍 (화면 점검 4번)
                    while (EnemyProjectiles.TryBlock(p, blockRadius)) FxBursts.Puff(p + Vector3.back * 1f, new Color(1.4f, 1.2f, 0.6f), 0.9f);
                }

                // 진 안의 적: 1초 헤맴 (적마다 한 번)
                CombatQuery.EnemiesWithin(center, radius, _inside);
                foreach (EnemyBase e in _inside)
                {
                    if (_confused.TryGetValue(e, out int life) && life == e.Health.Life) continue;
                    _confused[e] = e.Health.Life;
                    e.Status.ApplySlow(0.7f, confuseSeconds);
                    if (e.CanBeKnockedBack && e.TryGetComponent(out Rigidbody2D rb)) rb.MovePosition(rb.position + Random.insideUnitCircle * 0.6f);
                    FxBursts.Puff((Vector3)(Vector2)e.transform.position + Vector3.back * 1.1f, new Color(1f, 0.95f, 0.6f), 0.6f);
                }

                float fade = Mathf.Clamp01(t / 0.3f) * Mathf.Clamp01((duration - t) / 0.4f);
                SetAlpha(outer, 0.75f * fade);
                heart.localScale = Vector3.one * (0.7f + 0.12f * Mathf.Sin(t * 6f));
                heartSprite.color = new Color(1f, 0.88f, 0.45f, 0.7f * fade);
                SetAlpha(inner, 0.4f * fade);
                for (int i = 0; i < arms.Count; i++) Spiral(arms[i], center, radius * 0.95f, t * 2.4f + i * Mathf.PI * 2f / 3f, 0.6f * fade);

                if (whirlwinds)
                {
                    for (int i = 0; i < winds.Count; i++)
                    {
                        float a = t * 2.2f + i * Mathf.PI * 2f / 3f;
                        winds[i].transform.position = (Vector3)(center + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * radius * 0.45f);
                    }

                    // 회오리와 같은 끌기 (보스도 30% 빠르기로 끌린다, 코드 검토 6번)
                    foreach (EnemyBase e in _inside) e.Status.Pull(center, pullSpeed, 0.15f);

                    if (t >= nextTick)
                    {
                        nextTick = t + 0.5f;
                        float scale = coefficient / Mathf.Max(0.01f, data.DamageAt(1));
                        foreach (EnemyBase e in _inside) e.Health.TryTakeDamage(RollDamage(windDamagePerSecond * 0.5f * scale));
                    }
                }

                yield return null;
            }

            foreach (Transform s in stoneList) FxBursts.Puff(s.position + Vector3.back * 0.4f, new Color(0.7f, 0.68f, 0.62f), 0.8f);
            Destroy(root.gameObject);
        }

        private static LineRenderer Line(Transform parent, string name, int points, Color color, float width)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var lr = go.AddComponent<LineRenderer>();
            lr.useWorldSpace = true;
            lr.positionCount = points;
            lr.startWidth = width;
            lr.endWidth = width;
            lr.startColor = color;
            lr.endColor = color;
            lr.numCapVertices = 2;
            lr.alignment = LineAlignment.TransformZ; // 바닥에 눕는다
            go.transform.rotation = Quaternion.identity;
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            lr.receiveShadows = false;
            lr.sortingOrder = -3;
            if (SkillArt.Get != null && SkillArt.Get.trail != null) lr.sharedMaterial = SkillArt.Get.trail;
            return lr;
        }

        /// <summary>돌기둥 자리를 잇는 다각형 (scale = 가운데에서 몇 배 거리)</summary>
        private static LineRenderer Polygon(Transform root, string name, Vector2 center, Vector2[] corners, float scale, Color color, float width)
        {
            LineRenderer lr = Line(root, name, corners.Length, color, width);
            lr.loop = true;
            for (int i = 0; i < corners.Length; i++)
            {
                Vector2 p = center + (corners[i] - center) * scale;
                lr.SetPosition(i, new Vector3(p.x, p.y, -0.03f));
            }

            return lr;
        }

        /// <summary>바깥(r)에서 가운데로 반 바퀴 휘어 들어가는 바람 띠. 가운데 쪽이 옅다</summary>
        private static void Spiral(LineRenderer lr, Vector2 center, float r, float angle, float alpha)
        {
            int n = lr.positionCount;
            for (int i = 0; i < n; i++)
            {
                float k = i / (float)(n - 1);
                float a = angle + k * Mathf.PI * 1.1f;
                float rr = Mathf.Lerp(r, 0.25f, k);
                lr.SetPosition(i, new Vector3(center.x + Mathf.Cos(a) * rr, center.y + Mathf.Sin(a) * rr, -0.04f));
            }

            Color s = lr.startColor, e = lr.endColor;
            s.a = alpha;
            e.a = alpha * 0.2f;
            lr.startColor = s;
            lr.endColor = e;
        }

        private static void SetAlpha(LineRenderer lr, float alpha)
        {
            Color c = lr.startColor;
            c.a = alpha;
            lr.startColor = c;
            lr.endColor = c;
        }
    }
}
