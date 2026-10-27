using System.Collections.Generic;
using HistoryHeroes.Combat;
using HistoryHeroes.Common;
using UnityEngine;

namespace HistoryHeroes.Enemy
{
    /// <summary>
    /// 수정 기둥 보호막 (확장 1차 5장): 네 귀퉁이 수정 기둥(체력 = 보스 최대체력 3%)이 보스 받는 피해를 75% 줄인다.
    /// 기둥 하나를 부술 때마다 25%씩 → 셋을 부수면 보호막이 사라진다 ("보호막 깨짐!"). 망각의 왕은 2단계에 부서진 기둥 2개가 다시 솟고,
    /// 무너지는 바닥이 더해진다. 기둥은 적 편 체력이라 영웅이 겨냥할 수 있다.
    /// </summary>
    public sealed class PillarShieldGimmick : BossGimmick
    {
        public const float PillarHp = 0.03f;
        public const float MaxShield = 0.75f;
        public const float PerPillar = 0.25f;

        private readonly List<GameObject> _objects = new List<GameObject>();
        private readonly GameObject[] _pillars = new GameObject[4];
        private int _broken;
        private bool _regrown;
        private SpriteRenderer _bubble;

        public override string Hint => BossGimmicks.HintFor(GimmickKind.PillarShield);
        public float Shield => Mathf.Max(0f, MaxShield - PerPillar * _broken);
        public int Broken => _broken;
        public IReadOnlyList<GameObject> Pillars => _pillars;

        protected override void OnBegin()
        {
            _broken = 0;
            _regrown = false;
            for (int i = 0; i < 4; i++) _pillars[i] = SpawnPillar(i);
            var bubble = new GameObject("ShieldBubble").AddComponent<SpriteRenderer>();
            bubble.sprite = PlaceholderSprites.RimCircle;
            bubble.sortingOrder = 30;
            _bubble = bubble;
            _objects.Add(bubble.gameObject);
            if (boss is PatternBoss pattern) pattern.PhaseChanged += OnPhase;
            Apply();
        }

        protected override void OnEnd()
        {
            if (boss != null)
            {
                boss.Health.DamageScale = 1f;
                if (boss is PatternBoss pattern) pattern.PhaseChanged -= OnPhase;
            }

            BossGimmicks.DestroyAll(_objects);
            for (int i = 0; i < _pillars.Length; i++) _pillars[i] = null;
            _bubble = null;
            if (TryGetComponent(out CrumblingFloorGimmick floor)) floor.End();
        }

        private GameObject SpawnPillar(int corner)
        {
            var go = new GameObject($"CrystalPillar{corner}") { layer = LayerMask.NameToLayer("Enemy") };
            go.transform.position = arena.Corner(corner);
            go.AddComponent<CircleCollider2D>().radius = 0.45f;
            var health = go.AddComponent<Health>();
            health.Init(Team.Enemy, Mathf.Max(10f, boss.Health.Max * PillarHp));
            health.Died += _ => OnPillarBroken(corner);
            GameObject prefab = GimmickArt.Get() != null ? GimmickArt.Get().pillar : null;
            GameObject model = Dungeon.RoomDevices.FitModel(go.transform, prefab, 0.95f);
            if (model == null)
            {
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = PlaceholderSprites.Circle;
                sr.color = new Color(0.7f, 0.45f, 1f);
                go.transform.localScale = Vector3.one * 0.9f;
            }

            FxBursts.Puff(go.transform.position + Vector3.back * 0.6f, new Color(0.75f, 0.5f, 1f), 1.2f);
            _objects.Add(go);
            return go;
        }

        private void OnPillarBroken(int corner)
        {
            if (!Running) return;
            GameObject pillar = _pillars[corner];
            _pillars[corner] = null;
            _broken++;
            if (pillar != null)
            {
                FxBursts.Explosion(pillar.transform.position, 0.9f, new Color(0.75f, 0.5f, 1f));
                Destroy(pillar, 0.05f);
            }

            Apply();
            if (Shield <= 0f)
            {
                UI.HypeFx.Banner("보호막 깨짐!", new Color(0.8f, 0.6f, 1f));
                CameraShake.Add(0.3f);
            }
            else BossGimmicks.Notice?.Invoke($"보호막 {Mathf.RoundToInt(Shield * 100f)}%");
        }

        /// <summary>2단계: 부서진 기둥 2개가 다시 솟는다 (망각의 왕) + 무너지는 바닥</summary>
        private void OnPhase(int phase)
        {
            if (!Running || phase < 2 || _regrown || !(boss is OblivionKingBoss)) return;
            _regrown = true;
            int regrow = Mathf.Min(2, _broken);
            for (int i = 0; i < 4 && regrow > 0; i++)
            {
                if (_pillars[i] != null) continue;
                _pillars[i] = SpawnPillar(i);
                _broken--;
                regrow--;
            }

            Apply();
            BossGimmicks.Notice?.Invoke("기둥이 다시 솟았다! 바닥도 무너지기 시작해요");
            CrumblingFloorGimmick floor = TryGetComponent(out CrumblingFloorGimmick f) ? f : gameObject.AddComponent<CrumblingFloorGimmick>();
            floor.Begin(boss, arena);
        }

        private void Apply()
        {
            if (boss != null) boss.Health.DamageScale = 1f - Shield;
        }

        private void LateUpdate()
        {
            if (!Running || _bubble == null || boss == null) return;
            float s = Shield;
            _bubble.enabled = s > 0f;
            _bubble.transform.position = boss.transform.position + Vector3.back * 0.5f;
            _bubble.transform.localScale = Vector3.one * (Mathf.Max(1.6f, boss.transform.localScale.x * 1.1f) + 0.08f * Mathf.Sin(Time.time * 4f));
            _bubble.color = new Color(0.75f, 0.55f, 1f, 0.25f + 0.6f * s);
        }
    }
}
