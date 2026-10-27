using System.Collections.Generic;
using HistoryHeroes.Dungeon;
using HistoryHeroes.Enemy;
using HistoryHeroes.UI;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace HistoryHeroes.EditorTools
{
    /// <summary>
    /// v0.6 던전 데이터 (Content 2·3장, Modes 2장): 방 15개 틀 · 구간 5개 · 레이아웃 6종 × 4 + 정해진 모양.
    /// 방 안쪽은 가로 −4~4, 세로 −7~7 (플레이어 시작 (0, −5.5), 문 (0, 6.7)) → 장애물은 세로 −3~5.5에만 둔다.
    /// </summary>
    public static partial class SceneBuilder
    {
        private static void BuildDungeon(Data d, Prefabs p)
        {
            DungeonData dungeon = d.Dungeon;

            // ----- 방 15개 (Modes 2-7: 적 예산·종류 수) -----
            dungeon.plan = new List<RoomPlan>
            {
                Plan(RoomKind.Normal, 6, 1, 1),
                Plan(RoomKind.Normal, 8, 2, 2),
                Plan(RoomKind.Normal, 10, 2, 2),
                Plan(RoomKind.Normal, 12, 2, 2),
                Plan(RoomKind.Elite, 6, 1, 2, elites: 1),
                Plan(RoomKind.Reward, 0, 0, 0),
                Plan(RoomKind.Normal, 14, 3, 3),
                Plan(RoomKind.Normal, 15, 3, 3),
                Plan(RoomKind.Normal, 16, 3, 3),
                Plan(RoomKind.Elite, 6, 1, 2, elites: 2),
                Plan(RoomKind.Reward, 0, 0, 0),
                Plan(RoomKind.Normal, 18, 3, 4),
                Plan(RoomKind.Normal, 19, 3, 4),
                Plan(RoomKind.Normal, 20, 3, 4),
                Plan(RoomKind.MidBoss, 4, 1, 1),
            };

            // ----- 구간 5개 (Content 3장: 새 적, 함정, 중간 보스) -----
            EnemyCost Cost(EnemyBase prefab, int cost, int from) => new EnemyCost { prefab = prefab, cost = cost, fromRoom = from };
            var c1 = Chapter(1, "균열의 초원", TrapKind.Thorns, p.MidBison, "들소 망령", false,
                Cost(p.Charger, 2, 1), Cost(p.Archer, 2, 2));
            var c2 = Chapter(2, "잊힌 성채", TrapKind.FireVent, p.MidTorch, "횃불 기사", false,
                Cost(p.Charger, 2, 1), Cost(p.Archer, 2, 1), Cost(p.Shield, 3, 2), Cost(p.Bomber, 3, 3));
            var c3 = Chapter(3, "녹슨 공방", TrapKind.Conveyor, p.MidGolem, "증기 골렘", false,
                Cost(p.Charger, 2, 1), Cost(p.Archer, 2, 1), Cost(p.Shield, 3, 1), Cost(p.Bomber, 3, 1), Cost(p.Splitter, 3, 2));
            var c4 = Chapter(4, "그림자 협곡", TrapKind.Rockfall, p.MidHunter, "그림자 사냥꾼", false,
                Cost(p.Charger, 2, 1), Cost(p.Archer, 2, 1), Cost(p.Shield, 3, 1), Cost(p.Bomber, 3, 1), Cost(p.Splitter, 3, 1), Cost(p.Summoner, 4, 2));
            var c5 = Chapter(5, "망각의 왕좌", TrapKind.Oblivion, p.MidExecutor, "망각의 집행자", true,
                Cost(p.Charger, 2, 1), Cost(p.Archer, 2, 1), Cost(p.Shield, 3, 1), Cost(p.Bomber, 3, 1), Cost(p.Splitter, 3, 1), Cost(p.Summoner, 4, 1));
            dungeon.chapters = new List<ChapterData> { c1, c2, c3, c4, c5 };

            dungeon.layouts = CreateLayouts();
            dungeon.specialLayout = Layout("special", LayoutKind.Special, new List<ObstacleDef>(), new List<TrapSpot>());
            EditorUtility.SetDirty(dungeon);
        }

        private static RoomPlan Plan(RoomKind kind, int budget, int minTypes, int maxTypes, int elites = 0)
        {
            return new RoomPlan { kind = kind, budget = budget, minTypes = minTypes, maxTypes = maxTypes, elites = elites };
        }

        private static ChapterData Chapter(int chapter, string name, TrapKind trap, EnemyBase midBoss, string midBossName, bool enhanced, params EnemyCost[] enemies)
        {
            var c = AssetUtil.LoadOrCreate<ChapterData>($"{DataDir}/Dungeons/Chapter_{chapter}.asset");
            c.chapter = chapter;
            c.displayName = name;
            c.trap = trap;
            c.midBoss = midBoss;
            c.midBossName = midBossName;
            c.midBossHp = 100f; // 120 × 100 × 방 15 (×1.6) = 1구간 약 19,000 — 그 판의 빌드로 약 1분 (Content 2-2 "60~90초")
            c.midBossAttack = 1.2f;
            c.enhanced = enhanced;
            c.enemies = new List<EnemyCost>(enemies);
            EditorUtility.SetDirty(c);
            return c;
        }

        // ---------------------------------------------------------------- 레이아웃 6종 × 4 (Modes 2-2)

        private static ObstacleDef High(float x, float y, float w, float h, bool circle = false) => new ObstacleDef { kind = ObstacleKind.High, circle = circle, position = new Vector2(x, y), size = new Vector2(w, h) };
        private static ObstacleDef Low(float x, float y, float w, float h, bool circle = false) => new ObstacleDef { kind = ObstacleKind.Low, circle = circle, position = new Vector2(x, y), size = new Vector2(w, h) };
        private static ObstacleDef Crate(float x, float y, int hits = 4) => new ObstacleDef { kind = ObstacleKind.Breakable, position = new Vector2(x, y), size = new Vector2(0.9f, 0.9f), hits = hits };
        private static TrapSpot Spot(float x, float y, float w, float h, float angle = 0f) => new TrapSpot { position = new Vector2(x, y), size = new Vector2(w, h), angle = angle };

        private static List<RoomLayout> CreateLayouts()
        {
            var list = new List<RoomLayout>();
            var none = new List<TrapSpot>();

            // A. 열린 광장: 장애물 거의 없음
            list.Add(Layout("open_1", LayoutKind.Open, new List<ObstacleDef>(), none));
            list.Add(Layout("open_2", LayoutKind.Open, new List<ObstacleDef> { Low(-2.6f, 1f, 1.4f, 1f), Low(2.6f, 3f, 1.4f, 1f) }, none));
            list.Add(Layout("open_3", LayoutKind.Open, new List<ObstacleDef> { Low(0f, 1.6f, 2.2f, 1.2f, circle: true) }, none));
            list.Add(Layout("open_4", LayoutKind.Open, new List<ObstacleDef> { Low(-2.8f, -1.5f, 0.9f, 0.9f), Low(2.8f, -1.5f, 0.9f, 0.9f), Low(-2.8f, 4.5f, 0.9f, 0.9f), Low(2.8f, 4.5f, 0.9f, 0.9f) }, none));

            // B. 기둥 방: 엄폐하면서 쏘기, 벽 튕김
            list.Add(Layout("pillars_1", LayoutKind.Pillars, new List<ObstacleDef> { High(-2f, 0f, 0.9f, 0.9f, true), High(2f, 0f, 0.9f, 0.9f, true), High(-2f, 3.5f, 0.9f, 0.9f, true), High(2f, 3.5f, 0.9f, 0.9f, true) }, none));
            list.Add(Layout("pillars_2", LayoutKind.Pillars, new List<ObstacleDef>
            {
                High(-2.3f, -0.8f, 0.8f, 0.8f, true), High(2.3f, -0.8f, 0.8f, 0.8f, true), High(-2.3f, 1.8f, 0.8f, 0.8f, true),
                High(2.3f, 1.8f, 0.8f, 0.8f, true), High(-2.3f, 4.4f, 0.8f, 0.8f, true), High(2.3f, 4.4f, 0.8f, 0.8f, true),
            }, none));
            list.Add(Layout("pillars_3", LayoutKind.Pillars, new List<ObstacleDef> { High(0f, 2.6f, 1f, 1f, true), High(-2.6f, 0.8f, 0.9f, 0.9f, true), High(2.6f, 0.8f, 0.9f, 0.9f, true), High(0f, -1.2f, 0.9f, 0.9f, true) }, none));
            list.Add(Layout("pillars_4", LayoutKind.Pillars, new List<ObstacleDef>
            {
                High(-1.2f, -0.5f, 0.6f, 0.6f), High(1.2f, -0.5f, 0.6f, 0.6f), High(-3f, 1.5f, 0.6f, 0.6f), High(3f, 1.5f, 0.6f, 0.6f),
                High(-1.2f, 3f, 0.6f, 0.6f), High(1.2f, 3f, 0.6f, 0.6f), High(-3f, 4.8f, 0.6f, 0.6f), High(3f, 4.8f, 0.6f, 0.6f),
            }, none));

            // C. 좁은 복도: 앞에서 몰려오는 적, 관통
            list.Add(Layout("corridor_1", LayoutKind.Corridor, new List<ObstacleDef> { High(-3.3f, 1.2f, 1.4f, 7f), High(3.3f, 1.2f, 1.4f, 7f) }, none));
            list.Add(Layout("corridor_2", LayoutKind.Corridor, new List<ObstacleDef> { High(-3f, 1f, 2f, 7.5f), High(3.2f, 2.5f, 1.2f, 1f, true) }, none));
            list.Add(Layout("corridor_3", LayoutKind.Corridor, new List<ObstacleDef> { High(-2.8f, -0.8f, 2.4f, 3.6f), High(2.8f, 3.4f, 2.4f, 3.6f) }, none));
            list.Add(Layout("corridor_4", LayoutKind.Corridor, new List<ObstacleDef> { High(0f, 1.6f, 1f, 5f), High(-3.5f, 1.6f, 1f, 3f), High(3.5f, 1.6f, 1f, 3f) }, none));

            // D. 위험 지형: 구간 함정 자리 (종류는 구간이 정한다)
            list.Add(Layout("hazard_1", LayoutKind.Hazard, new List<ObstacleDef>(), new List<TrapSpot> { Spot(0f, 0.2f, 7.4f, 1.2f), Spot(0f, 3.6f, 7.4f, 1.2f, 180f) }));
            list.Add(Layout("hazard_2", LayoutKind.Hazard, new List<ObstacleDef> { High(0f, 2f, 0.8f, 0.8f, true) }, new List<TrapSpot> { Spot(-2.2f, 1.8f, 3f, 4.6f, 90f), Spot(2.2f, 1.8f, 3f, 4.6f, -90f) }));
            list.Add(Layout("hazard_3", LayoutKind.Hazard, new List<ObstacleDef>(), new List<TrapSpot> { Spot(0f, 2f, 3.6f, 3.6f), Spot(-3f, -1.6f, 1.6f, 1.6f, 0f), Spot(3f, 5f, 1.6f, 1.6f, 180f) }));
            list.Add(Layout("hazard_4", LayoutKind.Hazard, new List<ObstacleDef> { High(-2.8f, 2f, 0.7f, 0.7f, true), High(2.8f, 2f, 0.7f, 0.7f, true) }, new List<TrapSpot> { Spot(-1.8f, -0.6f, 2.4f, 2f), Spot(1.8f, 2f, 2.4f, 2f, 180f), Spot(-1.8f, 4.6f, 2.4f, 1.6f) }));

            // E. 부서지는 벽: 길 만들기
            var row = new List<ObstacleDef>();
            for (float x = -3.5f; x <= 3.6f; x += 1f)
            {
                if (Mathf.Abs(x + 0.5f) < 0.1f || Mathf.Abs(x - 2.5f) < 0.1f) continue; // 틈 2곳
                row.Add(Crate(x, 1.8f));
            }

            list.Add(Layout("break_1", LayoutKind.Breakable, row, none));
            list.Add(Layout("break_2", LayoutKind.Breakable, new List<ObstacleDef>
            {
                Crate(-3.5f, 0f), Crate(-2.5f, 0f), Crate(-1.5f, 0f), Crate(1.5f, 3.2f), Crate(2.5f, 3.2f), Crate(3.5f, 3.2f), High(0f, 1.6f, 0.8f, 0.8f, true),
            }, none));
            list.Add(Layout("break_3", LayoutKind.Breakable, new List<ObstacleDef>
            {
                Crate(-1f, 1f, 3), Crate(0f, 1f, 3), Crate(1f, 1f, 3), Crate(-1f, 3f, 3), Crate(1f, 3f, 3), Crate(-1.6f, 2f, 3), Crate(1.6f, 2f, 3),
            }, none));
            list.Add(Layout("break_4", LayoutKind.Breakable, new List<ObstacleDef>
            {
                Crate(-3f, -1f, 5), Crate(2.5f, 0.2f, 5), Crate(-1.2f, 2.4f, 5), Crate(1.8f, 3.6f, 5), Crate(-2.8f, 4.6f, 5), Crate(0.4f, 5f, 5),
            }, none));

            // F. 이중 구역: 가운데 구덩이(낮은 장애물 — 투사체는 넘어간다) + 반드시 통로 (근접 도달 규칙)
            list.Add(Layout("split_1", LayoutKind.Split, new List<ObstacleDef> { Low(-1.4f, 1.6f, 5.2f, 1.4f), Low(3.5f, 1.6f, 1f, 1.4f) }, none));      // 통로 x≈2.4
            list.Add(Layout("split_2", LayoutKind.Split, new List<ObstacleDef> { Low(1.4f, 1.6f, 5.2f, 1.4f), Low(-3.5f, 1.6f, 1f, 1.4f) }, none));      // 통로 x≈-2.4
            list.Add(Layout("split_3", LayoutKind.Split, new List<ObstacleDef> { Low(-2.4f, 2.2f, 3.2f, 1.4f), Low(2.4f, 2.2f, 3.2f, 1.4f) }, none));     // 가운데 통로
            list.Add(Layout("split_4", LayoutKind.Split, new List<ObstacleDef> { Low(-3.2f, 1.4f, 1.6f, 1.4f), Low(0f, 1.4f, 2.4f, 1.4f), Low(3.2f, 1.4f, 1.6f, 1.4f) }, none)); // 통로 2곳

            list.AddRange(CreateExpansionLayouts()); // 확장 1차 1장: 새 모양 · 구간 전용 방 · 전용 모양
            return list;
        }

        private static RoomLayout Layout(string id, LayoutKind kind, List<ObstacleDef> obstacles, List<TrapSpot> traps)
        {
            var l = AssetUtil.LoadOrCreate<RoomLayout>($"{DataDir}/Dungeons/Layouts/Layout_{id}.asset");
            l.id = id;
            l.kind = kind;
            l.obstacles = obstacles;
            l.traps = traps;
            EditorUtility.SetDirty(l);
            return l;
        }

        // ---------------------------------------------------------------- 보상 방 화면

        private static RewardRoomPanel BuildRewardRoomPanel(Transform root)
        {
            RectTransform holder = Rect("RewardRoom", root, Vector2.zero, Vector2.one, Center, Vector2.zero, Vector2.zero);
            var screen = holder.gameObject.AddComponent<RewardRoomPanel>();
            RectTransform panel = Rect("Panel", holder, Vector2.zero, Vector2.one, Center, Vector2.zero, Vector2.zero);
            Img(panel, new Color(0.02f, 0.04f, 0.08f, 0.85f), null).raycastTarget = true;

            // 그래픽 평가 "보상 화면은 검은 배경에 색 블록 3개": 빛살 + 초록 리본 제목 + 아이콘 카드 3장 (차례로 튀어나온다)
            if (_kit != null)
            {
                RectTransform rays = Rect("Rays", panel, Center, Center, Center, new Vector2(0f, 430f), new Vector2(900f, 900f));
                Img(rays, new Color(0.55f, 1f, 0.7f, 0.18f), _kit.Rays);
                rays.gameObject.AddComponent<UiSpin>();
                RectTransform ribbon = Rect("Ribbon", panel, Center, Center, Center, new Vector2(0f, 425f), new Vector2(820f, 150f));
                Image ribbonImage = Img(ribbon, new Color(0.25f, 0.7f, 0.45f), _kit.Ribbon);
                ribbonImage.type = Image.Type.Sliced;
                ribbon.gameObject.AddComponent<UiPop>();
            }

            TMP_Text title = Label("Title", panel, "보상 방", 60, TextAlignmentOptions.Center, Color.white);
            Place(title.rectTransform, Center, Center, new Vector2(0f, 438f), new Vector2(760f, 100f));
            title.enableAutoSizing = true;
            title.fontSizeMin = 30f;
            title.fontSizeMax = 56f;

            var buttons = new Button[3];
            var labels = new TMP_Text[3];
            var iconImages = new Image[3];
            Color[] colors = { new Color(0.3f, 0.8f, 0.45f), new Color(0.35f, 0.55f, 1f), new Color(1f, 0.75f, 0.2f) };
            string[] icons = { "heal", "cards", "chest" };
            for (int i = 0; i < 3; i++)
            {
                buttons[i] = Btn($"Reward{i + 1}", panel, "", new Vector2(0f, 180f - i * 240f), new Vector2(840f, 210f), colors[i], out labels[i]);
                labels[i].fontSize = 48;
                labels[i].alignment = TextAlignmentOptions.MidlineLeft;
                labels[i].rectTransform.offsetMin = new Vector2(220f, 0f);
                Sprite icon = Icon(icons[i]);
                if (icon != null)
                {
                    RectTransform slot = Rect("IconSlot", buttons[i].transform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(30f, 6f), new Vector2(150f, 150f));
                    Image slotImage = Img(slot, Color.Lerp(colors[i], Color.black, 0.35f), _kit != null ? _kit.Slot : null);
                    if (_kit != null) slotImage.type = Image.Type.Sliced;
                    RectTransform iconRect = Rect("Icon", slot, Center, Center, Center, Vector2.zero, new Vector2(104f, 104f));
                    iconImages[i] = Img(iconRect, Color.white, icon);
                    iconImages[i].preserveAspect = true;
                }

                var pop = buttons[i].gameObject.AddComponent<UiPop>();
                using (var pp = new Props(pop)) pp.Float("delay", 0.08f * (i + 1));
            }

            using (var props = new Props(screen))
            {
                props.Ref("root", panel.gameObject).Ref("titleLabel", title).Refs("buttons", buttons).Refs("labels", labels).Refs("icons", iconImages);
            }

            return screen;
        }
    }
}
