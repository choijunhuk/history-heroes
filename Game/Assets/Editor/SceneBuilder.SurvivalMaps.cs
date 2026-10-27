using HistoryHeroes.Common;
using HistoryHeroes.Dungeon;
using HistoryHeroes.Survival;
using HistoryHeroes.UI;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;

namespace HistoryHeroes.EditorTools
{
    /// <summary>
    /// 서바이벌 큰 모드 맵 3개 (확장 1차 6-2장, 개발자: "뱀서류로도 하나… 모드를 두개정도로"):
    /// 1 균열의 들판 (80×80, 7분, 바위·연못·짚더미) · 2 무너진 성곽 도시 (80×80, 집 블록 골목·화로) · 3 증기 공장 (100×60, 컨베이어 3줄·증기).
    /// 맵마다 숫자(SurvivalData) + 지형 묶음 + 시작 자리. BattleBootstrap이 고른 맵만 켠다.
    /// </summary>
    public static partial class SceneBuilder
    {
        public struct SurvivalMaps
        {
            public SurvivalData[] Data;
            public GameObject[] Roots;
            public Transform[] Starts;
        }

        private static SurvivalData[] CreateSurvivalMaps(Data d, Prefabs p)
        {
            SurvivalData field = CreateSurvivalData(d, p); // 맵 1 = 예전 맵 (Survival_Main 그대로 → 참조가 끊기지 않는다)
            field.mapId = "field";
            field.mapName = "균열의 들판";
            field.mapBlurb = "바위 · 연못 · 짚더미 (부서지면 줍는 물건)";
            field.mapHalf = new Vector2(40f, 40f);
            field.duration = 420f; // 7분 (검토 9장 6번 시간표)
            field.eliteTimes = new[] { 105f, 270f };
            field.midBossTime = 210f;
            field.midBoss = p.MidBison;
            field.midBossName = "들소 망령";
            field.bossHpMultiplier = 110f; // 7분 판이라 10분 판(160)보다 낮게 [제안]
            field.mission = SurvivalMission.Haystacks;
            field.missionTarget = 10;
            field.braziers = false;
            field.lookKey = "survival";
            field.mapColor = new Color(0.35f, 0.68f, 0.38f);
            field.swarmTimes = new[] { 60f, 150f, 190f, 300f, 360f, 390f };
            field.swarmKinds = new[] { SwarmKind.Line, SwarmKind.Ring, SwarmKind.Flock, SwarmKind.March, SwarmKind.Ring, SwarmKind.Line };
            field.minion = p.Minion;
            field.shield = p.Shield;
            EditorUtility.SetDirty(field);

            SurvivalData city = MapData("city", d, p);
            city.mapName = "무너진 성곽 도시";
            city.mapBlurb = "무너진 집 블록이 만든 골목 · 화로";
            city.mapHalf = new Vector2(40f, 40f);
            city.midBoss = p.MidHunter;
            city.midBossName = "그림자 사냥꾼";
            city.bossPrefab = p.MidTorch;
            city.bossName = "횃불 기사 (서바이벌)";
            city.bossHpMultiplier = 60f;
            city.mission = SurvivalMission.Braziers;
            city.missionTarget = 8;
            city.braziers = true;
            city.lookKey = "ch2";
            city.mapColor = new Color(0.78f, 0.45f, 0.25f);
            EditorUtility.SetDirty(city);

            SurvivalData factory = MapData("factory", d, p);
            factory.mapName = "증기 공장";
            factory.mapBlurb = "맵을 가로지르는 컨베이어 띠 3줄 · 증기 구역";
            factory.mapHalf = new Vector2(50f, 30f);
            factory.midBoss = p.MidGolem;
            factory.midBossName = "증기 골렘";
            factory.bossPrefab = p.SteelColossus;
            factory.bossName = "강철 거상 (서바이벌)";
            factory.bossHpMultiplier = 3.2f; // 구간 보스는 원래 체력이 크다 [제안, 벤치로]
            factory.mission = SurvivalMission.ElitesBefore;
            factory.missionTarget = 3;
            factory.missionDeadline = 420f; // 검토(2장·6장) 1번: 엘리트는 2·4·6분 → 6분 전 3마리는 불가능 → 7분 전
            factory.braziers = true;
            factory.lookKey = "ch3";
            factory.mapColor = new Color(0.75f, 0.62f, 0.3f);
            EditorUtility.SetDirty(factory);
            return new[] { field, city, factory };
        }

