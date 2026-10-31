using HistoryHeroes.Meta;
using HistoryHeroes.Missions;
using HistoryHeroes.Heroes;
using HistoryHeroes.UI;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace HistoryHeroes.EditorTools
{
    /// <summary>
    /// v0.5 미션·업적 데이터 (Missions 2·3·7장)와 로비 미션 화면.
    /// 무한의 균열·일일 도전은 3구간 보스 격파 뒤에 풀에 들어온다 (v0.6). 도감 이야기는 아직 없어서 Never (2-2 "해금된 다른 미션으로 대신").
    /// </summary>
    public static partial class SceneBuilder
    {
        /// <summary>오늘의 영웅 순서는 로스터에서 복사해 영웅 추가 때 빠지는 일을 막는다.</summary>
        public static System.Collections.Generic.List<string> MissionHeroOrder(HeroRoster roster) =>
            roster.heroes.ConvertAll(hero => hero.id);

        private static MissionTable CreateMissions(HeroRoster roster)
        {
            var t = AssetUtil.LoadOrCreate<MissionTable>($"{DataDir}/Missions/MissionTable.asset");
            const MissionDays all = MissionDays.All;

            // 기존 미션 표 에셋도 다시 만들 때 새 영웅 순서를 받게 한다.
            t.heroOrder = MissionHeroOrder(roster);

            t.dailyFixed.Clear();
            t.dailyFixed.Add(M("login", "게임 접속", MissionEvents.Login, 1, 10));
            t.dailyFixed.Add(M("run2", "아무 모드나 {0}판 플레이 (져도 인정)", MissionEvents.Run, 2, 20));

            t.dailyPool.Clear();
            t.dailyPool.Add(M("elite3", "엘리트 {0}마리 처치", MissionEvents.Elite, 3, 15));
            t.dailyPool.Add(M("kill300", "적 {0}마리 처치", MissionEvents.Kill, 300, 15));
            t.dailyPool.Add(M("nohit3", "피격 없이 방 {0}개 클리어", MissionEvents.NoHitRoom, 3, 20));
            t.dailyPool.Add(M("basic150", "기본 공격으로 적 {0}마리 처치", MissionEvents.BasicKill, 60, 15)); // 전체 검토 11번: 150은 하루 3판 → 60 (id는 저장 호환으로 그대로)
            t.dailyPool.Add(M("fusion1", "조합 스킬 {0}개 만들기", MissionEvents.Fusion, 1, 20));
            t.dailyPool.Add(M("actives3", "액티브 스킬 {0}개를 Lv3 이상으로", MissionEvents.ActivesLv3, 3, 15));
            t.dailyPool.Add(M("passives4", "패시브 {0}칸을 다 채우기", MissionEvents.PassivesFull, 4, 15));
            t.dailyPool.Add(M("herolevel", "영웅 레벨업 {0}회 (골드)", MissionEvents.HeroLevel, 1, 15));
            t.dailyPool.Add(M("equip1", "장비 {0}개 장착 바꾸기", MissionEvents.Equip, 1, 15, MissionGate.Equipment));
            // 모드 전용 (요일, 하루 최대 1개)
            t.dailyPool.Add(M("heroday", "오늘의 영웅으로 던전 1판", MissionEvents.HeroDungeon, 1, 20, mode: true, days: MissionDays.Weekdays, heroOfDay: true));
            t.dailyPool.Add(M("boss1", "보스맵 도전 {0}회 (져도 인정)", MissionEvents.BossTry, 1, 20, MissionGate.BossRecord, mode: true, days: all));
            t.dailyPool.Add(M("record1", "기록 {0}개 저장하기", MissionEvents.Record, 1, 15, mode: true, days: MissionDays.Weekdays | MissionDays.Sat));
            t.dailyPool.Add(M("survival5", "서바이벌 5분 이상 생존", MissionEvents.Survival5, 1, 20, MissionGate.Survival, mode: true, days: MissionDays.Sat));
            t.dailyPool.Add(M("rift5", "무한의 균열 5층 이상", MissionEvents.RiftFloor5, 1, 20, MissionGate.RiftDaily, mode: true, days: MissionDays.Sun));
            t.dailyPool.Add(M("challenge1", "일일 도전 공식 도전 1회", MissionEvents.DailyChallenge, 1, 15, MissionGate.RiftDaily, mode: true, days: all));
            t.dailyPool.Add(M("story1", "도감에서 영웅 이야기 1개 읽기", "story_read", 1, 15, MissionGate.Never));

            t.weekly.Clear();
            t.weekly.Add(M("box4", "일일 미션 상자(100)를 {0}일 열기", MissionEvents.DailyBox100, 4, 20));
            t.weekly.Add(M("twoheroes", "서로 다른 영웅 {0}명으로 던전 클리어", MissionEvents.DungeonClear, 2, 20, distinct: true));
            t.weekly.Add(M("boss2", "보스맵 {0}회 격파 (같은 보스여도 됨)", MissionEvents.BossWin, 2, 20));
            t.weekly.Add(M("recipe1", "새로운 조합 {0}개 발견", MissionEvents.RecipeNew, 1, 20, fallback: "fusion3"));
            t.weekly.Add(M("survival20", "서바이벌·무한의 균열 합계 20분 플레이", MissionEvents.SurvivalSeconds, 1200, 20, MissionGate.Survival, fallback: "dungeon3"));
            t.weekly.Add(M("nolegend", "전설 스킬 없이 던전 {0}판 클리어", MissionEvents.NoLegendClear, 1, 20));
            t.weeklyFallbacks.Clear();
            t.weeklyFallbacks.Add(M("fusion3", "조합 {0}회 만들기", MissionEvents.Fusion, 3, 20));
            t.weeklyFallbacks.Add(M("dungeon3", "던전 {0}판 클리어", MissionEvents.DungeonClear, 3, 20));

            t.dailyChests.Clear();
            t.dailyChests.Add(new ChestDef { points = 20, gold = 200 });
            t.dailyChests.Add(new ChestDef { points = 40, gold = 200, gems = 10 });
            t.dailyChests.Add(new ChestDef { points = 60, gold = 300, gems = 15 });
            t.dailyChests.Add(new ChestDef { points = 80, gold = 300, gearShards = 3 });
            t.dailyChests.Add(new ChestDef { points = 100, gems = 25, ancientShards = 5 });
            t.weeklyChests.Clear();
            t.weeklyChests.Add(new ChestDef { points = 40, gold = 2000, gems = 10 });
            t.weeklyChests.Add(new ChestDef { points = 80, gold = 3000, gems = 10, gearShards = 10, heroShards = 5 });
            t.weeklyChests.Add(new ChestDef { points = 120, gems = 10, tickets = 1, ancientShards = 20 });

            t.achievements.Clear();
            t.achievements.Add(A("chapters", "구간 정복", "진행", "{0}구간 보스 격파", AchievementKind.BossChapters, new[] { 1, 2, 3, 4, 5 }, new[] { 50, 50, 50, 50, 50 }));
            t.achievements.Add(A("stars", "별 수집가", "진행", "별 {0}개", AchievementKind.Stars, new[] { 10, 20, 30 }, new[] { 50, 100, 150 }));
            t.achievements.Add(A("basic", "멈춤의 달인", "전투", "기본 공격으로 처치 {0}", AchievementKind.BasicKills, new[] { 1000, 10000, 50000 }, new[] { 30, 80, 150 }));
            t.achievements.Add(A("shadow", "그림자처럼", "전투", "피격 없이 방 클리어 {0}회", AchievementKind.NoHitRooms, new[] { 10, 50, 200 }, new[] { 30, 80, 150 }));
            t.achievements.Add(A("elite", "엘리트 사냥꾼", "전투", "엘리트 처치 {0}", AchievementKind.EliteKills, new[] { 50, 300, 1000 }, new[] { 20, 50, 100 }));
            t.achievements.Add(A("recipes", "조합 연구가", "빌드", "조합 발견 {0}", AchievementKind.Recipes, new[] { 1, 3, 6, -1 }, new[] { 30, 60, 100, 200 }));
            t.achievements.Add(A("legend", "전설의 순간", "빌드", "전설 스킬 처음 얻기", AchievementKind.Legendary, new[] { 1 }, new[] { 50 }));
            t.achievements.Add(A("nolegend", "전설 없이도", "빌드", "전설 없이 구간 클리어 {0}", AchievementKind.NoLegendChapters, new[] { 1, 3, 5 }, new[] { 50, 100, 150 }));
            t.achievements.Add(A("dream", "드림팀", "영웅", "영웅 {0}명 해금", AchievementKind.HeroesUnlocked, new[] { 2, 3, 4 }, new[] { 50, 50, 50 }));
            t.achievements.Add(A("veteran", "노련한 영웅", "영웅", "한 영웅 Lv{0}", AchievementKind.HeroLevel, new[] { 10, 20, 30 }, new[] { 30, 60, 100 }));
            t.achievements.Add(A("allround", "만능", "영웅", "영웅 {0}명 모두로 3구간 클리어", AchievementKind.AllHeroesChapter3, new[] { 4 }, new[] { 150 }));
            t.achievements.Add(A("archivist", "기록 보관가", "기록", "기록 저장 {0}회", AchievementKind.RecordsSaved, new[] { 1, 10, 50 }, new[] { 20, 50, 100 }));
            t.achievements.Add(A("tailored", "맞춤 전략", "기록", "서로 다른 기록으로 보스 {0}명 격파", AchievementKind.BossRecords, new[] { 3, 5 }, new[] { 80, 150 }));
            t.achievements.Add(A("survivor", "생존자", "모드", "서바이벌 10분 생존 {0}회", AchievementKind.SurvivalClears, new[] { 1, 10 }, new[] { 50, 100 }));
            t.achievements.Add(A("rift", "균열 탐험가", "모드", "무한의 균열 {0}층", AchievementKind.RiftFloor, new[] { 20, 40, 60, 80 }, new[] { 30, 60, 100, 150 }));
            t.achievements.Add(A("daily", "매일의 도전자", "모드", "일일 도전 참여 {0}일", AchievementKind.DailyDays, new[] { 7, 30, 100 }, new[] { 30, 80, 150 }));

            EditorUtility.SetDirty(t);
            return t;
        }

        private static MissionDef M(string id, string title, string key, int target, int points, MissionGate gate = MissionGate.None,
            bool mode = false, MissionDays days = MissionDays.All, bool distinct = false, bool heroOfDay = false, string fallback = null)
        {
            return new MissionDef
            {
                id = id, title = title, eventKey = key, target = target, points = points, gate = gate,
                modeMission = mode, days = days, distinct = distinct, heroOfDay = heroOfDay, fallbackId = fallback,
            };
        }

        private static AchievementDef A(string id, string title, string category, string goal, AchievementKind kind, int[] targets, int[] gems)
        {
            return new AchievementDef { id = id, title = title, category = category, goal = goal, kind = kind, targets = targets, gems = gems };
        }

        private static MissionPanel BuildMissionPanel(Transform parent, MetaTable meta, Heroes.HeroRoster roster)
        {
            RectTransform panel = Overlay(parent, "MissionPanel", out GameObject holder);
            var screen = holder.AddComponent<MissionPanel>();

            TMP_Text title = Label("Title", panel, "미션", 56, TextAlignmentOptions.Center, new Color(1f, 0.85f, 0.35f));
            Place(title.rectTransform, Center, Center, new Vector2(0f, 850f), new Vector2(900f, 80f));

            var tabs = new Button[3];
            var tabLabels = new TMP_Text[3];
            string[] names = { "일일", "주간", "업적" };
            for (int i = 0; i < 3; i++)
            {
                tabs[i] = Btn($"Tab{names[i]}", panel, names[i], new Vector2(-336f + i * 336f, 750f), new Vector2(328f, 96f), RowColor, out tabLabels[i]);
                tabLabels[i].fontSize = 34;
            }

            TMP_Text info = Label("Info", panel, "", 27, TextAlignmentOptions.Center, new Color(1f, 1f, 1f, 0.85f));
            Place(info.rectTransform, Center, Center, new Vector2(0f, 660f), new Vector2(1000f, 60f));

            var chests = new Button[5];
            var chestLabels = new TMP_Text[5];
            var chestGlows = new Image[5];
            for (int i = 0; i < 5; i++)
            {
                chests[i] = Btn($"Chest{i + 1}", panel, "", new Vector2(-404f + i * 202f, 540f), new Vector2(194f, 150f), RowColor, out chestLabels[i]);
                RectTransform glowRect = Rect("ReadyGlow", chests[i].transform, Vector2.zero, Vector2.one, Center, Vector2.zero, new Vector2(20f, 20f));
                chestGlows[i] = Img(glowRect, new Color(1f, 0.8f, 0.25f, 0.38f), _kit != null ? _kit.Glow : null);
                chestGlows[i].raycastTarget = false;
                glowRect.SetAsFirstSibling();
                glowRect.gameObject.SetActive(false);
                chestLabels[i].fontSize = 30;
                chestLabels[i].enableAutoSizing = true;
                chestLabels[i].fontSizeMin = 16f;
                chestLabels[i].fontSizeMax = 30f;
                chestLabels[i].rectTransform.offsetMin = new Vector2(6f, 4f);
                chestLabels[i].rectTransform.offsetMax = new Vector2(-6f, -4f);
            }

            ButtonList list = BuildButtonList(panel, "List", new Vector2(0f, -80f), new Vector2(1000f, 1040f), 118f, 30f);
            Transform template = list.transform.Find("Content/RowTemplate");
            RectTransform track = Rect("ProgressTrack", template, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 10f), new Vector2(-48f, 10f));
            Img(track, new Color(0f, 0f, 0f, 0.45f), null).raycastTarget = false;
            RectTransform fillRect = Rect("ProgressFill", track, Vector2.zero, new Vector2(0f, 1f), new Vector2(0f, 0.5f), Vector2.zero, Vector2.zero);
            Image progressFill = Img(fillRect, new Color(0.95f, 0.77f, 0.32f, 0.94f), null);
            progressFill.raycastTarget = false;
            TMP_Text message = Label("Message", panel, "", 28, TextAlignmentOptions.Center);
            Place(message.rectTransform, Center, Center, new Vector2(0f, -650f), new Vector2(1000f, 90f));

            Button reroll = Btn("Reroll", panel, "미션 바꾸기", new Vector2(-230f, -790f), new Vector2(500f, 100f), new Color(0.32f, 0.25f, 0.45f), out TMP_Text rerollLabel);
            rerollLabel.fontSize = 28;
            Button back = Btn("Back", panel, "뒤로", new Vector2(290f, -790f), new Vector2(380f, 100f), ButtonGray, out _);

            using (var props = new Props(screen))
            {
                props.Ref("root", panel.gameObject).Ref("metaTable", meta).Ref("roster", roster).Ref("titleLabel", title).Ref("infoLabel", info)
                    .Refs("tabButtons", tabs).Refs("tabLabels", tabLabels).Refs("chestButtons", chests).Refs("chestLabels", chestLabels).Refs("chestGlows", chestGlows)
                    .Ref("list", list).Ref("messageLabel", message).Ref("rerollButton", reroll).Ref("rerollLabel", rerollLabel).Ref("backButton", back);
            }

            return screen;
        }
    }
}
