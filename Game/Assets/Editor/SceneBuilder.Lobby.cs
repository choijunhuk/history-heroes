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
        private static void BuildLobbyScene(Scene scene, SkillDatabase database, BossData[] bosses, Heroes.HeroRoster roster, HistoryHeroes.Meta.MetaTable meta)
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
            TMP_Text sub = Label("Subtitle", main, "History Heroes  ·  v" + UnityEditor.PlayerSettings.bundleVersion.Substring(0, 3), 30, TextAlignmentOptions.Center, new Color(1f, 1f, 1f, 0.6f));
            Place(sub.rectTransform, Center, Center, new Vector2(0f, 615f), new Vector2(900f, 50f));

            Button prev = Btn("PrevChapter", main, "<", new Vector2(-420f, 400f), new Vector2(120f, 120f), ButtonGray, out TMP_Text prevLabel);
            prevLabel.fontSize = 56;
            Button next = Btn("NextChapter", main, ">", new Vector2(420f, 400f), new Vector2(120f, 120f), ButtonGray, out TMP_Text nextLabel);
            nextLabel.fontSize = 56;
            TMP_Text chapter = Label("Chapter", main, "1구간 · 균열의 초원", 50, TextAlignmentOptions.Center);
            Place(chapter.rectTransform, Center, Center, new Vector2(0f, 400f), new Vector2(680f, 110f));
            TMP_Text chapterInfo = Label("ChapterInfo", main, "", 30, TextAlignmentOptions.Center, new Color(1f, 1f, 1f, 0.75f));
            Place(chapterInfo.rectTransform, Center, Center, new Vector2(0f, 262f), new Vector2(940f, 150f));
            chapterInfo.lineSpacing = 10f;

            Button dungeon = Btn("Dungeon", main, "던전 시작", new Vector2(0f, 60f), new Vector2(820f, 200f), ButtonGreen, out TMP_Text dungeonLabel);
            dungeonLabel.fontSize = 64;
            // 반쪽 버튼 두 줄 (v0.6): 보스 | 서바이벌, 균열 | 일일 도전, 그 아래 보관함 | 도감
            void Half(TMP_Text label)
            {
                label.fontSize = 38;
                label.enableAutoSizing = true;
                label.fontSizeMin = 22f;
                label.fontSizeMax = 38f;
                label.rectTransform.offsetMin = new Vector2(12f, 0f);
                label.rectTransform.offsetMax = new Vector2(-12f, 0f);
            }

            Button boss = Btn("Boss", main, "보스 도전", new Vector2(-212f, -150f), new Vector2(396f, 150f), new Color(0.55f, 0.25f, 0.35f), out TMP_Text bossLabel);
            Half(bossLabel);
            Button survival = Btn("Survival", main, "서바이벌", new Vector2(212f, -150f), new Vector2(396f, 150f), new Color(0.6f, 0.38f, 0.14f), out TMP_Text survivalLabel);
            Half(survivalLabel);
            Button rift = Btn("Rift", main, "무한의 균열", new Vector2(-212f, -320f), new Vector2(396f, 150f), new Color(0.3f, 0.2f, 0.55f), out TMP_Text riftLabel);
            Half(riftLabel);
            Button daily = Btn("Daily", main, "일일 도전", new Vector2(212f, -320f), new Vector2(396f, 150f), new Color(0.15f, 0.42f, 0.45f), out TMP_Text dailyLabel);
            Half(dailyLabel);
            Button archive = Btn("Archive", main, "기록 보관함", new Vector2(-212f, -485f), new Vector2(396f, 130f), ButtonBlue, out TMP_Text archiveLabel);
            Half(archiveLabel);
            Button codex = Btn("Codex", main, "조합 도감", new Vector2(212f, -485f), new Vector2(396f, 130f), new Color(0.38f, 0.25f, 0.45f), out TMP_Text codexLabel);
            Half(codexLabel);
            Button heroButton = Btn("Hero", main, "영웅", new Vector2(0f, -645f), new Vector2(820f, 120f), new Color(0.45f, 0.35f, 0.2f), out TMP_Text heroLabel);
            Button settingsButton = Btn("Settings", main, "설정", new Vector2(430f, 820f), new Vector2(180f, 80f), ButtonGray, out TMP_Text settingsLabel);
            settingsLabel.fontSize = 30;
            Button shopButton = Btn("Shop", main, "상점", new Vector2(-430f, 820f), new Vector2(180f, 80f), new Color(0.45f, 0.32f, 0.12f), out TMP_Text shopLabel);
            shopLabel.fontSize = 30;
            Button missionButton = Btn("Missions", main, "미션", new Vector2(-430f, 725f), new Vector2(180f, 80f), new Color(0.2f, 0.42f, 0.3f), out TMP_Text missionLabel);
            missionLabel.fontSize = 30;
            // v1.0: 신규 7일 미션 (왼쪽, 미션 아래) · 순찰 (오른쪽, 설정 아래)
            Button newcomerButton = Btn("Newcomer", main, "7일 미션", new Vector2(-430f, 630f), new Vector2(180f, 80f), new Color(0.45f, 0.3f, 0.12f), out TMP_Text newcomerLabel);
            newcomerLabel.fontSize = 28;
            Button patrolButton = Btn("Patrol", main, "순찰", new Vector2(430f, 715f), new Vector2(180f, 100f), new Color(0.25f, 0.35f, 0.2f), out TMP_Text patrolLabel);
            patrolLabel.fontSize = 28;
            TMP_Text note = Label("Note", main, "그래픽은 아직 임시 도형이에요", 24, TextAlignmentOptions.Center, new Color(1f, 1f, 1f, 0.4f));
            Place(note.rectTransform, Center, Center, new Vector2(0f, -800f), new Vector2(900f, 40f));

            // ----- 패널 (위에 덮는 순서: 보스 시작 전 → 보관함 → 도감 → 다시 고르기) -----
            BossIntroPanel bossIntro = BuildBossIntroPanel(safe, database);
            RecordArchivePanel archivePanel = BuildArchivePanel(safe, database);
            CodexPanel codexPanel = BuildCodexPanel(safe, database);
            RelicPanel relicPanel = BuildRelicPanel(safe, meta != null ? meta.relics : null);
            using (var props = new Props(codexPanel)) props.Ref("relics", relicPanel).Ref("relicButton", codexPanel.transform.Find("Panel/Relics").GetComponent<Button>());
            RecordSaveScreen reselect = BuildRecordSave(root, database);
            HeartPanel heartPanel = BuildHeartPanel(root);
            HeroPanel heroPanel = BuildHeroPanel(safe, roster);
            SettingsPanel settingsPanel = BuildSettingsPanel(safe, lobby: true);
            EquipmentPanel equipmentPanel = BuildEquipmentPanel(safe, meta);
            var gacha = UnityEditor.AssetDatabase.LoadAssetAtPath<HistoryHeroes.Equipment.GachaTable>($"{DataDir}/Equipment/GachaTable.asset");
            ShopPanel shopPanel = BuildShopPanel(safe, gacha);
            TrainingPanel trainingPanel = BuildTrainingPanel(safe, meta);
            RunePanel runePanel = BuildRunePanel(safe, meta, database);
            PetPanel petPanel = BuildPetPanel(safe, meta);
            MissionPanel missionPanel = BuildMissionPanel(safe, meta, roster);
            ChoicePanel choicePanel = BuildChoicePanel(safe);
            ModeInfoPanel modeInfo = BuildModeInfoPanel(safe);
            NewcomerPanel newcomerPanel = BuildNewcomerPanel(safe);
            OpeningPanel opening = BuildOpeningPanel(root);
            using (var props = new Props(heroPanel)) props.Ref("equipment", equipmentPanel).Ref("metaTable", meta).Ref("training", trainingPanel).Ref("runes", runePanel).Ref("pets", petPanel);
            using (var props = new Props(shopPanel)) props.Ref("roster", roster);
            using (var props = new Props(bossIntro)) props.Ref("reselect", reselect).Ref("hearts", heartPanel);

            using (var props = new Props(lobby))
            {
                props.Refs("bosses", bosses).Ref("chapterLabel", chapter).Ref("chapterInfo", chapterInfo)
                    .Ref("previousChapter", prev).Ref("nextChapter", next)
                    .Ref("dungeonButton", dungeon).Ref("bossButton", boss).Ref("bossButtonLabel", bossLabel)
                    .Ref("survivalButton", survival).Ref("survivalButtonLabel", survivalLabel)
                    .Ref("archiveButton", archive).Ref("archiveButtonLabel", archiveLabel)
                    .Ref("codexButton", codex).Ref("codexButtonLabel", codexLabel)
                    .Ref("bossIntro", bossIntro).Ref("archive", archivePanel).Ref("codex", codexPanel).Ref("hearts", heartPanel)
                    .Ref("walletLabel", wallet).Ref("noticeLabel", notice)
                    .Ref("heroButton", heroButton).Ref("heroButtonLabel", heroLabel).Ref("roster", roster).Ref("heroPanel", heroPanel)
                    .Ref("settingsButton", settingsButton).Ref("settings", settingsPanel).Ref("metaTable", meta)
                    .Ref("shopButton", shopButton).Ref("shop", shopPanel)
                    .Ref("missionButton", missionButton).Ref("missionLabel", missionLabel).Ref("missions", missionPanel)
                    .Ref("choice", choicePanel)
                    .Ref("riftButton", rift).Ref("riftButtonLabel", riftLabel).Ref("dailyButton", daily).Ref("dailyButtonLabel", dailyLabel).Ref("modeInfo", modeInfo)
                    .Ref("newcomerButton", newcomerButton).Ref("newcomerLabel", newcomerLabel).Ref("newcomerPanel", newcomerPanel)
                    .Ref("patrolButton", patrolButton).Ref("patrolLabel", patrolLabel).Ref("opening", opening);
            }

            EditorSceneManager.SaveScene(scene, LobbyScenePath);
        }

        /// <summary>v1.0 정보: 이용약관 · 개인정보 · 크레딧 · 정보 (스크롤 글)</summary>
        private static InfoPanel BuildInfoPanel(Transform parent)
        {
            RectTransform panel = Overlay(parent, "InfoPanel", out GameObject holder);
            var screen = holder.AddComponent<InfoPanel>();
            TMP_Text title = Label("Title", panel, "정보", 56, TextAlignmentOptions.Center, new Color(1f, 0.85f, 0.35f));
            Place(title.rectTransform, Center, Center, new Vector2(0f, 840f), new Vector2(900f, 90f));
            Button TabButton(string name, string text, float x)
            {
                Button b = Btn(name, panel, text, new Vector2(x, 730f), new Vector2(240f, 90f), ButtonBlue, out TMP_Text l);
                l.fontSize = 28;
                return b;
            }

            Button terms = TabButton("Terms", "이용약관", -375f);
            Button privacy = TabButton("Privacy", "개인정보", -125f);
            Button credits = TabButton("Credits", "크레딧", 125f);
            Button about = TabButton("About", "정보", 375f);

            // 스크롤 글: 보이는 칸(마스크) + 글(높이 자동) + ScrollRect
            RectTransform viewport = Rect("Viewport", panel, Center, Center, Center, new Vector2(0f, -40f), new Vector2(1000f, 1360f));
            Img(viewport, new Color(0f, 0f, 0f, 0.25f), null).raycastTarget = true;
            viewport.gameObject.AddComponent<RectMask2D>();
            RectTransform content = Rect("Content", viewport, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), Vector2.zero, new Vector2(0f, 100f));
            TMP_Text body = Label("Body", content, "", 30, TextAlignmentOptions.TopLeft);
            Stretch(body.rectTransform);
            body.rectTransform.offsetMin = new Vector2(30f, 0f);
            body.rectTransform.offsetMax = new Vector2(-30f, -24f);
            body.lineSpacing = 8f;
            var fitter = content.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            var layout = content.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(30, 30, 24, 40);
            layout.childControlHeight = true;
            layout.childControlWidth = true;
            layout.childForceExpandHeight = false;
            var scroll = viewport.gameObject.AddComponent<ScrollRect>();
            scroll.content = content;
            scroll.viewport = viewport;
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 40f;

            Button back = Btn("Back", panel, "뒤로", new Vector2(0f, -800f), new Vector2(400f, 100f), ButtonGray, out _);
            using (var props = new Props(screen))
            {
                props.Ref("root", panel.gameObject).Ref("titleLabel", title).Ref("bodyLabel", body).Ref("scroll", scroll)
                    .Ref("termsButton", terms).Ref("privacyButton", privacy).Ref("creditsButton", credits).Ref("aboutButton", about).Ref("backButton", back);
            }

            return screen;
        }

        /// <summary>v1.0 오프닝 컷 3장면 (화면 전체, 다음 · 건너뛰기)</summary>
        private static OpeningPanel BuildOpeningPanel(Transform root)
        {
            RectTransform holder = Rect("Opening", root, Vector2.zero, Vector2.one, Center, Vector2.zero, Vector2.zero);
            var screen = holder.gameObject.AddComponent<OpeningPanel>();
            RectTransform panel = Rect("Panel", holder, Vector2.zero, Vector2.one, Center, Vector2.zero, Vector2.zero);
            Image bg = Img(panel, new Color(0.08f, 0.06f, 0.12f), null);
            bg.raycastTarget = true;
            TMP_Text text = Label("Text", panel, "", 54, TextAlignmentOptions.Center);
            Place(text.rectTransform, Center, Center, new Vector2(0f, 120f), new Vector2(960f, 900f));
            text.lineSpacing = 12f;
            TMP_Text page = Label("Page", panel, "1 / 3", 30, TextAlignmentOptions.Center, new Color(1f, 1f, 1f, 0.5f));
            Place(page.rectTransform, Center, Center, new Vector2(0f, -520f), new Vector2(400f, 60f));
            Button next = Btn("OpeningNext", panel, "다음", new Vector2(0f, -660f), new Vector2(600f, 150f), ButtonGreen, out TMP_Text nextLabel);
            nextLabel.fontSize = 52;
            Button skip = Btn("OpeningSkip", panel, "건너뛰기", new Vector2(380f, 840f), new Vector2(260f, 90f), ButtonGray, out _);
            using (var props = new Props(screen))
            {
                props.Ref("root", panel.gameObject).Ref("background", bg).Ref("textLabel", text).Ref("pageLabel", page)
                    .Ref("nextButton", next).Ref("nextLabel", nextLabel).Ref("skipButton", skip);
            }

            return screen;
        }

        /// <summary>v0.6 모드 안내 (무한의 균열 · 일일 도전): 제목 + 설명 + 버튼 2개 + 뒤로</summary>
        private static ModeInfoPanel BuildModeInfoPanel(Transform parent)
        {
            RectTransform panel = Overlay(parent, "ModeInfoPanel", out GameObject holder);
            var screen = holder.AddComponent<ModeInfoPanel>();
            TMP_Text title = Label("Title", panel, "", 60, TextAlignmentOptions.Center, new Color(1f, 0.85f, 0.35f));
            Place(title.rectTransform, Center, Center, new Vector2(0f, 820f), new Vector2(900f, 100f));
            TMP_Text body = Label("Body", panel, "", 42, TextAlignmentOptions.Top);
            Place(body.rectTransform, Center, Center, new Vector2(0f, 60f), new Vector2(1000f, 1360f));
            body.lineSpacing = 10f;
            body.enableAutoSizing = true; // 글이 길면 줄인다 (읽기 쉬운 크기 우선)
            body.fontSizeMin = 26f;
            body.fontSizeMax = 42f;
            Button primary = Btn("ModePrimary", panel, "", new Vector2(-250f, -660f), new Vector2(440f, 140f), ButtonGreen, out TMP_Text primaryLabel);
            Half(primaryLabel);
            Button secondary = Btn("ModeSecondary", panel, "", new Vector2(250f, -660f), new Vector2(440f, 140f), new Color(0.3f, 0.2f, 0.55f), out TMP_Text secondaryLabel);
            Half(secondaryLabel);
            Button back = Btn("Back", panel, "뒤로", new Vector2(0f, -810f), new Vector2(400f, 100f), ButtonGray, out _);
            using (var props = new Props(screen))
            {
                props.Ref("root", panel.gameObject).Ref("titleLabel", title).Ref("bodyLabel", body)
                    .Ref("primaryButton", primary).Ref("primaryLabel", primaryLabel)
                    .Ref("secondaryButton", secondary).Ref("secondaryLabel", secondaryLabel).Ref("backButton", back);
            }

            return screen;

            void Half(TMP_Text label)
            {
                label.fontSize = 36;
                label.enableAutoSizing = true;
                label.fontSizeMin = 22f;
                label.fontSizeMax = 36f;
                label.rectTransform.offsetMin = new Vector2(12f, 0f);
                label.rectTransform.offsetMax = new Vector2(-12f, 0f);
            }
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

            Button start = Btn("Start", panel, "도전", new Vector2(-150f, -430f), new Vector2(560f, 150f), new Color(0.55f, 0.25f, 0.35f), out TMP_Text startLabel);
            startLabel.fontSize = 46;
            Button practice = Btn("Practice", panel, "연습\n<size=60%>하트·보상 없음</size>", new Vector2(320f, -430f), new Vector2(330f, 150f), ButtonGray, out TMP_Text practiceLabel);
            practiceLabel.fontSize = 36;
            Button back = Btn("Back", panel, "뒤로", new Vector2(0f, -610f), new Vector2(400f, 110f), ButtonGray, out _);

            using (var props = new Props(screen))
            {
                props.Ref("root", panel.gameObject).Ref("titleLabel", title).Ref("testsLabel", tests).Ref("recommendLabel", recommend)
                    .Refs("slotButtons", buttons).Refs("slotBackgrounds", backgrounds).Refs("slotLabels", labels)
                    .Ref("startButton", start).Ref("startLabel", startLabel).Ref("backButton", back).Ref("practiceButton", practice).Ref("database", database);
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

        private static HeroPanel BuildHeroPanel(Transform parent, Heroes.HeroRoster roster)
        {
            RectTransform panel = Overlay(parent, "HeroPanel", out GameObject holder);
            var screen = holder.AddComponent<HeroPanel>();

            TMP_Text title = Label("Title", panel, "영웅", 64, TextAlignmentOptions.Center, new Color(1f, 0.85f, 0.35f));
            Place(title.rectTransform, Center, Center, new Vector2(0f, 800f), new Vector2(900f, 100f));

            var buttons = new Button[4];
            var backgrounds = new Image[4];
            var labels = new TMP_Text[4];
            for (int i = 0; i < 4; i++)
            {
                buttons[i] = Btn($"Hero{i + 1}", panel, "", new Vector2(0f, 560f - i * 265f), new Vector2(980f, 245f), RowColor, out TMP_Text label);
                backgrounds[i] = buttons[i].GetComponent<Image>();
                label.fontSize = 32;
                label.alignment = TextAlignmentOptions.MidlineLeft;
                label.rectTransform.offsetMin = new Vector2(30f, 8f);
                label.rectTransform.offsetMax = new Vector2(-30f, -8f);
                labels[i] = label;
            }

            Button select = Btn("SelectHero", panel, "고르기", new Vector2(-250f, -450f), new Vector2(440f, 110f), ButtonGreen, out TMP_Text selectLabel);
            Button unlock = Btn("UnlockHero", panel, "해금", new Vector2(250f, -450f), new Vector2(440f, 110f), new Color(0.38f, 0.25f, 0.45f), out TMP_Text unlockLabel);
            unlockLabel.fontSize = 28;
            Button level = Btn("HeroLevelUp", panel, "레벨업", new Vector2(-250f, -575f), new Vector2(440f, 110f), new Color(0.2f, 0.42f, 0.5f), out TMP_Text levelLabel);
            levelLabel.fontSize = 28;
            Button awaken = Btn("HeroAwaken", panel, "각성", new Vector2(250f, -575f), new Vector2(440f, 110f), new Color(0.5f, 0.4f, 0.15f), out TMP_Text awakenLabel);
            awakenLabel.fontSize = 28;
            // 아래 줄: 수련 | 룬 | 펫 | 뒤로 (v0.5 펫)
            Button trainingButton = Btn("Training", panel, "수련 (모든 영웅)", new Vector2(-378f, -700f), new Vector2(240f, 110f), ButtonBlue, out TMP_Text trainingLabel);
            trainingLabel.fontSize = 24;
            Button runeButton = Btn("HeroRunes", panel, "룬", new Vector2(-126f, -700f), new Vector2(240f, 110f), new Color(0.32f, 0.25f, 0.45f), out TMP_Text runeLabel);
            runeLabel.fontSize = 24;
            Button petButton = Btn("HeroPet", panel, "펫", new Vector2(126f, -700f), new Vector2(240f, 110f), new Color(0.25f, 0.42f, 0.3f), out TMP_Text petLabel);
            petLabel.fontSize = 24;
            foreach (TMP_Text t in new[] { trainingLabel, runeLabel, petLabel })
            {
                t.enableAutoSizing = true;
                t.fontSizeMin = 18f;
                t.fontSizeMax = 26f;
                t.rectTransform.offsetMin = new Vector2(8f, 0f);
                t.rectTransform.offsetMax = new Vector2(-8f, 0f);
            }
            TMP_Text heroMessage = Label("HeroMessage", panel, "", 26, TextAlignmentOptions.Center);
            Place(heroMessage.rectTransform, Center, Center, new Vector2(0f, -800f), new Vector2(900f, 50f));
            Button equip = Btn("HeroEquipment", panel, "장비", new Vector2(250f, -450f), new Vector2(440f, 110f), ButtonBlue, out TMP_Text equipLabel);
            equipLabel.fontSize = 28;
            Button back = Btn("Back", panel, "뒤로", new Vector2(378f, -700f), new Vector2(240f, 110f), ButtonGray, out _);

            using (var props = new Props(screen))
            {
                props.Ref("petButton", petButton).Ref("petLabel", petLabel);
                props.Ref("equipButton", equip).Ref("equipLabel", equipLabel)
                    .Ref("levelButton", level).Ref("levelLabel", levelLabel).Ref("awakenButton", awaken).Ref("awakenLabel", awakenLabel)
                    .Ref("trainingButton", trainingButton).Ref("messageLabel", heroMessage).Ref("runeButton", runeButton).Ref("runeLabel", runeLabel);
                props.Ref("root", panel.gameObject).Ref("roster", roster).Refs("heroButtons", buttons).Refs("heroBackgrounds", backgrounds)
                    .Refs("heroLabels", labels).Ref("selectButton", select).Ref("selectLabel", selectLabel)
                    .Ref("unlockButton", unlock).Ref("unlockLabel", unlockLabel).Ref("backButton", back);
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

        private static SettingsPanel BuildSettingsPanel(Transform parent, bool lobby = false)
        {
            RectTransform panel = Overlay(parent, "SettingsPanel", out GameObject holder);
            var screen = holder.AddComponent<SettingsPanel>();

            TMP_Text title = Label("Title", panel, "설정", 64, TextAlignmentOptions.Center, new Color(1f, 0.85f, 0.35f));
            Place(title.rectTransform, Center, Center, new Vector2(0f, 800f), new Vector2(900f, 100f));
            // 두 칸 (왼쪽 · 오른쪽), 묶음마다 제목
            Vector2 half = new Vector2(450f, 110f);
            const float left = -235f, right = 235f;
            TMP_Text Section(string name, string text, float y)
            {
                TMP_Text t = Label(name, panel, text, 34, TextAlignmentOptions.Left, new Color(1f, 0.85f, 0.35f));
                Place(t.rectTransform, Center, Center, new Vector2(0f, y), new Vector2(940f, 50f));
                return t;
            }

            Button Half(string name, string text, float x, float y, Color color, out TMP_Text label)
            {
                Button b = Btn(name, panel, text, new Vector2(x, y), half, color, out label);
                label.fontSize = 28;
                label.enableAutoSizing = true;
                label.fontSizeMin = 20f;
                label.fontSizeMax = 28f;
                label.rectTransform.offsetMin = new Vector2(10f, 0f);
                label.rectTransform.offsetMax = new Vector2(-10f, 0f);
                return b;
            }

            Section("SectionScreen", "화면", 700f);
            Button shake = Half("Shake", "화면 흔들림: 보통", left, 620f, ButtonBlue, out TMP_Text shakeLabel);
            Button numbers = Half("DamageNumbers", "피해 숫자: 켜기", right, 620f, ButtonBlue, out TMP_Text numbersLabel);
            Button flash = Half("ReduceFlashes", "번쩍임 줄이기: 끄기", left, 495f, ButtonBlue, out TMP_Text flashLabel);
            Button frame = Half("Frame", "프레임: 60", right, 495f, ButtonBlue, out TMP_Text frameLabel);
            TMP_Text help = Label("Help", panel, "번쩍임 줄이기: 화면을 크게 덮는 섬광을 옅게, 1초에 3번까지만", 24, TextAlignmentOptions.Center, new Color(1f, 1f, 1f, 0.6f));
            Place(help.rectTransform, Center, Center, new Vector2(0f, 415f), new Vector2(940f, 50f));

            Section("SectionControl", "조작", 345f);
            Color control = new Color(0.3f, 0.27f, 0.42f);
            Button joyMode = Half("JoystickMode", "조이스틱: 누른 자리", left, 265f, control, out TMP_Text joyModeLabel);
            Button joySize = Half("JoystickSize", "조이스틱 크기: 보통", right, 265f, control, out TMP_Text joySizeLabel);
            Button joyOpacity = Half("JoystickOpacity", "조이스틱 진하기: 보통", left, 140f, control, out TMP_Text joyOpacityLabel);

            // v1.0 알림 (LiveOps 9장: 종류별로 끈다)
            Section("SectionNotice", "알림", 60f);
            Color notice = new Color(0.25f, 0.35f, 0.25f);
            Button hearts = Half("NotifyHearts", "하트가 다 차면: 켜기", left, -20f, notice, out TMP_Text heartsLabel);
            Button patrol = Half("NotifyPatrol", "순찰이 가득 차면: 켜기", right, -20f, notice, out TMP_Text patrolLabel);
            TMP_Text noticeHelp = Label("NoticeHelp", panel, "밤 9시~아침 8시에는 보내지 않아요 · 하루 2번까지", 24, TextAlignmentOptions.Center, new Color(1f, 1f, 1f, 0.6f));
            Place(noticeHelp.rectTransform, Center, Center, new Vector2(0f, -100f), new Vector2(940f, 50f));

            Button replay = null, infoButton = null;
            InfoPanel info = null;
            if (lobby)
            {
                replay = Half("ReplayTutorial", "튜토리얼 다시 보기", left, -230f, new Color(0.38f, 0.3f, 0.18f), out _);
                infoButton = Half("Info", "정보 · 약관 · 크레딧", right, -230f, ButtonGray, out _);
                info = BuildInfoPanel(parent);
            }

            Button back = Btn("Back", panel, "뒤로", new Vector2(0f, -420f), new Vector2(400f, 110f), ButtonGray, out _);

            using (var props = new Props(screen))
            {
                props.Ref("root", panel.gameObject).Ref("shakeButton", shake).Ref("shakeLabel", shakeLabel)
                    .Ref("numbersButton", numbers).Ref("numbersLabel", numbersLabel)
                    .Ref("flashButton", flash).Ref("flashLabel", flashLabel).Ref("backButton", back)
                    .Ref("heartsButton", hearts).Ref("heartsLabel", heartsLabel).Ref("patrolButton", patrol).Ref("patrolLabel", patrolLabel)
                    .Ref("replayTutorialButton", replay).Ref("infoButton", infoButton).Ref("info", info)
                    .Ref("frameButton", frame).Ref("frameLabel", frameLabel)
                    .Ref("joystickModeButton", joyMode).Ref("joystickModeLabel", joyModeLabel)
                    .Ref("joystickSizeButton", joySize).Ref("joystickSizeLabel", joySizeLabel)
                    .Ref("joystickOpacityButton", joyOpacity).Ref("joystickOpacityLabel", joyOpacityLabel);
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
            // 조합 14개 + 스킬별 조합 수가 한 화면을 넘어서 위아래로 밀어 본다
            RectTransform viewport = Rect("Viewport", panel, Center, Center, Center, new Vector2(0f, 20f), new Vector2(920f, 1200f));
            Img(viewport, new Color(0f, 0f, 0f, 0.001f), null).raycastTarget = true; // 끌기를 받는 판
            viewport.gameObject.AddComponent<RectMask2D>();
            TMP_Text list = Label("List", viewport, "", 32, TextAlignmentOptions.TopLeft);
            RectTransform listRect = list.rectTransform;
            listRect.anchorMin = new Vector2(0f, 1f);
            listRect.anchorMax = new Vector2(1f, 1f);
            listRect.pivot = new Vector2(0.5f, 1f);
            listRect.anchoredPosition = Vector2.zero;
            listRect.sizeDelta = new Vector2(0f, 1200f);
            list.lineSpacing = 6f;
            list.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            var scroll = viewport.gameObject.AddComponent<ScrollRect>();
            scroll.viewport = viewport;
            scroll.content = listRect;
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 40f;
            Button relicButton = Btn("Relics", panel, "유물 도감", new Vector2(-220f, -720f), new Vector2(400f, 110f), new Color(0.45f, 0.32f, 0.12f), out _);
            Button back = Btn("Back", panel, "뒤로", new Vector2(220f, -720f), new Vector2(400f, 110f), ButtonGray, out _);

            using (var props = new Props(screen))
            {
                props.Ref("root", panel.gameObject).Ref("progressLabel", progress).Ref("listLabel", list).Ref("backButton", back).Ref("database", database);
            }

            return screen;
        }
    }
}
