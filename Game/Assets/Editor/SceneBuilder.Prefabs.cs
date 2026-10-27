using HistoryHeroes.Combat;
using HistoryHeroes.Enemy;
using HistoryHeroes.Progress;
using HistoryHeroes.Skills;
using UnityEditor;
using UnityEngine;

namespace HistoryHeroes.EditorTools
{
    public static partial class SceneBuilder
    {
        private sealed class Prefabs
        {
            public Projectile PlayerArrow, EnemyArrow;
            public ExperienceGem Gem;
            public EnemyBase Charger, Archer, Bomber;
            public EnemyBase Shield, Splitter, SplitterSmall, Summoner, Minion;
            // 중간 보스 (Modes 2-8) · 보스맵 보스 (Content 3장)
            public EnemyBase MidBison, MidTorch, MidGolem, MidHunter, MidExecutor;
            public EnemyBase WraithLord, SteelColossus, ShadowTwins, OblivionKing;
            public EnemyBase RiftWarden;
            public Projectile Bullet, Orb, SwordWave;
            public ActiveSkillBehaviour KhanVolley, KhanStorm, GuanSpin, GuanFive, NapoCannon, NapoAusterlitz, TeslaCoil, TeslaTower;
            public Projectile IceShardProjectile;
            public ActiveSkillBehaviour IceShard, PoisonCloud, GuardShield, Boomerang, Whirlwind, Meteor;
            public ActiveSkillBehaviour FlameTrail, BlizzardShard, ToxicBloom, IronWall, ThunderGod, TwinBoomerang, Blizzard, HeavenBow;
            public ActiveSkillBehaviour SpinBlade, FlameField, Lightning, ArrowRain, SteelWhirl, ThunderFire;
        }

