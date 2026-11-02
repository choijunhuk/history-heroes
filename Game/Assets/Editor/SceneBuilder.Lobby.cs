using HistoryHeroes.Modes;
using HistoryHeroes.Equipment;
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
            // 원근 3D 카메라 (가운데 구간 섬 무대) — 그래픽 평가 3번
            Camera camera = LobbyCamera();

            Transform root = CreateCanvas();
            RectTransform safe = Rect("Safe", root, Vector2.zero, Vector2.one, Center, Vector2.zero, Vector2.zero);
            safe.gameObject.AddComponent<SafeAreaFitter>();

            // ----- 메인 (두 큰 모드: 던전 · 서바이벌, 개발자 결정 2026-10-27) -----
            RectTransform main = Rect("Main", safe, Vector2.zero, Vector2.one, Center, Vector2.zero, Vector2.zero);
            var lobby = main.gameObject.AddComponent<LobbyScreen>();
            LobbyMainUi m = BuildLobbyMain(main, camera);
            Button prev = m.Prev, next = m.Next, dungeon = m.Dungeon, boss = m.Boss, survival = m.Survival, rift = m.Rift, daily = m.Daily;
            Button archive = m.Archive, codex = m.Codex, heroButton = m.Hero, settingsButton = m.Settings, shopButton = m.Shop;
            Button missionButton = m.Missions, newcomerButton = m.Newcomer, patrolButton = m.Patrol;
            TMP_Text chapter = m.Chapter, chapterInfo = m.ChapterInfo, bossLabel = m.BossLabel, survivalLabel = m.SurvivalLabel;
            TMP_Text riftLabel = m.RiftLabel, dailyLabel = m.DailyLabel, archiveLabel = m.ArchiveLabel, codexLabel = m.CodexLabel;
            TMP_Text heroLabel = m.HeroName, missionLabel = m.MissionLabel, newcomerLabel = m.NewcomerLabel, patrolLabel = m.PatrolLabel;
            TMP_Text wallet = m.Wallet, notice = m.Notice;
            HeroPortraits heroFaces = null; // 로비 가운데 무대가 영웅을 보여준다

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
            // 계정 진행 고르기 · 삭제 확인은 고르기 창을 빌린다 (설정보다 나중에 만들어 위에 덮인다)
            using (var props = new Props(settingsPanel.GetComponentInChildren<AccountSection>(true))) props.Ref("choice", choicePanel);
            ModeInfoPanel modeInfo = BuildModeInfoPanel(safe);
            NewcomerPanel newcomerPanel = BuildNewcomerPanel(safe);
            OpeningPanel opening = BuildOpeningPanel(root);
            SurvivalMapPanel survivalMaps = BuildSurvivalMapPanel(safe); // 확장 1차 6-6장
            using (var props = new Props(heroPanel)) props.Ref("relics", relicPanel).Ref("equipment", equipmentPanel).Ref("metaTable", meta).Ref("training", trainingPanel).Ref("runes", runePanel).Ref("pets", petPanel);
            using (var props = new Props(shopPanel)) props.Ref("roster", roster).Ref("relics", meta != null ? meta.relics : null).Ref("choice", choicePanel);
            using (var props = new Props(relicPanel)) props.Ref("shop", shopPanel);
            using (var props = new Props(bossIntro)) props.Ref("reselect", reselect).Ref("hearts", heartPanel).Ref("opening", opening).Ref("thorTrial", LoadThorTrial()).Refs("godTrials", LoadGodTrials());

            using (var props = new Props(lobby))
            {
                props.Refs("bosses", bosses).Ref("chapterLabel", chapter).Ref("chapterInfo", chapterInfo)
                    .Ref("previousChapter", prev).Ref("nextChapter", next)
                    .Ref("dungeonButton", dungeon).Ref("bossButton", boss).Ref("bossButtonLabel", bossLabel)
                    .Ref("survivalButton", survival).Ref("survivalButtonLabel", survivalLabel).Ref("survivalMaps", survivalMaps)
                    .Ref("archiveButton", archive).Ref("archiveButtonLabel", archiveLabel)
                    .Ref("codexButton", codex).Ref("codexButtonLabel", codexLabel)
                    .Ref("bossIntro", bossIntro).Ref("archive", archivePanel).Ref("codex", codexPanel).Ref("hearts", heartPanel)
                    .Ref("walletLabel", wallet).Ref("noticeLabel", notice)
                    .Ref("heroButton", heroButton).Ref("heroButtonLabel", heroLabel).Ref("heroButtonPortraits", heroFaces).Ref("roster", roster).Ref("heroPanel", heroPanel)
                    .Ref("settingsButton", settingsButton).Ref("settings", settingsPanel).Ref("metaTable", meta)
                    .Ref("shopButton", shopButton).Ref("shop", shopPanel)
                    .Ref("missionButton", missionButton).Ref("missionLabel", missionLabel).Ref("missions", missionPanel)
                    .Ref("choice", choicePanel)
                    .Ref("riftButton", rift).Ref("riftButtonLabel", riftLabel).Ref("dailyButton", daily).Ref("dailyButtonLabel", dailyLabel).Ref("modeInfo", modeInfo)
                    .Ref("newcomerButton", newcomerButton).Ref("newcomerLabel", newcomerLabel).Ref("newcomerPanel", newcomerPanel)
                    .Ref("heroUpgradeBadge", m.HeroBadge).Ref("newcomerBadge", m.NewcomerBadge)
                    .Ref("patrolButton", patrolButton).Ref("patrolLabel", patrolLabel).Ref("opening", opening)
                    .Ref("homeMissionButton", m.HomeMission).Ref("homeMissionLabel", m.HomeMissionLabel).Ref("homeMissionFill", m.HomeMissionFill).Ref("goldLabel", m.Gold).Ref("gemLabel", m.Gem).Ref("heartLabel", m.Heart).Ref("levelLabel", m.Level).Ref("diorama", m.Diorama);
            }

            // 안내를 닫고 새 계정으로 진행하면 최신 저장을 잃을 수 있어 닫기 단추를 두지 않는다.
            RectTransform unsupported = Rect("UnsupportedSaveNotice", root, Vector2.zero, Vector2.one, Center, Vector2.zero, Vector2.zero);
            Img(unsupported, new Color(0.08f, 0.06f, 0.12f), null).raycastTarget = true;
            TMP_Text unsupportedText = Label("Notice", unsupported, "게임 업데이트가 필요해요\n\n<size=70%>" + HistoryHeroes.Records.SaveSystem.UnsupportedNewerSaveText + "</size>", 54, TextAlignmentOptions.Center);
            Place(unsupportedText.rectTransform, Center, Center, Vector2.zero, new Vector2(920f, 900f));
            unsupported.gameObject.SetActive(false);
            using (var props = new Props(lobby)) props.Ref("unsupportedSaveNotice", unsupported.gameObject).Ref("reselect", reselect);

            LobbyGuide firstUseGuide = BuildLobbyGuide(root, safe, main);
            using (var props = new Props(lobby)) props.Ref("firstUseGuide", firstUseGuide);

            // 첫 실행 동의: 맨 위에 덮는다 (안내 · 튜토리얼보다 먼저)
            ConsentPanel consentPanel = BuildConsentPanel(root);
            using (var props = new Props(lobby)) props.Ref("consent", consentPanel);

            // 보상 받기 연출: 안전 영역 안 모든 창 위 (마지막 자식). 동의 창은 root에 따로 있어 그보다 위
            BuildRewardPopup(safe).transform.SetAsLastSibling();

            ApplyTopBars(safe);
            AddUiSounds(root);
            ApplyTypeScale();
            ApplyButtonGroups();
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
            // 안내 카드: 높이 = 글 (제목 아래 ~ 단추 위 가운데). 2026-11-02 개발자: "박스 안에 위로 정렬 = 별로"
            RectTransform card = Rect("BodyCard", panel, Center, Center, Center, new Vector2(0f, 90f), new Vector2(1000f, 900f));
            Image cardImage = Img(card, SettingsCardColor, _kit != null ? _kit.Panel : null);
            if (_kit != null) cardImage.type = Image.Type.Sliced;
            var column = card.gameObject.AddComponent<VerticalLayoutGroup>();
            column.padding = new RectOffset(48, 48, 48, 48);
            column.childControlWidth = column.childControlHeight = true;
            column.childForceExpandWidth = true;
            column.childForceExpandHeight = false;
            card.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            TMP_Text body = Label("Body", card, "", 38, TextAlignmentOptions.Center);
            body.lineSpacing = 8f;
            body.textWrappingMode = TextWrappingModes.Normal;
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
        /// <param name="color">바탕색. 반투명(알파 0.95 미만)이면 무늬 배경 없이 뒤 화면이 어둡게 비친다</param>
        private static RectTransform Overlay(Transform parent, string name, out GameObject holder, Color? color = null)
        {
            RectTransform h = Rect(name, parent, Vector2.zero, Vector2.one, Center, Vector2.zero, Vector2.zero);
            holder = h.gameObject;
            RectTransform panel = Rect("Panel", h, Vector2.zero, Vector2.one, Center, Vector2.zero, Vector2.zero);
            Img(panel, color ?? PanelColor, null).raycastTarget = true;
            Bleed(panel);
            panel.gameObject.AddComponent<PanelIntro>(); // 열릴 때 살짝 커지며 나타남 (UI-Research 5장)
            return panel;
        }

        /// <summary>안전 영역 밖까지 늘리는 바탕 여유 (상태 표시줄 · 카메라 구멍 · 아래 제스처 바보다 넉넉히)</summary>
        private const float BleedPad = 400f;

        /// <summary>
        /// 창 바탕을 안전 영역 밖까지 늘린다 (2026-11-02 개발자: "홈 화면을 뺀 화면에서 위 공간이 안 채워져 홈 화면이 비친다").
        /// 판(Panel)은 안전 영역 그대로라 내용 위치는 안 바뀐다. 바탕 그림 · 무늬 · 가장자리는 첫 자식 "Bleed"로 옮기고,
        /// 판 자체는 투명하게 남겨 누름만 막는다.
        /// </summary>
        private static Image Bleed(RectTransform panel)
        {
            Image source = panel.GetComponent<Image>();
            var moved = new System.Collections.Generic.List<Transform>();
            foreach (Transform child in panel) moved.Add(child); // ScreenBackdrop이 붙인 무늬 · 가장자리
            RectTransform bleed = Rect("Bleed", panel, Vector2.zero, Vector2.one, Center, Vector2.zero, Vector2.zero);
            bleed.offsetMin = new Vector2(-40f, -BleedPad);
            bleed.offsetMax = new Vector2(40f, BleedPad);
            bleed.SetAsFirstSibling();
            var image = bleed.gameObject.AddComponent<Image>();
            image.sprite = source.sprite;
            image.type = source.type;
            image.color = source.color;
            image.raycastTarget = false;
            foreach (Transform child in moved) child.SetParent(bleed, false);
            source.sprite = null;
            source.color = Color.clear;
            return image;
        }

        private static BossIntroPanel BuildBossIntroPanel(Transform parent, SkillDatabase database)
        {
            RectTransform panel = Overlay(parent, "BossIntroPanel", out GameObject holder);
            var screen = holder.AddComponent<BossIntroPanel>();

            TMP_Text title = Label("Title", panel, "보스", 68, TextAlignmentOptions.Center, new Color(1f, 0.85f, 0.35f));
            Place(title.rectTransform, Center, Center, new Vector2(0f, 680f), new Vector2(900f, 100f));
            Button normalTab = Btn("NormalBossTab", panel, "기존 보스", new Vector2(-260f, 890f), new Vector2(470f, 90f), ButtonBlue, out _);
            Button trialTab = Btn("GodTrialTab", panel, "신의 시련", new Vector2(260f, 890f), new Vector2(470f, 90f), ButtonBlue, out _);
            var trialButtons = new Button[5];
            var trialLabels = new TMP_Text[5];
            string[] trialNames = { "TrialThor", "TrialAnubis", "TrialZeus", "TrialSusanoo", "TrialAthena" };
            string[] trialTitles = { "토르", "아누비스", "제우스", "스사노오", "아테나" };
            for (int i = 0; i < trialButtons.Length; i++)
            {
                // 다섯 단추가 제목(중심 680, 높이 100) 바로 위 한 줄에 들어가도록 폭과 간격을 고정한다.
                trialButtons[i] = Btn(trialNames[i], panel, trialTitles[i], new Vector2(-388f + i * 194f, 780f), new Vector2(184f, 90f), ButtonBlue, out trialLabels[i]);
                trialLabels[i].fontSize = 22f;
            }
            TMP_Text tests = Label("Tests", panel, "", 32, TextAlignmentOptions.Center);
            Place(tests.rectTransform, Center, Center, new Vector2(0f, 580f), new Vector2(980f, 80f));
            TMP_Text recommend = Label("Recommend", panel, "", 28, TextAlignmentOptions.Center, new Color(0.7f, 0.9f, 1f));
            Place(recommend.rectTransform, Center, Center, new Vector2(0f, 500f), new Vector2(980f, 70f));
            Button weeklyGoal = Btn("ChooseGodGoal", panel, "주간 목표 선택", new Vector2(0f, 420f), new Vector2(920f, 70f), ButtonBlue, out TMP_Text weeklyLabel);
            weeklyLabel.fontSize = 25;
            TMP_Text pick = Label("Pick", panel, "싸울 기록을 고르세요 (기록은 이겨도 져도 사라지지 않아요)", 26, TextAlignmentOptions.Center, new Color(1f, 1f, 1f, 0.6f));
            Place(pick.rectTransform, Center, Center, new Vector2(0f, 356f), new Vector2(1000f, 40f)); // 위 줄이 늘 때마다 1번 칸에 가려졌다 → 칸 위 끝(333)보다 위에

            var buttons = new Button[3];
            var backgrounds = new Image[3];
            var labels = new TMP_Text[3];
            for (int i = 0; i < 3; i++)
            {
                buttons[i] = Btn($"Slot{i + 1}", panel, "", new Vector2(0f, 238f - i * 236f), new Vector2(980f, 190f), RowColor, out TMP_Text label);
                backgrounds[i] = buttons[i].GetComponent<Image>();
                label.fontSize = 30;
                label.alignment = TextAlignmentOptions.MidlineLeft;
                label.rectTransform.offsetMin = new Vector2(30f, 10f);
                label.rectTransform.offsetMax = new Vector2(-30f, -10f);
                labels[i] = label;
            }

            Button temp = Btn("Temp", panel, "", new Vector2(0f, -440f), new Vector2(980f, 210f), ButtonBlue, out TMP_Text tempLabel);
            Image tempBackground = temp.GetComponent<Image>();
            tempLabel.fontSize = 27;
            tempLabel.alignment = TextAlignmentOptions.MidlineLeft;
            tempLabel.rectTransform.offsetMin = new Vector2(30f, 10f);
            tempLabel.rectTransform.offsetMax = new Vector2(-30f, -10f);

            Button start = Btn("Start", panel, "도전", new Vector2(-150f, -620f), new Vector2(560f, 125f), ButtonGreen, out TMP_Text startLabel); // 주 행동
            startLabel.fontSize = 46;
            Button practice = Btn("Practice", panel, "연습\n<size=72%>하트·보상 없음</size>", new Vector2(320f, -620f), new Vector2(330f, 125f), ButtonGray, out TMP_Text practiceLabel);
            practiceLabel.fontSize = 36;
            Button back = Btn("Back", panel, "뒤로", new Vector2(0f, -780f), new Vector2(400f, 100f), ButtonGray, out _);

            using (var props = new Props(screen))
            {
                props.Ref("root", panel.gameObject).Ref("titleLabel", title).Ref("testsLabel", tests).Ref("recommendLabel", recommend)
                    .Ref("normalTab", normalTab).Ref("trialTab", trialTab).Refs("trialButtons", trialButtons).Refs("trialLabels", trialLabels).Ref("weeklyGoalButton", weeklyGoal).Ref("weeklyGoalLabel", weeklyLabel)
                    .Refs("slotButtons", buttons).Refs("slotBackgrounds", backgrounds).Refs("slotLabels", labels)
                    .Ref("tempButton", temp).Ref("tempBackground", tempBackground).Ref("tempLabel", tempLabel)
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
            TMP_Text help = Label("Help", panel, "기록은 보스맵에서 계속 쓸 수 있어요. 잠그면 덮어쓰기와 지우기를 막아요.", 26, TextAlignmentOptions.Center, new Color(1f, 1f, 1f, 0.6f));
            Place(help.rectTransform, Center, Center, new Vector2(0f, 700f), new Vector2(1000f, 70f));

            var titles = new TMP_Text[3];
            var details = new TMP_Text[3];
            var deletes = new Button[3];
            var deleteLabels = new TMP_Text[3];
            var locks = new Button[3];
            var lockLabels = new TMP_Text[3];
            for (int i = 0; i < 3; i++)
            {
                RectTransform box = Rect($"Slot{i + 1}", panel, Center, Center, Center, new Vector2(0f, 500f - i * 320f), new Vector2(980f, 300f));
                Img(box, RowColor, null);
                titles[i] = Label("Title", box, "", 34, TextAlignmentOptions.TopLeft);
                Place(titles[i].rectTransform, Center, Center, new Vector2(0f, 105f), new Vector2(920f, 50f));
                details[i] = Label("Details", box, "", 28, TextAlignmentOptions.TopLeft, new Color(1f, 1f, 1f, 0.85f));
                Place(details[i].rectTransform, Center, Center, new Vector2(0f, -2f), new Vector2(920f, 150f));
                details[i].enableAutoSizing = true;
                details[i].fontSizeMin = 22f;
                details[i].fontSizeMax = 28f;
                locks[i] = Btn("Lock", box, "잠그기", new Vector2(-320f, -115f), new Vector2(300f, 65f), ButtonBlue, out lockLabels[i]);
                deletes[i] = Btn("Delete", box, "지우기", new Vector2(320f, -115f), new Vector2(300f, 65f), new Color(0.5f, 0.22f, 0.22f), out deleteLabels[i]);
                lockLabels[i].fontSize = deleteLabels[i].fontSize = 26;
            }

            RectTransform tempBox = Rect("Temp", panel, Center, Center, Center, new Vector2(0f, -460f), new Vector2(980f, 300f));
            Img(tempBox, RowColor, null);
            TMP_Text tempTitle = Label("Title", tempBox, "임시 칸", 34, TextAlignmentOptions.TopLeft);
            Place(tempTitle.rectTransform, Center, Center, new Vector2(0f, 105f), new Vector2(920f, 50f));
            TMP_Text tempDetails = Label("Details", tempBox, "", 28, TextAlignmentOptions.TopLeft, new Color(1f, 1f, 1f, 0.85f));
            Place(tempDetails.rectTransform, Center, Center, new Vector2(0f, -2f), new Vector2(920f, 150f));
            tempDetails.enableAutoSizing = true;
            tempDetails.fontSizeMin = 22f;
            tempDetails.fontSizeMax = 28f;
            Button moveTemp = Btn("MoveTemp", tempBox, "보관함으로 옮기기", new Vector2(0f, -115f), new Vector2(620f, 65f), ButtonBlue, out _);

            Button back = Btn("Back", panel, "뒤로", new Vector2(0f, -790f), new Vector2(400f, 100f), ButtonGray, out _);

            using (var props = new Props(screen))
            {
                props.Ref("root", panel.gameObject).Refs("slotTitles", titles).Refs("slotDetails", details)
                    .Refs("deleteButtons", deletes).Refs("deleteLabels", deleteLabels).Refs("lockButtons", locks).Refs("lockLabels", lockLabels)
                    .Ref("tempTitle", tempTitle).Ref("tempDetails", tempDetails).Ref("moveTempButton", moveTemp)
                    .Ref("statusLabel", help).Ref("backButton", back).Ref("database", database);
            }

            return screen;
        }

        private static HeroPanel BuildHeroPanel(Transform parent, Heroes.HeroRoster roster)
        {
            RectTransform panel = Overlay(parent, "HeroPanel", out GameObject holder);
            var screen = holder.AddComponent<HeroPanel>();

            TMP_Text title = Label("Title", panel, "영웅", 64, TextAlignmentOptions.Center, new Color(1f, 0.85f, 0.35f));
            Place(title.rectTransform, Center, Center, new Vector2(0f, 850f), new Vector2(900f, 100f));

            // 그래픽 재평가 3번: 고른 영웅을 크게 (무대 빛 + 받침 고리 + 큰 초상), 그 아래 이름·역할·고유 능력 카드
            // 고른 영웅은 크게 보여 주고, 아래 고르기 칸은 가로로 넘긴다.
            int heroCount = Mathf.Max(4, roster != null ? roster.heroes.Count : 4);
            RawImage big = null;
            if (_kit != null)
            {
                RectTransform stage = Rect("StageGlow", panel, Center, Center, Center, new Vector2(0f, 470f), new Vector2(820f, 820f));
                Img(stage, new Color(1f, 0.8f, 0.4f, 0.28f), _kit.Glow);
                RectTransform pedestal = Rect("Pedestal", panel, Center, Center, Center, new Vector2(0f, 205f), new Vector2(460f, 130f));
                Img(pedestal, new Color(1f, 0.85f, 0.45f, 0.75f), _kit.Ring);
            }

            RectTransform bigRect = Rect("BigPortrait", panel, Center, Center, Center, new Vector2(0f, 480f), new Vector2(540f, 620f));
            big = bigRect.gameObject.AddComponent<RawImage>();
            big.color = new Color(1f, 1f, 1f, 0f);
            big.raycastTarget = false;

            RectTransform card = Rect("DetailCard", panel, Center, Center, Center, new Vector2(0f, 75f), new Vector2(1000f, 210f));
            Image cardImage = Img(card, new Color(0.12f, 0.16f, 0.28f, 0.96f), _kit != null ? _kit.Panel : null);
            if (_kit != null) cardImage.type = Image.Type.Sliced;
            TMP_Text detailName = Label("Name", card, "", 54, TextAlignmentOptions.Center, Color.white);
            Place(detailName.rectTransform, Center, Center, new Vector2(0f, 64f), new Vector2(960f, 62f));
            detailName.enableAutoSizing = true;
            detailName.fontSizeMin = 30f;
            detailName.fontSizeMax = 54f;
            TMP_Text detailBody = Label("Body", card, "", 26, TextAlignmentOptions.Center, new Color(1f, 1f, 1f, 0.9f));
            Place(detailBody.rectTransform, Center, Center, new Vector2(0f, -30f), new Vector2(940f, 120f));
            detailBody.lineSpacing = 14f; // 세 줄이 겹치지 않게
            detailBody.enableAutoSizing = true;
            detailBody.fontSizeMin = 20f; // 화면 점검 2차 5번: 설명이 너무 작다 → 대표 효과 글을 줄이고 글씨 하한을 올림
            detailBody.fontSizeMax = 28f;

            // 고정 폭 칸을 가로로 넘긴다. 12명이 되어도 이름과 3D 초상에 같은 자리를 준다.
            const float step = 160f;
            const float tileWidth = 150f;
            float tileHeight = 200f;
            float contentWidth = heroCount * step;
            RectTransform viewport = Rect("HeroScrollViewport", panel, Center, Center, Center, new Vector2(0f, -560f), new Vector2(1040f, 215f));
            Img(viewport, new Color(0f, 0f, 0f, 0.001f), null).raycastTarget = true;
            viewport.gameObject.AddComponent<RectMask2D>();
            RectTransform content = Rect("HeroScrollContent", viewport, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), Vector2.zero, new Vector2(contentWidth, 210f));
            ScrollRect heroScroll = viewport.gameObject.AddComponent<ScrollRect>();
            heroScroll.viewport = viewport;
            heroScroll.content = content;
            heroScroll.horizontal = true;
            heroScroll.vertical = false;
            heroScroll.movementType = ScrollRect.MovementType.Clamped;
            var buttons = new Button[heroCount];
            var backgrounds = new Image[heroCount];
            var labels = new TMP_Text[heroCount];
            var portraits = new RawImage[heroCount];
            for (int i = 0; i < heroCount; i++)
            {
                buttons[i] = Btn($"Hero{i + 1}", content, "", new Vector2(-contentWidth * 0.5f + step * (i + 0.5f), 0f), new Vector2(tileWidth, tileHeight), RowColor, out TMP_Text label);
                backgrounds[i] = buttons[i].GetComponent<Image>();
                RectTransform lr = label.rectTransform;
                lr.anchorMin = new Vector2(0f, 0f);
                lr.anchorMax = new Vector2(1f, 0.40f); // 이름 + 레벨 두 줄이 22px 아래로 줄지 않게
                lr.offsetMin = new Vector2(6f, 2f);
                lr.offsetMax = new Vector2(-6f, -2f);
                label.margin = Vector4.zero;
                label.fontSize = 28f;
                label.enableAutoSizing = true;
                label.fontSizeMin = 16f;
                label.fontSizeMax = 28f;
                label.lineSpacing = -12f;
                labels[i] = label;

                // 3D 초상 (Art3D-Plan 7번): 끌면 돈다. 누르면 칸 버튼이 받는다
                RectTransform portrait = Rect($"Portrait{i + 1}", buttons[i].transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -6f), new Vector2(120f, 130f));
                portraits[i] = portrait.gameObject.AddComponent<RawImage>();
                portraits[i].color = new Color(1f, 1f, 1f, 0f); // 그림이 생기기 전에는 투명
                portraits[i].raycastTarget = true;
                // 잠긴 영웅: 초상 아래에 "잠김" (어두운 옷과 헷갈리지 않게 — 화면 점검 2차)
                // 어두운 받침 띠 위에 (금빛 고른 칸 위에서도 읽히게, 새 영웅 화면 점검)
                RectTransform lockBack = Rect("LockBack", portrait, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, 0f), new Vector2(0f, 38f));
                Img(lockBack, new Color(0f, 0f, 0f, 0.62f), null).raycastTarget = false;
                TMP_Text lockLabel = Label("Lock", lockBack, "잠김", 28, TextAlignmentOptions.Center, new Color(1f, 0.85f, 0.35f));
                Stretch(lockLabel.rectTransform);
                lockLabel.raycastTarget = false;
                lockLabel.textWrappingMode = TextWrappingModes.NoWrap; // 영웅이 11명 넘어 칸이 좁으면 "잠/김"으로 접혔다 → 한 줄로 (칸 밖으로 조금 넘쳐도 됨)
                lockLabel.overflowMode = TextOverflowModes.Overflow;
                lockBack.gameObject.SetActive(false); // 받침 띠째로 켜고 끈다 (HeroPortraits.SetLocked)
            }

            using (var props = new Props(panel.gameObject.AddComponent<HeroPortraits>())) props.Ref("roster", roster).Refs("images", portraits).Int("textureSize", 480);

            // ----- 탕탕특공대식 영웅 화면 (2026-10-29 개발자) -----
            // 양옆 장비 여섯 칸은 저장 번호 순서로 연결하고, 화면에서는 공격·체력 줄로 나눈다.
            Vector2 slotSize = new Vector2(200f, 170f);
            var gearSlots = new Button[EquipmentInventory.SlotCount];
            var gearLabels = new TMP_Text[EquipmentInventory.SlotCount];
            gearSlots[(int)EquipSlot.Weapon] = IconButton("GearWeapon", panel, "sword", "", new Vector2(-405f, 660f), slotSize, ButtonBlue, out gearLabels[(int)EquipSlot.Weapon]);
            gearSlots[(int)EquipSlot.Necklace] = IconButton("GearNecklace", panel, "gem", "", new Vector2(-405f, 470f), slotSize, ButtonBlue, out gearLabels[(int)EquipSlot.Necklace]);
            gearSlots[(int)EquipSlot.Gloves] = IconButton("GearGloves", panel, "atk_up", "", new Vector2(-405f, 280f), slotSize, ButtonBlue, out gearLabels[(int)EquipSlot.Gloves]);
            gearSlots[(int)EquipSlot.Armor] = IconButton("GearArmor", panel, "shield", "", new Vector2(405f, 660f), slotSize, ButtonBlue, out gearLabels[(int)EquipSlot.Armor]);
            gearSlots[(int)EquipSlot.Belt] = IconButton("GearBelt", panel, "def_up", "", new Vector2(405f, 470f), slotSize, ButtonBlue, out gearLabels[(int)EquipSlot.Belt]);
            gearSlots[(int)EquipSlot.Boots] = IconButton("GearBoots", panel, "boot", "", new Vector2(405f, 280f), slotSize, ButtonBlue, out gearLabels[(int)EquipSlot.Boots]);

            // 공격력 · 체력 (크게)
            TMP_Text power = Label("Power", panel, "", 44, TextAlignmentOptions.Center, Color.white);
            Place(power.rectTransform, Center, Center, new Vector2(0f, -100f), new Vector2(1000f, 70f));
            power.enableAutoSizing = true;
            power.fontSizeMin = 30f;
            power.fontSizeMax = 44f;

            // 장비와 분리해 펫·룬·유물을 한 줄에서 연다.
            Vector2 extraSize = new Vector2(300f, 66f);
            Button petButton = Btn("HeroPet", panel, "펫", new Vector2(-340f, -175f), extraSize, ButtonBlue, out TMP_Text petLabel);
            Button runeButton = Btn("HeroRunes", panel, "룬", new Vector2(0f, -175f), extraSize, ButtonBlue, out TMP_Text runeLabel);
            Button relicButton = Btn("HeroRelics", panel, "유물", new Vector2(340f, -175f), extraSize, ButtonBlue, out TMP_Text relicLabel);
            petLabel.fontSize = 25f;
            runeLabel.fontSize = 25f;
            relicLabel.fontSize = 25f;
            petLabel.enableAutoSizing = runeLabel.enableAutoSizing = relicLabel.enableAutoSizing = true;
            petLabel.fontSizeMin = runeLabel.fontSizeMin = relicLabel.fontSizeMin = 19f;

            // 레벨업 = 주 행동 (크게) · 각성 = 보조. 잠긴 영웅이면 같은 자리에 3분 체험 · 해금
            Button level = Btn("HeroLevelUp", panel, "레벨업", new Vector2(-200f, -285f), new Vector2(580f, 120f), ButtonGreen, out TMP_Text levelLabel);
            levelLabel.fontSize = 34;
            Button awaken = Btn("HeroAwaken", panel, "각성", new Vector2(300f, -285f), new Vector2(380f, 120f), ButtonBlue, out TMP_Text awakenLabel);
            awakenLabel.fontSize = 28;
            Button trial = Btn("HeroTrial", panel, "3분 체험", new Vector2(-200f, -285f), new Vector2(580f, 120f), ButtonBlue, out _);
            trial.gameObject.SetActive(false);
            Button unlock = Btn("UnlockHero", panel, "해금", new Vector2(300f, -285f), new Vector2(380f, 120f), new Color(0.38f, 0.25f, 0.45f), out TMP_Text unlockLabel);
            unlockLabel.fontSize = 26;
            // 둘째 줄: 수련 (모든 영웅 공용) · 이 영웅으로 하기
            Button trainingButton = Btn("Training", panel, "수련 (모든 영웅)", new Vector2(-250f, -405f), new Vector2(480f, 88f), ButtonBlue, out TMP_Text trainingLabel);
            trainingLabel.fontSize = 26;
            Button select = Btn("SelectHero", panel, "고르기", new Vector2(250f, -405f), new Vector2(480f, 88f), ButtonGreen, out TMP_Text selectLabel);
            TMP_Text heroMessage = Label("HeroMessage", panel, "", 26, TextAlignmentOptions.Center);
            Place(heroMessage.rectTransform, Center, Center, new Vector2(0f, -690f), new Vector2(900f, 50f));
            // 예전 "장비" 단추는 장비 칸으로 바뀌었다 (필드는 남겨 두고 숨긴다)
            Button equip = Btn("HeroEquipment", panel, "장비", new Vector2(0f, -2400f), new Vector2(10f, 10f), ButtonBlue, out TMP_Text equipLabel);
            equip.gameObject.SetActive(false);
            Button back = Btn("Back", panel, "뒤로", new Vector2(0f, -790f), new Vector2(400f, 110f), ButtonGray, out _);

            using (var props = new Props(screen))
            {
                props.Ref("petButton", petButton).Ref("petLabel", petLabel);
                props.Ref("equipButton", equip).Ref("equipLabel", equipLabel)
                    .Ref("levelButton", level).Ref("levelLabel", levelLabel).Ref("awakenButton", awaken).Ref("awakenLabel", awakenLabel)
                    .Ref("trainingButton", trainingButton).Ref("messageLabel", heroMessage).Ref("runeButton", runeButton).Ref("runeLabel", runeLabel);
                props.Ref("root", panel.gameObject).Ref("roster", roster).Refs("heroButtons", buttons).Refs("heroBackgrounds", backgrounds)
                    .Ref("heroScroll", heroScroll)
                    .Refs("heroLabels", labels).Ref("selectButton", select).Ref("selectLabel", selectLabel)
                    .Ref("unlockButton", unlock).Ref("unlockLabel", unlockLabel).Ref("trialButton", trial).Ref("backButton", back)
                    .Ref("bigPortrait", big).Ref("detailName", detailName).Ref("detailBody", detailBody)
                    .Refs("gearSlots", gearSlots).Refs("gearLabels", gearLabels).Ref("relicButton", relicButton).Ref("relicLabel", relicLabel).Ref("powerLabel", power);
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



    }
}
