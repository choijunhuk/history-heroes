using HistoryHeroes.Common;
using HistoryHeroes.UI;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

namespace HistoryHeroes.EditorTools
{
    /// <summary>
    /// 로비 첫 화면 (그래픽 평가 3번 "로비가 가장 약하다" + 조사: 아처로 2 로비 + 개발자 결정 "두 큰 모드"):
    /// 위 재화 칸 · 가운데 구간 섬 무대(3D) 위 지금 영웅 · 구간 카드(리본 이름 · 별 · 보스 도전) ·
    /// 큰 모드 2개(던전 · 서바이벌) · 도전 줄(무한의 균열 · 일일 도전) · 아래 탭 5개 · 양옆 작은 버튼.
    /// </summary>
    public static partial class SceneBuilder
    {
        private sealed class LobbyMainUi
        {
            public TMP_Text Wallet, Notice, Gold, Gem, Heart, Level, Chapter, ChapterInfo, HeroName;
            public Button Prev, Next, Dungeon, Boss, Survival, Rift, Daily, Archive, Codex, Hero, Settings, Shop, Missions, Newcomer, Patrol, HomeMission;
            public Image HomeMissionFill;
            public TMP_Text BossLabel, SurvivalLabel, RiftLabel, DailyLabel, ArchiveLabel, CodexLabel, MissionLabel, NewcomerLabel, PatrolLabel, HomeMissionLabel, HeroBadge, NewcomerBadge;
            public LobbyDiorama Diorama;
        }

        private static readonly Color LobbyYellow = new Color(1f, 0.76f, 0.16f);
        private static readonly Color LobbyOrange = new Color(1f, 0.48f, 0.18f);

        /// <summary>로비 카메라: 원근 · 3D 렌더러 · 그림자 · 후처리 (가운데 섬 무대를 그린다)</summary>
        private static Camera LobbyCamera()
        {
            var cameraObject = new GameObject("Main Camera") { tag = "MainCamera" };
            var camera = cameraObject.AddComponent<Camera>();
            camera.orthographic = false;
            camera.fieldOfView = 32f;
            camera.nearClipPlane = 0.5f;
            camera.farClipPlane = 80f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.1f, 0.12f, 0.18f);
            UniversalAdditionalCameraData data = camera.GetUniversalAdditionalCameraData();
            data.SetRenderer(1); // 3D 렌더러
            data.renderShadows = true;
            data.renderPostProcessing = true;

            // 영웅 발밑이 화면 55% 높이에 오게 조금 아래를 본다. 세로 화면에서 섬 폭(약 6.4)이 다 들어오게 거리를 잡는다
            const float pitch = 24f;
            float aspect = 9f / 19.5f;
            float halfTan = Mathf.Tan(camera.fieldOfView * 0.5f * Mathf.Deg2Rad);
            float distance = 5.6f / aspect / (2f * halfTan); // 섬을 크게 (재평가: 작은 덩어리로 보인다)
            Quaternion rotation = Quaternion.Euler(pitch, 0f, 0f);
            Vector3 up = rotation * Vector3.up;
            float visibleHeight = 2f * distance * halfTan;
            Vector3 aim = new Vector3(0f, 0f, -0.3f) - up * (0.09f * visibleHeight); // 발밑이 화면 55% 높이 (이름표 바로 위)
            cameraObject.transform.SetPositionAndRotation(aim - rotation * Vector3.forward * distance, rotation);

            var volume = new GameObject("PostFx").AddComponent<Volume>();
            volume.isGlobal = true;
            volume.sharedProfile = BattleVolumeProfile();
            return camera;
        }

