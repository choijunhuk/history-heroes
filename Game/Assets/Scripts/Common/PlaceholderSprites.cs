using UnityEngine;

namespace HistoryHeroes.Common
{
    /// <summary>
    /// 아트가 나오기 전까지 쓰는 임시 도형 (흰 원·흰 사각형, 1유닛 크기). 색은 SpriteRenderer.color로 입힌다.
    /// 실행 중에 만들어서 에셋이 없어도 된다. (Art.md: v0.1은 도형으로 재미부터 검증)
    /// </summary>
    public static class PlaceholderSprites
    {
        private const int Size = 64;
        private static Sprite _circle;
        private static Sprite _square;

        public static Sprite Circle => _circle != null ? _circle : _circle = Make(circle: true);
        public static Sprite Square => _square != null ? _square : _square = Make(circle: false);

        private static Sprite _rimCircle, _rimSquare, _pit, _chevron;

        /// <summary>오른쪽(+X)을 가리키는 꺾쇠 "›" (컨베이어 흐름 화살표). 1유닛, 가장자리 부드럽게</summary>
        public static Sprite Chevron => _chevron != null ? _chevron : _chevron = MakeChevron();

        private static Sprite MakeChevron()
        {
            const int size = 128;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp, name = "Chevron" };
            var pixels = new Color32[size * size];
            // 꺾쇠 중심선: (0.22, 0.92) → (0.72, 0.5) → (0.22, 0.08), 두께 0.13
            Vector2 a = new Vector2(0.22f, 0.92f), tip = new Vector2(0.72f, 0.5f), b = new Vector2(0.22f, 0.08f);
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    var p = new Vector2((x + 0.5f) / size, (y + 0.5f) / size);
                    float d = Mathf.Min(SegmentDistance(p, a, tip), SegmentDistance(p, tip, b));
                    float alpha = Mathf.Clamp01((0.13f - d) * size * 0.5f);
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)(alpha * 255f));
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply();
            return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
        }

        private static float SegmentDistance(Vector2 p, Vector2 a, Vector2 b)
        {
            Vector2 ab = b - a;
            float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude);
            return Vector2.Distance(p, a + ab * t);
        }

        /// <summary>
        /// 테두리가 진한 원 (예고 · 장판, 3D 화면 점검 2번): 안은 옅게(55%), 가장자리 띠는 꽉 차게 → 비스듬한 카메라와 돌바닥 위에서도 경계가 보인다
        /// </summary>
        public static Sprite RimCircle => _rimCircle != null ? _rimCircle : _rimCircle = MakeRim(circle: true);

        /// <summary>테두리가 진한 네모 (선 · 네모 예고)</summary>
        public static Sprite RimSquare => _rimSquare != null ? _rimSquare : _rimSquare = MakeRim(circle: false);

        /// <summary>구덩이: 가운데가 어둡고 가장자리가 밝은 테 (그림자가 아니라 구멍으로 보이게, 3D 화면 점검 4번)</summary>
        public static Sprite Pit => _pit != null ? _pit : _pit = MakePit();

        private const int RimSize = 128;

        private static Sprite MakeRim(bool circle)
        {
            var texture = new Texture2D(RimSize, RimSize, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp, name = circle ? "RimCircle" : "RimSquare",
            };
            var pixels = new Color32[RimSize * RimSize];
            float c = (RimSize - 1) * 0.5f, half = RimSize * 0.5f;
            for (int y = 0; y < RimSize; y++)
            {
                for (int x = 0; x < RimSize; x++)
                {
                    // 가장자리까지 남은 거리 (픽셀)
                    float edge = circle ? half - Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c)) : Mathf.Min(Mathf.Min(x, y), Mathf.Min(RimSize - 1 - x, RimSize - 1 - y)) + 0.5f;
                    float alpha = edge <= 0f ? 0f : edge < 1f ? edge : edge < RimSize * 0.06f ? 1f : 0.55f;
                    pixels[y * RimSize + x] = new Color32(255, 255, 255, (byte)(Mathf.Clamp01(alpha) * 255f));
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply();
            return Sprite.Create(texture, new Rect(0, 0, RimSize, RimSize), new Vector2(0.5f, 0.5f), RimSize);
        }

        private static Sprite MakePit()
        {
            var texture = new Texture2D(RimSize, RimSize, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp, name = "Pit" };
            var pixels = new Color32[RimSize * RimSize];
            for (int y = 0; y < RimSize; y++)
            {
                for (int x = 0; x < RimSize; x++)
                {
                    float edge = Mathf.Min(Mathf.Min(x, y), Mathf.Min(RimSize - 1 - x, RimSize - 1 - y)) / (RimSize * 0.5f); // 0 가장자리 ~ 1 가운데
                    float v = edge < 0.06f ? 1f : Mathf.Lerp(0.85f, 0.35f, Mathf.Clamp01((edge - 0.06f) / 0.3f)); // 밝은 테 → 안쪽으로 어두워짐
                    byte b = (byte)(v * 255f);
                    pixels[y * RimSize + x] = new Color32(b, b, b, 255);
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply();
            return Sprite.Create(texture, new Rect(0, 0, RimSize, RimSize), new Vector2(0.5f, 0.5f), RimSize);
        }

        private static readonly System.Collections.Generic.Dictionary<int, Sprite> _fans = new System.Collections.Generic.Dictionary<int, Sprite>();

        /// <summary>
        /// 부채꼴 (근접 베기 표시). 오른쪽(+X)을 가운데로 degrees만큼 펼쳐진 원 조각, 지름 1유닛.
        /// 바깥쪽이 진하고 안쪽이 옅어서 "휘두른" 느낌이 난다.
        /// </summary>
        public static Sprite Fan(int degrees)
        {
            degrees = Mathf.Clamp(degrees, 10, 360);
            if (_fans.TryGetValue(degrees, out Sprite cached) && cached != null) return cached;

            const int size = 128;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp, name = $"Fan{degrees}" };
            var pixels = new Color32[size * size];
            float center = (size - 1) * 0.5f;
            float half = degrees * 0.5f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = x - center, dy = y - center;
                    float distance = Mathf.Sqrt(dx * dx + dy * dy) / (size * 0.5f);
                    float angle = Mathf.Abs(Mathf.Atan2(dy, dx) * Mathf.Rad2Deg);
                    bool inside = distance <= 1f && (degrees >= 360 || angle <= half);
                    float alpha = inside ? Mathf.Lerp(0.15f, 1f, distance) * Mathf.Clamp01((1f - distance) * 20f) : 0f;
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)(alpha * 255f));
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply();
            Sprite sprite = Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
            _fans[degrees] = sprite;
            return sprite;
        }

        private static Sprite Make(bool circle)
        {
            var texture = new Texture2D(Size, Size, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                name = circle ? "PlaceholderCircle" : "PlaceholderSquare",
            };

            var pixels = new Color32[Size * Size];
            float center = (Size - 1) * 0.5f;
            for (int y = 0; y < Size; y++)
            {
                for (int x = 0; x < Size; x++)
                {
                    byte alpha = 255;
                    if (circle)
                    {
                        float distance = Mathf.Sqrt((x - center) * (x - center) + (y - center) * (y - center));
                        alpha = (byte)(Mathf.Clamp01(Size * 0.5f - distance) * 255f); // 가장자리 1픽셀 부드럽게
                    }

                    pixels[y * Size + x] = new Color32(255, 255, 255, alpha);
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply();
            return Sprite.Create(texture, new Rect(0, 0, Size, Size), new Vector2(0.5f, 0.5f), Size);
        }
    }
}
