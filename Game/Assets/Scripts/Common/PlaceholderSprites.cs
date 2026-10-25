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
