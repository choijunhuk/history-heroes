using System;
using System.Collections.Generic;
using HistoryHeroes.Combat;
using HistoryHeroes.Common;
using HistoryHeroes.Player;
using HistoryHeroes.Stats;
using UnityEngine;
using UnityEngine.Rendering;

namespace HistoryHeroes.Dungeon
{
    /// <summary>장치 모델 · 재질 (장면 빌더가 블렌더 소품으로 채운다. 없으면 2D 모양 그대로)</summary>
    [Serializable]
    public sealed class DeviceArt
    {
        public GameObject berry, keg, gate, saw, lever;
        public Material water, waterFlow;
    }

    /// <summary>방 하나의 장치들이 같이 쓰는 것 (방을 옮기면 새로 만든다)</summary>
    public sealed class DeviceRoom
    {
        public PlayerController Player;
        public readonly List<TrapZone> Zones = new List<TrapZone>();
        public readonly List<PowderKeg> Kegs = new List<PowderKeg>();
        /// <summary>장치가 나중에 만든 것 (회복 구슬) — 방을 옮길 때 지운다</summary>
        public readonly List<GameObject> Extras = new List<GameObject>();
        /// <summary>열매 덤불 회복 구슬: 방마다 2개까지</summary>
        public int OrbsLeft = 2;
        public Transform Parent;

        public float PlayerAttack => Player != null ? Player.Stats.Get(StatType.Attack) : 100f;

        public void Clear()
        {
            foreach (GameObject go in Extras)
            {
                if (go != null) UnityEngine.Object.Destroy(go);
            }

            Extras.Clear();
        }
    }

    /// <summary>
    /// 구간 장치 만들기 (확장 1차 2장 + 검토 9장 9·10번). 모든 장치는 예고가 있고 적에게도 똑같이 작용한다.
    /// 피해: 나 = 내 최대체력 비율 (상한), 적 = 내 공격력 배수 (폭약 통) 또는 적 최대체력 비율 (톱니).
    /// </summary>
    public static class RoomDevices
    {
        public const float BerryHeal = 0.05f;
        private static int _wall = -1;
        private static int _bodies = -1;

        public static int WallLayer => _wall >= 0 ? _wall : (_wall = LayerMask.NameToLayer("Wall"));
        /// <summary>영웅 + 적 몸</summary>
        public static int BodyMask => _bodies >= 0 ? _bodies : (_bodies = LayerMask.GetMask("Player", "Enemy"));

        public static GameObject Spawn(DeviceSpot d, DeviceRoom room, DeviceArt art)
        {
            switch (d.kind)
            {
                case DeviceKind.BerryBush: return Berry(d, room, art);
                case DeviceKind.Creek: return Creek(d, room, art);
                case DeviceKind.PowderKeg: return Keg(d, room, art);
                case DeviceKind.IronGate: return IronGate.Create(d, room, art != null ? art.gate : null);
                case DeviceKind.SawGear: return SawGear.Create(d, room, art != null ? art.saw : null);
                case DeviceKind.Lever: return Lever.Create(d, room, art != null ? art.lever : null);
                default: return null;
            }
        }

        private static GameObject Body(string name, DeviceSpot d, DeviceRoom room, int layer, Color color, float size, out SpriteRenderer sr)
        {
            var go = new GameObject(name) { layer = layer };
            go.transform.SetParent(room.Parent, false);
            go.transform.position = d.position;
            go.transform.localScale = new Vector3(size, size, 1f);
            sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = PlaceholderSprites.Circle;
            sr.color = color;
            sr.sortingOrder = -6;
            go.AddComponent<CircleCollider2D>().radius = 0.5f;
            return go;
        }

        private static GameObject Berry(DeviceSpot d, DeviceRoom room, DeviceArt art)
        {
            float size = Mathf.Max(0.7f, d.size.x);
            GameObject go = Body("BerryBush", d, room, WallLayer, new Color(0.3f, 0.6f, 0.3f), size, out SpriteRenderer sr);
            var breakable = go.AddComponent<Breakable>();
            breakable.Setup(2, sr);
            go.AddComponent<BerryBush>().Setup(room, breakable);
            if (art != null && FitModel(go.transform, art.berry, size * 1.05f) != null) sr.enabled = false;
            return go;
        }

        private static GameObject Keg(DeviceSpot d, DeviceRoom room, DeviceArt art)
        {
            float size = Mathf.Max(0.7f, d.size.x);
            GameObject go = Body("PowderKeg", d, room, WallLayer, new Color(0.7f, 0.35f, 0.2f), size, out SpriteRenderer sr);
            var breakable = go.AddComponent<Breakable>();
            breakable.Setup(3, sr);
            GameObject model = art != null ? FitModel(go.transform, art.keg, size) : null;
            if (model != null) sr.enabled = false;
            var keg = go.AddComponent<PowderKeg>();
            keg.Setup(room, breakable, model != null ? model.transform : go.transform);
            room.Kegs.Add(keg);
            return go;
        }

