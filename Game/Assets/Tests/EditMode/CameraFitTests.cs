using HistoryHeroes.Combat;
using HistoryHeroes.Common;
using NUnit.Framework;
using UnityEngine;

namespace HistoryHeroes.Tests
{
    /// <summary>3D 전환 (Art3D-Plan 3·4번): 비스듬한 원근 카메라 맞춤 계산 · 상태 이상 색</summary>
    public sealed class CameraFitTests
    {
        private const float Tilt = 38f, Fov = 34f, Top = 0.1f, Bottom = 0.01f, Side = 0.01f;

        [TestCase(9f / 16f)]
        [TestCase(9f / 19.5f)]
        [TestCase(9f / 21f)]
        public void Room_FitsInsideMargins_AtClosestDistance(float aspect)
        {
            var room = new Vector2(10f, 16f);
            CameraFit.Solution s = CameraFit.Solve(room, Tilt, Fov, aspect, Top, Bottom, Side);
            Assert.That(s.Position.z, Is.LessThan(0f), "카메라는 땅 위(-Z)");

            float maxX = 0f, minY = 1f, maxY = 0f;
            foreach (Vector2 corner in Corners(room))
            {
                Vector3 p = CameraFit.Project(corner, s.Position, Tilt, Fov, aspect);
                Assert.That(p.z, Is.GreaterThan(0f), "모서리가 카메라 앞");
                Assert.That(p.x, Is.InRange(Side - 0.001f, 1f - Side + 0.001f), $"가로 {corner}");
                Assert.That(p.y, Is.InRange(Bottom - 0.001f, 1f - Top + 0.001f), $"세로 {corner}");
                maxX = Mathf.Max(maxX, Mathf.Abs(p.x - 0.5f));
                minY = Mathf.Min(minY, p.y);
                maxY = Mathf.Max(maxY, p.y);
            }

            // 가장 가까운 거리: 가로나 세로 한쪽은 여백에 거의 닿는다
            bool touchesSide = maxX > 0.5f - Side - 0.01f;
            bool touchesVertical = maxY - minY > 1f - Top - Bottom - 0.01f;
            Assert.IsTrue(touchesSide || touchesVertical, "더 가까이 갈 수 있는데 멀리 있다");
            // 세로는 여백 사이 가운데
            Assert.AreEqual(minY - Bottom, 1f - Top - maxY, 0.01f);
        }

        [Test]
        public void GroundView_CoversTheRoom()
        {
            var room = new Vector2(10f, 16f);
            float aspect = 9f / 19.5f;
            CameraFit.Solution s = CameraFit.Solve(room, Tilt, Fov, aspect, Top, Bottom, Side);
            foreach (Vector2 corner in Corners(room)) Assert.IsTrue(s.Ground.Contains(corner), $"땅 범위가 {corner}를 덮는다");
            Assert.That(s.Radius, Is.GreaterThan(room.magnitude * 0.5f));
            // 원근: 먼 쪽(위)이 가까운 쪽보다 넓다 → 위 모서리가 더 멀다
            Assert.That(s.Ground.yMax, Is.GreaterThan(room.y * 0.5f));
        }

        [Test]
        public void StraightDown_MatchesOrthographicLikeFit()
        {
            // 기울기 0이면 위에서 내려다본다: 세로 화면에서는 가로 폭이 기준
            var room = new Vector2(10f, 16f);
            float aspect = 9f / 19.5f;
            CameraFit.Solution s = CameraFit.Solve(room, 0f, Fov, aspect, 0f, 0f, 0f);
            float tanHalf = Mathf.Tan(Fov * 0.5f * Mathf.Deg2Rad);
            float needW = room.x * 0.5f / (tanHalf * aspect), needH = room.y * 0.5f / tanHalf;
            Assert.AreEqual(Mathf.Max(needW, needH), s.Distance, 0.05f);
            Assert.AreEqual(0f, s.FocusY, 0.01f);
        }

        [TestCase(55f, 60f)]
        [TestCase(70f, 70f)]
        public void SteepAngles_StayBelowTheHorizon(float tilt, float fov)
        {
            // 화면 위 광선이 지평선을 넘는 조합은 기울기를 줄인다 → 땅 범위가 유한하고 방을 덮는다 (Codex 3D 코드 리뷰 3번)
            var room = new Vector2(10f, 16f);
            float aspect = 9f / 19.5f;
            CameraFit.Solution s = CameraFit.Solve(room, tilt, fov, aspect, Top, Bottom, Side);
            Assert.That(s.Tilt + fov * 0.5f, Is.LessThanOrEqualTo(CameraFit.MaxTopAngle + 0.001f));
            foreach (Vector2 corner in Corners(room)) Assert.IsTrue(s.Ground.Contains(corner), $"땅 범위가 {corner}를 덮는다");
            Assert.That(s.Ground.height, Is.LessThan(s.Distance * 20f), "끝없이 먼 땅이 아니다");
        }

        [Test]
        public void StatusTint_FollowsTheStatusColor()
        {
            var red = new Color(0.85f, 0.25f, 0.25f);
            Color frozen = Visual3D.StatusTint(new Color(0.6f, 0.85f, 1f), red);
            Assert.That(frozen.b, Is.GreaterThan(frozen.r), "빙결 = 푸르게");
            Color poison = Visual3D.StatusTint(Color.Lerp(red, new Color(0.45f, 0.9f, 0.35f), 0.5f), red);
            Assert.That(poison.g, Is.GreaterThan(poison.r), "독 = 푸르스름한 초록");
            Color same = Visual3D.StatusTint(red, red);
            Assert.AreEqual(1f, same.r, 0.001f);
            Assert.AreEqual(1f, same.g, 0.001f);
        }

        private static Vector2[] Corners(Vector2 size) => new[]
        {
            new Vector2(-size.x / 2f, -size.y / 2f), new Vector2(size.x / 2f, -size.y / 2f),
            new Vector2(-size.x / 2f, size.y / 2f), new Vector2(size.x / 2f, size.y / 2f),
        };
    }
}
