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
            var go = new GameObject("ChestOpening");
            go.transform.position = at;
            var opening = go.AddComponent<ChestOpening>();
            host.StartCoroutine(opening.Run(chestPrefab, levels, onOpened));
        }

        private IEnumerator Run(GameObject chestPrefab, int levels, Action onOpened)
        {
            var holder = new GameObject("Chest").transform;
            holder.SetParent(transform, false);
            GameObject model = Dungeon.RoomDevices.FitModel(holder, chestPrefab, 1.3f);
            Audio.AudioManager.Play("card", 0.8f);

            // 1) 덜덜 흔들림 (점점 세게)
            for (float t = 0f; t < 0.6f; t += Dt)
            {
                float k = t / 0.6f;
                holder.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(t * 60f) * 10f * k);
                holder.localScale = Vector3.one * (1f + 0.06f * k * Mathf.Abs(Mathf.Sin(t * 30f)));
                yield return null;
            }

            // 2) 툭 튀며 열림 + 빛기둥
            holder.localRotation = Quaternion.identity;
            Color color = levels >= 3 ? new Color(1f, 0.6f, 1f) : new Color(1f, 0.85f, 0.35f);
            FxBursts.Explosion(transform.position, 1.6f, color);
            CameraShake.Add(levels >= 3 ? 0.45f : 0.25f);
            Transform beam = Beam(levels >= 3 ? 1.6f : 1.1f, color);
            onOpened?.Invoke();

            for (float t = 0f; t < 0.9f; t += Dt)
            {
                float k = t / 0.9f;
                holder.localScale = Vector3.one * (1f + 0.25f * Mathf.Sin(Mathf.Min(1f, k * 3f) * Mathf.PI));
                if (beam != null)
                {
                    // 빛기둥: 빠르게 솟고 (0.25초) 천천히 가늘어진다. 바닥에서 위(-Z)로
                    float height = Mathf.Lerp(0.5f, 6f, Mathf.Min(1f, k * 4f));
                    beam.localScale = new Vector3(beam.localScale.x * (1f - Dt * 0.8f), height, 1f);
                    beam.localPosition = Vector3.back * (height * 0.5f);
                }

                yield return null;
            }

            if (model != null) Destroy(model);
            Destroy(gameObject);
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