        private static Prefabs CreatePrefabs(Data d)
        {
            var p = new Prefabs
            {
                PlayerArrow = ProjectilePrefab("Arrow_Player", d.Khan.color, new Vector2(0.55f, 0.12f), 0.15f, 3f, fx: Fx.Arrow),
                EnemyArrow = ProjectilePrefab("Arrow_Enemy", new Color(0.95f, 0.2f, 0.55f), new Vector2(0.45f, 0.16f), 0.15f, 4f, fx: Fx.EnemyArrow),
                Gem = GemPrefab(),
                // 영웅별 기본 공격 (Heroes.md): 나폴레옹 총알, 테슬라 전기 구체, 관우 검기 (벽 튕김 패시브)
                Bullet = ProjectilePrefab("Bullet_Napoleon", new Color(1f, 0.85f, 0.4f), new Vector2(0.32f, 0.14f), 0.12f, 2.5f, fx: Fx.Bullet),
                Orb = ProjectilePrefab("Orb_Tesla", new Color(0.55f, 0.9f, 1f), new Vector2(0.34f, 0.34f), 0.17f, 3f, _circle, Fx.Orb),
                SwordWave = ProjectilePrefab("SwordWave_Guan", new Color(0.85f, 1f, 0.9f, 0.85f), new Vector2(0.2f, 0.8f), 0.3f, 1.2f, fx: Fx.Wave),
            };

            Material line = AssetDatabase.GetBuiltinExtraResource<Material>("Sprites-Default.mat");
            p.Charger = EnemyPrefab<ChargerEnemy>("Enemy_Charger", d.Charger, _circle, line, withBlastMarker: false);
            p.Archer = EnemyPrefab<ArcherEnemy>("Enemy_Archer", d.Archer, _square, line, withBlastMarker: false);
            p.Bomber = EnemyPrefab<BomberEnemy>("Enemy_Bomber", d.Bomber, _circle, line, withBlastMarker: true);

            // v0.6 적 (Content 4장)
            p.Shield = EnemyPrefab<ShieldEnemy>("Enemy_Shield", d.Shield, _square, line, withBlastMarker: false, extra: (go, props) =>
            {
                // 앞을 막는 방패 (판자 모양). 방패를 내리면 흐려진다
                var shield = new GameObject("Shield");
                shield.transform.SetParent(go.transform, false);
                var sr = shield.AddComponent<SpriteRenderer>();
                sr.sprite = _square;
                sr.color = new Color(0.85f, 0.88f, 0.95f, 0.95f);
                sr.sortingOrder = 3;
                shield.transform.localScale = new Vector3(0.22f, 1.05f, 1f);
                props.Ref("shieldVisual", sr);
            });
            p.SplitterSmall = EnemyPrefab<SplitterEnemy>("Enemy_SplitterSmall", d.SplitterSmall, _circle, line, withBlastMarker: false);
            p.Splitter = EnemyPrefab<SplitterEnemy>("Enemy_Splitter", d.Splitter, _circle, line, withBlastMarker: false, extra: (go, props) => props.Ref("smallPrefab", p.SplitterSmall));
            p.Minion = EnemyPrefab<ChargerEnemy>("Enemy_Minion", d.Minion, _circle, line, withBlastMarker: false);
            p.Summoner = EnemyPrefab<SummonerEnemy>("Enemy_Summoner", d.Summoner, _square, line, withBlastMarker: false, extra: (go, props) => props.Ref("minion", p.Minion));
            CreateBossPrefabs(p, line);
            p.RiftWarden = EnemyPrefab<RiftWardenBoss>("Boss_RiftWarden", d.WardenEnemy, _circle, line, withBlastMarker: false, extra: (go, props) =>
            {
                // 내려찍기 예고 원 (보스를 따라가지 않게 Awake에서 떼어 낸다)
                props.Ref("slamMarker", ChildSprite(go, "SlamMarker", _circle, new Color(1f, 0.25f, 0.2f, 0.3f), 1f, -1));
                // 빈틈 표시: 발밑 노란 원 = "지금 멈춰서 쏘세요"
                props.Ref("openingRing", ChildSprite(go, "OpeningRing", _circle, new Color(1f, 0.9f, 0.25f, 0.35f), 1.6f, -1));
                // 파동 예고: 몸이 보라색으로 빛남
                props.Ref("waveGlow", ChildSprite(go, "WaveGlow", _circle, new Color(0.75f, 0.45f, 1f, 0.4f), 1.35f, 3));
                props.Mask("wallMask", Mask(WallLayer));
            });

            p.SpinBlade = Behaviour<OrbitBladesBehaviour>("Skill_SpinBlade", null);
            p.SteelWhirl = Behaviour<OrbitBladesBehaviour>("Skill_SteelWhirl", props => props
                .Bool("blockProjectiles", true)
                .Color("color", new Color(0.75f, 0.85f, 1f))
                .Vector2("bladeSize", new Vector2(0.6f, 0.22f)));
            p.FlameField = Behaviour<FlameFieldBehaviour>("Skill_FlameField", null);
            p.Lightning = Behaviour<LightningBehaviour>("Skill_Lightning", null);
            p.ThunderFire = Behaviour<LightningBehaviour>("Skill_ThunderFire", props => props
                .Bool("leaveField", true)
                .Color("boltColor", new Color(1f, 0.7f, 0.3f)));
            p.ArrowRain = Behaviour<ArrowRainBehaviour>("Skill_ArrowRain", null);

            // v0.3 공용 액티브·조합 (Skills.md 2장·4-4)
            p.IceShardProjectile = ProjectilePrefab("Shard_Ice", new Color(0.65f, 0.9f, 1f), new Vector2(0.4f, 0.12f), 0.13f, 2f, fx: Fx.Shard);
            p.IceShard = Behaviour<IceShardBehaviour>("Skill_IceShard", props => props.Ref("shardPrefab", p.IceShardProjectile));
            p.BlizzardShard = Behaviour<IceShardBehaviour>("Skill_BlizzardShard", props => props.Ref("shardPrefab", p.IceShardProjectile)
                .Bool("chill", true).Float("slow", 0.4f).Float("fanDegrees", 60f));
            p.PoisonCloud = Behaviour<PoisonCloudBehaviour>("Skill_PoisonCloud", null);
            p.ToxicBloom = Behaviour<PoisonCloudBehaviour>("Skill_ToxicBloom", props => props.Bool("bloom", true));
            p.GuardShield = Behaviour<GuardShieldBehaviour>("Skill_GuardShield", null);
            p.IronWall = Behaviour<GuardShieldBehaviour>("Skill_IronWall", props => props.Bool("explodeOnBreak", true)
                .Color("shieldColor", new Color(0.75f, 0.8f, 0.95f, 0.35f)));
            p.Boomerang = Behaviour<BoomerangBehaviour>("Skill_Boomerang", null);
            p.TwinBoomerang = Behaviour<BoomerangBehaviour>("Skill_TwinBoomerang", props => props.Bool("split", true).Float("spreadDegrees", 18f));
            p.Whirlwind = Behaviour<WhirlwindBehaviour>("Skill_Whirlwind", null);
            p.Blizzard = Behaviour<WhirlwindBehaviour>("Skill_Blizzard", props => props.Bool("freezeAtEnd", true)
                .Color("color", new Color(0.8f, 0.95f, 1f, 0.45f)));
            p.Meteor = Behaviour<MeteorBehaviour>("Skill_Meteor", null);
            p.FlameTrail = Behaviour<FlameFieldBehaviour>("Skill_FlameTrail", props => props.Bool("trail", true));
            p.ThunderGod = Behaviour<LightningBehaviour>("Skill_ThunderGod", props => props.Int("chainTargets", 3)
                .Color("boltColor", new Color(0.8f, 0.85f, 1f)));
            p.HeavenBow = Behaviour<ArrowRainBehaviour>("Skill_HeavenBow", props => props.Float("returnCoefficient", 0.75f));

            // 영웅 전용 (Skills.md 3장·4-4)
            p.KhanVolley = Behaviour<VolleyBehaviour>("Skill_KhanVolley", null);
            p.KhanStorm = Behaviour<VolleyBehaviour>("Skill_KhanStorm", props => props.Int("bursts", 2).Float("fanDegrees", 70f));
            p.GuanSpin = Behaviour<SpinSlashBehaviour>("Skill_GuanSpin", null);
            p.GuanFive = Behaviour<SpinSlashBehaviour>("Skill_GuanFive", props => props.Int("spins", 5).Float("guardWhileSpinning", 0.5f)
                .Color("slashColor", new Color(1f, 0.9f, 0.4f, 0.6f)));
            p.NapoCannon = Behaviour<CannonBarrageBehaviour>("Skill_NapoCannon", null);
            p.NapoAusterlitz = Behaviour<CannonBarrageBehaviour>("Skill_NapoAusterlitz", props => props.Bool("leaveField", true));
            p.TeslaCoil = Behaviour<TeslaCoilBehaviour>("Skill_TeslaCoil", null);
            p.TeslaTower = Behaviour<TeslaCoilBehaviour>("Skill_TeslaTower", props => props.Int("screenTargets", 5).Float("zapInterval", 1f)
                .Vector2("coilSize", new Vector2(0.9f, 2.2f)).Color("coilColor", new Color(0.6f, 0.5f, 1f)));
            return p;
        }