        /// <summary>10분 맵 기본 (코드 기본값 + 적 + 엘리트 2·4·6·8분 + 중간 보스 5분)</summary>
        private static SurvivalData MapData(string id, Data d, Prefabs p)
        {
            var data = AssetUtil.LoadOrCreate<SurvivalData>($"{DataDir}/Survival/Survival_{id}.asset");
            var defaults = ScriptableObject.CreateInstance<SurvivalData>();
            EditorUtility.CopySerialized(defaults, data);
            Object.DestroyImmediate(defaults);
            data.name = "Survival_" + id;
            data.mapId = id;
            data.charger = p.Charger;
            data.archer = p.Archer;
            data.bomber = p.Bomber;
            data.minion = p.Minion;
            data.shield = p.Shield;
            data.midBossTime = 300f;
            return data;
        }

        private static SurvivalMaps BuildSurvivalMaps(Transform survivalRoot)
        {
            var maps = new SurvivalMaps { Roots = new GameObject[3], Starts = new Transform[3] };
            for (int i = 0; i < 3; i++)
            {
                var root = new GameObject("Map_" + SurvivalProgress.MapIds[i]).transform;
                root.SetParent(survivalRoot, false);
                maps.Roots[i] = root.gameObject;
                maps.Starts[i] = i == 0 ? BuildSurvivalMap(root) : i == 1 ? BuildCityMap(root) : BuildFactoryMap(root);
            }

            return maps;
        }

        // ---------------------------------------------------------------- 공통

        private static Material SurvivalMat(string name, string texture, Vector2 tiling, Color tint)
        {
            Material m = Art3DCharacters.Lit("Stage_" + name, texture, null, tint);
            m.SetTextureScale("_BaseMap", tiling);
            m.SetFloat("_Smoothness", 0.08f);
            EditorUtility.SetDirty(m);
            return m;
        }

        /// <summary>바닥 + 바깥 벽 4개 (half = 맵 절반 크기)</summary>
        private static void SurvivalBounds(Transform root, Vector2 half, Material floor, string tag)
        {
            Vector2 len = half * 2f + Vector2.one * 2f;
            DressFloor(Block("Floor", root, Vector2.zero, len, new Color(0.2f, 0.2f, 0.2f), -10, collider: false), floor);
            Material side = SurvivalMat(tag + "WallSide", "T_Wall_Stone", new Vector2(1f, len.y / 1.5f), Color.white);
            Material end = SurvivalMat(tag + "WallEnd", "T_Wall_Stone", new Vector2(len.x / 1.5f, 1f), Color.white);
            Color wall = new Color(0.36f, 0.31f, 0.26f);
            DressWall(Block("Wall Left", root, new Vector2(-half.x - 0.5f, 0f), new Vector2(1f, len.y), wall, -5, collider: true), side, end);
            DressWall(Block("Wall Right", root, new Vector2(half.x + 0.5f, 0f), new Vector2(1f, len.y), wall, -5, collider: true), side, end);
            DressWall(Block("Wall Bottom", root, new Vector2(0f, -half.y - 0.5f), new Vector2(len.x, 1f), wall, -5, collider: true), side, end);
            DressWall(Block("Wall Top", root, new Vector2(0f, half.y + 0.5f), new Vector2(len.x, 1f), wall, -5, collider: true), side, end);
        }

        /// <summary>둥근 판정 + 블렌더 소품 (바닥에서 본 크기를 footprint에 맞춘다)</summary>
        private static GameObject SolidProp(string name, Transform parent, Vector2 at, float radius, string prefab, float footprint, float turn)
        {
            var go = new GameObject(name) { layer = WallLayer };
            go.transform.SetParent(parent, false);
            go.transform.position = at;
            go.AddComponent<CircleCollider2D>().radius = radius;
            GameObject model = Art3DStage.Prefab(prefab);
            if (model != null)
            {
                var inst = (GameObject)Object.Instantiate(model, go.transform, false);
                inst.transform.localRotation = Quaternion.Euler(0f, 0f, turn);
                Bounds b = new Bounds(inst.transform.position, Vector3.zero);
                bool any = false;
                foreach (Renderer r in inst.GetComponentsInChildren<Renderer>())
                {
                    if (!any) b = r.bounds;
                    else b.Encapsulate(r.bounds);
                    any = true;
                }

                float size = any ? Mathf.Max(b.size.x, b.size.y) : 1f;
                if (size > 0.01f) inst.transform.localScale *= footprint / size;
            }

            return go;
        }

