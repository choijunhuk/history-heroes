using HistoryHeroes.Combat;
using HistoryHeroes.Common;
using HistoryHeroes.Dungeon;
using HistoryHeroes.Enemy;
using HistoryHeroes.Modes;
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
    public static partial class SceneBuilder
    {
        // 방 안쪽 크기 (벽 안). 세로 화면에 맞춘 8 × 14
        private const float ArenaHalfWidth = 4f;
        private const float ArenaHalfHeight = 7f;

        // 보스 경기장 (원형, Modes 2-9). 방 너비 안에 들어가게
        private const float BossArenaRadius = 3.9f;

        private static void BuildBattleScene(Scene scene, Data d, Prefabs p)
        {
            // ----- 카메라·빛 (Art3D-Plan 3번: 비스듬한 원근 카메라 + 해·그림자) -----
            // 받침(rig, 서바이벌은 플레이어를 따라감) → 거치대(CameraFit: 방이 한 화면에 들어오는 거리·기울기) → 카메라(흔들림).
            // 흔들림은 카메라의 local 위치라 따라가기·맞춤과 안 싸운다
            _stage = CreateStageMaterials();
            var rig = new GameObject("CameraRig");
            var cameraFollow = rig.AddComponent<CameraFollow>();
            cameraFollow.enabled = false; // BattleBootstrap이 서바이벌에서만 켠다
            var mount = new GameObject("CameraMount");
            mount.transform.SetParent(rig.transform, false);
            var cameraObject = new GameObject("Main Camera") { tag = "MainCamera" };
            cameraObject.transform.SetParent(mount.transform, false);
            var camera = cameraObject.AddComponent<Camera>();
            camera.orthographic = false;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.07f, 0.08f, 0.1f);
            UniversalAdditionalCameraData cameraData = camera.GetUniversalAdditionalCameraData();
            cameraData.SetRenderer(1); // UniversalRenderer3D (0번은 로비가 쓰는 2D 렌더러)
            cameraData.renderShadows = true;
            cameraData.renderPostProcessing = true; // 그래픽 평가 1번: 톤 매핑·색 보정·빛 번짐 (낮음 품질은 StageLook이 끈다)
            var fit = mount.AddComponent<CameraFit>();
            using (var props = new Props(fit)) props.Ref("viewCamera", camera);
            cameraObject.AddComponent<CameraShake>(); // 손맛: 화면 흔들림 (조사 A9)
            fit.Apply();

            // 손맛: 피해 숫자 (조사 A9). 글꼴은 한국어 TMP 글꼴을 같이 쓴다
            var numbers = new GameObject("DamageNumbers").AddComponent<DamageNumbers>();
            using (var props = new Props(numbers)) props.Ref("font", _titleFont != null ? _titleFont : _font); // 굵은 제목 글꼴 숫자 (뽕맛)

            BuildLighting(_stage, camera);
            BuildFxBursts(); // 그래픽 평가 2번: 맞음 불꽃 · 쓰러짐 연기 · 터짐 고리
            BuildOutside(_stage);

            // ----- 모드별 묶음: 장면이 열릴 때 BattleBootstrap이 한쪽만 켠다 -----
            var dungeonRoot = new GameObject("DungeonMode").transform;
            var bossRoot = new GameObject("BossMode").transform;
            var survivalRoot = new GameObject("SurvivalMode").transform;

            // ----- 방: 바닥, 벽, 문 (벽은 두 모드가 같이 쓴다) -----
            var arena = new GameObject("Arena").transform;
            DressFloor(Block("Floor", dungeonRoot, Vector2.zero, new Vector2(ArenaHalfWidth * 2f, ArenaHalfHeight * 2f), new Color(0.18f, 0.24f, 0.18f), -10, collider: false), _stage.RoomFloor);
            Color wall = new Color(0.36f, 0.31f, 0.26f);
            DressWall(Block("Wall Left", arena, new Vector2(-ArenaHalfWidth - 0.5f, 0f), new Vector2(1f, ArenaHalfHeight * 2f + 2f), wall, -5, collider: true), _stage.WallSide, _stage.WallEnd);
            DressWall(Block("Wall Right", arena, new Vector2(ArenaHalfWidth + 0.5f, 0f), new Vector2(1f, ArenaHalfHeight * 2f + 2f), wall, -5, collider: true), _stage.WallSide, _stage.WallEnd);
            DressWall(Block("Wall Bottom", arena, new Vector2(0f, -ArenaHalfHeight - 0.5f), new Vector2(ArenaHalfWidth * 2f + 2f, 1f), wall, -5, collider: true), _stage.WallSide, _stage.WallEnd);
            DressWall(Block("Wall Top", arena, new Vector2(0f, ArenaHalfHeight + 0.5f), new Vector2(ArenaHalfWidth * 2f + 2f, 1f), wall, -5, collider: true), _stage.WallSide, _stage.WallEnd);

            GameObject doorObject = Block("Door", dungeonRoot, new Vector2(0f, ArenaHalfHeight - 0.3f), new Vector2(1.8f, 0.6f), Color.gray, -4, collider: true);
            doorObject.layer = 0;
            doorObject.GetComponent<BoxCollider2D>().isTrigger = true;
            var door = doorObject.AddComponent<Door>();
            using (var props = new Props(door)) props.Ref("sprite", doorObject.GetComponent<SpriteRenderer>());
            DressDoor(dungeonRoot, doorObject, door); // 그래픽 평가 4번: 돌 아치 문
            WireStageDecor(BuildDecoGroups(arena)); // 구간별 벽 장식 · 바닥 장식

            var playerStart = new GameObject("PlayerStart").transform;
            playerStart.position = new Vector2(0f, -ArenaHalfHeight + 1.5f);

            // ----- UI (조이스틱이 플레이어에 연결돼야 해서 먼저 만든다) -----
            Ui ui = BuildUi();
            using (var props = new Props(ui.RecordSave)) props.Ref("database", d.Database);

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
                props.Ref("hero", d.Khan).Ref("joystick", ui.Joystick).Ref("projectilePrefab", p.PlayerArrow).Ref("bodyRenderer", playerBody)
                    .Mask("enemyMask", Mask(EnemyLayer)).Mask("wallMask", Mask(WallLayer));
            }

            using (var props = new Props(playerObject.AddComponent<StopIndicator>())) props.Ref("player", player).Ref("fill", stopFill);

            // 영웅 표시: 발밑에 늘 보이는 하늘색 원 (무리 속에서도 영웅을 바로 찾게 — Codex 화면 점검 1번)
            // 재평가 1번: 예고 원(꽉 찬 원)과 다른 모양 = 밝은 고리. 고리 그림이 없으면 예전 원
            if (_kit != null && _kit.Ring != null) Visual(playerObject.transform, "HeroRing", _kit.Ring, new Color(0.45f, 0.95f, 1f, 0.95f), Vector2.one * 1.5f, -2);
            else Visual(playerObject.transform, "HeroRing", _circle, new Color(0.35f, 0.85f, 1f, 0.38f), Vector2.one * 1.25f, -2);

            // 재평가 1번 "캐릭터를 먼저 보이게": 영웅 머리 위 은은한 점 조명 (영웅과 발밑이 주변보다 밝다)
            var heroLight = new GameObject("HeroLight").AddComponent<Light>();
            heroLight.transform.SetParent(playerObject.transform, false);
            heroLight.transform.localPosition = new Vector3(0f, -0.6f, -2.6f);
            heroLight.type = LightType.Point;
            heroLight.range = 4.5f;
            heroLight.intensity = 1.6f;
            heroLight.color = new Color(1f, 0.95f, 0.85f);
            heroLight.shadows = LightShadows.None;

            // 3D 모델 (Art3D-Plan 4번): 임시 도형 몸·방향 표시는 숨기고, 멈춤 고리는 땅에 남긴다
            var playerVisual = playerObject.AddComponent<Visual3D>();
            using (var props = new Props(playerVisual))
            {
                props.Ref("modelPrefab", d.Khan.model3D).Float("height", d.Khan.modelHeight).Float("runSpeed", 5f).Bool("keepOnDeath", true)
                    .Refs("hideRenderers", new Object[] { playerBody, playerObject.transform.Find("Facing").GetComponent<SpriteRenderer>() });
            }

            // 펫 (v0.5): BattleBootstrap이 장착한 펫으로 켠다. 충돌 없음 (적이 공격하지 않는다)
            var petObject = new GameObject("Pet");
            SpriteRenderer petBody = Visual(petObject.transform, "Body", _circle, Color.white, Vector2.one * 0.42f, 4);
            SpriteRenderer petMarker = Visual(petObject.transform, "Marker", _circle, Color.white, Vector2.one * 0.35f, 6);
            var pet = petObject.AddComponent<HistoryHeroes.Pets.PetController>();
            using (var props = new Props(pet)) props.Ref("body", petBody).Ref("marker", petMarker).Mask("enemyMask", Mask(EnemyLayer));
            // 3D 펫 (Art3D-Plan 5번): 장착한 펫 모델을 PetController가 고른다. 몸 도형은 모델이 있으면 숨긴다
            using (var props = new Props(petObject.AddComponent<Visual3D>())) props.Float("runSpeed", 6f).Refs("hideRenderers", new Object[] { petBody });
            petObject.SetActive(false);
            using (var props = new Props(cameraFollow)) props.Ref("target", playerObject.transform).Ref("fit", fit).Float("boundsHalfSize", SurvivalHalf);

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
            using (var props = new Props(ui.Pause)) props.Ref("player", player);
            using (var props = new Props(ui.LevelUp)) props.Ref("experience", experience).Ref("skills", skills);

            var layoutRoot = new GameObject("Layout").transform; // 방 레이아웃 장애물·함정 (v0.6)
            layoutRoot.SetParent(dungeonRoot, false);
            var runObject = new GameObject("DungeonRun");
            runObject.transform.SetParent(dungeonRoot, false);
            var run = runObject.AddComponent<DungeonRun>();
            using (var props = new Props(run))
            {
                props.Ref("dungeon", d.Dungeon).Ref("database", d.Database).Ref("player", player).Ref("experience", experience).Ref("skills", skills)
                    .Ref("door", door).Ref("playerStart", playerStart)
                    .Ref("levelUp", ui.LevelUp).Ref("hud", ui.Hud).Ref("result", ui.Result).Ref("recordSave", ui.RecordSave).Ref("fade", ui.Fade)
                    .Ref("bossHud", ui.BossHud).Ref("rewardRoom", ui.RewardRoom).Ref("layoutRoot", layoutRoot)
                    .Refs("riftBosses", AllBosses(d.RiftWarden)); // 균열 10층마다 (1~5구간 순서)
            }

            // ----- 보스맵: 원형 경기장 + 보스 (Modes 2-9) -----
            BuildBossArena(bossRoot);
            DressBossArena(bossRoot, _stage);
            var bossStart = new GameObject("BossPlayerStart").transform;
            bossStart.SetParent(bossRoot, false);
            bossStart.position = new Vector2(0f, -BossArenaRadius + 1.2f);
            var bossSpawn = new GameObject("BossSpawn").transform;
            bossSpawn.SetParent(bossRoot, false);
            bossSpawn.position = new Vector2(0f, 1.6f);

            var bossRunObject = new GameObject("BossRun");
            bossRunObject.transform.SetParent(bossRoot, false);
            var bossRun = bossRunObject.AddComponent<BossRun>();
            using (var props = new Props(bossRun))
            {
                props.Refs("bosses", AllBosses(d.RiftWarden)).Ref("database", d.Database).Ref("player", player).Ref("skills", skills)
                    .Ref("playerStart", bossStart).Ref("bossSpawn", bossSpawn)
                    .Ref("levelUp", ui.LevelUp).Ref("hud", ui.Hud).Ref("bossHud", ui.BossHud).Ref("result", ui.Result);
            }

            using (var props = new Props(ui.Voice))
            {
                props.Ref("player", player).Ref("experience", experience).Ref("skills", skills).Ref("boss", bossRun);
            }

            using (var props = new Props(ui.Guide))
            {
                props.Ref("dungeon", run).Ref("boss", bossRun).Ref("levelUp", ui.LevelUp).Ref("recordSave", ui.RecordSave);
            }

            // ----- 서바이벌: 80×80 열린 맵 + 10분 버티기 (GameDesign 8장) -----
            Survival.SurvivalData survivalData = CreateSurvivalData(d, p);
            Transform survivalStart = BuildSurvivalMap(survivalRoot);
            var survivalRunObject = new GameObject("SurvivalRun");
            survivalRunObject.transform.SetParent(survivalRoot, false);
            var survivalRun = survivalRunObject.AddComponent<Survival.SurvivalRun>();
            using (var props = new Props(survivalRun))
            {
                props.Ref("data", survivalData).Ref("database", d.Database).Ref("player", player).Ref("experience", experience).Ref("skills", skills)
                    .Ref("playerStart", survivalStart).Ref("metaTable", d.Meta)
                    .Ref("levelUp", ui.LevelUp).Ref("hud", ui.Hud).Ref("bossHud", ui.BossHud).Ref("result", ui.Result);
            }

            var bootstrap = systems.AddComponent<BattleBootstrap>();
            using (var props = new Props(bootstrap))
            {
                props.Ref("dungeonRoot", dungeonRoot.gameObject).Ref("bossRoot", bossRoot.gameObject).Ref("roster", d.Roster).Ref("player", player).Ref("metaTable", d.Meta)
                    .Ref("skills", Object.FindAnyObjectByType<SkillManager>(FindObjectsInactive.Include)).Ref("database", d.Database)
                    .Ref("survivalRoot", survivalRoot.gameObject).Ref("arenaRoot", arena.gameObject).Ref("cameraFollow", cameraFollow).Ref("survival", survivalData)
                    .Ref("pet", pet).Ref("experience", experience);
            }

            survivalRoot.gameObject.SetActive(false); // 던전이 기본 (BattleBootstrap이 모드대로 켠다)

            using (var props = new Props(Object.FindAnyObjectByType<DungeonRun>(FindObjectsInactive.Include)))
            {
                props.Ref("metaTable", d.Meta);
            }

            using (var props = new Props(Object.FindAnyObjectByType<BossRun>(FindObjectsInactive.Include)))
            {
                props.Ref("metaTable", d.Meta);
            }

            var cheats = systems.AddComponent<Dev.DevCheats>();
            using (var props = new Props(cheats)) props.Ref("experience", experience).Ref("levelUp", ui.LevelUp).Ref("skills", skills);
            SetPresets(cheats, d);

            AddUiSounds(ui.Hud.transform.root); // v1.0 버튼 소리
            EditorSceneManager.SaveScene(scene, BattleScenePath);
        }

        /// <summary>원형 바닥 + 둥근 벽(EdgeCollider2D 원). 네모 방의 바닥은 던전 모드에만 있다.</summary>
        private static void BuildBossArena(Transform bossRoot)
        {
            SpriteRenderer rim = Visual(bossRoot, "BossRim", _circle, new Color(0.36f, 0.31f, 0.26f), Vector2.one * (BossArenaRadius * 2f + 0.8f), -11);
            rim.transform.position = Vector3.zero;
            SpriteRenderer floor = Visual(bossRoot, "BossFloor", _circle, new Color(0.2f, 0.21f, 0.27f), Vector2.one * (BossArenaRadius * 2f), -10);
            floor.transform.position = Vector3.zero;

            var wall = new GameObject("BossWall") { layer = WallLayer };
            wall.transform.SetParent(bossRoot, false);
            var edge = wall.AddComponent<EdgeCollider2D>();
            const int segments = 40;
            var points = new Vector2[segments + 1];
            for (int i = 0; i <= segments; i++)
            {
                float a = i * Mathf.PI * 2f / segments;
                points[i] = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * BossArenaRadius;
            }

            edge.points = points;
            edge.edgeRadius = 0.05f;
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
            public RecordSaveScreen RecordSave;
            public BossHud BossHud;
            public CanvasGroup Fade;
            public RewardRoomPanel RewardRoom;
            public PauseMenu Pause;
            public TutorialGuide Guide;
            public HeroVoice Voice;
        }

        private static readonly Vector2 Center = new Vector2(0.5f, 0.5f);

        // 카드 3장 + 간격 = 1032 (기준 너비 1080 안에 들어감)
        private const float CardWidth = 336f;
        private const float CardHeight = 560f;

        /// <summary>캔버스 + 이벤트 시스템 (전투·로비 공용). 기준 1080×1920, Expand</summary>
        private static Transform CreateCanvas()
        {
            var canvasObject = new GameObject("Canvas", typeof(RectTransform));
            var canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080f, 1920f);
            // Expand: 화면 비율이 어떻든 1080×1920 기준 영역이 통째로 보인다 (세로로 긴 폰에서 카드가 잘리지 않음)
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
            canvasObject.AddComponent<GraphicRaycaster>();

            var eventSystem = new GameObject("EventSystem", typeof(EventSystem));
            eventSystem.AddComponent<InputSystemUIInputModule>().AssignDefaultActions();
            return canvasObject.transform;
        }

        private static Ui BuildUi()
        {
            var ui = new Ui();
            Transform root = CreateCanvas();

            // ----- HUD -----
            RectTransform hudRect = Rect("HUD", root, Vector2.zero, Vector2.one, Center, Vector2.zero, Vector2.zero);
            hudRect.gameObject.AddComponent<SafeAreaFitter>(); // 카메라 구멍·상태 표시줄 피하기
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

            // 방 진행 바 (조사 A6) · 알림 (A7)
            RectTransform roomBar = Rect("RoomBar", hudRect, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -178f), new Vector2(600f, 36f));
            TMP_Text hudNotice = Label("Notice", hudRect, "", 30, TextAlignmentOptions.Center, new Color(1f, 0.9f, 0.55f));
            Place(hudNotice.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -330f), new Vector2(960f, 56f));
            hudNotice.gameObject.SetActive(false);

            // 별 ★2 도전 중일 때만 보이는 맞은 횟수 (Modes 3-2)
            TMP_Text hitsLabel = Label("HitsLabel", hudRect, "맞음 0/15", 30, TextAlignmentOptions.TopRight, new Color(1f, 1f, 1f, 0.8f));
            Place(hitsLabel.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-40f, -160f), new Vector2(300f, 46f));
            hitsLabel.gameObject.SetActive(false);

            using (var props = new Props(ui.Hud))
            {
                props.Ref("hpFill", hpFill).Ref("hpLabel", hpLabel).Ref("expFill", expFill)
                    .Ref("levelLabel", levelLabel).Ref("roomLabel", roomLabel).Ref("killLabel", killLabel).Ref("hitsLabel", hitsLabel)
                    .Ref("roomBar", roomBar).Ref("noticeLabel", hudNotice);
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

            ui.BossHud = BuildBossHud(hudRect, root);
            ui.LevelUp = BuildLevelUp(root);
            ui.Result = BuildResult(root);
            ui.RecordSave = BuildRecordSave(root, null);
            ui.RewardRoom = BuildRewardRoomPanel(root);
            SettingsPanel settings = BuildSettingsPanel(root);
            ui.Pause = BuildPauseMenu(root, hudRect, settings);
            settings.transform.SetAsLastSibling(); // 설정이 일시정지 판 위에
            ui.Guide = BuildTutorialGuide(root);
            ui.Voice = BuildHeroVoice(root);
            BuildHypeFx(hudRect, root);
            return ui;
        }

        /// <summary>v1.0 영웅 대사 말풍선 (아래쪽, 터치를 막지 않는다)</summary>
        private static HeroVoice BuildHeroVoice(Transform root)
        {
            RectTransform holder = Rect("HeroVoice", root, Vector2.zero, Vector2.one, Center, Vector2.zero, Vector2.zero);
            var voice = holder.gameObject.AddComponent<HeroVoice>();
            RectTransform bubble = Rect("Bubble", holder, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 300f), new Vector2(920f, 96f));
            Img(bubble, new Color(0f, 0f, 0f, 0.6f), null).raycastTarget = false;
            TMP_Text label = Label("Text", bubble, "", 32, TextAlignmentOptions.Center);
            Stretch(label.rectTransform);
            label.raycastTarget = false;
            label.enableAutoSizing = true;
            label.fontSizeMin = 22f;
            label.fontSizeMax = 32f;
            using (var props = new Props(voice)) props.Ref("bubble", bubble.gameObject).Ref("label", label);
            return voice;
        }

        /// <summary>v1.0 튜토리얼 안내 (클리오 말풍선): 위쪽, 누르면 닫힌다</summary>
        private static TutorialGuide BuildTutorialGuide(Transform root)
        {
            RectTransform holder = Rect("TutorialGuide", root, Vector2.zero, Vector2.one, Center, Vector2.zero, Vector2.zero);
            var guide = holder.gameObject.AddComponent<TutorialGuide>();
            RectTransform banner = Rect("Banner", holder, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -400f), new Vector2(980f, 190f));
            Img(banner, new Color(0.05f, 0.07f, 0.12f, 0.9f), null).raycastTarget = true;
            Button button = banner.gameObject.AddComponent<Button>();
            TMP_Text label = Label("Text", banner, "", 32, TextAlignmentOptions.Center);
            Stretch(label.rectTransform);
            label.rectTransform.offsetMin = new Vector2(24f, 12f);
            label.rectTransform.offsetMax = new Vector2(-24f, -12f);
            label.enableAutoSizing = true;
            label.fontSizeMin = 22f;
            label.fontSizeMax = 32f;
            using (var props = new Props(guide)) props.Ref("banner", banner.gameObject).Ref("label", label).Ref("bannerButton", button);
            return guide;
        }

        /// <summary>v1.0 일시정지: HUD 왼쪽 위 버튼 + 계속 / 설정 / 판 포기</summary>
        private static PauseMenu BuildPauseMenu(Transform root, RectTransform hud, SettingsPanel settings)
        {
            Button open = Btn("PauseButton", hud, "II", Vector2.zero, new Vector2(100f, 84f), new Color(0.2f, 0.22f, 0.28f, 0.85f), out TMP_Text openLabel);
            openLabel.fontSize = 40;
            var openRect = (RectTransform)open.transform;
            openRect.anchorMin = openRect.anchorMax = new Vector2(0f, 1f);
            openRect.pivot = new Vector2(0f, 1f);
            openRect.anchoredPosition = new Vector2(40f, -110f);

            RectTransform panel = Overlay(root, "PauseMenu", out GameObject holder);
            var menu = holder.AddComponent<PauseMenu>();
            TMP_Text title = Label("Title", panel, "일시정지", 72, TextAlignmentOptions.Center, new Color(1f, 0.85f, 0.35f));
            Place(title.rectTransform, Center, Center, new Vector2(0f, 520f), new Vector2(900f, 110f));
            TMP_Text info = Label("Info", panel, "", 30, TextAlignmentOptions.Center, new Color(1f, 1f, 1f, 0.7f));
            Place(info.rectTransform, Center, Center, new Vector2(0f, 390f), new Vector2(960f, 100f));
            Button resume = Btn("Resume", panel, "계속", new Vector2(0f, 180f), new Vector2(820f, 160f), ButtonGreen, out TMP_Text resumeLabel);
            resumeLabel.fontSize = 56;
            Button settingsButton = Btn("PauseSettings", panel, "설정", new Vector2(0f, -10f), new Vector2(820f, 130f), ButtonBlue, out _);
            Button giveUp = Btn("GiveUp", panel, "판 포기", new Vector2(0f, -190f), new Vector2(820f, 130f), new Color(0.5f, 0.22f, 0.22f), out TMP_Text giveUpLabel);
            using (var props = new Props(menu))
            {
                props.Ref("root", panel.gameObject).Ref("openButton", open).Ref("infoLabel", info)
                    .Ref("resumeButton", resume).Ref("settingsButton", settingsButton).Ref("giveUpButton", giveUp).Ref("giveUpLabel", giveUpLabel)
                    .Ref("settings", settings);
            }

            return menu;
        }

        private static LevelUpScreen BuildLevelUp(Transform root)
        {
            RectTransform holder = Rect("LevelUp", root, Vector2.zero, Vector2.one, Center, Vector2.zero, Vector2.zero);
            var screen = holder.gameObject.AddComponent<LevelUpScreen>();

            RectTransform panel = Rect("Panel", holder, Vector2.zero, Vector2.one, Center, Vector2.zero, Vector2.zero);
            Img(panel, new Color(0.02f, 0.03f, 0.07f, 0.82f), null).raycastTarget = true; // 뒤쪽 터치 막기

            if (_kit != null)
            {
                // 제목 뒤에서 천천히 도는 금빛 빛살 + 붉은 리본 (그래픽 평가 3번)
                RectTransform rays = Rect("Rays", panel, Center, Center, Center, new Vector2(0f, 500f), new Vector2(900f, 900f));
                Img(rays, new Color(1f, 0.8f, 0.35f, 0.22f), _kit.Rays);
                rays.gameObject.AddComponent<UiSpin>();
                RectTransform ribbon = Rect("Ribbon", panel, Center, Center, Center, new Vector2(0f, 492f), new Vector2(760f, 150f));
                Image ribbonImage = Img(ribbon, new Color(0.86f, 0.24f, 0.22f), _kit.Ribbon);
                ribbonImage.type = Image.Type.Sliced;
                ribbon.gameObject.AddComponent<UiPop>();
            }

            TMP_Text title = Label("Title", panel, "레벨 업!", 84, TextAlignmentOptions.Center, new Color(1f, 0.85f, 0.35f));
            Place(title.rectTransform, Center, Center, new Vector2(0f, 506f), new Vector2(900f, 110f));
            title.gameObject.AddComponent<UiPop>();
            TMP_Text subtitle = Label("Subtitle", panel, "카드 1장을 고르세요", 34, TextAlignmentOptions.Center, new Color(1f, 1f, 1f, 0.85f));
            Place(subtitle.rectTransform, Center, Center, new Vector2(0f, 394f), new Vector2(900f, 50f));

            var cards = new SkillCardView[3];
            const float step = CardWidth + 12f;
            for (int i = 0; i < 3; i++) cards[i] = BuildCard(panel, i, new Vector2(-step + step * i, 40f));

            Button reroll = Btn("Reroll", panel, "리롤 (광고) 2/2", new Vector2(-220f, -400f), new Vector2(390f, 120f), new Color(0.22f, 0.3f, 0.45f), out TMP_Text rerollLabel);
            rerollLabel.enableAutoSizing = true; // "광고 보고 다시 뽑기 (남은 2)"가 한 줄에 들어가게
            rerollLabel.fontSizeMin = 22f;
            rerollLabel.fontSizeMax = 32f;
            rerollLabel.textWrappingMode = TextWrappingModes.NoWrap;
            rerollLabel.rectTransform.offsetMin = new Vector2(14f, 0f);
            rerollLabel.rectTransform.offsetMax = new Vector2(-14f, 0f);
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

        /// <summary>
        /// 레벨업 카드 (그래픽 평가 3번: "선택창이 html 같다"): 등급 색 테두리 · 큰 스킬 아이콘 · 종류 탭 · NEW 딱지 · 희귀 이상 뒤 빛 · 차례로 튀어나오기.
        /// 구조: 카드(누르기·튀어나오기) → 뒤 빛 / 바탕 / 테두리 / 탭 / 아이콘 / 글자
        /// </summary>
        private static SkillCardView BuildCard(Transform parent, int index, Vector2 position)
        {
            RectTransform card = Rect($"Card {index + 1}", parent, Center, Center, Center, position, new Vector2(CardWidth, CardHeight));
            card.gameObject.AddComponent<CanvasGroup>();
            var pop = card.gameObject.AddComponent<UiPop>();

            Image glow = null, frame = null, iconSlot = null, icon = null;
            GameObject newBadge = null;
            if (_kit != null)
            {
                RectTransform glowRect = Rect("Glow", card, Center, Center, Center, Vector2.zero, new Vector2(CardWidth + 200f, CardHeight + 200f));
                glow = Img(glowRect, new Color(1f, 1f, 1f, 0.5f), _kit.Glow);
            }

            RectTransform body = Rect("Body", card, Vector2.zero, Vector2.one, Center, Vector2.zero, Vector2.zero);
            Stretch(body);
            Image background = Img(body, SkillCardView.CardBase, _kit != null ? _kit.Panel : null);
            if (_kit != null)
            {
                background.type = Image.Type.Sliced;
                background.pixelsPerUnitMultiplier = 1.2f;
            }

            background.raycastTarget = true;
            var button = card.gameObject.AddComponent<Button>();
            button.targetGraphic = background;

            if (_kit != null)
            {
                RectTransform frameRect = Rect("Frame", card, Vector2.zero, Vector2.one, Center, Vector2.zero, Vector2.zero);
                Stretch(frameRect);
                frameRect.offsetMin = new Vector2(-6f, -6f);
                frameRect.offsetMax = new Vector2(6f, 6f);
                frame = Img(frameRect, SkillCardView.Common, _kit.Frame);
                frame.type = Image.Type.Sliced;
                frame.pixelsPerUnitMultiplier = 1.1f;
            }

            // 맨 위 탭: 종류(액티브/패시브/조합)를 색으로 구분
            RectTransform band = Rect("KindBand", card, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f), new Vector2(0f, -4f), new Vector2(CardWidth - 90f, 54f));
            Image bandImage = Img(band, SkillCardView.ActiveBand, _kit != null ? _kit.Badge : null);
            if (_kit != null) bandImage.type = Image.Type.Sliced;
            TMP_Text kind = Label("Kind", band, "", 28, TextAlignmentOptions.Center);
            Stretch(kind.rectTransform);
            if (_titleFont != null)
            {
                kind.font = _titleFont;
                if (_buttonMat != null) kind.fontSharedMaterial = _buttonMat;
            }

            // 큰 스킬 아이콘 (오목한 칸 안)
            if (_kit != null)
            {
                RectTransform slotRect = Rect("IconSlot", card, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f), new Vector2(0f, -112f), new Vector2(128f, 128f));
                iconSlot = Img(slotRect, SkillCardView.Common, _kit.Slot);
                iconSlot.type = Image.Type.Sliced;
                RectTransform iconRect = Rect("Icon", slotRect, Center, Center, Center, new Vector2(0f, -2f), new Vector2(92f, 92f));
                icon = Img(iconRect, Color.white, null);
                icon.preserveAspect = true;
                var iconShadow = iconRect.gameObject.AddComponent<Shadow>();
                iconShadow.effectColor = new Color(0f, 0f, 0f, 0.55f);
                iconShadow.effectDistance = new Vector2(0f, -4f);

                RectTransform badge = Rect("NewBadge", card, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 0.5f), new Vector2(-46f, -62f), new Vector2(92f, 44f));
                Image badgeImage = Img(badge, new Color(1f, 0.25f, 0.3f), _kit.Badge);
                badgeImage.type = Image.Type.Sliced;
                badge.localRotation = Quaternion.Euler(0f, 0f, -12f);
                TMP_Text badgeLabel = Label("Text", badge, "NEW", 26, TextAlignmentOptions.Center);
                Stretch(badgeLabel.rectTransform);
                if (_titleFont != null)
                {
                    badgeLabel.font = _titleFont;
                    if (_buttonMat != null) badgeLabel.fontSharedMaterial = _buttonMat;
                }

                badgeLabel.margin = new Vector4(0f, 0f, 0f, 4f);
                newBadge = badge.gameObject;
            }

            TMP_Text nameLabel = TopLabel("Name", card, 42, 182f, 56f);
            nameLabel.enableAutoSizing = true;
            nameLabel.fontSizeMin = 28f;
            nameLabel.fontSizeMax = 42f;
            if (_titleFont != null)
            {
                nameLabel.font = _titleFont;
                if (_buttonMat != null) nameLabel.fontSharedMaterial = _buttonMat;
            }

            TMP_Text level = TopLabel("Level", card, 30, 236f, 40f);
            level.color = new Color(1f, 0.88f, 0.45f);

            // 설명: 어두운 둥근 판 위에 크게. 길면 조금 줄어든다
            RectTransform box = Rect("DescriptionBox", card, Vector2.zero, Vector2.one, Center, Vector2.zero, Vector2.zero);
            box.offsetMin = new Vector2(16f, 58f);
            box.offsetMax = new Vector2(-16f, -282f);
            Image boxImage = Img(box, new Color(0.03f, 0.05f, 0.1f, 0.55f), _kit != null ? _kit.Row : null);
            if (_kit != null)
            {
                boxImage.type = Image.Type.Sliced;
                boxImage.pixelsPerUnitMultiplier = 1.4f;
            }

            TMP_Text description = Label("Description", box, "", 32, TextAlignmentOptions.Center);
            description.enableAutoSizing = true;
            description.fontSizeMin = 22f;
            description.fontSizeMax = 32f;
            description.lineSpacing = 6f;
            RectTransform dr = description.rectTransform;
            Stretch(dr);
            dr.offsetMin = new Vector2(12f, 10f);
            dr.offsetMax = new Vector2(-12f, -10f);

            // 힌트 줄 (조합 진행·막히는 조합·★3 경고). 있으면 설명 칸이 위로 줄어든다 (SkillCardView.SetHint)
            RectTransform hintBox = Rect("HintBox", card, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 8f), new Vector2(CardWidth - 28f, 100f));
            Image hintImage = Img(hintBox, new Color(0.02f, 0.03f, 0.06f, 0.82f), _kit != null ? _kit.Row : null);
            if (_kit != null)
            {
                hintImage.type = Image.Type.Sliced;
                hintImage.pixelsPerUnitMultiplier = 1.4f;
            }
            TMP_Text material = Label("Material", card, "", 25, TextAlignmentOptions.Center, new Color(1f, 0.9f, 0.3f));
            Place(material.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 8f), new Vector2(CardWidth - 36f, 96f));
            material.enableAutoSizing = true;
            material.fontSizeMin = 17f;
            material.fontSizeMax = 25f;
            material.lineSpacing = -4f;

            var view = card.gameObject.AddComponent<SkillCardView>();
            using (var props = new Props(view))
            {
                props.Ref("button", button).Ref("background", background).Ref("kindBand", bandImage).Ref("kindLabel", kind)
                    .Ref("nameLabel", nameLabel).Ref("levelLabel", level).Ref("descriptionLabel", description).Ref("materialLabel", material).Ref("descriptionBox", box).Ref("hintBox", hintBox.gameObject)
                    .Ref("frame", frame).Ref("glow", glow).Ref("iconSlot", iconSlot).Ref("icon", icon).Ref("newBadge", newBadge).Ref("pop", pop).Int("order", index)
                    .Ref("attackIcon", Icon("atk_up")).Ref("hpIcon", Icon("hp_up")).Ref("speedIcon", Icon("aspd_up")).Ref("healIcon", Icon("heal"));
            }

            card.SetAsLastSibling();

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
            TMP_Text body = Label("Body", panel, "", 38, TextAlignmentOptions.TopLeft);
            body.lineSpacing = 18f;
            body.enableAutoSizing = true; // 보상 줄이 많으면 글씨를 줄여서 버튼 위에 다 들어가게
            body.fontSizeMin = 24f;
            body.fontSizeMax = 38f;
            Place(body.rectTransform, Center, Center, new Vector2(0f, 20f), new Vector2(880f, 1060f));

            Button primary = Btn("Primary", panel, "기록 남기기", new Vector2(0f, -640f), new Vector2(600f, 140f), new Color(0.2f, 0.55f, 0.32f), out TMP_Text primaryLabel);
            Button secondary = Btn("Secondary", panel, "로비로", new Vector2(0f, -800f), new Vector2(440f, 110f), new Color(0.3f, 0.32f, 0.38f), out TMP_Text secondaryLabel);

            using (var props = new Props(screen))
            {
                props.Ref("root", panel.gameObject).Ref("titleLabel", title).Ref("bodyLabel", body)
                    .Ref("primaryButton", primary).Ref("primaryLabel", primaryLabel).Ref("secondaryButton", secondary).Ref("secondaryLabel", secondaryLabel);
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

        /// <summary>
        /// 그림 칸. 그림을 안 주면 (UI 부품이 있을 때) 모양을 골라 준다 (그래픽 평가 3번):
        /// 전체 화면 불투명 바탕 → 그라데이션 + 무늬 + 가장자리 어둡게 / 큰 칸 → 둥근 테두리 칸 / 얇은 띠·눈금 → 그대로 납작하게
        /// </summary>
        private static Image Img(RectTransform rt, Color color, Sprite sprite)
        {
            var image = rt.gameObject.AddComponent<Image>();
            image.color = color;
            image.sprite = sprite;
            image.raycastTarget = false;
            if (sprite != null || _kit == null || color.a < 0.95f) return image;

            bool fullScreen = rt.anchorMin == Vector2.zero && rt.anchorMax == Vector2.one && rt.sizeDelta == Vector2.zero;
            Vector2 size = rt.sizeDelta;
            if (fullScreen) ScreenBackdrop(image);
            else if (rt.anchorMin == rt.anchorMax && size.x >= 60f && size.y >= 48f)
            {
                image.sprite = _kit.Row;
                image.type = Image.Type.Sliced;
                image.pixelsPerUnitMultiplier = BorderScale(size.y, 90f);
                image.color = LiftRow(color);
            }

            return image;
        }

        private static void Fill(RectTransform background, Color backColor, Color fillColor, out Image fill)
        {
            Image back = Img(background, backColor, _kit != null ? _kit.BarBg : _square);
            RectTransform fillRect = Rect("Fill", background, Vector2.zero, Vector2.one, Center, Vector2.zero, Vector2.zero);
            Stretch(fillRect);
            fill = Img(fillRect, fillColor, _kit != null ? _kit.BarFill : _square);
            if (_kit != null)
            {
                // 막대 틀: 어두운 홈 (색은 그림에 있다) · 채움은 틀 안쪽으로 조금 들어간다
                back.type = Image.Type.Sliced;
                back.color = Color.white;
                back.pixelsPerUnitMultiplier = BorderScale(background.sizeDelta.y, 48f);
                fillRect.offsetMin = new Vector2(3f, 3f);
                fillRect.offsetMax = new Vector2(-3f, -3f);
            }

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
            // 큰 글자(제목)는 굵은 제목 글꼴, 나머지는 나눔고딕. 둘 다 어두운 테두리 + 그림자 (그래픽 평가 3번)
            bool title = size >= 54f && _titleFont != null;
            label.font = title ? _titleFont : _font;
            Material preset = title ? _titleMat : _bodyMat;
            if (preset != null) label.fontSharedMaterial = preset;
            label.text = text;
            label.fontSize = size;
            label.alignment = align;
            Color c = color ?? Color.white;
            label.color = c;
            // 금색 제목은 위가 밝고 아래가 진한 그라데이션
            if (title && c.r > 0.9f && c.g > 0.7f && c.g < 0.92f && c.b < 0.55f)
            {
                label.color = Color.white;
                label.enableVertexGradient = true;
                label.colorGradient = new VertexGradient(new Color(1f, 0.96f, 0.66f), new Color(1f, 0.96f, 0.66f), new Color(1f, 0.66f, 0.16f), new Color(1f, 0.66f, 0.16f));
            }

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
            Image image;
            if (_kit != null)
            {
                // 두툼한 게임 버튼: 선명한 색 · 아래 두께 · 광택 · 밑 그림자 · 누르면 눌림 (그래픽 평가 3번)
                image = Img(rt, Vivid(color), _kit.Button);
                image.type = Image.Type.Sliced;
                image.pixelsPerUnitMultiplier = BorderScale(size.y, 120f);
                var shadow = rt.gameObject.AddComponent<Shadow>();
                shadow.effectColor = new Color(0f, 0f, 0f, 0.4f);
                shadow.effectDistance = new Vector2(0f, -7f);
                rt.gameObject.AddComponent<ButtonJuice>();
            }
            else image = Img(rt, color, null);

            image.raycastTarget = true;
            var button = rt.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            // 눌림 색: 살짝 어둡게, 못 누르면 회색빛
            ColorBlock colors = button.colors;
            colors.pressedColor = new Color(0.85f, 0.85f, 0.85f);
            colors.disabledColor = new Color(0.55f, 0.55f, 0.6f, 0.9f);
            button.colors = colors;

            label = Label("Label", rt, text, 34, TextAlignmentOptions.Center);
            if (_titleFont != null)
            {
                label.font = _titleFont;
                if (_buttonMat != null) label.fontSharedMaterial = _buttonMat;
            }

            Stretch(label.rectTransform);
            label.margin = new Vector4(8f, 0f, 8f, _kit != null ? 9f / BorderScale(size.y, 120f) : 0f); // 아래 두께만큼 글자를 올린다
            return button;
        }
    }
}
