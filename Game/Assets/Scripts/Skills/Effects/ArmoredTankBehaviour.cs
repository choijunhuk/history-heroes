using System.Collections;
using System.Collections.Generic;
using HistoryHeroes.Combat;
using HistoryHeroes.Common;
using HistoryHeroes.Enemy;
using UnityEngine;

namespace HistoryHeroes.Skills
{
    /// <summary>
    /// 다빈치 전용 액티브 "장갑 전차" (확장: 새 영웅): 원뿔 전차가 6초 동안 영웅 둘레를 돌며 0.6초마다 사방 8갈래로 쏜다 (각 30%, 레벨마다 지속 +1초).
    /// 전용 조합 "나선 비행기" (합체: 장갑 전차 + 회오리): 전차가 떠올라 회오리를 끌고 다닌다 — 반경 2.4 안 적을 가운데로 끌고 초당 50%.
    /// </summary>
    public sealed class ArmoredTankBehaviour : ActiveSkillBehaviour
    {
        [SerializeField] private GameObject tankModel;
        [SerializeField] private float orbitRadius = 2.2f;
        [SerializeField] private float orbitSpeed = 1.6f;
        [SerializeField] private float volleyEvery = 0.6f;
        [SerializeField] private int barrels = 8;
        [SerializeField] private float shotSpeed = 12f;
        [Header("나선 비행기")]
        [SerializeField] private bool spiral;
        [SerializeField] private float pullRadius = 2.4f;
        [SerializeField] private float pullSpeed = 2.2f;
        [SerializeField] private float spiralDamagePerSecond = 0.5f;
        [SerializeField] private float liftHeight = 1.4f;

        private readonly List<EnemyBase> _near = new List<EnemyBase>();
        private bool _running;

        protected override bool Activate()
        {
            if (_running) return false;
            if (CombatQuery.Nearest(player.transform.position, 9f) == null) return false;
            StartCoroutine(Run());
            return true;
        }

        private IEnumerator Run()
        {
            _running = true;
            var tank = new GameObject(spiral ? "SpiralPlane" : "ArmoredTank").transform;
            tank.SetParent(EffectRoot, false);
            tank.position = player.transform.position;
            GameObject model = tankModel != null ? SkillArt.Attach(tankModel, tank, 1.2f) : null;
            if (model == null)
            {
                var sr = tank.gameObject.AddComponent<SpriteRenderer>();
                sr.sprite = PlaceholderSprites.Circle;
                sr.color = new Color(0.45f, 0.32f, 0.2f);
                tank.localScale = Vector3.one * 1.1f;
            }

            ParticleSystem swirl = spiral ? SkillArt.Embers(tank, new Color(0.8f, 0.9f, 1f, 0.8f), 30f, 1.2f, 0.35f) : null;
            // 나선 비행기: 바닥에 끌어당기는 범위 (청록 테두리, 빙글) + 떠 있는 그림자 (화면 점검 5번 "떠서 끌어당기는 모습")
            SpriteRenderer pullRing = null, shadow = null;
            if (spiral)
            {
                pullRing = FloorSprite("PullRange", PlaceholderSprites.RimCircle, new Color(0.55f, 0.95f, 1f, 0.45f), pullRadius * 2f);
                shadow = FloorSprite("Shadow", PlaceholderSprites.Circle, new Color(0f, 0f, 0f, 0.3f), 1.1f);
            }
            float duration = data.DurationAt(level) > 0f ? data.DurationAt(level) : 6f;
            float coefficient = data.DamageAt(level);
            float t = 0f, nextVolley = 0.2f, nextTick = 0f;
            float phase = Random.value * Mathf.PI * 2f;
            FxBursts.Puff(tank.position + Vector3.back * 0.4f, new Color(0.9f, 0.8f, 0.6f), 1.4f);
            while (t < duration && player != null)
            {
                if (IsPaused)
                {
                    yield return null;
                    continue;
                }

                t += Time.deltaTime;
                float a = phase + t * orbitSpeed;
                Vector2 center = player.transform.position;
                Vector2 dir = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                // 벽 너머로 나가지 않게: 벽까지 0.4 남기고 멈춘다 (코드 검토 7번: 벽 밖에서 쏜 탄이 버려지던 것)
                float reach = orbitRadius;
                RaycastHit2D wall = Physics2D.Raycast(center, dir, orbitRadius + 0.4f, player.WallMask);
                if (wall.collider != null) reach = Mathf.Max(0.3f, wall.distance - 0.4f);
                Vector2 pos = center + dir * reach;
                float lift = spiral ? -liftHeight - 0.15f * Mathf.Sin(t * 4f) : 0f; // 위 = -Z
                tank.position = new Vector3(pos.x, pos.y, lift);
                if (pullRing != null)
                {
                    pullRing.transform.position = new Vector3(pos.x, pos.y, -0.02f);
                    pullRing.transform.rotation = Quaternion.Euler(0f, 0f, -t * 120f);
                    pullRing.transform.localScale = Vector3.one * pullRadius * 2f * (0.94f + 0.06f * Mathf.Sin(t * 6f));
                }

                if (shadow != null) shadow.transform.position = new Vector3(pos.x, pos.y, -0.01f);
                tank.rotation = Quaternion.Euler(0f, 0f, t * (spiral ? 540f : 90f)); // 빙글 (전차는 천천히, 비행기는 빠르게)

                if (t >= nextVolley)
                {
                    nextVolley = t + volleyEvery;
                    for (int i = 0; i < barrels; i++)
                    {
                        float ang = i * Mathf.PI * 2f / barrels + t;
                        var shot = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang));
                        player.LaunchSkillProjectile(pos, shot, shotSpeed, RollDamage(coefficient), owner: this);
                    }

                    Audio.AudioManager.Play("shoot", 0.25f, 0.1f);
                }

                if (spiral)
                {
                    // 회오리: 둘레 적을 가운데로 끌고 0.5초마다 피해
                    CombatQuery.EnemiesWithin(pos, pullRadius, _near);
                    foreach (EnemyBase e in _near) e.Status.Pull(pos, pullSpeed, 0.15f); // 회오리와 같은 끌기 (보스 30%, 코드 검토 6번)

                    if (t >= nextTick)
                    {
                        nextTick = t + 0.5f;
                        foreach (EnemyBase e in _near) e.Health.TryTakeDamage(RollDamage(spiralDamagePerSecond * 0.5f * (coefficient / Mathf.Max(0.01f, data.DamageAt(1)))));
                    }
                }

                yield return null;
            }

            if (swirl != null) swirl.Stop();
            if (pullRing != null) Destroy(pullRing.gameObject);
            if (shadow != null) Destroy(shadow.gameObject);
            FxBursts.Puff(tank.position + Vector3.back * 0.4f, new Color(0.8f, 0.8f, 0.8f), 1.2f);
            Destroy(tank.gameObject);
            _running = false;
        }

        private SpriteRenderer FloorSprite(string name, Sprite sprite, Color color, float size)
        {
            var go = new GameObject(name);
            go.transform.SetParent(EffectRoot, false);
            go.transform.localScale = Vector3.one * size;
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.color = color;
            sr.sortingOrder = -3;
            return sr;
        }

        protected override void OnDestroy()
        {
            _running = false;
            base.OnDestroy();
        }
    }
}