        /// <summary>개울: 낮은 장애물 띠 (Default 레이어 = 몸만 막고 탄은 넘어간다) + 흐르는 물</summary>
        private static GameObject Creek(DeviceSpot d, DeviceRoom room, DeviceArt art)
        {
            var go = new GameObject("Creek") { layer = 0 };
            go.transform.SetParent(room.Parent, false);
            go.transform.position = d.position;
            go.transform.rotation = Quaternion.Euler(0f, 0f, d.angle);
            go.AddComponent<BoxCollider2D>().size = d.size;
            go.AddComponent<CreekFlow>().Setup(d.size, art != null ? art.water : null, art != null ? art.waterFlow : null);
            return go;
        }

        /// <summary>
        /// 블렌더 소품을 owner 밑에 놓고 바닥에서 본 크기(가로·세로 중 큰 쪽)를 footprint(월드)에 맞춘다. 모델 프리팹은 이미 게임 방향으로 서 있다
        /// </summary>
        public static GameObject FitModel(Transform owner, GameObject prefab, float footprint)
        {
            if (prefab == null) return null;
            if (Common.Art2D.Enabled)
            {
                // 2D 그림 시험판: 그림이 있는 장치는 그림 한 장 (받침점 = 자리 아래 끝, 너비 = 자리)
                Sprite art = Common.Art2D.Library.PropFor(prefab.name);
                if (art != null) return FitSprite(owner, art, footprint);
            }
            GameObject go = UnityEngine.Object.Instantiate(prefab, owner, false);
            go.name = "Model";
            go.transform.localPosition = Vector3.zero;
            Vector3 ownerScale = owner.lossyScale;
            // 주인 배율을 되돌려 모델이 찌그러지지 않게 (주인은 판정용 크기)
            go.transform.localScale = new Vector3(1f / Mathf.Max(0.0001f, ownerScale.x), 1f / Mathf.Max(0.0001f, ownerScale.y), 1f / Mathf.Max(0.0001f, Mathf.Abs(ownerScale.z)));
            Bounds b = WorldBounds(go);
            float size = Mathf.Max(b.size.x, b.size.y);
            if (size > 0.01f) go.transform.localScale *= footprint / size;
            foreach (Renderer r in go.GetComponentsInChildren<Renderer>())
            {
                r.shadowCastingMode = ShadowCastingMode.On;
                r.lightProbeUsage = LightProbeUsage.Off;
            }

            StageLook.Retint(go);
            return go;
        }

        /// <summary>2D 그림 시험판: 주인 자리에 그림을 세운다 (주인 배율을 되돌려 찌그러지지 않게)</summary>
        public static GameObject FitSprite(Transform owner, Sprite art, float footprint)
        {
            var go = new GameObject("Model");
            go.transform.SetParent(owner, false);
            Vector3 s = owner.lossyScale;
            float k = footprint * 1.1f / Mathf.Max(0.01f, art.bounds.size.x);
            go.transform.localScale = new Vector3(k / Mathf.Max(0.0001f, Mathf.Abs(s.x)), k / Mathf.Max(0.0001f, Mathf.Abs(s.y)), 1f);
            go.transform.position = owner.position + new Vector3(0f, -footprint * 0.4f, -0.01f);
            var r = go.AddComponent<SpriteRenderer>();
            r.sprite = art;
            r.sortingOrder = Common.Art2D.SortOrder(go.transform.position.y);
            return go;
        }

        public static Bounds WorldBounds(GameObject go)
        {
            var b = new Bounds(go.transform.position, Vector3.zero);
            bool any = false;
            foreach (Renderer r in go.GetComponentsInChildren<Renderer>())
            {
                if (!any) b = r.bounds;
                else b.Encapsulate(r.bounds);
                any = true;
            }

            return b;
        }

        public static DamageInfo ToPlayer(float amount, string source) =>
            new DamageInfo { Amount = amount, Kind = DamageKind.Enemy, AttackerTeam = Team.Enemy, SourceId = source };

        /// <summary>장치가 적에게 (함정과 같은 종류: 기본 공격 비중·처치 미션에 세지 않는다)</summary>
        public static DamageInfo ToEnemy(float amount, string source) =>
            new DamageInfo { Amount = amount, Kind = DamageKind.Enemy, AttackerTeam = Team.Player, SourceId = source };

        /// <summary>폭약 통: 내 처치로 센다 (액티브 피해, 검토 9장 9번)</summary>
        public static DamageInfo FromPlayer(float amount, string source) =>
            new DamageInfo { Amount = amount, Kind = DamageKind.Active, AttackerTeam = Team.Player, SourceId = source };

        /// <summary>몸을 밀어낸다 (물리 위치까지)</summary>
        public static void Push(Collider2D c, Vector2 delta)
        {
            Rigidbody2D rb = c.attachedRigidbody;
            if (rb != null) rb.position += delta;
            else c.transform.position += (Vector3)delta;
        }
    }
}
