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
            for (int i = 0; i < stones; i++)
            {
                float a = i * Mathf.PI * 2f / stones;
                var s = new GameObject($"Stone{i}").transform;
                s.SetParent(root, false);
                s.position = center + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * radius;
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
                    while (EnemyProjectiles.TryBlock(p, blockRadius)) FxBursts.Puff(p + Vector3.back * 0.8f, new Color(0.9f, 0.85f, 0.6f), 0.5f);
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
    }
}