        // ---------------------------------------------------------------- 맵 2: 무너진 성곽 도시

        private static Transform BuildCityMap(Transform root)
        {
            var half = new Vector2(40f, 40f);
            SurvivalBounds(root, half, SurvivalMat("CityFloor", "T_Floor_Stone", (half * 2f + Vector2.one * 2f) / 6f, new Color(0.92f, 0.88f, 0.82f)), "City");

            // 집 블록: 12칸마다 7×7 블록 → 골목 폭 5. 가운데 4칸은 광장(시작 자리), 15%는 빈 터
            var houses = new GameObject("Houses").transform;
            houses.SetParent(root, false);
            var random = new System.Random(21);
            string[] kinds = { "Ruin_House_A", "Ruin_House_B", "Ruin_House_C", "Ruin_House_D" };
            float[] centers = { -30f, -18f, -6f, 6f, 18f, 30f };
            foreach (float x in centers)
            {
                foreach (float y in centers)
                {
                    if (Mathf.Abs(x) < 7f && Mathf.Abs(y) < 7f) continue;
                    if (random.NextDouble() < 0.15) continue;
                    // 판정은 둥글게 (적이 모서리에 끼지 않고 미끄러져 돈다)
                    SolidProp("House", houses, new Vector2(x, y), 3.3f, kinds[random.Next(kinds.Length)], 6.8f, random.Next(4) * 90f);
                }
            }

            // 잔해 장식 (판정 없음)
            GameObject rubble = Art3DStage.Prefab("Deco_Rubble");
            if (rubble != null)
            {
                for (int i = 0; i < 40; i++)
                {
                    var go = (GameObject)Object.Instantiate(rubble, houses, false);
                    float gx = centers[random.Next(centers.Length)] + 6f * (random.NextDouble() < 0.5 ? -1f : 1f);
                    float gy = (float)(random.NextDouble() * 2.0 - 1.0) * 36f;
                    go.transform.position = random.NextDouble() < 0.5 ? new Vector3(gx, gy, 0f) : new Vector3(gy, gx, 0f);
                    go.transform.rotation = Quaternion.Euler(0f, 0f, (float)random.NextDouble() * 360f);
                }
            }

            var start = new GameObject("PlayerStart").transform;
            start.SetParent(root, false);
            start.position = Vector2.zero;
            return start;
        }

        // ---------------------------------------------------------------- 맵 3: 증기 공장

        private static Transform BuildFactoryMap(Transform root)
        {
            var half = new Vector2(50f, 30f);
            SurvivalBounds(root, half, SurvivalMat("FactoryFloor", "T_Floor_Stone", (half * 2f + Vector2.one * 2f) / 6f, new Color(0.85f, 0.78f, 0.66f)), "Factory");
            var random = new System.Random(33);

            // 컨베이어 띠 3줄 (맵을 가로지른다, 방향은 번갈아)
            var belts = new GameObject("Belts").transform;
            belts.SetParent(root, false);
            float[] lanes = { -19f, 1f, 19f };
            GameObject segment = Art3DStage.Prefab("Conveyor_Segment");
            for (int i = 0; i < lanes.Length; i++)
            {
                float angle = i % 2 == 0 ? 0f : 180f;
                Hazard(belts, $"Belt{i}", new Vector2(0f, lanes[i]), new Vector2(half.x * 2f - 4f, 3f), angle, TrapZone.Effect.Conveyor, 0f);
                if (segment == null) continue;
                for (float x = -half.x + 3.5f; x <= half.x - 3.5f; x += 3f)
                {
                    var seg = (GameObject)Object.Instantiate(segment, belts, false);
                    seg.transform.position = new Vector3(x, lanes[i], 0.01f);
                    seg.transform.rotation = Quaternion.Euler(0f, 0f, angle);
                    FitFootprint(seg, 3f);
                }
            }

            // 증기 구역 8곳 (띠·시작 자리 밖)
            for (int i = 0; i < 8; i++)
            {
                Vector2 at = FactorySpot(random, half, lanes, 2.5f);
                Hazard(belts, $"Steam{i}", at, new Vector2(2.6f, 2.6f), 0f, TrapZone.Effect.Steam, i * 0.7f);
            }

            // 기둥 18개 · 보일러 5개 (둥근 판정)
            var props = new GameObject("Machines").transform;
            props.SetParent(root, false);
            for (int i = 0; i < 18; i++) SolidProp("Pipe", props, FactorySpot(random, half, lanes, 2.2f), 0.7f, "Pipe_Column", 1.5f, random.Next(4) * 90f);
            for (int i = 0; i < 5; i++) SolidProp("Boiler", props, FactorySpot(random, half, lanes, 3.5f), 1.6f, "Boiler", 3.4f, random.Next(4) * 90f);

            var start = new GameObject("PlayerStart").transform;
            start.SetParent(root, false);
            start.position = new Vector2(0f, -9f); // 띠 사이
            return start;
        }