        /// <summary>가운데 섬 무대: 떠 있는 돌 섬 + 구간마다 다른 장식 + 영웅 자리 (발밑 빛 고리)</summary>
        private static LobbyDiorama BuildLobbyDiorama(Camera camera)
        {
            Stage stage = _stage ?? CreateStageMaterials();
            var root = new GameObject("LobbyDiorama");
            var diorama = root.AddComponent<LobbyDiorama>();

            var sun = new GameObject("Sun").AddComponent<Light>();
            sun.transform.SetParent(root.transform, false);
            sun.type = LightType.Directional;
            sun.intensity = 1.35f;
            sun.shadows = LightShadows.Hard;
            sun.shadowStrength = 0.65f;
            sun.transform.rotation = Quaternion.Euler(48f, -35f, 0f);
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.55f, 0.57f, 0.64f);

            // 게임 장식(블렌더 소품)은 "XY 바닥 · -Z 위"로 세워져 있다 → +90° 눕힌 받침 안에 놓으면 Unity의 Y 위 세상에 선다
            var ground = new GameObject("Ground").transform;
            ground.SetParent(root.transform, false);
            ground.localRotation = Quaternion.Euler(90f, 0f, 0f);

            const float radius = 2.7f;
            var top = new GameObject("Top");
            top.transform.SetParent(ground, false);
            top.transform.localScale = new Vector3(radius * 2f, radius * 2f, 1f);
            top.AddComponent<MeshFilter>().sharedMesh = DiscMesh();
            var floor = top.AddComponent<MeshRenderer>();
            floor.sharedMaterial = stage.BossFloor;
            floor.shadowCastingMode = ShadowCastingMode.Off;

            // 섬 옆면(돌벽) + 아래 바위 덩이 (떠 있는 섬)
            var side = new GameObject("Side");
            side.transform.SetParent(root.transform, false);
            side.transform.localPosition = new Vector3(0f, -0.3f, 0f);
            side.transform.localScale = new Vector3(radius, 0.3f, radius); // 내장 원기둥은 지름 2 · 높이 2
            side.AddComponent<MeshFilter>().sharedMesh = Builtin(Stage3D.Shape.Pillar);
            side.AddComponent<MeshRenderer>().sharedMaterial = stage.Rim;
            var rock = new GameObject("Rock");
            rock.transform.SetParent(root.transform, false);
            rock.transform.localPosition = new Vector3(0f, -1.1f, 0f);
            rock.transform.localScale = new Vector3(radius * 0.95f, 0.9f, radius * 0.95f); // 내장 공은 지름 2
            rock.AddComponent<MeshFilter>().sharedMesh = Builtin(Stage3D.Shape.Rock);
            rock.AddComponent<MeshRenderer>().sharedMaterial = stage.Outside;

            // 영웅 자리 + 발밑 빛 고리
            var spot = new GameObject("HeroSpot").transform;
            spot.SetParent(root.transform, false);
            spot.localPosition = new Vector3(0f, 0.02f, -0.3f);
            var ring = new GameObject("HeroRing");
            ring.transform.SetParent(ground, false);
            ring.transform.localPosition = new Vector3(0f, -0.3f, -0.03f);
            ring.transform.localScale = new Vector3(1.7f, 1.7f, 1f);
            ring.AddComponent<MeshFilter>().sharedMesh = Builtin(Stage3D.Shape.Floor);
            var ringRenderer = ring.AddComponent<MeshRenderer>();
            ringRenderer.sharedMaterial = FxGlowMaterial("HeroRing", new Color(1f, 0.85f, 0.5f), _kit != null ? _kit.Ring : null); // 은은한 금빛 (전투 고리보다 약하게)
            ringRenderer.shadowCastingMode = ShadowCastingMode.Off;

            // 구간마다 장식 (게임 좌표: x 옆, y 뒤쪽(+) — 영웅 뒤와 양옆에 둔다)
            var groups = new GameObject[5];
            for (int c = 1; c <= 5; c++)
            {
                var group = new GameObject("ch" + c);
                group.transform.SetParent(ground, false);
                foreach (DecoItem item in DioramaDeco(c))
                {
                    GameObject prefab = Art3DStage.Prefab(item.Prefab);
                    if (prefab == null) continue;
                    var go = (GameObject)Object.Instantiate(prefab, group.transform, false);
                    go.name = item.Prefab;
                    go.transform.localPosition = item.Position;
                    go.transform.localRotation = Quaternion.Euler(0f, 0f, item.Turn);
                    go.transform.localScale = Vector3.one * item.Scale;
                    if (item.Fire) Fire(go.transform, go.transform.TransformPoint(new Vector3(0f, 0f, -0.99f)), item.Scale);
                }

                group.SetActive(c == 1);
                groups[c - 1] = group;
            }

