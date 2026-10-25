using HistoryHeroes.Modes;
using HistoryHeroes.Skills;
using HistoryHeroes.UI;
using TMPro;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace HistoryHeroes.EditorTools
{
    /// <summary>v0.2 로비 장면: 구간 고르기, 던전 / 보스 도전 / 기록 보관함 / 조합 도감 (UX.md 1장 축소판).</summary>
    public static partial class SceneBuilder
    {
        private static void BuildLobbyScene(Scene scene, SkillDatabase database, BossData[] bosses)
        {
            var cameraObject = new GameObject("Main Camera") { tag = "MainCamera" };
            cameraObject.transform.position = new Vector3(0f, 0f, -10f);
            var camera = cameraObject.AddComponent<Camera>();
            camera.orthographic = true;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.07f, 0.08f, 0.11f);

            Transform root = CreateCanvas();
            RectTransform safe = Rect("Safe", root, Vector2.zero, Vector2.one, Center, Vector2.zero, Vector2.zero);
            safe.gameObject.AddComponent<SafeAreaFitter>();

            // ----- 메인 -----
            RectTransform main = Rect("Main", safe, Vector2.zero, Vector2.one, Center, Vector2.zero, Vector2.zero);
            var lobby = main.gameObject.AddComponent<LobbyScreen>();

            RectTransform topBar = Rect("TopBar", main, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -20f), new Vector2(-40f, 70f));
            Img(topBar, new Color(0f, 0f, 0f, 0.35f), null);
            TMP_Text wallet = Label("Wallet", topBar, "골드 0   보석 0   하트 5/5   Lv 1", 28, TextAlignmentOptions.Center);
            Stretch(wallet.rectTransform);
            TMP_Text notice = Label("Notice", main, "", 28, TextAlignmentOptions.Center, new Color(1f, 0.85f, 0.4f));
            Place(notice.rectTransform, Center, Center, new Vector2(0f, 530f), new Vector2(1000f, 80f));
            notice.gameObject.SetActive(false);

            TMP_Text title = Label("Title", main, "역사 영웅", 96, TextAlignmentOptions.Center, new Color(1f, 0.85f, 0.35f));
            Place(title.rectTransform, Center, Center, new Vector2(0f, 700f), new Vector2(900f, 130f));
            TMP_Text sub = Label("Subtitle", main, "History Heroes  ·  v0.2", 30, TextAlignmentOptions.Center, new Color(1f, 1f, 1f, 0.6f));
            Place(sub.rectTransform, Center, Center, new Vector2(0f, 615f), new Vector2(900f, 50f));

            Button prev = Btn("PrevChapter", main, "<", new Vector2(-420f, 400f), new Vector2(120f, 120f), ButtonGray, out TMP_Text prevLabel);
            prevLabel.fontSize = 56;
            Button next = Btn("NextChapter", main, ">", new Vector2(420f, 400f), new Vector2(120f, 120f), ButtonGray, out TMP_Text nextLabel);
            nextLabel.fontSize = 56;
            TMP_Text chapter = Label("Chapter", main, "1구간 · 균열의 초원", 50, TextAlignmentOptions.Center);
            Place(chapter.rectTransform, Center, Center, new Vector2(0f, 400f), new Vector2(680f, 110f));
            TMP_Text chapterInfo = Label("ChapterInfo", main, "", 30, TextAlignmentOptions.Center, new Color(1f, 1f, 1f, 0.75f));
            Place(chapterInfo.rectTransform, Center, Center, new Vector2(0f, 265f), new Vector2(940f, 140f));

            Button dungeon = Btn("Dungeon", main, "던전 시작", new Vector2(0f, 50f), new Vector2(820f, 210f), ButtonGreen, out TMP_Text dungeonLabel);
            dungeonLabel.fontSize = 64;
            Button boss = Btn("Boss", main, "보스 도전", new Vector2(0f, -190f), new Vector2(820f, 160f), new Color(0.55f, 0.25f, 0.35f), out TMP_Text bossLabel);
            bossLabel.fontSize = 44;
            Button archive = Btn("Archive", main, "기록 보관함", new Vector2(0f, -380f), new Vector2(820f, 130f), ButtonBlue, out TMP_Text archiveLabel);
            Button codex = Btn("Codex", main, "조합 도감", new Vector2(0f, -540f), new Vector2(820f, 130f), new Color(0.38f, 0.25f, 0.45f), out TMP_Text codexLabel);
            TMP_Text note = Label("Note", main, "그래픽은 아직 임시 도형이에요", 24, TextAlignmentOptions.Center, new Color(1f, 1f, 1f, 0.4f));
            Place(note.rectTransform, Center, Center, new Vector2(0f, -800f), new Vector2(900f, 40f));

            // ----- 패널 (위에 덮는 순서: 보스 시작 전 → 보관함 → 도감 → 다시 고르기) -----
            BossIntroPanel bossIntro = BuildBossIntroPanel(safe, database);
            RecordArchivePanel archivePanel = BuildArchivePanel(safe, database);
            CodexPanel codexPanel = BuildCodexPanel(safe, database);
            RecordSaveScreen reselect = BuildRecordSave(root, database);
            HeartPanel heartPanel = BuildHeartPanel(root);
            using (var props = new Props(bossIntro)) props.Ref("reselect", reselect).Ref("hearts", heartPanel);

            using (var props = new Props(lobby))
            {
                props.Refs("bosses", bosses).Ref("chapterLabel", chapter).Ref("chapterInfo", chapterInfo)
                    .Ref("previousChapter", prev).Ref("nextChapter", next)
                    .Ref("dungeonButton", dungeon).Ref("bossButton", boss).Ref("bossButtonLabel", bossLabel)
                    .Ref("archiveButton", archive).Ref("archiveButtonLabel", archiveLabel)
                    .Ref("codexButton", codex).Ref("codexButtonLabel", codexLabel)
                    .Ref("bossIntro", bossIntro).Ref("archive", archivePanel).Ref("codex", codexPanel).Ref("hearts", heartPanel)
                    .Ref("walletLabel", wallet).Ref("noticeLabel", notice);
            }

            EditorSceneManager.SaveScene(scene, LobbyScenePath);
        }

        /// <summary>패널 공통: 컴포넌트를 단 부모(늘 켜짐) + 덮는 판(root, 끄고 켬)</summary>
        private static RectTransform Overlay(Transform parent, string name, out GameObject holder)
        {
            RectTransform h = Rect(name, parent, Vector2.zero, Vector2.one, Center, Vector2.zero, Vector2.zero);
            holder = h.gameObject;
            RectTransform panel = Rect("Panel", h, Vector2.zero, Vector2.one, Center, Vector2.zero, Vector2.zero);
            Img(panel, PanelColor, null).raycastTarget = true;
            return panel;
        }

        private static BossIntroPanel BuildBossIntroPanel(Transform parent, SkillDatabase database)
        {
            RectTransform panel = Overlay(parent, "BossIntroPanel", out GameObject holder);
            var screen = holder.AddComponent<BossIntroPanel>();

            TMP_Text title = Label("Title", panel, "보스", 68, TextAlignmentOptions.Center, new Color(1f, 0.85f, 0.35f));
            Place(title.rectTransform, Center, Center, new Vector2(0f, 770f), new Vector2(900f, 100f));
            TMP_Text tests = Label("Tests", panel, "", 32, TextAlignmentOptions.Center);
            Place(tests.rectTransform, Center, Center, new Vector2(0f, 670f), new Vector2(980f, 90f));
            TMP_Text recommend = Label("Recommend", panel, "", 28, TextAlignmentOptions.Center, new Color(0.7f, 0.9f, 1f));
            Place(recommend.rectTransform, Center, Center, new Vector2(0f, 590f), new Vector2(980f, 70f));
            TMP_Text pick = Label("Pick", panel, "싸울 기록을 고르세요 (기록은 이겨도 져도 사라지지 않아요)", 26, TextAlignmentOptions.Center, new Color(1f, 1f, 1f, 0.6f));
            Place(pick.rectTransform, Center, Center, new Vector2(0f, 510f), new Vector2(1000f, 50f));

            var buttons = new Button[3];
            var backgrounds = new Image[3];
            var labels = new TMP_Text[3];
            for (int i = 0; i < 3; i++)
            {
                buttons[i] = Btn($"Slot{i + 1}", panel, "", new Vector2(0f, 340f - i * 250f), new Vector2(980f, 225f), RowColor, out TMP_Text label);
                backgrounds[i] = buttons[i].GetComponent<Image>();
                label.fontSize = 27;
                label.alignment = TextAlignmentOptions.MidlineLeft;
                label.rectTransform.offsetMin = new Vector2(30f, 10f);
                label.rectTransform.offsetMax = new Vector2(-30f, -10f);
                labels[i] = label;
            }

            Button start = Btn("Start", panel, "도전", new Vector2(0f, -430f), new Vector2(620f, 150f), new Color(0.55f, 0.25f, 0.35f), out TMP_Text startLabel);
            startLabel.fontSize = 48;
            Button back = Btn("Back", panel, "뒤로", new Vector2(0f, -610f), new Vector2(400f, 110f), ButtonGray, out _);

            using (var props = new Props(screen))
            {
                props.Ref("root", panel.gameObject).Ref("titleLabel", title).Ref("testsLabel", tests).Ref("recommendLabel", recommend)
                    .Refs("slotButtons", buttons).Refs("slotBackgrounds", backgrounds).Refs("slotLabels", labels)
                    .Ref("startButton", start).Ref("startLabel", startLabel).Ref("backButton", back).Ref("database", database);
            }

            return screen;
        }

        private static RecordArchivePanel BuildArchivePanel(Transform parent, SkillDatabase database)
        {
            RectTransform panel = Overlay(parent, "ArchivePanel", out GameObject holder);
            var screen = holder.AddComponent<RecordArchivePanel>();

            TMP_Text title = Label("Title", panel, "기록 보관함", 64, TextAlignmentOptions.Center, new Color(1f, 0.85f, 0.35f));
            Place(title.rectTransform, Center, Center, new Vector2(0f, 790f), new Vector2(900f, 100f));
            TMP_Text help = Label("Help", panel, "기록은 보스맵에서 계속 쓸 수 있어요. 새 기록을 만들면 빈 칸이나 고른 칸에 들어가요.", 26, TextAlignmentOptions.Center, new Color(1f, 1f, 1f, 0.6f));
            Place(help.rectTransform, Center, Center, new Vector2(0f, 700f), new Vector2(1000f, 70f));

            var titles = new TMP_Text[3];
            var details = new TMP_Text[3];
            var deletes = new Button[3];
            var deleteLabels = new TMP_Text[3];
            for (int i = 0; i < 3; i++)
            {
                RectTransform box = Rect($"Slot{i + 1}", panel, Center, Center, Center, new Vector2(0f, 470f - i * 360f), new Vector2(980f, 320f));
                Img(box, RowColor, null);
                titles[i] = Label("Title", box, "", 34, TextAlignmentOptions.TopLeft);
                Place(titles[i].rectTransform, Center, Center, new Vector2(0f, 110f), new Vector2(920f, 60f));
                details[i] = Label("Details", box, "", 26, TextAlignmentOptions.TopLeft, new Color(1f, 1f, 1f, 0.85f));
                Place(details[i].rectTransform, Center, Center, new Vector2(0f, -10f), new Vector2(920f, 170f));
                deletes[i] = Btn("Delete", box, "지우기", new Vector2(320f, -115f), new Vector2(300f, 70f), new Color(0.5f, 0.22f, 0.22f), out deleteLabels[i]);
                deleteLabels[i].fontSize = 26;
            }

            Button back = Btn("Back", panel, "뒤로", new Vector2(0f, -660f), new Vector2(400f, 110f), ButtonGray, out _);

            using (var props = new Props(screen))
            {
                props.Ref("root", panel.gameObject).Refs("slotTitles", titles).Refs("slotDetails", details)
                    .Refs("deleteButtons", deletes).Refs("deleteLabels", deleteLabels).Ref("backButton", back).Ref("database", database);
            }

            return screen;
        }

        private static HeartPanel BuildHeartPanel(Transform parent)
        {
            RectTransform holder = Rect("HeartPanel", parent, Vector2.zero, Vector2.one, Center, Vector2.zero, Vector2.zero);
            var screen = holder.gameObject.AddComponent<HeartPanel>();
            RectTransform dim = Rect("Panel", holder, Vector2.zero, Vector2.one, Center, Vector2.zero, Vector2.zero);
            Img(dim, new Color(0f, 0f, 0f, 0.8f), null).raycastTarget = true;
            RectTransform box = Rect("Box", dim, Center, Center, Center, Vector2.zero, new Vector2(900f, 760f));
            Img(box, RowColor, null);

            TMP_Text message = Label("Message", box, "하트가 부족해요", 40, TextAlignmentOptions.Center);
            Place(message.rectTransform, Center, Center, new Vector2(0f, 220f), new Vector2(840f, 260f));
            Button ad = Btn("WatchAd", box, "광고 보고 하트 +2", new Vector2(0f, -10f), new Vector2(760f, 130f), ButtonBlue, out TMP_Text adLabel);
            adLabel.fontSize = 32;
            Button gem = Btn("BuyHearts", box, "보석 60개로 하트 +5", new Vector2(0f, -160f), new Vector2(760f, 130f), new Color(0.38f, 0.25f, 0.45f), out TMP_Text gemLabel);
            gemLabel.fontSize = 32;
            Button close = Btn("Close", box, "닫기", new Vector2(0f, -300f), new Vector2(360f, 100f), ButtonGray, out _);

            using (var props = new Props(screen))
            {
                props.Ref("root", dim.gameObject).Ref("messageLabel", message).Ref("adButton", ad).Ref("adLabel", adLabel)
                    .Ref("gemButton", gem).Ref("gemLabel", gemLabel).Ref("closeButton", close);
            }

            return screen;
        }

        private static CodexPanel BuildCodexPanel(Transform parent, SkillDatabase database)
        {
            RectTransform panel = Overlay(parent, "CodexPanel", out GameObject holder);
            var screen = holder.AddComponent<CodexPanel>();

            TMP_Text title = Label("Title", panel, "조합 도감", 64, TextAlignmentOptions.Center, new Color(1f, 0.85f, 0.35f));
            Place(title.rectTransform, Center, Center, new Vector2(0f, 790f), new Vector2(900f, 100f));
            TMP_Text progress = Label("Progress", panel, "발견 0 / 0", 36, TextAlignmentOptions.Center);
            Place(progress.rectTransform, Center, Center, new Vector2(0f, 690f), new Vector2(900f, 60f));
            TMP_Text list = Label("List", panel, "", 32, TextAlignmentOptions.TopLeft);
            Place(list.rectTransform, Center, Center, new Vector2(0f, 20f), new Vector2(920f, 1200f));
            list.lineSpacing = 6f;
            Button back = Btn("Back", panel, "뒤로", new Vector2(0f, -720f), new Vector2(400f, 110f), ButtonGray, out _);

            using (var props = new Props(screen))
            {
                props.Ref("root", panel.gameObject).Ref("progressLabel", progress).Ref("listLabel", list).Ref("backButton", back).Ref("database", database);
            }

            return screen;
        }
    }
}
