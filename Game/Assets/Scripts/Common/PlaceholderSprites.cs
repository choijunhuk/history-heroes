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