        private static Projectile ProjectilePrefab(string name, Color color, Vector2 size, float radius, float lifetime, Sprite sprite = null, Fx fx = Fx.None)
        {
            var go = new GameObject(name);
            go.transform.localScale = new Vector3(size.x, size.y, 1f);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite != null ? sprite : _square;
            sr.color = color;
            sr.sortingOrder = 5;
            var projectile = go.AddComponent<Projectile>();
            using (var props = new Props(projectile)) props.Float("radius", radius).Float("lifetime", lifetime);
            DressProjectile(go, size, fx); // 3D 모형 (Art3D-Plan 6번)
            return AssetUtil.SavePrefab<Projectile>(go, $"{PrefabDir}/{name}.prefab");
        }

        private static ExperienceGem GemPrefab()
        {
            var go = new GameObject("ExpGem") { layer = PickupLayer };
            go.transform.localScale = Vector3.one * 0.28f;
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = _circle;
            sr.color = new Color(0.35f, 1f, 0.75f);
            sr.sortingOrder = 1;
            go.AddComponent<ExperienceGem>();
            DressGem(go); // 3D 보석 (Art3D-Plan 6번)
            return AssetUtil.SavePrefab<ExperienceGem>(go, $"{PrefabDir}/ExpGem.prefab");
        }

