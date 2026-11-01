using HistoryHeroes.Common;
using HistoryHeroes.Dungeon;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace HistoryHeroes.EditorTools
{
    /// <summary>
    /// 확장 1차 3장 특수 방 장면 조각: 갈림길 문 2개 (위 벽에 새긴 빛나는 아치 + 표지판 + 떠 있는 그림판),
    /// 특수 방 아이콘 · 보상 상자 · 석비 모델 연결.
    /// </summary>
    public static partial class SceneBuilder
    {
        /// <summary>갈림길 문 자리 (가운데 문 양옆). 도달 검사(LayoutReachabilityTests)와 같다</summary>
        public const float ForkX = 2.4f;

        private static ForkGate[] BuildForkGates(Transform dungeonRoot)
        {
            var gates = new ForkGate[2];
            GameObject arch = Art3DStage.Prefab("Door_Gate_Parts");
            GameObject sign = Art3DStage.Prefab("Fork_Sign");
            for (int i = 0; i < 2; i++)
            {
                float x = i == 0 ? -ForkX : ForkX;
                var go = new GameObject(i == 0 ? "ForkLeft" : "ForkRight");
                go.transform.SetParent(dungeonRoot, false);
                go.transform.position = new Vector3(x, ArenaHalfHeight - 0.3f, 0f);
                var box = go.AddComponent<BoxCollider2D>();
                box.isTrigger = true;
                box.size = new Vector2(1.2f, 0.6f);
                go.AddComponent<Door>();
                var gate = go.AddComponent<ForkGate>();

                var visuals = new GameObject("Visuals").transform;
                visuals.SetParent(go.transform, false);

                // 위 벽 바로 앞에 선 아치 (문짝 없이 빛만 — 뒤가 벽이라 문짝이 젖혀질 자리가 없다)
                if (arch != null)
                {
                    var a = (GameObject)Object.Instantiate(arch, visuals, false);
                    a.name = "Arch3D";
                    a.transform.position = new Vector3(x, ArenaHalfHeight - 0.3f, 0f); // 가운데 문(벽 틈)과 달리 벽 앞에 세운다
                    a.transform.localScale = Vector3.one * 0.8f;
                    foreach (string leaf in new[] { "Door_Leaf_XPos", "Door_Leaf_XNeg" })
                    {
                        Transform t = FindDeep(a.transform, leaf);
                        if (t != null) t.gameObject.SetActive(false);
                    }
                }

                Color glowColor = i == 0 ? new Color(0.55f, 1.05f, 1.6f) : new Color(1.7f, 1.0f, 0.3f);
                var glow = new GameObject("Glow");
                glow.transform.SetParent(visuals, false);
                glow.transform.position = new Vector3(x, ArenaHalfHeight - 0.03f, -0.76f); // 아치 안 (벽 앞면 바로 앞에 서 있는 판)
                glow.transform.rotation = Quaternion.Euler(-90f, 0f, 0f);
                glow.transform.localScale = new Vector3(1.1f, 1.55f, 1f);
                glow.AddComponent<MeshFilter>().sharedMesh = Builtin(Stage3D.Shape.Floor);
                var glowRenderer = glow.AddComponent<MeshRenderer>();
                glowRenderer.sharedMaterial = FxGlowMaterial(i == 0 ? "ForkLeft" : "ForkRight", glowColor, null, plain: true); // 무늬 없는 네모 = 문 모양 빛
                glowRenderer.shadowCastingMode = ShadowCastingMode.Off;

                if (sign != null)
                {
                    var post = (GameObject)Object.Instantiate(sign, visuals, false);
                    post.name = "Sign3D";
                    post.transform.position = new Vector3(i == 0 ? -3.5f : 3.5f, ArenaHalfHeight - 0.55f, 0f);
                    post.transform.rotation = Quaternion.Euler(0f, 0f, i == 0 ? -20f : 20f);
                    FitHeight(post, 1.2f);
                }

                // 떠 있는 그림판: 아이콘 + 짧은 글 (ForkGate가 카메라를 보게 돌린다)
                var board = new GameObject("Board").transform;
                board.SetParent(visuals, false);
                board.position = new Vector3(x, ArenaHalfHeight - 0.3f, -2.55f);
                SpriteRenderer plate = null;
                if (_kit != null && _kit.Row != null)
                {
                    plate = new GameObject("Plate").AddComponent<SpriteRenderer>();
                    plate.transform.SetParent(board, false);
                    plate.sprite = _kit.Row;
                    plate.drawMode = SpriteDrawMode.Sliced;
                    plate.size = new Vector2(2.7f, 1.15f);
                    plate.sortingOrder = 40;
                }

                var icon = new GameObject("Icon").AddComponent<SpriteRenderer>();
                icon.transform.SetParent(board, false);
                icon.transform.localPosition = new Vector3(-0.88f, 0f, -0.02f);
                icon.sortingOrder = 41;

                var label = new GameObject("Label").AddComponent<TextMeshPro>();
                label.transform.SetParent(board, false);
                label.transform.localPosition = new Vector3(0.32f, 0f, -0.02f);
                label.rectTransform.sizeDelta = new Vector2(1.75f, 0.95f);
                if (_titleFont != null) label.font = _titleFont;
                label.alignment = TextAlignmentOptions.Center;
                label.textWrappingMode = TextWrappingModes.NoWrap;
                label.enableAutoSizing = true;
                label.fontSizeMin = 0.6f;
                label.fontSizeMax = 3.2f;
                label.outlineWidth = 0.2f;
                label.outlineColor = new Color32(0, 0, 0, 200);
                label.sortingOrder = 42;
                label.text = i == 0 ? "평범한 방" : "도전 방";

                using (var props = new Props(gate))
                {
                    props.Ref("visuals", visuals.gameObject).Ref("board", board).Ref("label", label).Ref("icon", icon).Ref("plate", plate);
                }

                gates[i] = gate;
            }

            return gates;
        }

        /// <summary>모델 높이(-Z 방향)를 맞춘다 (블렌더 소품 원래 크기가 제각각)</summary>
        private static void FitHeight(GameObject go, float height)
        {
            Renderer[] renderers = go.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) return;
            Bounds b = renderers[0].bounds;
            foreach (Renderer r in renderers) b.Encapsulate(r.bounds);
            if (b.size.z > 0.01f) go.transform.localScale *= height / b.size.z;
        }

        /// <summary>기믹 보스 소품 (Resources/GimmickArt): 수정 기둥</summary>
        private static void CreateGimmickArt()
        {
            AssetUtil.EnsureFolder("Assets/Resources");
            var art = AssetUtil.LoadOrCreate<Enemy.GimmickArt>("Assets/Resources/GimmickArt.asset");
            art.pillar = Art3DStage.Prefab("Deco_Crystal");
            EditorUtility.SetDirty(art);
        }

        /// <summary>구간 장치 모델 · 물 재질 (확장 1차 2장)</summary>
        private static void WireDevices(DungeonRun run)
        {
            using (var props = new Props(run))
            {
                props.Ref("deviceArt.berry", Art3DStage.Prefab("Berry_Bush")).Ref("deviceArt.keg", Art3DStage.Prefab("Powder_Keg"))
                    .Ref("deviceArt.gate", Art3DStage.Prefab("Iron_Gate")).Ref("deviceArt.saw", Art3DStage.Prefab("Saw_Gear"))
                    .Ref("deviceArt.lever", Art3DStage.Prefab("Lever"))
                    .Ref("deviceArt.water", UnlitTransparent("Water", new Color(0.2f, 0.48f, 0.72f, 0.88f)))
                    .Ref("deviceArt.waterFlow", UnlitTransparent("WaterFlow", new Color(0.78f, 0.92f, 1f, 0.5f)));
            }
        }

        /// <summary>반투명 단색 재질 (URP Unlit, 섞기) — 개울 물</summary>
        private static Material UnlitTransparent(string name, Color color)
        {
            string path = $"{Art3DAssets.Root}/Materials/M_{name}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
                AssetDatabase.CreateAsset(mat, path);
            }

            mat.SetFloat("_Surface", 1f);
            mat.SetFloat("_Blend", 0f);
            mat.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            mat.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            mat.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
            mat.SetFloat("_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
            mat.SetFloat("_ZWrite", 0f);
            mat.SetOverrideTag("RenderType", "Transparent");
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.renderQueue = (int)RenderQueue.Transparent;
            mat.SetColor("_BaseColor", color);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        /// <summary>DungeonRun 특수 방 아이콘 · 모델 · 갈림길 문</summary>
        private static void WireSpecialRooms(DungeonRun run, ForkGate[] gates)
        {
            WireDevices(run);
            using (var props = new Props(run))
            {
                props.Refs("forkGates", gates)
                    .Ref("specialArt.normal", Icon("sword")).Ref("specialArt.survive", Icon("clock")).Ref("specialArt.noHit", Icon("shield"))
                    .Ref("specialArt.elite", Icon("star")).Ref("specialArt.cursed", Icon("rune")).Ref("specialArt.fork", Icon("door"))
                    .Ref("specialArt.stele", Icon("relic")).Ref("specialArt.pass", Icon("heal"))
                    .Ref("specialArt.chestModel", Art3DStage.Prefab("Chest_Gold")).Ref("specialArt.steleModel", Art3DStage.Prefab("Stele"));
                var icons = new Sprite[Blessings.All.Length];
                for (int i = 0; i < icons.Length; i++) icons[i] = Icon(Blessings.All[i].Icon);
                props.Refs("specialArt.blessings", icons);
            }
        }
    }
}
