using HistoryHeroes.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace HistoryHeroes.EditorTools
{
    /// <summary>
    /// 설정 화면 (2026-11-01 "화면 채우기" 개편 · 샘플 화면). 개발자: "궁수의 전설 같은 게임은 화면이 채워져 있는데 우리는 작게 구석에 박혀 있다".
    /// 새 UI 규칙 (Docs/Design/UX.md "화면 채우기"): 안전 영역 전체 폭(좌우 24) · 줄 높이 120 이상 · 아이콘 72 · 글자 본문 28+ 이름 26+ 메모 22+ 제목 44+ ·
    /// 제목 리본 하나 + 카드(묶음 제목) + 전체 폭 줄 · 주 행동이 가장 크게 · 세로로 길면 스크롤.
    /// 값 로직은 SettingsPanel/SettingRow가 예전 저장 값 그대로 다룬다 (여기는 모양만).
    /// </summary>
    public static partial class SceneBuilder
    {
        private const float SettingsWidth = 1032f, SettingsRowHeight = 120f;
        private static readonly Color SettingsCardColor = new Color(0.13f, 0.17f, 0.3f, 0.94f);
        private static readonly Color SettingsRowColor = new Color(0.08f, 0.1f, 0.18f, 1f);
        private static readonly Color Gold = new Color(1f, 0.85f, 0.35f);

        private static void FixedHeight(Component c, float height)
        {
            var le = c.gameObject.GetComponent<LayoutElement>() ?? c.gameObject.AddComponent<LayoutElement>();
            le.preferredHeight = height;
            le.minHeight = height;
        }

        /// <summary>카드: 둥근 바탕 + 묶음 제목 + 안쪽 세로 쌓기 (높이는 내용만큼)</summary>
        private static RectTransform SettingsCard(RectTransform content, string name, string header, Color accent)
        {
            RectTransform card = Rect(name, content, Center, Center, Center, Vector2.zero, new Vector2(SettingsWidth, 200f));
            Image image = Img(card, SettingsCardColor, _kit != null ? _kit.Panel : null);
            if (_kit != null) image.type = Image.Type.Sliced;
            var layout = card.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(20, 20, 16, 20);
            layout.spacing = 12f;
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            TMP_Text title = Label("Header", card, header, 38, TextAlignmentOptions.MidlineLeft, accent);
            title.margin = new Vector4(8f, 0f, 0f, 0f);
            FixedHeight(title, 64f);
            return card;
        }

        private static TMP_Text SettingsNote(RectTransform card, string name, string text)
        {
            TMP_Text note = Label(name, card, text, 24, TextAlignmentOptions.TopLeft, new Color(1f, 1f, 1f, 0.7f));
            note.margin = new Vector4(10f, 0f, 10f, 0f);
            FixedHeight(note, 66f);
            return note;
        }

        /// <summary>줄 바탕 + 왼쪽 아이콘 + 이름 (오른쪽은 호출한 쪽이 채운다)</summary>
        private static RectTransform SettingsRow(RectTransform card, string name, string iconKey, string text, Color accent, float height = SettingsRowHeight)
        {
            RectTransform row = Rect(name, card, Center, Center, Center, Vector2.zero, new Vector2(SettingsWidth - 40f, height));
            Image bg = Img(row, SettingsRowColor, null);
            bg.raycastTarget = true;
            FixedHeight(row, height);
            Sprite icon = Icon(iconKey);
            if (icon != null)
            {
                RectTransform ir = Rect("Icon", row, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(18f, 0f), new Vector2(72f, 72f));
                Img(ir, accent, icon).preserveAspect = true;
            }

            TMP_Text label = Label("Name", row, text, 32, TextAlignmentOptions.MidlineLeft);
            label.rectTransform.anchorMin = new Vector2(0f, 0f);
            label.rectTransform.anchorMax = new Vector2(0f, 1f);
            label.rectTransform.pivot = new Vector2(0f, 0.5f);
            label.rectTransform.anchoredPosition = new Vector2(106f, 0f);
            label.rectTransform.sizeDelta = new Vector2(250f, 0f);
            label.enableAutoSizing = true;
            label.fontSizeMin = 26f;
            label.fontSizeMax = 32f;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            return row;
        }

        /// <summary>켜기/끄기 줄: 줄 전체가 단추, 오른쪽 스위치</summary>
        private static SettingRow ToggleRow(RectTransform card, string name, string iconKey, string text, Color accent)
        {
            RectTransform row = SettingsRow(card, name, iconKey, text, accent);
            var button = row.gameObject.AddComponent<Button>();
            button.targetGraphic = row.GetComponent<Image>();
            ColorBlock cb = button.colors;
            cb.pressedColor = new Color(0.8f, 0.8f, 0.85f);
            button.colors = cb;

            RectTransform trackRect = Rect("Switch", row, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-24f, 0f), new Vector2(190f, 80f));
            Image track = Img(trackRect, new Color(0.32f, 0.35f, 0.44f), _kit != null ? _kit.Badge : null);
            if (_kit != null) track.type = Image.Type.Sliced;
            TMP_Text state = Label("State", trackRect, "꺼짐", 28, TextAlignmentOptions.MidlineRight);
            Stretch(state.rectTransform);
            state.rectTransform.offsetMin = new Vector2(18f, 0f);
            state.rectTransform.offsetMax = new Vector2(-18f, 0f);
            RectTransform knob = Rect("Knob", trackRect, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(6f, 0f), new Vector2(68f, 68f));
            Img(knob, Color.white, _circle);

            var view = row.gameObject.AddComponent<SettingRow>();
            using (var props = new Props(view))
            {
                props.Bool("toggle", true).Refs("buttons", new[] { button }).Refs("images", new[] { track }).Refs("labels", new TMP_Text[] { state }).Ref("knob", knob);
            }

            return view;
        }

        /// <summary>고르기 줄: 오른쪽에 칸 2~5개 (고른 칸이 노랗게)</summary>
        private static SettingRow SegmentRow(RectTransform card, string name, string iconKey, string text, Color accent, params string[] options)
        {
            RectTransform row = SettingsRow(card, name, iconKey, text, accent);
            const float area = 640f, gap = 8f;
            RectTransform box = Rect("Choices", row, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-16f, 0f), new Vector2(area, 88f));
            float w = (area - gap * (options.Length - 1)) / options.Length;
            var buttons = new Button[options.Length];
            var images = new Image[options.Length];
            var labels = new TMP_Text[options.Length];
            for (int i = 0; i < options.Length; i++)
            {
                buttons[i] = Btn("Choice" + i, box, options[i], new Vector2(-area * 0.5f + w * 0.5f + i * (w + gap), 0f), new Vector2(w, 88f), ButtonBlue, out labels[i]);
                images[i] = buttons[i].image;
                labels[i].fontSize = 30;
                labels[i].enableAutoSizing = true;
                labels[i].fontSizeMin = 24f;
                labels[i].fontSizeMax = 30f;
                labels[i].textWrappingMode = TextWrappingModes.NoWrap;
                labels[i].rectTransform.offsetMin = new Vector2(4f, 0f);
                labels[i].rectTransform.offsetMax = new Vector2(-4f, 0f);
            }

            var view = row.gameObject.AddComponent<SettingRow>();
            using (var props = new Props(view))
            {
                props.Bool("toggle", false).Refs("buttons", buttons).Refs("images", images).Refs("labels", labels);
            }

            return view;
        }

        private static Button ListButton(RectTransform card, string name, string iconKey, string text, Color color, float height)
        {
            Button b = Btn(name, card, text, Vector2.zero, new Vector2(SettingsWidth - 40f, height), color, out TMP_Text label);
            FixedHeight(b, height);
            label.fontSize = 34;
            label.enableAutoSizing = true;
            label.fontSizeMin = 28f;
            label.fontSizeMax = 36f;
            Sprite icon = Icon(iconKey);
            if (icon != null)
            {
                RectTransform ir = Rect("Icon", b.transform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(24f, 3f), new Vector2(68f, 68f));
                Img(ir, Color.white, icon).preserveAspect = true;
                label.rectTransform.offsetMin = new Vector2(110f, 0f);
                label.rectTransform.offsetMax = new Vector2(-110f, 0f);
            }

            return b;
        }

        private static SettingsPanel BuildSettingsPanel(Transform parent, bool lobby = false)
        {
            RectTransform panel = Overlay(parent, "SettingsPanel", out GameObject holder);
            var screen = holder.AddComponent<SettingsPanel>();

            // 안전 영역 안쪽 (로비는 부모가 이미 안전 영역이라 한 번만 맞춘다)
            RectTransform safe = Rect("Safe", panel, Vector2.zero, Vector2.one, Center, Vector2.zero, Vector2.zero);
            if (!lobby) safe.gameObject.AddComponent<SafeAreaFitter>();

            // ----- 제목 리본 + X -----
            RectTransform ribbon = Rect("TitleRibbon", safe, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -20f), new Vector2(780f, 130f));
            if (_kit != null)
            {
                Image ribbonImage = Img(ribbon, new Color(0.3f, 0.5f, 0.88f), _kit.Ribbon);
                ribbonImage.type = Image.Type.Sliced;
            }

            TMP_Text title = Label("Title", ribbon, "설정", 64, TextAlignmentOptions.Center);
            Stretch(title.rectTransform);
            Button closeX = Btn("CloseX", safe, "", Vector2.zero, new Vector2(110f, 110f), ButtonGray, out TMP_Text closeLabel);
            closeLabel.text = "";
            var closeRect = (RectTransform)closeX.transform;
            closeRect.anchorMin = closeRect.anchorMax = new Vector2(1f, 1f);
            closeRect.pivot = new Vector2(1f, 1f);
            closeRect.anchoredPosition = new Vector2(-24f, -30f);
            Sprite closeIcon = Icon("close");
            if (closeIcon != null) Img(Rect("Icon", closeRect, Center, Center, Center, Vector2.zero, new Vector2(56f, 56f)), Color.white, closeIcon).preserveAspect = true;

            // ----- 아래 큰 닫기 -----
            Button back = Btn("Back", safe, "닫기", Vector2.zero, new Vector2(SettingsWidth, 130f), ButtonBlue, out TMP_Text backLabel);
            var backRect = (RectTransform)back.transform;
            backRect.anchorMin = backRect.anchorMax = new Vector2(0.5f, 0f);
            backRect.pivot = new Vector2(0.5f, 0f);
            backRect.anchoredPosition = new Vector2(0f, 24f);
            backLabel.fontSize = 48;

            // ----- 가운데 스크롤 (카드들) -----
            RectTransform viewport = Rect("Viewport", safe, Vector2.zero, Vector2.one, Center, Vector2.zero, Vector2.zero);
            viewport.offsetMin = new Vector2(24f, 176f);
            viewport.offsetMax = new Vector2(-24f, -170f);
            viewport.gameObject.AddComponent<RectMask2D>();
            Img(viewport, new Color(0f, 0f, 0f, 0.001f), null).raycastTarget = true;
            RectTransform content = Rect("Content", viewport, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), Vector2.zero, new Vector2(0f, 100f));
            var layout = content.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(0, 0, 8, 24);
            layout.spacing = 22f;
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            var scroll = viewport.gameObject.AddComponent<ScrollRect>();
            scroll.content = content;
            scroll.viewport = viewport;
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 50f;

            Color orange = new Color(1f, 0.62f, 0.3f), sky = new Color(0.55f, 0.82f, 1f), violet = new Color(0.78f, 0.62f, 1f), mint = new Color(0.5f, 0.9f, 0.65f);
            string[] volume = { "0%", "25%", "50%", "75%", "100%" };

            RectTransform sound = SettingsCard(content, "CardSound", "소리 · 그래픽", orange);
            SettingRow music = SegmentRow(sound, "MusicVolume", "play", "배경음", orange, volume);
            SettingRow sfx = SegmentRow(sound, "SfxVolume", "lightning", "효과음", orange, volume);
            SettingRow vibration = ToggleRow(sound, "Vibration", "momentum", "진동", orange);
            // 그래픽 품질 3단 (v1.0 17번): 낮음 = 그림자 끔 · 해상도 75% (느린 폰), 높음 = 계단 줄임
            SettingRow graphics = SegmentRow(sound, "Graphics", "star", "그래픽", orange, "낮음", "보통", "높음");

            RectTransform screenCard = SettingsCard(content, "CardScreen", "화면", sky);
            SettingRow shake = SegmentRow(screenCard, "Shake", "whirlwind", "화면 흔들림", sky, "끄기", "약하게", "보통");
            SettingRow numbers = ToggleRow(screenCard, "DamageNumbers", "crit_dmg", "피해 숫자", sky);
            SettingRow flash = ToggleRow(screenCard, "ReduceFlashes", "meteor", "번쩍임 줄이기", sky);
            SettingsNote(screenCard, "Help", "번쩍임 줄이기: 화면을 크게 덮는 섬광을 옅게, 1초에 3번까지만");
            SettingRow frame = SegmentRow(screenCard, "Frame", "clock", "프레임", sky, "30 절약", "60 부드럽게");

            RectTransform control = SettingsCard(content, "CardControl", "조작", violet);
            SettingRow joyMode = SegmentRow(control, "JoystickMode", "move_up", "조이스틱", violet, "누른 자리", "고정");
            SettingRow joySize = SegmentRow(control, "JoystickSize", "spin_blade", "조이스틱 크기", violet, "작게", "보통", "크게");
            SettingRow joyOpacity = SegmentRow(control, "JoystickOpacity", "dodge", "조이스틱 진하기", violet, "연하게", "보통", "진하게");

            // v1.0 알림 (LiveOps 9장: 종류별로 끈다)
            RectTransform notice = SettingsCard(content, "CardNotice", "알림", mint);
            SettingRow hearts = ToggleRow(notice, "NotifyHearts", "heart", "하트가 다 차면", mint);
            SettingRow patrol = ToggleRow(notice, "NotifyPatrol", "patrol", "순찰이 가득 차면", mint);
            SettingsNote(notice, "NoticeHelp", "밤 9시~아침 8시에는 보내지 않아요 · 하루 2번까지");

            Button replay = null, infoButton = null;
            InfoPanel info = null;
            if (lobby)
            {
                // 계정 (LiveOps 3장): 상태 + 카카오 연결 · 지금 클라우드 저장 / 계정 삭제(위험)
                RectTransform account = SettingsCard(content, "CardAccount", "계정", Gold);
                RectTransform statusRow = SettingsRow(account, "AccountRow", "hero", "", Gold, 150f);
                statusRow.Find("Name").gameObject.SetActive(false);
                RectTransform badge = Rect("KakaoBadge", statusRow, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-20f, 0f), new Vector2(84f, 84f));
                Img(badge, new Color(0.99f, 0.9f, 0.05f), _circle);
                TMP_Text badgeText = Label("Mark", badge, "K", 52, TextAlignmentOptions.Center, new Color(0.22f, 0.12f, 0f));
                Stretch(badgeText.rectTransform);
                TMP_Text accountStatus = Label("AccountStatus", statusRow, "계정: 연결 중…", 30, TextAlignmentOptions.MidlineLeft);
                accountStatus.rectTransform.anchorMin = Vector2.zero;
                accountStatus.rectTransform.anchorMax = Vector2.one;
                accountStatus.rectTransform.offsetMin = new Vector2(106f, 6f);
                accountStatus.rectTransform.offsetMax = new Vector2(-116f, -6f);
                accountStatus.enableAutoSizing = true;
                accountStatus.fontSizeMin = 24f;
                accountStatus.fontSizeMax = 30f;
                Button kakao = ListButton(account, "KakaoLink", null, "카카오 계정 연결", ButtonGreen, 130f);
                kakao.image.color = new Color(0.99f, 0.9f, 0.1f);
                TMP_Text kakaoLabel = kakao.GetComponentInChildren<TMP_Text>();
                kakaoLabel.color = new Color(0.22f, 0.12f, 0f);
                kakaoLabel.fontSize = 40;
                Button cloud = ListButton(account, "CloudSave", "archive", "지금 클라우드에 저장", ButtonBlue, 120f);
                Button delete = ListButton(account, "DeleteAccount", "skull", "계정 삭제", new Color(0.7f, 0.2f, 0.2f), 100f);
                var accountSection = panel.gameObject.AddComponent<AccountSection>();
                using (var props = new Props(accountSection))
                {
                    props.Ref("statusLabel", accountStatus).Ref("kakaoButton", kakao).Ref("kakaoLabel", kakaoLabel)
                        .Ref("cloudButton", cloud).Ref("deleteButton", delete);
                }

                RectTransform about = SettingsCard(content, "CardInfo", "정보", sky);
                replay = ListButton(about, "ReplayTutorial", "training", "튜토리얼 다시 보기", ButtonBlue, 120f);
                infoButton = ListButton(about, "Info", "info", "정보 · 약관 · 크레딧", ButtonGray, 120f);
                info = BuildInfoPanel(parent);
            }

            using (var props = new Props(screen))
            {
                props.Ref("root", panel.gameObject).Ref("backButton", back).Ref("closeButton", closeX)
                    .Ref("musicRow", music).Ref("sfxRow", sfx).Ref("vibrationRow", vibration).Ref("graphicsRow", graphics)
                    .Ref("shakeRow", shake).Ref("numbersRow", numbers).Ref("flashRow", flash).Ref("frameRow", frame)
                    .Ref("joystickModeRow", joyMode).Ref("joystickSizeRow", joySize).Ref("joystickOpacityRow", joyOpacity)
                    .Ref("heartsRow", hearts).Ref("patrolRow", patrol)
                    .Ref("replayTutorialButton", replay).Ref("infoButton", infoButton).Ref("info", info);
            }

            return screen;
        }
    }
}
