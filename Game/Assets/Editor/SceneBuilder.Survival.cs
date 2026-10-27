using HistoryHeroes.Common;
using HistoryHeroes.Survival;
using UnityEngine;

namespace HistoryHeroes.EditorTools
{
    /// <summary>
    /// 서바이벌 맵 (Modes 2-10): 80×80 열린 바닥 + 바깥 벽, 바위 무리 12곳 (높은 장애물: 이동·투사체 막음),
    /// 연못 3곳 (낮은 장애물: 이동만 막고 투사체는 통과 → Default 레이어). 배치는 고정 씨앗이라 빌드마다 같다.
    /// </summary>
    public static partial class SceneBuilder
    {
        private const float SurvivalHalf = 40f;

        private static SurvivalData CreateSurvivalData(Data d, Prefabs p)
        {
            var data = AssetUtil.LoadOrCreate<SurvivalData>($"{DataDir}/Survival/Survival_Main.asset");
            // 숫자는 SurvivalData 코드의 기본값이 기준 (빌드할 때마다 되돌린다 → 고칠 곳이 한 곳)
            var defaults = ScriptableObject.CreateInstance<SurvivalData>();
            UnityEditor.EditorUtility.CopySerialized(defaults, data);
            Object.DestroyImmediate(defaults);
            data.name = "Survival_Main"; // 기본값을 통째로 복사하면 이름도 비워진다
            data.mapHalfSize = SurvivalHalf;
            data.charger = p.Charger;
            data.archer = p.Archer;
            data.bomber = p.Bomber;
            data.bossPrefab = p.RiftWarden;
            data.bossName = d.RiftWarden.displayName + " (서바이벌)";
            UnityEditor.EditorUtility.SetDirty(data);
            return data;
        }

        /// <summary>서바이벌 맵을 survivalRoot 아래에 만든다. 플레이어 시작 자리를 돌려준다</summary>
        private static Transform BuildSurvivalMap(Transform survivalRoot)
        {
            // 바닥: 풀밭 한 장 (3D, Art3D-Plan 3번). 무늬가 카메라가 움직이는 걸 보여 준다 (옛 8×8 체크무늬 대신)
            float len = SurvivalHalf * 2f + 2f;
            DressFloor(Block("SurvivalFloor", survivalRoot, Vector2.zero, new Vector2(len, len), new Color(0.17f, 0.23f, 0.17f), -10, collider: false), _stage.Grass);

            Color wall = new Color(0.36f, 0.31f, 0.26f);
            DressWall(Block("Wall Left", survivalRoot, new Vector2(-SurvivalHalf - 0.5f, 0f), new Vector2(1f, len), wall, -5, collider: true), _stage.LongSide, _stage.LongEnd);
            DressWall(Block("Wall Right", survivalRoot, new Vector2(SurvivalHalf + 0.5f, 0f), new Vector2(1f, len), wall, -5, collider: true), _stage.LongSide, _stage.LongEnd);
            DressWall(Block("Wall Bottom", survivalRoot, new Vector2(0f, -SurvivalHalf - 0.5f), new Vector2(len, 1f), wall, -5, collider: true), _stage.LongSide, _stage.LongEnd);
            DressWall(Block("Wall Top", survivalRoot, new Vector2(0f, SurvivalHalf + 0.5f), new Vector2(len, 1f), wall, -5, collider: true), _stage.LongSide, _stage.LongEnd);

            var random = new System.Random(5);
            float Range(float min, float max) => min + (float)random.NextDouble() * (max - min);

            // 바위 무리 12곳: 동그란 바위 2~3개씩 (둥글어야 적이 미끄러져 돌아간다)
            var rocks = new GameObject("Rocks").transform;
            rocks.SetParent(survivalRoot, false);
            Color rock = new Color(0.42f, 0.4f, 0.38f);
            for (int i = 0; i < 12; i++)
            {
                Vector2 center = FreeSpot(random, 7f, SurvivalHalf - 4f);
                int pieces = 2 + random.Next(2);
                for (int j = 0; j < pieces; j++)
                {
                    float size = Range(1.1f, 2.2f);
                    Vector2 offset = new Vector2(Range(-1.2f, 1.2f), Range(-1.2f, 1.2f));
                    SpriteRenderer stone = Obstacle($"Rock {i}-{j}", rocks, center + offset, size, rock, -6, WallLayer);
                    Stage3D.Add(stone.transform, Stage3D.Shape.Rock, size * 0.55f, _stage.Rock, castShadows: true); // 3D 바위 (판정은 원 그대로)
                    stone.enabled = false;
                }
            }

            // 연못 3곳 (낮은 장애물)
            var ponds = new GameObject("Ponds").transform;
            ponds.SetParent(survivalRoot, false);
            Color water = new Color(0.2f, 0.38f, 0.62f, 0.9f);
            for (int i = 0; i < 3; i++)
            {
                Vector2 center = FreeSpot(random, 10f, SurvivalHalf - 7f);
                Obstacle($"Pond {i}", ponds, center, Range(4f, 6f), water, -8, 0);
            }

            var start = new GameObject("SurvivalPlayerStart").transform;
            start.SetParent(survivalRoot, false);
            start.position = Vector2.zero;
            return start;
        }

        /// <summary>가운데(시작 자리)에서 minFromCenter 이상 떨어진 자리</summary>
        private static Vector2 FreeSpot(System.Random random, float minFromCenter, float limit)
        {
            for (int attempt = 0; attempt < 50; attempt++)
            {
                var v = new Vector2(((float)random.NextDouble() * 2f - 1f) * limit, ((float)random.NextDouble() * 2f - 1f) * limit);
                if (v.magnitude >= minFromCenter) return v;
            }

            return new Vector2(limit, limit);
        }

        /// <summary>동그란 장애물 (원 콜라이더 → 적이 옆으로 미끄러져 지나간다)</summary>
        private static SpriteRenderer Obstacle(string name, Transform parent, Vector2 position, float diameter, Color color, int order, int layer)
        {
            SpriteRenderer sr = Visual(parent, name, _circle, color, Vector2.one * diameter, order);
            sr.transform.position = position;
            sr.gameObject.layer = layer;
            sr.gameObject.AddComponent<CircleCollider2D>().radius = 0.5f; // 스프라이트 지름 1 × 스케일
            return sr;
        }
    }
}