        private static Vector2 FactorySpot(System.Random random, Vector2 half, float[] lanes, float clearance)
        {
            for (int attempt = 0; attempt < 60; attempt++)
            {
                var p = new Vector2((float)(random.NextDouble() * 2.0 - 1.0) * (half.x - 4f), (float)(random.NextDouble() * 2.0 - 1.0) * (half.y - 4f));
                if ((p - new Vector2(0f, -9f)).magnitude < 7f) continue;
                bool onBelt = false;
                foreach (float lane in lanes) onBelt |= Mathf.Abs(p.y - lane) < 1.5f + clearance;
                if (!onBelt) return p;
            }

            return new Vector2(half.x - 6f, -half.y + 6f);
        }

        private static void FitFootprint(GameObject go, float footprint)
        {
            Renderer[] rs = go.GetComponentsInChildren<Renderer>();
            if (rs.Length == 0) return;
            Bounds b = rs[0].bounds;
            foreach (Renderer r in rs) b.Encapsulate(r.bounds);
            float size = Mathf.Max(b.size.x, b.size.y);
            if (size > 0.01f) go.transform.localScale *= footprint / size;
        }

        /// <summary>고정 함정 하나 (TrapZone + 바닥 그림 + 시작 때 켜는 SurvivalHazard)</summary>
        private static void Hazard(Transform parent, string name, Vector2 at, Vector2 size, float angle, TrapZone.Effect effect, float phase)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = at;
            var areaObject = new GameObject("Area");
            areaObject.transform.SetParent(go.transform, false);
            var sr = areaObject.AddComponent<SpriteRenderer>();
            sr.sprite = _square;
            sr.sortingOrder = -9;
            var zone = go.AddComponent<TrapZone>();
            using (var props = new Props(zone)) props.Ref("area", sr);
            var hazard = go.AddComponent<SurvivalHazard>();
            using (var props = new Props(hazard))
            {
                props.Int("effect", (int)effect).Vector2("size", size).Float("angle", angle).Float("damage", 40f).Float("phase", phase);
            }
        }

        // ---------------------------------------------------------------- 로비: 맵 고르기

        private static SurvivalMapPanel BuildSurvivalMapPanel(Transform parent)
        {
            var maps = new SurvivalData[SurvivalProgress.MapIds.Length];
            for (int i = 0; i < maps.Length; i++)
            {
                string file = i == 0 ? "Survival_Main" : "Survival_" + SurvivalProgress.MapIds[i];
                maps[i] = AssetDatabase.LoadAssetAtPath<SurvivalData>($"{DataDir}/Survival/{file}.asset");
            }

            RectTransform holder = Rect("SurvivalMaps", parent, Vector2.zero, Vector2.one, Center, Vector2.zero, Vector2.zero);
            var panel = holder.gameObject.AddComponent<SurvivalMapPanel>();
            RectTransform dim = Rect("Panel", holder, Vector2.zero, Vector2.one, Center, Vector2.zero, Vector2.zero);
            Img(dim, new Color(0.02f, 0.04f, 0.08f, 0.9f), null).raycastTarget = true;

            TMP_Text title = Label("Title", dim, "서바이벌 — 맵 고르기", 60, TextAlignmentOptions.Center, Color.white);
            Place(title.rectTransform, Center, Center, new Vector2(0f, 760f), new Vector2(900f, 110f));

            RectTransform card = Rect("Card", dim, Center, Center, Center, new Vector2(0f, 40f), new Vector2(900f, 1180f));
            Img(card, new Color(0.12f, 0.14f, 0.2f), null);
            var pop = card.gameObject.AddComponent<UiPop>();

            RectTransform bannerRect = Rect("Banner", card, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -30f), new Vector2(-60f, 420f));
            Image banner = Img(bannerRect, new Color(0.35f, 0.7f, 0.4f), _kit != null ? _kit.Panel : null);
            if (_kit != null) banner.type = Image.Type.Sliced;
            Sprite icon = Icon("survival");
            if (icon != null)
            {
                RectTransform iconRect = Rect("Icon", bannerRect, Center, Center, Center, new Vector2(0f, 20f), new Vector2(260f, 260f));
                Img(iconRect, new Color(1f, 1f, 1f, 0.9f), icon).preserveAspect = true;
            }

