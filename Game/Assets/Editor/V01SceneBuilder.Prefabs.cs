using HistoryHeroes.Combat;
using HistoryHeroes.Enemy;
using HistoryHeroes.Progress;
using HistoryHeroes.Skills;
using UnityEditor;
using UnityEngine;

namespace HistoryHeroes.EditorTools
{
    public static partial class V01SceneBuilder
    {
        private sealed class Prefabs
        {
            public Projectile PlayerArrow, EnemyArrow;
            public ExperienceGem Gem;
            public EnemyBase Charger, Archer, Bomber;
            public ActiveSkillBehaviour SpinBlade, FlameField, Lightning, ArrowRain, SteelWhirl, ThunderFire;
        }

        private static Prefabs CreatePrefabs(Data d)
        {
            var p = new Prefabs
            {
                PlayerArrow = ProjectilePrefab("Arrow_Player", d.Khan.color, new Vector2(0.55f, 0.12f), 0.15f, 3f),
                EnemyArrow = ProjectilePrefab("Arrow_Enemy", new Color(0.95f, 0.2f, 0.55f), new Vector2(0.45f, 0.16f), 0.15f, 4f),
                Gem = GemPrefab(),
            };

            Material line = AssetDatabase.GetBuiltinExtraResource<Material>("Sprites-Default.mat");
            p.Charger = EnemyPrefab<ChargerEnemy>("Enemy_Charger", d.Charger, _circle, line, withBlastMarker: false);
            p.Archer = EnemyPrefab<ArcherEnemy>("Enemy_Archer", d.Archer, _square, line, withBlastMarker: false);
            p.Bomber = EnemyPrefab<BomberEnemy>("Enemy_Bomber", d.Bomber, _circle, line, withBlastMarker: true);

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
            return p;
        }

        private static Projectile ProjectilePrefab(string name, Color color, Vector2 size, float radius, float lifetime)
        {
            var go = new GameObject(name);
            go.transform.localScale = new Vector3(size.x, size.y, 1f);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = _square;
            sr.color = color;
            sr.sortingOrder = 5;
            var projectile = go.AddComponent<Projectile>();
            using (var props = new Props(projectile)) props.Float("radius", radius).Float("lifetime", lifetime);
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
            return AssetUtil.SavePrefab<ExperienceGem>(go, $"{PrefabDir}/ExpGem.prefab");
        }

        private static EnemyBase EnemyPrefab<T>(string name, EnemyData data, Sprite sprite, Material lineMaterial, bool withBlastMarker)
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
            }

            return AssetUtil.SavePrefab<EnemyBase>(go, $"{PrefabDir}/Enemies/{name}.prefab");
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
