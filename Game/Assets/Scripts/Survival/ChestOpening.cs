using System;
using System.Collections;
using HistoryHeroes.Combat;
using HistoryHeroes.Common;
using UnityEngine;
using UnityEngine.Rendering;

namespace HistoryHeroes.Survival
{
    /// <summary>
    /// 보물 상자 열기 연출 (확장 1차 7장 뽕맛): 상자가 0.6초 덜덜 흔들리다 → 툭 튀며 열리고 → 하늘로 빛기둥 + 금빛 폭발 → 레벨업.
    /// 3회(5%)면 빛기둥이 무지개빛으로 더 굵다. 멈춤(레벨업 창)과 상관없이 실제 시간으로 돈다
    /// </summary>
    public sealed class ChestOpening : MonoBehaviour
    {
        /// <summary>QA 화면 찍기용 느리게 (1 = 보통). 개발 빌드에서만 바꾼다</summary>
        public static float DevSlow = 1f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => DevSlow = 1f;

        private static float Dt => Time.unscaledDeltaTime / Mathf.Max(1f, DevSlow);

        public static void Play(MonoBehaviour host, Vector2 at, GameObject chestPrefab, int levels, Action onOpened)
        {
            ChestOpening opening = Make(at);
            var holder = new GameObject("Chest").transform;
            holder.SetParent(opening.transform, false);
            Dungeon.RoomDevices.FitModel(holder, chestPrefab, 1.3f);
            Color color = levels >= 3 ? new Color(1f, 0.6f, 1f) : new Color(1f, 0.85f, 0.35f);
            host.StartCoroutine(opening.Run(holder, color, levels >= 3 ? 0.45f : 0.25f, levels >= 3 ? 1.6f : 1.1f, true, onOpened));
        }

        /// <summary>이미 놓인 상자 · 석비(보상 방 받침대)를 그 자리에서 연다. 모델은 뚜껑이 열린 채 남는다</summary>
        public static void PlayOn(MonoBehaviour host, Transform model, Color color, Action onOpened)
        {
            ChestOpening opening = Make(model.position);
            host.StartCoroutine(opening.Run(model, color, 0.3f, 1.2f, false, onOpened));
        }

        private static ChestOpening Make(Vector2 at)
        {
            var go = new GameObject("ChestOpening");
            go.transform.position = at;
            Destroy(go, 3f * Mathf.Max(1f, DevSlow)); // 연출(1.5초)을 돌리던 쪽이 멈춰도 빛기둥이 남지 않게
            return go.AddComponent<ChestOpening>();
        }

        /// <param name="owned">true면 끝나고 모델까지 지운다 (생존 상자). false면 크기 · 기울기만 되돌린다</param>
        private IEnumerator Run(Transform holder, Color color, float shake, float beamWidth, bool owned, Action onOpened)
        {
            Quaternion baseRotation = holder.rotation;
            Vector3 baseScale = holder.localScale;
            Transform lid = FindChild(holder, "Chest_Lid");
            Quaternion lidBase = lid != null ? lid.localRotation : Quaternion.identity;
            Audio.AudioManager.Play("card", 0.8f);

            // 1) 덜덜 흔들림 (점점 세게) — 화면 안에서 좌우로 (세계 Z축)
            for (float t = 0f; t < 0.6f && holder != null; t += Dt)
            {
                float k = t / 0.6f;
                holder.rotation = Quaternion.AngleAxis(Mathf.Sin(t * 60f) * 10f * k, Vector3.forward) * baseRotation;
                holder.localScale = baseScale * (1f + 0.06f * k * Mathf.Abs(Mathf.Sin(t * 30f)));
                yield return null;
            }

            if (holder == null)
            {
                Destroy(gameObject);
                yield break;
            }

            // 2) 툭 튀며 열림 + 빛기둥
            holder.rotation = baseRotation;
            FxBursts.Explosion(transform.position, 1.6f, color);
            CameraShake.Add(shake);
            Transform beam = Beam(beamWidth, color);
            onOpened?.Invoke();

            for (float t = 0f; t < 0.9f && holder != null; t += Dt)
            {
                float k = t / 0.9f;
                holder.localScale = baseScale * (1f + 0.25f * Mathf.Sin(Mathf.Min(1f, k * 3f) * Mathf.PI));
                if (lid != null)
                {
                    // 뚜껑: 0.2초 만에 뒤로 젖혀진다 (경첩 = 뚜껑 원점, 앞쪽이 들린다)
                    float open = 1f - (1f - Mathf.Min(1f, k * 4.5f)) * (1f - Mathf.Min(1f, k * 4.5f));
                    lid.localRotation = lidBase * Quaternion.Euler(105f * open, 0f, 0f);
                }

                if (beam != null)
                {
                    // 빛기둥: 빠르게 솟고 (0.25초) 천천히 가늘어진다. 바닥에서 위(-Z)로
                    float height = Mathf.Lerp(0.5f, 6f, Mathf.Min(1f, k * 4f));
                    beam.localScale = new Vector3(beam.localScale.x * (1f - Dt * 0.8f), height, 1f);
                    beam.localPosition = Vector3.back * (height * 0.5f);
                }

                yield return null;
            }

            if (!owned && holder != null) holder.localScale = baseScale;
            Destroy(gameObject); // 생존 상자는 이 밑에 있어 같이 지워진다
        }

        private static Transform FindChild(Transform root, string name)
        {
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
            {
                if (t.name == name) return t;
            }

            return null;
        }

        /// <summary>하늘로 솟는 빛기둥 (더하기 빛 재질, 위 = -Z)</summary>
        private Transform Beam(float width, Color color)
        {
            Material glow = Skills.SkillArt.Get != null ? Skills.SkillArt.Get.glow : null;
            if (glow == null) return null;
            var go = new GameObject("Beam");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = Vector3.back * 2f;
            go.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f); // 서 있는 판 (화면 쪽을 본다)
            go.transform.localScale = new Vector3(width, 0.5f, 1f);
            go.AddComponent<MeshFilter>().sharedMesh = Resources.GetBuiltinResource<Mesh>(Stage3D.BuiltinName(Stage3D.Shape.Floor));
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = glow;
            r.shadowCastingMode = ShadowCastingMode.Off;
            var block = new MaterialPropertyBlock();
            block.SetColor("_BaseColor", color * 2.2f);
            r.SetPropertyBlock(block);
            return go.transform;
        }
    }
}
