using HistoryHeroes.Combat;
using HistoryHeroes.Common;
using HistoryHeroes.Dungeon;
using HistoryHeroes.Enemy;
using HistoryHeroes.Player;
using HistoryHeroes.Progress;
using HistoryHeroes.Skills;
using HistoryHeroes.UI;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace HistoryHeroes.EditorTools
{
    public static partial class V01SceneBuilder
    {
        // 방 안쪽 크기 (벽 안). 세로 화면에 맞춘 8 × 14
        private const float ArenaHalfWidth = 4f;
        private const float ArenaHalfHeight = 7f;

        private static void BuildScene(Scene scene, Data d, Prefabs p)
        {
            // ----- 카메라·빛 -----
            var cameraObject = new GameObject("Main Camera") { tag = "MainCamera" };
            cameraObject.transform.position = new Vector3(0f, 1f, -10f); // 위쪽 HUD 자리만큼 올림
            var camera = cameraObject.AddComponent<Camera>();
            camera.orthographic = true;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.07f, 0.08f, 0.1f);
            cameraObject.AddComponent<CameraFit>();

            var light = new GameObject("Global Light 2D").AddComponent<Light2D>();
            light.lightType = Light2D.LightType.Global;
            light.intensity = 1f;

            // ----- 방: 바닥, 벽, 문 -----
            var arena = new GameObject("Arena").transform;
            Block("Floor", arena, Vector2.zero, new Vector2(ArenaHalfWidth * 2f, ArenaHalfHeight * 2f), new Color(0.18f, 0.24f, 0.18f), -10, collider: false);
            Color wall = new Color(0.36f, 0.31f, 0.26f);
            Block("Wall Left", arena, new Vector2(-ArenaHalfWidth - 0.5f, 0f), new Vector2(1f, ArenaHalfHeight * 2f + 2f), wall, -5, collider: true);
            Block("Wall Right", arena, new Vector2(ArenaHalfWidth + 0.5f, 0f), new Vector2(1f, ArenaHalfHeight * 2f + 2f), wall, -5, collider: true);
            Block("Wall Bottom", arena, new Vector2(0f, -ArenaHalfHeight - 0.5f), new Vector2(ArenaHalfWidth * 2f + 2f, 1f), wall, -5, collider: true);
            Block("Wall Top", arena, new Vector2(0f, ArenaHalfHeight + 0.5f), new Vector2(ArenaHalfWidth * 2f + 2f, 1f), wall, -5, collider: true);

            GameObject doorObject = Block("Door", arena, new Vector2(0f, ArenaHalfHeight - 0.3f), new Vector2(1.8f, 0.6f), Color.gray, -4, collider: true);
            doorObject.layer = 0;
            doorObject.GetComponent<BoxCollider2D>().isTrigger = true;
            var door = doorObject.AddComponent<Door>();
            using (var props = new Props(door)) props.Ref("sprite", doorObject.GetComponent<SpriteRenderer>());

            var playerStart = new GameObject("PlayerStart").transform;
            playerStart.position = new Vector2(0f, -ArenaHalfHeight + 1.5f);

            // ----- UI (조이스틱이 플레이어에 연결돼야 해서 먼저 만든다) -----
            Ui ui = BuildUi();

            // ----- 플레이어 -----
            var playerObject = new GameObject("Player") { layer = PlayerLayer };
            playerObject.transform.position = playerStart.position;
            var body = playerObject.AddComponent<Rigidbody2D>();
            body.gravityScale = 0f;
            body.freezeRotation = true;
            body.interpolation = RigidbodyInterpolation2D.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            playerObject.AddComponent<CircleCollider2D>().radius = 0.4f;
            playerObject.AddComponent<Health>();
            SpriteRenderer playerBody = Visual(playerObject.transform, "Body", _circle, d.Khan.color, new Vector2(0.9f, 0.9f), 3);
            using (var flash = new Props(playerObject.AddComponent<HitFlash>())) flash.Ref("target", playerBody).Color("flashColor", new Color(1f, 0.3f, 0.3f));
            SpriteRenderer stopFill = Visual(playerObject.transform, "StopIndicator", _circle, Color.white, Vector2.one, -1);
            Visual(playerObject.transform, "Facing", _square, new Color(1f, 1f, 1f, 0.8f), new Vector2(0.18f, 0.18f), 4).transform.localPosition = new Vector2(0f, 0.3f);
            var player = playerObject.AddComponent<PlayerController>();
            using (var props = new Props(player))
            {
                props.Ref("hero", d.Khan).Ref("joystick", ui.Joystick).Ref("projectilePrefab", p.PlayerArrow)
                    .Mask("enemyMask", Mask(EnemyLayer)).Mask("wallMask", Mask(WallLayer));
            }

            using (var props = new Props(playerObject.AddComponent<StopIndicator>())) props.Ref("player", player).Ref("fill", stopFill);

            // ----- 시스템 -----
            var systems = new GameObject("Systems");
            var experience = systems.AddComponent<ExperienceSystem>();
            using (var props = new Props(experience)) props.Ref("player", player).Ref("gemPrefab", p.Gem);

            var enemyProjectiles = systems.AddComponent<EnemyProjectiles>();
            using (var props = new Props(enemyProjectiles)) props.Ref("prefab", p.EnemyArrow).Mask("wallMask", Mask(WallLayer));

            var skills = systems.AddComponent<SkillManager>();
            using (var props = new Props(skills))
            {
                props.Ref("player", player).Refs("skillPool", d.Pool).Refs("recipes", d.Recipes)
                    .Ref("rarityTable", d.Rarity).Mask("enemyMask", Mask(EnemyLayer));
            }

            using (var props = new Props(ui.Hud)) props.Ref("player", player).Ref("experience", experience);
            using (var props = new Props(ui.LevelUp)) props.Ref("experience", experience).Ref("skills", skills);

            var run = new GameObject("DungeonRun").AddComponent<DungeonRun>();
            using (var props = new Props(run))
            {
                props.Ref("dungeon", d.Dungeon).Ref("player", player).Ref("experience", experience).Ref("skills", skills)
                    .Ref("door", door).Ref("playerStart", playerStart)
                    .Ref("levelUp", ui.LevelUp).Ref("hud", ui.Hud).Ref("result", ui.Result).Ref("fade", ui.Fade);
            }

            var cheats = systems.AddComponent<Dev.DevCheats>();
            using (var props = new Props(cheats)) props.Ref("experience", experience).Ref("levelUp", ui.LevelUp).Ref("skills", skills);
            SetPresets(cheats, d);

            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
        }

        /// <summary>
        /// 빌드 3종 프리셋 (GameDesign 6-8). 셋 다 레벨업 약 15번어치로 맞춰서 공정하게 비교한다.
        /// 조합은 "재료 레벨 + 조합 카드 1장" = 7번으로 센다.
        /// </summary>
        private static void SetPresets(Dev.DevCheats cheats, Data d)
        {
            SkillData Find(string id) => d.Pool.Find(s => s.id == id);
            var presets = new (string name, (SkillData skill, int level, bool fusion)[] skills)[]
            {
                ("연사형 (공속 4 · 멀티샷 3 · 공격력 4 · 흡혈 4)", new[]
                {
                    (Find("aspd_up"), 4, false), (Find("multishot"), 3, false), (Find("atk_up"), 4, false), (Find("lifesteal"), 4, false),
                }),
                ("칼날 탱커형 (강철 회오리 · 방어력 5 · 최대체력 4)", new[]
                {
                    ((SkillData)d.SteelWhirl, 1, true), (Find("def_up"), 5, false), (Find("hp_up"), 4, false),
                }),
                ("번개 광역형 (뇌화 폭풍 Lv3 · 최대체력 5 · 공격력 1)", new[]
                {
                    ((SkillData)d.ThunderFire, 3, true), (Find("hp_up"), 5, false), (Find("atk_up"), 1, false),
                }),
            };

            var so = new SerializedObject(cheats);
            SerializedProperty list = so.FindProperty("presets");
            list.arraySize = presets.Length;
            for (int i = 0; i < presets.Length; i++)
            {
                SerializedProperty preset = list.GetArrayElementAtIndex(i);
                preset.FindPropertyRelative("name").stringValue = presets[i].name;
                SerializedProperty entries = preset.FindPropertyRelative("skills");
                entries.arraySize = presets[i].skills.Length;
                for (int j = 0; j < presets[i].skills.Length; j++)
                {
                    SerializedProperty e = entries.GetArrayElementAtIndex(j);
                    e.FindPropertyRelative("skill").objectReferenceValue = presets[i].skills[j].skill;
                    e.FindPropertyRelative("level").intValue = presets[i].skills[j].level;
                    e.FindPropertyRelative("fusionResult").boolValue = presets[i].skills[j].fusion;
                }
            }

            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static GameObject Block(string name, Transform parent, Vector2 position, Vector2 size, Color color, int order, bool collider)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = position;
            go.transform.localScale = new Vector3(size.x, size.y, 1f);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = _square;
            sr.color = color;
            sr.sortingOrder = order;
            if (collider)
            {
                go.layer = WallLayer;
                go.AddComponent<BoxCollider2D>(); // 크기 1 × 스케일 = 보이는 크기
            }

            return go;
        }

        private static SpriteRenderer Visual(Transform parent, string name, Sprite sprite, Color color, Vector2 size, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localScale = new Vector3(size.x, size.y, 1f);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.color = color;
            sr.sortingOrder = order;
            return sr;
        }

        // =====================================================================
        // UI (기준 해상도 1080 × 1920 세로)
        // =====================================================================
        private sealed class Ui
        {
            public FloatingJoystick Joystick;
            public HudView Hud;
            public LevelUpScreen LevelUp;
            public RunResultScreen Result;
            public CanvasGroup Fade;
        }

        private static readonly Vector2 Center = new Vector2(0.5f, 0.5f);

        private static Ui BuildUi()
        {
            var ui = new Ui();

            var canvasObject = new GameObject("Canvas", typeof(RectTransform));
            var canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080f, 1920f);
            scaler.matchWidthOrHeight = 0.5f;
            canvasObject.AddComponent<GraphicRaycaster>();
            Transform root = canvasObject.transform;

            var eventSystem = new GameObject("EventSystem", typeof(EventSystem));
            eventSystem.AddComponent<InputSystemUIInputModule>().AssignDefaultActions();

            // ----- HUD -----
            RectTransform hudRect = Rect("HUD", root, Vector2.zero, Vector2.one, Center, Vector2.zero, Vector2.zero);
            ui.Hud = hudRect.gameObject.AddComponent<HudView>();

            RectTransform expBg = Rect("ExpBar", hudRect, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -10f), new Vector2(-40f, 18f));
            Fill(expBg, new Color(0.1f, 0.1f, 0.12f, 0.85f), new Color(0.3f, 0.85f, 1f), out Image expFill);

            RectTransform hpBg = Rect("HpBar", hudRect, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(40f, -50f), new Vector2(560f, 46f));
            Fill(hpBg, new Color(0.1f, 0.1f, 0.12f, 0.85f), new Color(0.85f, 0.22f, 0.25f), out Image hpFill);
            TMP_Text hpLabel = Label("HpLabel", hpBg, "1000 / 1000", 28, TextAlignmentOptions.Center);
            Stretch(hpLabel.rectTransform);

            TMP_Text levelLabel = Label("LevelLabel", hudRect, "Lv 1", 46, TextAlignmentOptions.TopRight);
            Place(levelLabel.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-40f, -40f), new Vector2(260f, 64f));
            TMP_Text killLabel = Label("KillLabel", hudRect, "처치 0", 32, TextAlignmentOptions.TopRight);
            Place(killLabel.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-40f, -108f), new Vector2(300f, 50f));
            TMP_Text roomLabel = Label("RoomLabel", hudRect, "방 1/10", 38, TextAlignmentOptions.Top);
            Place(roomLabel.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -112f), new Vector2(520f, 60f));

            using (var props = new Props(ui.Hud))
            {
                props.Ref("hpFill", hpFill).Ref("hpLabel", hpLabel).Ref("expFill", expFill)
                    .Ref("levelLabel", levelLabel).Ref("roomLabel", roomLabel).Ref("killLabel", killLabel);
            }

            // ----- 조이스틱 (화면 아래쪽 어디든 누르면 그 자리에 생김) -----
            RectTransform joystickRect = Rect("Joystick", root, Vector2.zero, Vector2.one, Center, Vector2.zero, Vector2.zero);
            ui.Joystick = joystickRect.gameObject.AddComponent<FloatingJoystick>();
            RectTransform stickBg = Rect("Background", joystickRect, Vector2.zero, Vector2.zero, Center, Vector2.zero, new Vector2(240f, 240f));
            Img(stickBg, new Color(1f, 1f, 1f, 0.16f), _circle);
            RectTransform knob = Rect("Knob", stickBg, Center, Center, Center, Vector2.zero, new Vector2(110f, 110f));
            Img(knob, new Color(1f, 1f, 1f, 0.5f), _circle);
            stickBg.gameObject.SetActive(false);
            using (var props = new Props(ui.Joystick)) props.Ref("background", stickBg).Ref("knob", knob);

            // ----- 방 이동 페이드 -----
            RectTransform fadeRect = Rect("Fade", root, Vector2.zero, Vector2.one, Center, Vector2.zero, Vector2.zero);
            Img(fadeRect, Color.black, null);
            ui.Fade = fadeRect.gameObject.AddComponent<CanvasGroup>();
            ui.Fade.alpha = 0f;
            ui.Fade.blocksRaycasts = false;
            ui.Fade.interactable = false;

            ui.LevelUp = BuildLevelUp(root);
            ui.Result = BuildResult(root);
            return ui;
        }

        private static LevelUpScreen BuildLevelUp(Transform root)
        {
            RectTransform holder = Rect("LevelUp", root, Vector2.zero, Vector2.one, Center, Vector2.zero, Vector2.zero);
            var screen = holder.gameObject.AddComponent<LevelUpScreen>();

            RectTransform panel = Rect("Panel", holder, Vector2.zero, Vector2.one, Center, Vector2.zero, Vector2.zero);
            Img(panel, new Color(0f, 0f, 0f, 0.8f), null).raycastTarget = true; // 뒤쪽 터치 막기

            TMP_Text title = Label("Title", panel, "레벨 업!", 84, TextAlignmentOptions.Center, new Color(1f, 0.85f, 0.35f));
            Place(title.rectTransform, Center, Center, new Vector2(0f, 500f), new Vector2(900f, 110f));
            TMP_Text subtitle = Label("Subtitle", panel, "카드 1장을 고르세요", 34, TextAlignmentOptions.Center, new Color(1f, 1f, 1f, 0.75f));
            Place(subtitle.rectTransform, Center, Center, new Vector2(0f, 420f), new Vector2(900f, 50f));

            var cards = new SkillCardView[3];
            for (int i = 0; i < 3; i++) cards[i] = BuildCard(panel, i, new Vector2(-350f + 350f * i, 40f));

            Button reroll = Btn("Reroll", panel, "리롤 (광고) 2/2", new Vector2(-220f, -400f), new Vector2(390f, 120f), new Color(0.22f, 0.3f, 0.45f), out TMP_Text rerollLabel);
            Button hide = Btn("HideFusion", panel, "이번 판 숨기기", new Vector2(220f, -400f), new Vector2(390f, 120f), new Color(0.38f, 0.25f, 0.45f), out _);
            TMP_Text note = Label("RerollNote", panel, "리롤은 다시 뽑을 기회일 뿐, 더 좋은 카드를 보장하지 않아요", 24, TextAlignmentOptions.Center, new Color(1f, 1f, 1f, 0.55f));
            Place(note.rectTransform, Center, Center, new Vector2(0f, -500f), new Vector2(1000f, 40f));

            using (var props = new Props(screen))
            {
                props.Ref("root", panel.gameObject).Refs("cards", cards)
                    .Ref("rerollButton", reroll).Ref("rerollLabel", rerollLabel).Ref("hideFusionButton", hide);
            }

            return screen;
        }

        private static SkillCardView BuildCard(Transform parent, int index, Vector2 position)
        {
            RectTransform card = Rect($"Card {index + 1}", parent, Center, Center, Center, position, new Vector2(320f, 560f));
            Image background = Img(card, SkillCardView.Common, null);
            background.raycastTarget = true;
            var button = card.gameObject.AddComponent<Button>();
            button.targetGraphic = background;

            TMP_Text kind = TopLabel("Kind", card, 28, 24f, 40f);
            TMP_Text nameLabel = TopLabel("Name", card, 44, 74f, 110f);
            TMP_Text level = TopLabel("Level", card, 32, 190f, 44f);
            level.color = new Color(1f, 0.95f, 0.7f);

            TMP_Text description = Label("Description", card, "", 27, TextAlignmentOptions.Top);
            description.lineSpacing = 12f;
            RectTransform dr = description.rectTransform;
            dr.anchorMin = Vector2.zero;
            dr.anchorMax = Vector2.one;
            dr.offsetMin = new Vector2(18f, 70f);
            dr.offsetMax = new Vector2(-18f, -250f);

            TMP_Text material = Label("Material", card, "조합 재료", 26, TextAlignmentOptions.Bottom, new Color(1f, 0.9f, 0.3f));
            Place(material.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 24f), new Vector2(300f, 40f));
            material.rectTransform.pivot = new Vector2(0.5f, 0f);

            var view = card.gameObject.AddComponent<SkillCardView>();
            using (var props = new Props(view))
            {
                props.Ref("button", button).Ref("background", background).Ref("kindLabel", kind).Ref("nameLabel", nameLabel)
                    .Ref("levelLabel", level).Ref("descriptionLabel", description).Ref("materialLabel", material);
            }

            return view;
        }

        private static RunResultScreen BuildResult(Transform root)
        {
            RectTransform holder = Rect("Result", root, Vector2.zero, Vector2.one, Center, Vector2.zero, Vector2.zero);
            var screen = holder.gameObject.AddComponent<RunResultScreen>();

            RectTransform panel = Rect("Panel", holder, Vector2.zero, Vector2.one, Center, Vector2.zero, Vector2.zero);
            Img(panel, new Color(0.04f, 0.05f, 0.08f, 0.95f), null).raycastTarget = true;

            TMP_Text title = Label("Title", panel, "클리어!", 100, TextAlignmentOptions.Center, new Color(1f, 0.85f, 0.35f));
            Place(title.rectTransform, Center, Center, new Vector2(0f, 640f), new Vector2(900f, 130f));
            TMP_Text body = Label("Body", panel, "", 40, TextAlignmentOptions.TopLeft);
            body.lineSpacing = 28f;
            Place(body.rectTransform, Center, Center, new Vector2(0f, 40f), new Vector2(820f, 1000f));

            Button retry = Btn("Retry", panel, "다시 도전", new Vector2(0f, -680f), new Vector2(520f, 140f), new Color(0.2f, 0.55f, 0.32f), out _);

            using (var props = new Props(screen))
            {
                props.Ref("root", panel.gameObject).Ref("titleLabel", title).Ref("bodyLabel", body).Ref("retryButton", retry);
            }

            return screen;
        }

        // ----- UI 도우미 -----
        private static RectTransform Rect(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 position, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.pivot = pivot;
            rt.anchoredPosition = position;
            rt.sizeDelta = size;
            return rt;
        }

        private static void Place(RectTransform rt, Vector2 anchorMin, Vector2 anchorMax, Vector2 position, Vector2 size)
        {
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.pivot = new Vector2(anchorMin.x == anchorMax.x ? anchorMin.x : 0.5f, anchorMin.y == anchorMax.y ? anchorMin.y : 0.5f);
            rt.anchoredPosition = position;
            rt.sizeDelta = size;
        }

        private static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        private static Image Img(RectTransform rt, Color color, Sprite sprite)
        {
            var image = rt.gameObject.AddComponent<Image>();
            image.color = color;
            image.sprite = sprite;
            image.raycastTarget = false;
            return image;
        }

        private static void Fill(RectTransform background, Color backColor, Color fillColor, out Image fill)
        {
            Img(background, backColor, _square);
            RectTransform fillRect = Rect("Fill", background, Vector2.zero, Vector2.one, Center, Vector2.zero, Vector2.zero);
            Stretch(fillRect);
            fill = Img(fillRect, fillColor, _square);
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Horizontal;
            fill.fillOrigin = (int)Image.OriginHorizontal.Left;
            fill.fillAmount = 1f;
        }

        private static TMP_Text Label(string name, Transform parent, string text, float size, TextAlignmentOptions align, Color? color = null)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var label = go.AddComponent<TextMeshProUGUI>();
            label.font = _font;
            label.text = text;
            label.fontSize = size;
            label.alignment = align;
            label.color = color ?? Color.white;
            label.textWrappingMode = TextWrappingModes.Normal;
            label.raycastTarget = false;
            return label;
        }

        /// <summary>카드 위쪽에 붙는 한 줄 글자 (top: 카드 위에서부터 거리)</summary>
        private static TMP_Text TopLabel(string name, RectTransform card, float size, float top, float height)
        {
            TMP_Text label = Label(name, card, "", size, TextAlignmentOptions.Center);
            RectTransform rt = label.rectTransform;
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.anchoredPosition = new Vector2(0f, -top);
            rt.sizeDelta = new Vector2(-24f, height);
            return label;
        }

        private static Button Btn(string name, Transform parent, string text, Vector2 position, Vector2 size, Color color, out TMP_Text label)
        {
            RectTransform rt = Rect(name, parent, Center, Center, Center, position, size);
            Image image = Img(rt, color, null);
            image.raycastTarget = true;
            var button = rt.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            label = Label("Label", rt, text, 34, TextAlignmentOptions.Center);
            Stretch(label.rectTransform);
            return button;
        }
    }
}