            TMP_Text index = Label("Index", bannerRect, "맵 1 / 3", 34, TextAlignmentOptions.Center, new Color(1f, 1f, 1f, 0.85f));
            Place(index.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 40f), new Vector2(600f, 60f));

            TMP_Text name = Label("Name", card, "균열의 들판", 64, TextAlignmentOptions.Center, Color.white);
            Place(name.rectTransform, Center, Center, new Vector2(0f, 60f), new Vector2(820f, 100f));
            TMP_Text blurb = Label("Blurb", card, "", 34, TextAlignmentOptions.Center, new Color(0.8f, 0.85f, 0.9f));
            Place(blurb.rectTransform, Center, Center, new Vector2(0f, -10f), new Vector2(820f, 60f));
            TMP_Text stars = Label("Stars", card, "☆☆☆", 72, TextAlignmentOptions.Center, new Color(1f, 0.82f, 0.3f));
            Place(stars.rectTransform, Center, Center, new Vector2(0f, -100f), new Vector2(600f, 100f));
            TMP_Text info = Label("Info", card, "", 32, TextAlignmentOptions.Center, new Color(0.9f, 0.92f, 0.95f));
            Place(info.rectTransform, Center, Center, new Vector2(0f, -250f), new Vector2(840f, 200f));
            info.textWrappingMode = TextWrappingModes.Normal;
            TMP_Text lockText = Label("Lock", card, "", 36, TextAlignmentOptions.Center, new Color(1f, 0.55f, 0.45f));
            Place(lockText.rectTransform, Center, Center, new Vector2(0f, -390f), new Vector2(840f, 60f));

            Button start = Btn("Start", card, "시작", new Vector2(0f, -490f), new Vector2(620f, 130f), new Color(0.3f, 0.75f, 0.4f), out TMP_Text startLabel);
            Button prev = Btn("Prev", dim, "◀", new Vector2(-470f, 40f), new Vector2(110f, 200f), ButtonGray, out _);
            Button next = Btn("Next", dim, "▶", new Vector2(470f, 40f), new Vector2(110f, 200f), ButtonGray, out _);
            Button close = Btn("Close", dim, "닫기", new Vector2(0f, -720f), new Vector2(360f, 100f), ButtonGray, out _);

            using (var props = new Props(panel))
            {
                props.Ref("root", dim.gameObject).Refs("maps", maps).Ref("banner", banner).Ref("indexLabel", index).Ref("nameLabel", name)
                    .Ref("blurbLabel", blurb).Ref("starsLabel", stars).Ref("infoLabel", info).Ref("lockLabel", lockText)
                    .Ref("prevButton", prev).Ref("nextButton", next).Ref("startButton", start).Ref("startLabel", startLabel).Ref("closeButton", close)
                    .Ref("cardPop", pop);
            }

            return panel;
        }

        /// <summary>서바이벌 판에 맵 소품 연결</summary>
        private static void WireSurvivalArt(SurvivalRun run)
        {
            using (var props = new Props(run))
            {
                props.Ref("art.haystack", Art3DStage.Prefab("Haystack")).Ref("art.brazier", Art3DStage.Prefab("Survival_Brazier"))
                    .Ref("art.magnet", Art3DStage.Prefab("Pickup_Magnet")).Ref("art.bomb", Art3DStage.Prefab("Pickup_Bomb"))
                    .Ref("art.meat", Art3DStage.Prefab("Pickup_Meat")).Ref("art.goldBag", Art3DStage.Prefab("Pickup_GoldBag"))
                    .Ref("art.chest", Art3DStage.Prefab("Chest_Gold")).Ref("art.fence", Art3DStage.Prefab("Iron_Gate"));
            }
        }
    }
}