        private static EnemyBase EnemyPrefab<T>(string name, EnemyData data, Sprite sprite, Material lineMaterial, bool withBlastMarker,
            System.Action<GameObject, Props> extra = null)
            where T : EnemyBase
        {
            var go = new GameObject(name) { layer = EnemyLayer };
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.color = data.color;
            sr.sortingOrder = 2;

            go.AddComponent<CircleCollider2D>().radius = 0.45f;
            var body = go.AddComponent<Rigidbody2D>();
            body.gravityScale = 0f;
            body.freezeRotation = true;
            go.AddComponent<Health>();
            using (var flash = new Props(go.AddComponent<HitFlash>())) flash.Ref("target", sr);

            // 3D 모델 (Art3D-Plan 4·5번): 임시 도형 몸은 숨긴 채 상태 이상 색만 읽는다. 모델이 아직 없으면 도형 그대로
            if (data.model3D != null)
            {
                // 그림자를 끈 적은 발밑에 옅은 그림자 원 (땅에 붙어 보이게, 2D 그림이라 250마리도 싸다 — Codex 화면 점검 6번)
                if (data.model3D.TryGetComponent(out Model3DInfo info) && !info.castShadows)
                {
                    SpriteRenderer blob = ChildSprite(go, "Blob", _circle, new Color(0f, 0f, 0f, 0.32f), 0.95f, -3);
                    blob.enabled = true;
                }

                using (var v = new Props(go.AddComponent<Visual3D>()))
                {
                    v.Ref("modelPrefab", data.model3D).Float("height", data.modelHeight).Float("runSpeed", Mathf.Max(1.5f, data.moveSpeed * 1.6f))
                        .Refs("hideRenderers", new Object[] { sr }).Ref("tintSource", sr);
                }
            }

            T enemy = go.AddComponent<T>();

            // 공격 예고선 (돌진 방향, 조준선)
            var lineObject = new GameObject("Telegraph");
            lineObject.transform.SetParent(go.transform, false);
            var line = lineObject.AddComponent<LineRenderer>();
            line.useWorldSpace = true;
            line.positionCount = 2;
            line.widthMultiplier = 0.08f;
            line.sharedMaterial = lineMaterial;
            line.startColor = line.endColor = new Color(1f, 0.15f, 0.15f, 0.75f);
            line.sortingOrder = 4;
            line.enabled = false;

            using (var props = new Props(enemy))
            {
                props.Ref("data", data).Mask("playerMask", Mask(PlayerLayer)).Ref("telegraphLine", line);

                if (withBlastMarker)
                {
                    // 투척병: 폭탄이 떨어질 자리 표시
                    var markerObject = new GameObject("BlastMarker");
                    markerObject.transform.SetParent(go.transform, false);
                    var marker = markerObject.AddComponent<SpriteRenderer>();
                    marker.sprite = _circle;
                    marker.color = new Color(1f, 0.2f, 0.2f, 0.35f);
                    marker.sortingOrder = -1;
                    marker.enabled = false;
                    props.Ref("blastMarker", marker);
                }

                extra?.Invoke(go, props);
            }

            return AssetUtil.SavePrefab<EnemyBase>(go, $"{PrefabDir}/Enemies/{name}.prefab");
        }

        private static SpriteRenderer ChildSprite(GameObject parent, string name, Sprite sprite, Color color, float size, int order)
        {
            var child = new GameObject(name);
            child.transform.SetParent(parent.transform, false);
            child.transform.localScale = Vector3.one * size;
            var sr = child.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.color = color;
            sr.sortingOrder = order;
            sr.enabled = false;
            return sr;
        }

        private static ActiveSkillBehaviour Behaviour<T>(string name, System.Action<Props> configure) where T : ActiveSkillBehaviour
        {
            var go = new GameObject(name);
            T behaviour = go.AddComponent<T>();
            if (configure != null)
            {
                using (var props = new Props(behaviour)) configure(props);
            }

            return AssetUtil.SavePrefab<ActiveSkillBehaviour>(go, $"{PrefabDir}/Skills/{name}.prefab");
        }
    }
}