            using (var props = new Props(diorama))
            {
                props.Ref("view", camera).Ref("sun", sun).Ref("floor", floor).Ref("heroSpot", spot).Refs("chapterGroups", groups);
                SetColors(props.Property("floorTints"), new Color(0.95f, 1.08f, 0.85f), new Color(0.95f, 1f, 1.12f), new Color(1.12f, 0.95f, 0.78f), new Color(0.92f, 0.86f, 1.1f), new Color(0.86f, 0.8f, 1f));
                SetColors(props.Property("sunColors"), new Color(1f, 0.95f, 0.82f), new Color(0.94f, 0.97f, 1f), new Color(1f, 0.86f, 0.66f), new Color(0.88f, 0.82f, 1f), new Color(1f, 0.88f, 0.74f));
                SetColors(props.Property("backgrounds"), new Color(0.16f, 0.24f, 0.22f), new Color(0.12f, 0.15f, 0.24f), new Color(0.2f, 0.14f, 0.1f), new Color(0.13f, 0.1f, 0.2f), new Color(0.1f, 0.07f, 0.14f));
            }

            return diorama;
        }

        private static void SetColors(SerializedProperty list, params Color[] colors)
        {
            list.arraySize = colors.Length;
            for (int i = 0; i < colors.Length; i++) list.GetArrayElementAtIndex(i).colorValue = colors[i];
        }

        /// <summary>섬 장식 (게임 좌표, 섬 반지름 2.7). 영웅(가운데 조금 앞) 뒤·옆에만</summary>
        private static DecoItem[] DioramaDeco(int chapter)
        {
            switch (chapter)
            {
                case 1:
                    return new[]
                    {
                        new DecoItem("Deco_Tree", -1.6f, 1.3f, 0f, 20f, 0.75f), new DecoItem("Deco_Bush", 1.7f, 0.7f, 0f, 60f, 1.1f),
                        new DecoItem("Deco_Banner", 1.1f, 1.7f), new DecoItem("Deco_Flowers", -1f, -1.1f, 0f, 0f, 2.2f),
                        new DecoItem("Deco_Flowers", 1.3f, -0.9f, 0f, 90f, 2f), new DecoItem("Deco_GrassTuft", -1.8f, -0.2f, 0f, 0f, 2.6f),
                        new DecoItem("Deco_Rubble", 0.2f, 2f, 0f, 160f, 0.9f),
                    };
                case 2:
                    return new[]
                    {
                        new DecoItem("Deco_Banner", -1f, 1.8f), new DecoItem("Deco_Banner", 1f, 1.8f),
                        new DecoItem("Deco_Brazier", -2f, 0.5f, fire: true), new DecoItem("Deco_Brazier", 2f, 0.5f, fire: true),
                        new DecoItem("Deco_Statue", -1.7f, 1.6f, 0f, 20f, 0.9f), new DecoItem("Deco_Barrel", 1.8f, 1.4f, 0f, 30f),
                    };
                case 3:
                    return new[]
                    {
                        new DecoItem("Deco_Gear", -1.5f, 1.5f, 0f, 15f, 1.1f), new DecoItem("Deco_Barrel", 1.8f, 0.8f),
                        new DecoItem("Deco_Barrel", 1.3f, 1.6f, 0f, 50f), new DecoItem("Deco_Brazier", 2f, -0.5f, fire: true),
                        new DecoItem("Crate_B", -2f, 0.2f, 0f, 20f, 0.8f), new DecoItem("Floor_Grate", 0.7f, 1f, 0f, 10f),
                    };
                case 4:
                    return new[]
                    {
                        new DecoItem("Deco_Crystal", -1.7f, 1.3f, 0f, 0f, 1.1f), new DecoItem("Deco_Crystal", 1.8f, 1f, 0f, 70f, 0.9f),
                        new DecoItem("Deco_Crystal", 0.5f, 2f, 0f, 30f, 0.7f), new DecoItem("Deco_Rubble", -1.5f, -0.9f, 0f, 40f),
                        new DecoItem("Bones", 1.3f, -1f, 0f, 200f, 1.2f),
                    };
                default:
                    return new[]
                    {
                        new DecoItem("Deco_Statue", -1.8f, 1.4f, 0f, 15f), new DecoItem("Deco_Statue", 1.8f, 1.4f, 0f, -15f),
                        new DecoItem("Deco_Brazier", -2.1f, 0.1f, fire: true), new DecoItem("Deco_Brazier", 2.1f, 0.1f, fire: true),
                        new DecoItem("Deco_Crystal", 0f, 2.1f, 0f, 0f, 0.8f), new DecoItem("Floor_Rug", 0f, -0.5f, 0f, 90f, 1.1f),
                    };
            }
        }

        // ---------------------------------------------------------------- UI

        /// <summary>아이콘 위 · 글자 아래 버튼 (작은 버튼 · 아래 탭)</summary>
        private static Button IconButton(string name, Transform parent, string iconKey, string text, Vector2 position, Vector2 size, Color color, out TMP_Text label)
        {
            Button button = Btn(name, parent, text, position, size, color, out label);
            var rt = (RectTransform)button.transform;
            Sprite icon = Icon(iconKey);
            if (icon != null)
            {
                float s = Mathf.Min(size.x, size.y) * 0.5f;
                RectTransform iconRect = Rect("Icon", rt, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -size.y * 0.1f), new Vector2(s, s));
                Image image = Img(iconRect, Color.white, icon);
                image.preserveAspect = true;
                var shadow = iconRect.gameObject.AddComponent<Shadow>();
                shadow.effectColor = new Color(0f, 0f, 0f, 0.5f);
                shadow.effectDistance = new Vector2(0f, -3f);
            }

            RectTransform lr = label.rectTransform;
            lr.anchorMin = new Vector2(0f, 0f);
            lr.anchorMax = new Vector2(1f, icon != null ? 0.42f : 1f);
            lr.offsetMin = new Vector2(4f, 6f);
            lr.offsetMax = new Vector2(-4f, 0f);
            label.margin = Vector4.zero;
            label.fontSize = 24f;
            label.enableAutoSizing = true;
            label.fontSizeMin = 14f;
            label.fontSizeMax = 24f;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            return button;
        }

        /// <summary>큰 모드 카드: 왼쪽 큰 아이콘 + 오른쪽 이름(설명은 글 안에서 작게)</summary>
        private static Button ModeCard(string name, Transform parent, string iconKey, string text, Vector2 position, Vector2 size, Color color, out TMP_Text label)
        {
            Button button = Btn(name, parent, text, position, size, color, out label);
            var rt = (RectTransform)button.transform;
            Sprite icon = Icon(iconKey);
            float s = size.y * 0.52f;
            if (icon != null)
            {
                RectTransform iconRect = Rect("Icon", rt, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(22f, 6f), new Vector2(s, s));
                Image image = Img(iconRect, Color.white, icon);
                image.preserveAspect = true;
                var shadow = iconRect.gameObject.AddComponent<Shadow>();
                shadow.effectColor = new Color(0f, 0f, 0f, 0.5f);
                shadow.effectDistance = new Vector2(0f, -4f);
            }

            label.rectTransform.offsetMin = new Vector2(icon != null ? s + 30f : 12f, 0f);
            label.rectTransform.offsetMax = new Vector2(-12f, 0f);
            label.fontSize = 56f;
            label.enableAutoSizing = true;
            label.fontSizeMin = 26f;
            label.fontSizeMax = 56f;
            label.lineSpacing = -10f;
            return button;
        }

        /// <summary>재화 칸 (위): 어두운 둥근 칸 + 아이콘 + 숫자</summary>
        private static TMP_Text Pill(Transform parent, string iconKey, Color iconColor, Vector2 position, float width)
        {
            RectTransform pill = Rect("Pill_" + iconKey, parent, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Center, position, new Vector2(width, 64f));
            Image bg = Img(pill, new Color(0.04f, 0.06f, 0.12f, 0.88f), _kit != null ? _kit.Badge : null);
            if (_kit != null) bg.type = Image.Type.Sliced;
            Sprite reward = RewardIcon(iconKey); // 컬러 보상 그림이 있으면 그것 (금화 · 보석 · 하트)
            Sprite icon = reward != null ? reward : Icon(iconKey);
            if (reward != null) iconColor = Color.white;
            if (icon != null)
            {
                RectTransform iconRect = Rect("Icon", pill, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(26f, 2f), new Vector2(58f, 58f));
                Image image = Img(iconRect, iconColor, icon);
                image.preserveAspect = true;
                var outline = iconRect.gameObject.AddComponent<Outline>();
                outline.effectColor = new Color(0f, 0f, 0f, 0.7f);
                outline.effectDistance = new Vector2(2f, -2f);
            }

            TMP_Text label = Label("Value", pill, "0", 30, TextAlignmentOptions.MidlineLeft);
            Stretch(label.rectTransform);
            label.rectTransform.offsetMin = new Vector2(62f, 0f);
            label.rectTransform.offsetMax = new Vector2(-10f, 0f);
            label.enableAutoSizing = true;
            label.fontSizeMin = 18f;
            label.fontSizeMax = 30f;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            return label;
        }

        /// <summary>화면 아래에 붙인다 (키 큰 폰에서도 아래 묶음이 탭 위에 붙어 있게)</summary>
        private static void Bottom(Component c, float x, float y)
        {
            var rt = (RectTransform)c.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(x, y);
        }

        private static LobbyMainUi BuildLobbyMain(RectTransform main, Camera camera)
        {
            var ui = new LobbyMainUi();
            ui.Diorama = BuildLobbyDiorama(camera);

            // ----- 위: 계정 레벨 · 골드 · 보석 · 하트 -----
            RectTransform topBar = Rect("TopBar", main, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -16f), new Vector2(1040f, 80f));
            RectTransform levelBadge = Rect("Level", topBar, Center, Center, Center, new Vector2(-462f, 0f), new Vector2(92f, 92f));
            Image levelImage = Img(levelBadge, new Color(0.3f, 0.6f, 1f), _kit != null ? _kit.Slot : null);
            if (_kit != null) levelImage.type = Image.Type.Sliced;
            ui.Level = Label("Value", levelBadge, "1", 40, TextAlignmentOptions.Center);
            Stretch(ui.Level.rectTransform);
            if (_titleFont != null)
            {
                ui.Level.font = _titleFont;
                if (_buttonMat != null) ui.Level.fontSharedMaterial = _buttonMat;
            }

            TMP_Text lv = Label("Lv", levelBadge, "Lv", 20, TextAlignmentOptions.Top, new Color(1f, 0.95f, 0.7f));
            Stretch(lv.rectTransform);
            lv.rectTransform.offsetMax = new Vector2(0f, 16f);
            ui.Gold = Pill(topBar, "gold", new Color(1f, 0.84f, 0.3f), new Vector2(-230f, 0f), 280f);
            ui.Gem = Pill(topBar, "gem", new Color(0.55f, 0.85f, 1f), new Vector2(70f, 0f), 280f);
            ui.Heart = Pill(topBar, "heart", new Color(1f, 0.4f, 0.45f), new Vector2(370f, 0f), 300f);
            ui.Wallet = null;

            // ----- 홈 미션 띠 (2026-10-29 개발자: "미션이 홈에 없고 따로 있어서 불편") — 누르면 미션 창 -----
            ui.HomeMission = Btn("HomeMission", main, "", Vector2.zero, new Vector2(1040f, 88f), ButtonBlue, out ui.HomeMissionLabel);
            var missionRect = (RectTransform)ui.HomeMission.transform;
            missionRect.anchorMin = missionRect.anchorMax = new Vector2(0.5f, 1f);
            missionRect.pivot = new Vector2(0.5f, 1f);
            missionRect.anchoredPosition = new Vector2(0f, -106f);
            Sprite missionIcon = Icon("mission");
            if (missionIcon != null)
            {
                RectTransform mi = Rect("Icon", missionRect, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(18f, 4f), new Vector2(56f, 56f));
                Img(mi, Color.white, missionIcon).preserveAspect = true;
            }

            ui.HomeMissionLabel.font = _font;
            if (_bodyMat != null) ui.HomeMissionLabel.fontSharedMaterial = _bodyMat;
            ui.HomeMissionLabel.alignment = TextAlignmentOptions.MidlineLeft;
            ui.HomeMissionLabel.margin = new Vector4(90f, 0f, 24f, 14f);
            ui.HomeMissionLabel.fontSize = 30f;
            ui.HomeMissionLabel.enableAutoSizing = true;
            ui.HomeMissionLabel.fontSizeMin = 22f;
            ui.HomeMissionLabel.fontSizeMax = 30f;
            ui.HomeMissionLabel.textWrappingMode = TextWrappingModes.NoWrap;
            // 오늘 활동 점수 막대 (띠 아래쪽에 얇게)
            RectTransform missionBar = Rect("Progress", missionRect, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 14f), new Vector2(-120f, 10f));
            missionBar.offsetMin = new Vector2(90f, 14f);
            missionBar.offsetMax = new Vector2(-30f, 24f);
            Fill(missionBar, new Color(0f, 0f, 0f, 0.35f), new Color(1f, 0.82f, 0.3f), out ui.HomeMissionFill);
            ui.HomeMissionFill.fillAmount = 0f;

            ui.Notice = Label("Notice", main, "", 30, TextAlignmentOptions.Center, new Color(1f, 0.9f, 0.45f));
            Place(ui.Notice.rectTransform, Center, Center, new Vector2(0f, 600f), new Vector2(1000f, 80f));
            ui.Notice.gameObject.SetActive(false);

            // ----- 양옆 작은 버튼 -----
            Color side = new Color(0.32f, 0.38f, 0.55f);
            ui.Newcomer = IconButton("Newcomer", main, "gift7", "7일 미션", new Vector2(-455f, 690f), new Vector2(150f, 140f), new Color(1f, 0.55f, 0.25f), out ui.NewcomerLabel);
            ui.NewcomerBadge = Label("ReadyBadge", ui.Newcomer.transform, "", 40, TextAlignmentOptions.Center, new Color(1f, 0.28f, 0.25f));
            Place(ui.NewcomerBadge.rectTransform, Center, Center, new Vector2(62f, 52f), new Vector2(40f, 40f));
            ui.NewcomerBadge.raycastTarget = false;
            ui.Codex = IconButton("Codex", main, "codex", "조합 도감", new Vector2(-455f, 535f), new Vector2(150f, 140f), new Color(0.6f, 0.4f, 0.9f), out ui.CodexLabel);
            ui.Settings = IconButton("Settings", main, "settings", "설정", new Vector2(455f, 690f), new Vector2(150f, 140f), side, out _);
            ui.Patrol = IconButton("Patrol", main, "patrol", "순찰", new Vector2(455f, 535f), new Vector2(150f, 140f), side, out ui.PatrolLabel); // 부가 메뉴는 차분하게 (전체 UI 검토 7번: 던전 시작 단추와 다투지 않게)

            // ----- 가운데: 섬 무대 위 영웅 이름표 -----
            RectTransform plate = Rect("HeroPlate", main, Center, Center, Center, Vector2.zero, new Vector2(420f, 60f));
            Bottom(plate, 0f, 905f);
            Image plateImage = Img(plate, new Color(0.04f, 0.06f, 0.12f, 0.8f), _kit != null ? _kit.Badge : null);
            if (_kit != null) plateImage.type = Image.Type.Sliced;
            ui.HeroName = Label("Name", plate, "영웅", 30, TextAlignmentOptions.Center);
            Stretch(ui.HeroName.rectTransform);
            ui.HeroName.enableAutoSizing = true;
            ui.HeroName.fontSizeMin = 18f;
            ui.HeroName.fontSizeMax = 30f;

            // ----- 구간 카드: 리본 이름 · < > · 별과 보스 상태 · 보스 도전 -----
            RectTransform card = Rect("ChapterCard", main, Center, Center, Center, Vector2.zero, new Vector2(1000f, 230f));
            Bottom(card, 0f, 700f);
            Image cardImage = Img(card, new Color(0.13f, 0.17f, 0.3f, 0.94f), _kit != null ? _kit.Panel : null);
            if (_kit != null) cardImage.type = Image.Type.Sliced;
            if (_kit != null)
            {
                RectTransform ribbon = Rect("Ribbon", main, Center, Center, Center, Vector2.zero, new Vector2(680f, 120f));
                Bottom(ribbon, 0f, 808f);
                Image ribbonImage = Img(ribbon, new Color(0.86f, 0.26f, 0.24f), _kit.Ribbon);
                ribbonImage.type = Image.Type.Sliced;
            }

            ui.Chapter = Label("Chapter", main, "1구간 · 균열의 초원", 44, TextAlignmentOptions.Center);
            Place(ui.Chapter.rectTransform, Center, Center, Vector2.zero, new Vector2(520f, 80f));
            Bottom(ui.Chapter, 0f, 816f);
            ui.Chapter.enableAutoSizing = true;
            ui.Chapter.fontSizeMin = 26f;
            ui.Chapter.fontSizeMax = 44f;
            if (_titleFont != null)
            {
                ui.Chapter.font = _titleFont;
                if (_buttonMat != null) ui.Chapter.fontSharedMaterial = _buttonMat;
            }

            ui.Prev = Btn("PrevChapter", main, "<", Vector2.zero, new Vector2(110f, 110f), side, out TMP_Text prevLabel);
            Bottom(ui.Prev, -430f, 808f);
            prevLabel.fontSize = 56;
            ui.Next = Btn("NextChapter", main, ">", Vector2.zero, new Vector2(110f, 110f), side, out TMP_Text nextLabel);
            Bottom(ui.Next, 430f, 808f);
            nextLabel.fontSize = 56;
            ui.ChapterInfo = Label("ChapterInfo", card, "", 26, TextAlignmentOptions.MidlineLeft, new Color(1f, 1f, 1f, 0.85f));
            Place(ui.ChapterInfo.rectTransform, Center, Center, new Vector2(-150f, -40f), new Vector2(620f, 120f));
            ui.ChapterInfo.enableAutoSizing = true;
            ui.ChapterInfo.fontSizeMin = 18f;
            ui.ChapterInfo.fontSizeMax = 26f;
            ui.Boss = ModeCard("Boss", card, "boss", "보스 도전", new Vector2(320f, -40f), new Vector2(300f, 120f), new Color(0.9f, 0.25f, 0.35f), out ui.BossLabel);
            ui.BossLabel.fontSizeMax = 34f;

            // ----- 큰 모드 2개 -----
            ui.Dungeon = ModeCard("Dungeon", main, "dungeon", "던전\n<size=55%>15방 · 하트 1</size>", Vector2.zero, new Vector2(480f, 220f), LobbyYellow, out TMP_Text dungeonLabel);
            Bottom(ui.Dungeon, -250f, 452f);
            ui.Survival = ModeCard("Survival", main, "survival", "서바이벌", Vector2.zero, new Vector2(480f, 220f), LobbyOrange, out ui.SurvivalLabel);
            Bottom(ui.Survival, 250f, 452f);
            ui.SurvivalLabel.textWrappingMode = TextWrappingModes.NoWrap; // "하트 1"의 1만 셋째 줄로 넘어가지 않게

            // ----- 도전 줄 -----
            ui.Rift = ModeCard("Rift", main, "rift", "무한의 균열", Vector2.zero, new Vector2(480f, 120f), new Color(0.5f, 0.32f, 0.9f), out ui.RiftLabel);
            Bottom(ui.Rift, -250f, 262f);
            ui.RiftLabel.fontSizeMax = 34f;
            ui.RiftLabel.textWrappingMode = TextWrappingModes.NoWrap; // 둘째 줄이 길면 접지 말고 줄여서 (접히면 제목이 단추 위로 넘친다)
            ui.Daily = ModeCard("Daily", main, "daily", "일일 도전", Vector2.zero, new Vector2(480f, 120f), new Color(0.2f, 0.7f, 0.75f), out ui.DailyLabel);
            Bottom(ui.Daily, 250f, 262f);
            ui.DailyLabel.fontSizeMax = 34f;
            ui.DailyLabel.textWrappingMode = TextWrappingModes.NoWrap;

            // ----- 아래 탭 5개: 상점 · 영웅 · [전투] · 미션 · 기록 -----
            RectTransform bar = Rect("TabBar", main, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), Vector2.zero, new Vector2(0f, 170f));
            Img(bar, new Color(0.07f, 0.09f, 0.17f, 0.97f), null);
            if (_kit != null)
            {
                RectTransform line = Rect("Line", bar, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(0f, 10f));
                Img(line, new Color(1f, 0.8f, 0.4f, 0.5f), _kit.Divider);
            }

            Color tab = new Color(0.22f, 0.28f, 0.45f);
            const float tabY = 84f;
            ui.Shop = IconButton("Shop", main, "shop", "상점", new Vector2(-424f, tabY), new Vector2(196f, 150f), tab, out _);
            ui.Hero = IconButton("Hero", main, "hero", "영웅", new Vector2(-212f, tabY), new Vector2(196f, 150f), tab, out _);
            ui.HeroBadge = Label("UpgradeBadge", ui.Hero.transform, "", 40, TextAlignmentOptions.Center, new Color(1f, 0.28f, 0.25f));
            Place(ui.HeroBadge.rectTransform, Center, Center, new Vector2(78f, 58f), new Vector2(40f, 40f));
            ui.HeroBadge.raycastTarget = false;
            Button home = IconButton("HomeTab", main, "sword", "전투", new Vector2(0f, tabY + 22f), new Vector2(210f, 180f), LobbyYellow, out _);
            home.image.color = Vivid(LobbyYellow); // 지금 고른 탭 = 노랑 (단추 역할이 아니라 "고름" 상태 색이라 세 가지 규칙에서 뺀다)
            home.interactable = false; // 지금 화면 (고른 탭은 높고 노랗게)
            ColorBlock homeColors = home.colors;
            homeColors.disabledColor = Color.white;
            home.colors = homeColors;
            ui.Missions = IconButton("Missions", main, "mission", "미션", new Vector2(212f, tabY), new Vector2(196f, 150f), tab, out ui.MissionLabel);
            ui.Archive = IconButton("Archive", main, "archive", "기록", new Vector2(424f, tabY), new Vector2(196f, 150f), tab, out ui.ArchiveLabel);
            foreach (Button b in new[] { ui.Shop, ui.Hero, ui.Missions, ui.Archive }) Bottom(b, ((RectTransform)b.transform).anchoredPosition.x, tabY);
            Bottom(home, 0f, tabY + 22f);
            return ui;
        }
    }
}
