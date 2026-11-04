using System;
using System.Reflection;
using HistoryHeroes.Dungeon;
using HistoryHeroes.Player;
using NUnit.Framework;
using UnityEngine;

namespace HistoryHeroes.Tests
{
    public sealed class BattleReadabilityTests
    {
        [TestCase(5.7f, 0.9f, true)]
        [TestCase(0.9f, 5.7f, false)]
        public void CreekKeepsCollisionAndSharedMaterial_ButFlowsAlongLongAxis(float width, float height, bool alongX)
        {
            var root = new GameObject("Creek");
            Material water = null, flow = null;
            try
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
                Assert.IsNotNull(shader);
                water = new Material(shader);
                flow = new Material(shader);
                Color originalWater = new Color(0.2f, 0.48f, 0.72f, 0.88f);
                Color originalFlow = new Color(0.78f, 0.92f, 1f, 0.5f);
                water.SetColor("_BaseColor", originalWater);
                flow.SetColor("_BaseColor", originalFlow);
                originalWater = water.GetColor("_BaseColor");
                originalFlow = flow.GetColor("_BaseColor");
                var collider = root.AddComponent<BoxCollider2D>();
                collider.size = new Vector2(width, height);

                root.AddComponent<CreekFlow>().Setup(collider.size, water, flow);

                Assert.AreEqual(new Vector2(width, height), collider.size);
                var bed = root.transform.Find("Water").GetComponent<MeshRenderer>();
                Assert.AreSame(water, bed.sharedMaterial);
                Assert.AreEqual(originalWater, water.GetColor("_BaseColor"));
                Assert.AreEqual(0.52f, Alpha(bed), 0.001f);
                for (int i = 0; i < 4; i++)
                {
                    var ripple = root.transform.Find("Ripple" + i).GetComponent<MeshRenderer>();
                    Assert.AreSame(flow, ripple.sharedMaterial);
                    Assert.AreEqual(originalFlow, flow.GetColor("_BaseColor"));
                    Assert.AreEqual(0.27f, Alpha(ripple), 0.001f);
                    Assert.Greater(alongX ? ripple.transform.localScale.x : ripple.transform.localScale.y,
                        alongX ? ripple.transform.localScale.y : ripple.transform.localScale.x);
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
                if (water != null) UnityEngine.Object.DestroyImmediate(water);
                if (flow != null) UnityEngine.Object.DestroyImmediate(flow);
            }
        }

        [TestCase(256, 100)]
        [TestCase(128, 50)]
        public void HeroRingUsesSpriteWorldBoundsAndParentScale(int pixels, int ppu)
        {
            var parent = new GameObject("Parent");
            var player = new GameObject("Player");
            Texture2D texture = null;
            Sprite sprite = null;
            try
            {
                parent.transform.localScale = new Vector3(1.6f, 0.8f, 1f);
                player.SetActive(false);
                player.transform.SetParent(parent.transform, false);
                var controller = player.AddComponent<PlayerController>();
                var indicator = player.AddComponent<StopIndicator>();
                texture = new Texture2D(pixels, pixels);
                sprite = Sprite.Create(texture, new Rect(0f, 0f, pixels, pixels), new Vector2(0.5f, 0.5f), ppu);
                var ring = new GameObject("HeroRing").AddComponent<SpriteRenderer>();
                ring.transform.SetParent(player.transform, false);
                ring.transform.localScale = new Vector3(1.1f, 1.1f, 1f);
                ring.sprite = sprite;
                ring.color = new Color(0.45f, 0.95f, 1f, 0.4f);
                typeof(StopIndicator).GetField("player", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(indicator, controller);

                typeof(StopIndicator).GetMethod("Start", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(indicator, null);

                Assert.AreEqual(1.6f, sprite.bounds.size.x * ring.transform.lossyScale.x, 0.01f);
                Assert.AreEqual(1.6f, sprite.bounds.size.y * ring.transform.lossyScale.y, 0.01f);
                Assert.LessOrEqual(ring.color.a, 0.24f);
                Assert.AreEqual(1.3f, (float)typeof(StopIndicator).GetField("fullSize", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(indicator));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(parent);
                if (sprite != null) UnityEngine.Object.DestroyImmediate(sprite);
                if (texture != null) UnityEngine.Object.DestroyImmediate(texture);
            }
        }

        private static float Alpha(Renderer renderer)
        {
            var block = new MaterialPropertyBlock();
            renderer.GetPropertyBlock(block);
            return block.GetColor("_BaseColor").a;
        }
    }
}
