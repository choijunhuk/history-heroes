using HistoryHeroes.Dungeon;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HistoryHeroes.Common
{
    /// <summary>
    /// 2D 그림 시험판: 전투 장면이 열리면 던전 방의 3D 바닥 · 벽 · 문 · 바깥을 숨기고 코덱스 그림으로 다시 깐다.
    /// 카메라는 바로 위에서 내려다본다 (기울기 0) — 그림이 비스듬한 각도로 그려져 있어서 위에서 봐도 서 있어 보인다.
    /// 판정(벽 콜라이더 · 문 트리거)은 그대로. 보스 원형 경기장 · 서바이벌은 아직 3D (시험판 범위 = 던전 방 하나)
    /// </summary>
    public sealed class Art2DStage : MonoBehaviour
    {
        private const float WallThick = 1.5f;
        private Door _door;
        private SpriteRenderer _doorSprite;
        private readonly System.Collections.Generic.List<(Transform visuals, SpriteRenderer sprite)> _forks = new System.Collections.Generic.List<(Transform, SpriteRenderer)>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Hook()
        {
            SceneManager.sceneLoaded -= OnLoaded;
            SceneManager.sceneLoaded += OnLoaded;
            OnLoaded(SceneManager.GetActiveScene(), LoadSceneMode.Single);
        }

        private static void OnLoaded(Scene scene, LoadSceneMode mode)
        {
            if (!Art2D.Enabled || scene.name != "Battle" || FindAnyObjectByType<Art2DStage>() != null) return;
            new GameObject("Art2DStage").AddComponent<Art2DStage>();
        }

        private void Start()
        {
            GameObject dungeon = GameObject.Find("DungeonMode");
            if (dungeon == null || !dungeon.activeInHierarchy) return; // 던전 방만 (시험판)

            foreach (CameraFit fit in FindObjectsByType<CameraFit>(FindObjectsSortMode.None)) fit.Tilt = 0f;

            Transform wallLeft = Find("Arena/Wall Left");
            Transform wallTop = Find("Arena/Wall Top");
            float halfW = wallLeft != null ? Mathf.Abs(wallLeft.position.x) - 0.5f : 4f;
            float halfH = wallTop != null ? Mathf.Abs(wallTop.position.y) - 0.5f : 7f;

            Hide3D("Arena");
            Hide3D("Outside");
            Hide3D("DungeonMode/Floor");
            Hide3D("DungeonMode/DoorGate3D");
            GameObject mainDoor = GameObject.Find("DungeonMode/Door"); // 갈림길 문(ForkLeft/Right)에도 Door가 있어서 이름으로 찾는다
            _door = mainDoor != null ? mainDoor.GetComponent<Door>() : null;
            if (_door != null) Hide3D(_door.transform);

            Art2DLibrary lib = Art2D.Library;
            if (Camera.main != null) Camera.main.backgroundColor = new Color(0.07f, 0.07f, 0.09f);

            // 바닥: 이음새 없는 무늬를 깐다 (한 장 = 4 유닛)
            Tiled("Floor2D", lib.floor, new Vector2(0f, 0f), new Vector2(halfW * 2f + 1f, halfH * 2f + 1f), 4f, -2000, 0.02f);

            // 벽: 위 · 아래는 가로 벽, 양옆은 세로 벽 (위 벽은 앞면이 보이게 방 안쪽으로 조금 내려온다)
            Tiled("WallTop2D", lib.wallTop, new Vector2(0f, halfH + WallThick * 0.42f), new Vector2(halfW * 2f + 2f, WallThick), WallThick, Art2D.SortOrder(halfH + 1f), 0f);
            SpriteRenderer bottom = Tiled("WallBottom2D", lib.wallTop, new Vector2(0f, -halfH - WallThick * 0.42f), new Vector2(halfW * 2f + 2f, WallThick), WallThick, Art2D.SortOrder(-halfH - 1f), 0f);
            if (bottom != null) bottom.flipY = true;
            Tiled("WallLeft2D", lib.wallSide, new Vector2(-halfW - 0.45f, 0f), new Vector2(1f, halfH * 2f + 2f), 2f, Art2D.SortOrder(-halfH - 2f), 0f);
            SpriteRenderer right = Tiled("WallRight2D", lib.wallSide, new Vector2(halfW + 0.45f, 0f), new Vector2(1f, halfH * 2f + 2f), 2f, Art2D.SortOrder(-halfH - 2f), 0f);
            if (right != null) right.flipX = true;

            // 문 (열리면 그림이 바뀐다) · 문 옆 화로 · 모서리 깃발
            if (_door != null)
            {
                _doorSprite = Standing("Door2D", lib.doorClosed, new Vector2(_door.transform.position.x, halfH - 0.15f), 2.6f, Art2D.SortOrder(halfH + 0.5f));
            }

            // 바닥 장식: 이끼 몇 장 · 뼈 무더기 (가운데 싸움터는 비운다)
            Vector2[] mossAt = { new Vector2(-halfW + 1.1f, halfH - 2.2f), new Vector2(halfW - 1.3f, -halfH + 2.4f), new Vector2(-halfW + 1.6f, -halfH + 4.2f), new Vector2(halfW - 1f, 1.8f) };
            for (int i = 0; i < mossAt.Length; i++)
            {
                SpriteRenderer m = Standing("Moss" + i, lib.moss, mossAt[i], 1.4f + 0.3f * (i % 2), -1900);
                if (m != null) m.transform.rotation = Quaternion.Euler(0f, 0f, i * 77f);
            }

            Standing("Bones", lib.bones, new Vector2(halfW - 1.2f, halfH - 1.6f), 1.2f, Art2D.SortOrder(halfH - 1.6f));

            // 갈림길 문 2개: 3D 아치 대신 열린 문 그림 (표지판 글은 그대로)
            foreach (string fork in new[] { "ForkLeft", "ForkRight" })
            {
                Transform f = dungeon.transform.Find(fork); // 꺼져 있어도 찾는다 (GameObject.Find는 켜진 것만)
                if (f == null) continue;
                Transform visuals = f.Find("Visuals");
                foreach (string part in new[] { "Arch3D", "Glow" }) // 표지판 · 그림판은 남긴다 (갈 곳 글)
                {
                    Transform p = visuals != null ? visuals.Find(part) : null;
                    if (p != null) Hide3D(p);
                }
                SpriteRenderer s = Standing(f.name + "2D", lib.doorOpen, new Vector2(f.position.x, halfH - 0.15f), 2.2f, Art2D.SortOrder(halfH + 0.5f));
                if (s != null) _forks.Add((visuals != null ? visuals : f, s));
            }

            Standing("BrazierL", lib.brazier, new Vector2(-1.9f, halfH + 0.05f), 0.9f, Art2D.SortOrder(halfH + 0.4f));
            Standing("BrazierR", lib.brazier, new Vector2(1.9f, halfH + 0.05f), 0.9f, Art2D.SortOrder(halfH + 0.4f));
            Standing("BannerL", lib.banner, new Vector2(-halfW + 0.35f, halfH + 0.1f), 0.8f, Art2D.SortOrder(halfH + 0.3f));
            Standing("BannerR", lib.banner, new Vector2(halfW - 0.35f, halfH + 0.1f), 0.8f, Art2D.SortOrder(halfH + 0.3f));
        }

        private void Update()
        {
            foreach (var (visuals, sprite) in _forks) sprite.enabled = visuals.gameObject.activeInHierarchy;
            if (_door == null || _doorSprite == null) return;
            _doorSprite.enabled = _door.gameObject.activeInHierarchy;
            Sprite want = _door.IsOpen ? Art2D.Library.doorOpen : Art2D.Library.doorClosed;
            if (want != null && _doorSprite.sprite != want) _doorSprite.sprite = want;
        }

        private static Transform Find(string path)
        {
            GameObject go = GameObject.Find(path);
            return go != null ? go.transform : null;
        }

        private static void Hide3D(string path)
        {
            Transform t = Find(path);
            if (t != null) Hide3D(t);
        }

        /// <summary>3D 겉모습(메시)만 끈다. 2D 몸 스프라이트 · 콜라이더는 그대로</summary>
        private static void Hide3D(Transform root)
        {
            foreach (Renderer r in root.GetComponentsInChildren<Renderer>(true))
            {
                if (r is MeshRenderer || r is SkinnedMeshRenderer) r.enabled = false;
            }
        }

        /// <summary>
        /// 반복 그림 판: size = 덮을 넓이 (월드), tileWorld = 그림 한 장의 세로 크기 (월드). 그림 가로세로 비율은 지킨다
        /// </summary>
        private SpriteRenderer Tiled(string name, Sprite sprite, Vector2 center, Vector2 size, float tileWorld, int order, float z)
        {
            if (sprite == null) return null;
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            go.transform.position = new Vector3(center.x, center.y, z);
            float k = tileWorld / Mathf.Max(0.01f, sprite.bounds.size.y);
            go.transform.localScale = new Vector3(k, k, 1f);
            var r = go.AddComponent<SpriteRenderer>();
            r.sprite = sprite;
            r.drawMode = SpriteDrawMode.Tiled;
            r.tileMode = SpriteTileMode.Continuous;
            r.size = size / k;
            r.sortingOrder = order;
            return r;
        }

        /// <summary>서 있는 그림 (받침점 = 아래 가운데): width = 월드 너비</summary>
        private SpriteRenderer Standing(string name, Sprite sprite, Vector2 foot, float width, int order)
        {
            if (sprite == null) return null;
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            go.transform.position = new Vector3(foot.x, foot.y, -0.01f);
            float k = width / Mathf.Max(0.01f, sprite.bounds.size.x);
            go.transform.localScale = new Vector3(k, k, 1f);
            var r = go.AddComponent<SpriteRenderer>();
            r.sprite = sprite;
            r.sortingOrder = order;
            return r;
        }
    }
}
